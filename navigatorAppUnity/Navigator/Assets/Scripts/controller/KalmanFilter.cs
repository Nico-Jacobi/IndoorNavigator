using System.Collections.Generic;
using controller;
using model;
using UnityEngine;

namespace Controller
{
    public class KalmanFilter : MonoBehaviour
    {
        public Registry registry;

        [Header("Kalman Filter Settings")]
        public float processNoisePosition = 0.1f;
        public float processNoiseVelocity = 0.5f;
        public float measurementNoiseWifi = 2.0f;
        public float measurementNoiseImu = 0.1f;
        
        // state: [x, y, vx, vy]
        private Vector4 state;
        private Matrix4x4 P;    // error covariance
        private Matrix4x4 Q;    // process noise
        private Matrix4x4 F;    // state transition
        private Matrix4x4 I;    // identity
        
        private List<int> floorHistory;
        private int currentFloor;
        
        private float lastUpdateTimeIMU;
        private bool initialized = false;

        private Vector2 lastRawWifiEstimate;
        private float lastUpdateTimeWIFI;
        
        private void Awake()
        {
            InitializeMatrices();
            floorHistory = new List<int>();
            lastUpdateTimeIMU = Time.time;
        }

        private void InitializeMatrices()
        {
            I = Matrix4x4.identity;
            state = Vector4.zero;
            
            // high initial uncertainty
            P = new Matrix4x4();
            P.m00 = 100f; P.m11 = 100f; P.m22 = 100f; P.m33 = 100f;
            P.m01 = 0f; P.m02 = 0f; P.m03 = 0f;
            P.m10 = 0f; P.m12 = 0f; P.m13 = 0f;
            P.m20 = 0f; P.m21 = 0f; P.m23 = 0f;
            P.m30 = 0f; P.m31 = 0f; P.m32 = 0f;
            
            Q = new Matrix4x4();
            Q.m00 = processNoisePosition; Q.m11 = processNoisePosition;
            Q.m22 = processNoiseVelocity; Q.m33 = processNoiseVelocity;
            Q.m01 = 0f; Q.m02 = 0f; Q.m03 = 0f;
            Q.m10 = 0f; Q.m12 = 0f; Q.m13 = 0f;
            Q.m20 = 0f; Q.m21 = 0f; Q.m23 = 0f;
            Q.m30 = 0f; Q.m31 = 0f; Q.m32 = 0f;
            
            initialized = false;
        }

        public void UpdateWithWifi(Position rawWifiPrediction)
        {
            if (rawWifiPrediction == null)
            {
                lastUpdateTimeIMU = Time.time;
                return;
            }

            float deltaTime = Time.time - lastUpdateTimeIMU;
            
            if (!initialized)
            {
                // init with first wifi measurement
                state = new Vector4(rawWifiPrediction.X, rawWifiPrediction.Y, 0, 0);
                currentFloor = rawWifiPrediction.Floor;
                initialized = true;
            }
            else
            {
                Predict(deltaTime);
                Vector2 measurement = new Vector2(rawWifiPrediction.X, rawWifiPrediction.Y);
                UpdateWithPositionMeasurement(measurement, measurementNoiseWifi);
                
                // update imu velocity to prevent drift
                float elapsedTime = Time.time - lastUpdateTimeWIFI;
                Vector3 deltaPosition = new Vector3(measurement.x - lastRawWifiEstimate.x, measurement.y - lastRawWifiEstimate.y, 0);
                Vector3 estimatedVelocity = deltaPosition / elapsedTime;

                lastRawWifiEstimate = measurement;
                lastUpdateTimeWIFI = Time.time;
            
                registry.accelerationController.ResetVelocity(estimatedVelocity);
            }
            
            // update floor history
            floorHistory.Add(rawWifiPrediction.Floor);
            if (floorHistory.Count > 10)
            {
                floorHistory.RemoveAt(0);
            }
            
            UpdateFloorEstimate();
            lastUpdateTimeIMU = Time.time;
        }

        public void UpdateWithIMU(Vector2 acceleration, float headingDegrees)
        {
            if (!initialized) return;

            float deltaTime = Time.time - lastUpdateTimeIMU;
            if (deltaTime <= 0) return;
            
            Predict(deltaTime);
            
            // integrate acceleration for velocity change
            Vector2 velocityChange = acceleration * deltaTime;
            Vector2 newVelocity = new Vector2(state.z, state.w) + velocityChange;
            
            // use compass to constrain direction if moving
            float currentSpeed = newVelocity.magnitude;
            if (currentSpeed > 0.1f)
            {
                float headingRad = headingDegrees * Mathf.Deg2Rad;
                Vector2 compassDirection = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));
                Vector2 compassVelocity = compassDirection * currentSpeed;
                
                // blend compass direction with accelerometer magnitude
                float compassWeight = 0.3f;
                Vector2 blendedVelocity = Vector2.Lerp(newVelocity, compassVelocity, compassWeight);
                UpdateWithVelocityMeasurement(blendedVelocity, measurementNoiseImu);
            }
            else
            {
                UpdateWithVelocityMeasurement(newVelocity, measurementNoiseImu);
            }
            
            lastUpdateTimeIMU = Time.time;
        }

        private void Predict(float deltaTime)
        {
            // state transition matrix
            F = Matrix4x4.identity;
            F.m02 = deltaTime; // x = x + vx * dt
            F.m13 = deltaTime; // y = y + vy * dt
            
            // predict state
            Vector4 newState = new Vector4(
                state.x + state.z * deltaTime,
                state.y + state.w * deltaTime,
                state.z,
                state.w
            );
            state = newState;
            
            // predict error covariance
            P = MultiplyMatrices(MultiplyMatrices(F, P), TransposeMatrix(F));
            P = AddMatrices(P, Q);
        }

        private void UpdateWithPositionMeasurement(Vector2 measurement, float measurementNoise)
        {
            // measurement matrix - observe position only
            Matrix4x4 H = new Matrix4x4();
            H.m00 = 1; H.m11 = 1;
            
            Matrix4x4 R = Matrix4x4.identity;
            R.m00 = measurementNoise;
            R.m11 = measurementNoise;
            R.m22 = 0; R.m33 = 0;
            
            Vector2 predicted = new Vector2(state.x, state.y);
            Vector2 innovation = measurement - predicted;
            
            Matrix4x4 HT = TransposeMatrix(H);
            Matrix4x4 S = AddMatrices(MultiplyMatrices(MultiplyMatrices(H, P), HT), R);
            Matrix4x4 K = MultiplyMatrices(MultiplyMatrices(P, HT), InvertMatrix2x2(S));
            
            Vector4 correction = new Vector4(
                K.m00 * innovation.x + K.m01 * innovation.y,
                K.m10 * innovation.x + K.m11 * innovation.y,
                K.m20 * innovation.x + K.m21 * innovation.y,
                K.m30 * innovation.x + K.m31 * innovation.y
            );
            state += correction;
            
            Matrix4x4 KH = MultiplyMatrices(K, H);
            P = MultiplyMatrices(SubtractMatrices(I, KH), P);
        }

        private void UpdateWithVelocityMeasurement(Vector2 velocityMeasurement, float measurementNoise)
        {
            // measurement matrix - observe velocity only
            Matrix4x4 H = new Matrix4x4();
            H.m22 = 1; H.m33 = 1;
            
            Matrix4x4 R = Matrix4x4.identity;
            R.m00 = 0; R.m11 = 0;
            R.m22 = measurementNoise;
            R.m33 = measurementNoise;
            
            Vector2 predicted = new Vector2(state.z, state.w);
            Vector2 innovation = velocityMeasurement - predicted;
            
            Matrix4x4 HT = TransposeMatrix(H);
            Matrix4x4 S = AddMatrices(MultiplyMatrices(MultiplyMatrices(H, P), HT), R);
            Matrix4x4 K = MultiplyMatrices(MultiplyMatrices(P, HT), InvertMatrix2x2(S));
            
            Vector4 correction = new Vector4(
                K.m02 * innovation.x + K.m03 * innovation.y,
                K.m12 * innovation.x + K.m13 * innovation.y,
                K.m22 * innovation.x + K.m23 * innovation.y,
                K.m32 * innovation.x + K.m33 * innovation.y
            );
            state += correction;
            
            Matrix4x4 KH = MultiplyMatrices(K, H);
            P = MultiplyMatrices(SubtractMatrices(I, KH), P);
        }

        private void UpdateFloorEstimate()
        {
            if (floorHistory.Count == 0) return;
            
            // majority vote
            Dictionary<int, int> floorCounts = new Dictionary<int, int>();
            foreach (int floor in floorHistory)
            {
                if (floorCounts.ContainsKey(floor))
                    floorCounts[floor]++;
                else
                    floorCounts[floor] = 1;
            }
            
            int maxCount = 0;
            int mostFrequentFloor = currentFloor;
            foreach (var kvp in floorCounts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    mostFrequentFloor = kvp.Key;
                }
            }
            
            currentFloor = mostFrequentFloor;
        }

        public Position GetEstimate()
        {
            if (!initialized)
                return new Position(0, 0, 0);
                
            return new Position(state.x, state.y, currentFloor);
        }
        
        public Vector2 GetEstimatedVelocity()
        {
            return new Vector2(state.z, state.w);
        }

        public float GetPositionUncertainty()
        {
            return Mathf.Sqrt(P.m00 + P.m11);
        }

        // matrix utils
        private Matrix4x4 MultiplyMatrices(Matrix4x4 a, Matrix4x4 b)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[i, j] = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        result[i, j] += a[i, k] * b[k, j];
                    }
                }
            }
            return result;
        }

        private Matrix4x4 AddMatrices(Matrix4x4 a, Matrix4x4 b)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int i = 0; i < 16; i++)
            {
                result[i] = a[i] + b[i];
            }
            return result;
        }

        private Matrix4x4 SubtractMatrices(Matrix4x4 a, Matrix4x4 b)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int i = 0; i < 16; i++)
            {
                result[i] = a[i] - b[i];
            }
            return result;
        }

        private Matrix4x4 TransposeMatrix(Matrix4x4 m)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[i, j] = m[j, i];
                }
            }
            return result;
        }

        private Matrix4x4 InvertMatrix2x2(Matrix4x4 m)
        {
            // simplified 2x2 inversion for block diagonal matrices
            Matrix4x4 result = Matrix4x4.identity;
            
            float det = m.m00 * m.m11 - m.m01 * m.m10;
            if (Mathf.Abs(det) > 1e-6f)
            {
                result.m00 = m.m11 / det;
                result.m01 = -m.m01 / det;
                result.m10 = -m.m10 / det;
                result.m11 = m.m00 / det;
            }
            
            det = m.m22 * m.m33 - m.m23 * m.m32;
            if (Mathf.Abs(det) > 1e-6f)
            {
                result.m22 = m.m33 / det;
                result.m23 = -m.m23 / det;
                result.m32 = -m.m32 / det;
                result.m33 = m.m22 / det;
            }
            
            return result;
        }
    }
}