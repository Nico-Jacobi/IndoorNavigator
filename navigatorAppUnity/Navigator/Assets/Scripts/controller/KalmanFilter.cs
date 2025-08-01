using System.Collections.Generic;
using controller;
using model;
using UnityEngine;

namespace Controller
{
    public class KalmanFilter :  MonoBehaviour, PositionFilter
    {
        public Registry registry;

        public float processNoisePosition = 0.1f;
        public float processNoiseVelocity = 0.5f;
        public float measurementNoiseImu = 0.1f;
        
        public float walkingSpeed = 0f; 

        public float maxVelocity = 1.5f; // max reasonable walking speed m/s
        public int floorHistorySize = 10;
        public float minDeltaTime = 0.001f; // prevent division by zero
        
        // State: [x, y, vx, vy]
        private Vector4 state;
        private Matrix4x4 P;    // Error covariance
        private Matrix4x4 Q;    // Process noise
        private Matrix4x4 F;    // State transition
        private Matrix4x4 I;    // Identity
        
        private List<int> floorHistory;
        private int currentFloor;
        
        private float lastUpdateTimeIMU;
        private float lastUpdateTimeWIFI;
        private bool initialized = false;

        private Vector2 lastRawWifiEstimate;
        private bool hasWifiHistory = false;
        
        
        private float getMeasurementNoiseWifi()
        {
            return registry.settingsMenu.accuracy * 2f;
        }
        
        private void Awake()
        {
            InitializeMatrices();
            floorHistory = new List<int>();
            lastUpdateTimeIMU = Time.time;
            lastUpdateTimeWIFI = Time.time;
        }

        private void InitializeMatrices()
        {
            I = Matrix4x4.identity;
            state = Vector4.zero;
            
            // Initialize error covariance with high uncertainty
            P = Matrix4x4.zero;
            P.m00 = 100f; P.m11 = 100f; P.m22 = 10f; P.m33 = 10f;
            
            // Initialize process noise matrix properly
            InitializeProcessNoise();
            
            initialized = false;
        }

        private void InitializeProcessNoise()
        {
            Q = Matrix4x4.zero;
            Q.m00 = processNoisePosition;
            Q.m11 = processNoisePosition;
            Q.m22 = processNoiseVelocity;
            Q.m33 = processNoiseVelocity;
        }

        /// <summary>
        /// update the kalman filter with a wifi prediction
        /// will take the time passed into account by itself
        /// </summary>
        public void UpdateWithWifi(Position rawWifiPrediction)
        {
            if (rawWifiPrediction == null)
            {
                return; // Don't update timestamp if no measurement
            }

            float deltaTime = Time.time - lastUpdateTimeIMU;
            deltaTime = Mathf.Max(deltaTime, minDeltaTime);
            
            if (!initialized)
            {
                // Initialize with first WiFi measurement
                state = new Vector4(rawWifiPrediction.X, rawWifiPrediction.Y, 0, 0);
                currentFloor = rawWifiPrediction.Floor;
                lastRawWifiEstimate = new Vector2(rawWifiPrediction.X, rawWifiPrediction.Y);
                hasWifiHistory = true;
                initialized = true;
                lastUpdateTimeWIFI = Time.time;
            }
            else
            {
                Predict(deltaTime);
                Vector2 measurement = new Vector2(rawWifiPrediction.X, rawWifiPrediction.Y);
                UpdateWithPositionMeasurement(measurement, getMeasurementNoiseWifi());
                
                // Update IMU velocity estimate if we have previous WiFi data
                if (hasWifiHistory)
                {
                    float wifiDeltaTime = Time.time - lastUpdateTimeWIFI;
                    if (wifiDeltaTime > minDeltaTime)
                    {
                        Vector2 deltaPosition = measurement - lastRawWifiEstimate;
                        Vector2 estimatedVelocity = deltaPosition / wifiDeltaTime;
                        
                        // Calculate velocity magnitude (speed)
                        float currentSpeed = estimatedVelocity.magnitude;
                
                        // Smooth walking speed adjustment (same as SimplePositionFilter)
                        walkingSpeed = walkingSpeed * 0.7f + currentSpeed * 0.3f;
                        
                        // Clamp velocity to reasonable range
                        if (estimatedVelocity.magnitude > maxVelocity)
                        {
                            estimatedVelocity = estimatedVelocity.normalized * maxVelocity;
                        }
                        
                        Vector3 velocity3D = new Vector3(estimatedVelocity.x, estimatedVelocity.y, 0);
                        Debug.Log($"WiFi velocity estimate: {velocity3D.magnitude:F2} m/s");
                        registry.accelerationController.ResetVelocity(velocity3D);
                    }
                }
                
                lastRawWifiEstimate = measurement;
                lastUpdateTimeWIFI = Time.time;
            }
            
            // Update floor history with size limit
            UpdateFloorHistory(rawWifiPrediction.Floor);
            lastUpdateTimeIMU = Time.time;
        }

        
        /// <summary>
        /// update the kalman filter with the current IMU data
        /// will also take the time passed since last into account
         /// </summary>
        public void UpdateWithIMU(Vector2 acceleration, float headingDegrees)
        {
            float deltaTime = Time.time - lastUpdateTimeIMU;
            if (deltaTime < minDeltaTime) return;

            // Convert heading to radians and get direction vector
            float headingRad = -(headingDegrees + 90) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));

            // Use acceleration magnitude (length) only - direction comes from compass
            float accelerationMagnitude = acceleration.magnitude;
            //Debug.Log($"Acceleration magnitude: {accelerationMagnitude:F2} m/s²");
    
            // Predict step
            Predict(deltaTime);
    
            // Calculate velocity change using acceleration magnitude in the compass direction
            float velocityMagnitudeChange = accelerationMagnitude * deltaTime;
            Vector2 velocityChange = direction * velocityMagnitudeChange;
    
            // Get current estimated velocity and add the change
            Vector2 currentVelocity = new Vector2(state.z, state.w);
            Vector2 newVelocity = currentVelocity + velocityChange;
    
            // Update with the new velocity measurement
            UpdateWithVelocityMeasurement(newVelocity, measurementNoiseImu);
    
            lastUpdateTimeIMU = Time.time;
        }

 

        /// <summary>
        /// update the history of floors from the last measurements
        /// also makes a new floor prediction base on those
        /// </summary>
        private void UpdateFloorHistory(int newFloor)
        {
            floorHistory.Add(newFloor);
            if (floorHistory.Count > floorHistorySize)
            {
                floorHistory.RemoveAt(0);
            }
            UpdateFloorEstimate();
        }

        
        /// <summary>
        /// runs the predict step to estimate the position
        /// </summary>
        private void Predict(float deltaTime)
        {
            // Update state transition matrix
            F = Matrix4x4.identity;
            F.m02 = deltaTime; // x = x + vx * dt
            F.m13 = deltaTime; // y = y + vy * dt
            
            // Predict state: x_k = F * x_(k-1)
            state = new Vector4(
                state.x + state.z * deltaTime,
                state.y + state.w * deltaTime,
                state.z,
                state.w
            );
            
            // Predict error covariance: P_k = F * P_(k-1) * F^T + Q
            Matrix4x4 FT = TransposeMatrix(F);
            P = AddMatrices(MultiplyMatrices(MultiplyMatrices(F, P), FT), Q);
        }

        
        /// <summary>
        /// updates the state given the measurement and noise (which can only be estimated)
        /// </summary>
        private void UpdateWithPositionMeasurement(Vector2 measurement, float measurementNoise)
        {
            // Measurement matrix H - observes position only
            Matrix4x4 H = Matrix4x4.zero;
            H.m00 = 1; H.m11 = 1;
            
            // Measurement noise covariance R
            Matrix4x4 R = Matrix4x4.zero;
            R.m00 = measurementNoise * measurementNoise;
            R.m11 = measurementNoise * measurementNoise;
            
            PerformKalmanUpdate(H, R, new Vector4(measurement.x, measurement.y, 0, 0), 
                               new Vector4(state.x, state.y, 0, 0));
        }

        
        /// <summary>
        /// update the filter with velocity, but as it turns out this is quite inaccurate,
        /// so this method guesses the velocity based walking speed which is based on wifi measurements
        /// </summary>
        private void UpdateWithVelocityMeasurement(Vector2 velocityMeasurement, float measurementNoise)
        {
            // Convert heading to radians and get direction vector
            float headingRad = -(registry.compassReader.GetHeading() + 90) * Mathf.Deg2Rad;
            Vector2 velocity = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad)) * walkingSpeed;
        
            
            // Measurement matrix H - observes velocity only
            Matrix4x4 H = Matrix4x4.zero;
            H.m22 = 1; H.m33 = 1;
            
            // Measurement noise covariance R
            Matrix4x4 R = Matrix4x4.zero;
            R.m22 = measurementNoise * measurementNoise;
            R.m33 = measurementNoise * measurementNoise;
            
            PerformKalmanUpdate(H, R, new Vector4(0, 0, velocity.x, velocity.y), 
                               new Vector4(0, 0, state.z, state.w));
        }

        
        /// <summary>
        /// Performs one Kalman filter update step using the given measurement.
        /// </summary>
        private void PerformKalmanUpdate(Matrix4x4 H, Matrix4x4 R, Vector4 measurement, Vector4 predicted)
        {
            // Innovation: z - H*x
            Vector4 innovation = measurement - predicted;
            
            // Innovation covariance: S = H*P*H^T + R
            Matrix4x4 HT = TransposeMatrix(H);
            Matrix4x4 S = AddMatrices(MultiplyMatrices(MultiplyMatrices(H, P), HT), R);
            
            // Kalman gain: K = P*H^T*S^(-1)
            Matrix4x4 SInv = InvertMatrix(S);
            Matrix4x4 K = MultiplyMatrices(MultiplyMatrices(P, HT), SInv);
            
            // State update: x = x + K*innovation
            Vector4 correction = MultiplyMatrixVector(K, innovation);
            state += correction;
            
            // Covariance update: P = (I - K*H)*P
            Matrix4x4 KH = MultiplyMatrices(K, H);
            P = MultiplyMatrices(SubtractMatrices(I, KH), P);
            
            // Ensure P remains positive definite (Joseph form for numerical stability)
            EnsurePositiveDefinite();
        }

        /// <summary>
        /// Keeps P positive definite by fixing tiny/negative diagonal values.
        /// </summary>
        private void EnsurePositiveDefinite()
        {
            // Simple approach: add small diagonal term if needed
            for (int i = 0; i < 4; i++)
            {
                if (P[i, i] < 1e-6f)
                {
                    P[i, i] = 1e-6f;
                }
            }
        }

        /// <summary>
        /// updated the floor prediction based on the floor history
        /// </summary>
        private void UpdateFloorEstimate()
        {
            if (floorHistory.Count == 0) return;
            
            // Majority vote for floor estimation
            Dictionary<int, int> floorCounts = new Dictionary<int, int>();
            foreach (int floor in floorHistory)
            {
                floorCounts[floor] = floorCounts.ContainsKey(floor) ? floorCounts[floor] + 1 : 1;
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

        
        /// <summary>
        /// returns the position estimate
        /// might return 0,0,0 if not initialized or never updated
        /// </summary>
        public Position GetEstimate()
        {
            if (!initialized)
                return new Position(0, 0, 0);
                
            return new Position(state.x, state.y, currentFloor);
        }
        
        /// <summary>
        /// returns the estimated velocity according to the filter
        /// NOT the prediction based on imu or wifi
        /// </summary>
        public Vector2 GetEstimatedVelocity()
        {
            return new Vector2(state.z, state.w);
        }
        

        public bool IsInitialized => initialized;

        // Matrix utility methods
        
        /// <summary>
        /// multiplies the a matrix by the b vector
        /// </summary>
        private Vector4 MultiplyMatrixVector(Matrix4x4 m, Vector4 v)
        {
            return new Vector4(
                m.m00 * v.x + m.m01 * v.y + m.m02 * v.z + m.m03 * v.w,
                m.m10 * v.x + m.m11 * v.y + m.m12 * v.z + m.m13 * v.w,
                m.m20 * v.x + m.m21 * v.y + m.m22 * v.z + m.m23 * v.w,
                m.m30 * v.x + m.m31 * v.y + m.m32 * v.z + m.m33 * v.w
            );
        }

        /// <summary>
        /// multiplies the a matrix by the b matrix
        /// </summary>
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

        /// <summary>
        /// adds the b matrix to the a matrix
        /// </summary>
        private Matrix4x4 AddMatrices(Matrix4x4 a, Matrix4x4 b)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int i = 0; i < 16; i++)
            {
                result[i] = a[i] + b[i];
            }
            return result;
        }

        /// <summary>
        /// well, this one subtracts the b matrix from the a matrix, who could have guessed
         /// </summary>
        private Matrix4x4 SubtractMatrices(Matrix4x4 a, Matrix4x4 b)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int i = 0; i < 16; i++)
            {
                result[i] = a[i] - b[i];
            }
            return result;
        }

        /// <summary>
        /// utils function
        /// </summary>
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

        
        /// <summary>
        /// utils function
        /// </summary>
        private Matrix4x4 InvertMatrix(Matrix4x4 m)
        {
            // For block diagonal matrices, invert each 2x2 block separately
            Matrix4x4 result = Matrix4x4.zero;
            
            // Invert top-left 2x2 block
            float det1 = m.m00 * m.m11 - m.m01 * m.m10;
            if (Mathf.Abs(det1) > 1e-8f)
            {
                result.m00 = m.m11 / det1;
                result.m01 = -m.m01 / det1;
                result.m10 = -m.m10 / det1;
                result.m11 = m.m00 / det1;
            }
            else
            {
                // Fallback for singular matrix
                result.m00 = 1e6f;
                result.m11 = 1e6f;
            }
            
            // Invert bottom-right 2x2 block
            float det2 = m.m22 * m.m33 - m.m23 * m.m32;
            if (Mathf.Abs(det2) > 1e-8f)
            {
                result.m22 = m.m33 / det2;
                result.m23 = -m.m23 / det2;
                result.m32 = -m.m32 / det2;
                result.m33 = m.m22 / det2;
            }
            else
            {
                // Fallback for singular matrix
                result.m22 = 1e6f;
                result.m33 = 1e6f;
            }
            
            return result;
        }

       
        /// <summary>
        /// Resets the kalman filter
        /// eg when building is changed
        /// </summary>
        public void Reset()
        {
            InitializeMatrices();
            floorHistory.Clear();
            hasWifiHistory = false;
            lastUpdateTimeIMU = Time.time;
            lastUpdateTimeWIFI = Time.time;
        }
    }
}