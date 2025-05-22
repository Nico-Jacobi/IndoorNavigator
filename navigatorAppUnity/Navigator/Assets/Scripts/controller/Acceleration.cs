using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace controller
{
    /// <summary>
    /// Estimates movement vector (velocity) by integrating accelerometer data with simple gravity removal.
    /// Keeps a rolling log of velocity samples and feeds data to Kalman filter.
    /// </summary>
    public class Acceleration : MonoBehaviour
    {
        public Registry registry; // Reference to registry for Kalman filter access
        
        private Vector3 _gravity = Vector3.zero;
        private Vector3 _velocity = Vector3.zero;
        private float _lastTime;
        private float _lastKalmanUpdateTime;

        private const float GravityFilterAlpha = 0.8f;
        private const float UpdateInterval = 0.1f; // 100ms for velocity log
        private const float KalmanUpdateInterval = 0.05f; // 50ms for Kalman updates (20Hz)

        private float _accumulatedTime = 0f;
        private float _accumulatedKalmanTime = 0f;

        private const int MaxLogSize = 100; // 10 seconds / 0.1s intervals
        private readonly Queue<Vector3> _velocityLog = new Queue<Vector3>(MaxLogSize);

        
        public TMP_Text speedText; //todo remove

        
        public void Awake()
        {
            Input.gyro.enabled = true;
            _lastTime = Time.time;
            _lastKalmanUpdateTime = Time.time;

            // Pre-fill the velocity log with zeros
            for (int i = 0; i < MaxLogSize; i++)
            {
                _velocityLog.Enqueue(Vector3.zero);
            }
        }


        public void Update()
        {
            float currentTime = Time.time;
            float deltaTime = currentTime - _lastTime;
            float kalmanDeltaTime = currentTime - _lastKalmanUpdateTime;
            
            _accumulatedTime += deltaTime;
            _accumulatedKalmanTime += kalmanDeltaTime;

            // Update internal velocity calculation every 100ms
            if (_accumulatedTime >= UpdateInterval)
            {
                _lastTime = currentTime;
                _accumulatedTime = 0f;

                Vector3 rawAccel = Input.acceleration;

                _gravity = Vector3.Lerp(_gravity, rawAccel, 1 - GravityFilterAlpha);
                Vector3 linearAccel = rawAccel - _gravity;
                _velocity += linearAccel * UpdateInterval; // integrate velocity

                // Log velocity in circular buffer
                if (_velocityLog.Count == MaxLogSize)
                    _velocityLog.Dequeue(); // remove oldest

                _velocityLog.Enqueue(_velocity);
                
                UpdateSpeedText();
            }

            // Update Kalman filter more frequently (every 50ms or 20Hz)
            if (_accumulatedKalmanTime >= KalmanUpdateInterval)
            {
                _lastKalmanUpdateTime = currentTime;
                _accumulatedKalmanTime = 0f;
                
                UpdateKalmanFilter();
            }
        }

        private void UpdateKalmanFilter()
        {
            if (registry?.kalmanFilter == null) return;

            // Get current accelerometer data
            Vector3 rawAccel = Input.acceleration;
            Vector3 linearAccel = rawAccel - _gravity;
            
            // Convert to 2D (assuming movement is primarily in X-Y plane)
            Vector2 acceleration2D = new Vector2(linearAccel.x, linearAccel.y);
            
            // Get compass heading (you'll need to implement this based on your compass system)
            float headingDegrees = GetCompassHeading();
            
            // Update Kalman filter with IMU data
            registry.kalmanFilter.UpdateWithIMU(acceleration2D, headingDegrees);
        }

        private float GetCompassHeading()
        {
            // Option 1: If you have a compass reader in your registry
            if (registry?.compassReader != null)
            {
                var recentHeadings = registry.compassReader.RecentHeadings();
                if (recentHeadings != null && recentHeadings.Count > 0)
                {
                    return recentHeadings[recentHeadings.Count - 1]; // Get most recent heading
                }
            }
            
            // Option 2: Use Unity's built-in compass (if available)
            if (Input.compass.enabled)
            {
                return Input.compass.trueHeading;
            }
            
            // Option 3: Use gyroscope to estimate heading change
            if (Input.gyro.enabled)
            {
                // This is a simplified approach - you might want to integrate gyro data over time
                Vector3 rotationRate = Input.gyro.rotationRateUnbiased;
                // Convert rotation rate to heading change (this is simplified)
                return -rotationRate.z * Mathf.Rad2Deg; // Negative because of coordinate system
            }
            
            // Fallback: return 0 (North) if no compass data available
            return 0f;
        }
        
        
        private void UpdateSpeedText()
        {
            if (speedText == null) return;

            float sumSpeed = 0f;
            foreach (var v in _velocityLog)
            {
                sumSpeed += v.magnitude;
            }

            float avgSpeed = sumSpeed / _velocityLog.Count;
            
            // Also show Kalman filter estimate if available
            string kalmanInfo = "";
            if (registry?.kalmanFilter != null)
            {
                Vector2 kalmanVelocity = registry.kalmanFilter.GetEstimatedVelocity();
                kalmanInfo = $"\nKalman Speed: {kalmanVelocity.magnitude:F3} m/s";
            }
            
            speedText.text = $"Avg Speed (last 10s): {avgSpeed:F3} m/s{kalmanInfo}";
        }

        /// <summary>
        /// Returns the current estimated movement vector (velocity) in device coordinates.
        /// </summary>
        public Vector3 GetMovementVector()
        {
            return _velocity;
        }

        /// <summary>
        /// Returns a copy of the velocity log (oldest first).
        /// </summary>
        public Vector3[] GetVelocityLog()
        {
            return _velocityLog.ToArray();
        }

        /// <summary>
        /// Call this to reset velocity estimate when you get external position/speed updates (e.g., Wi-Fi).
        /// </summary>
        public void ResetVelocity(Vector3 newVelocity)
        {
            return; //todo
            _velocity = newVelocity;
            _velocityLog.Clear();
            _velocityLog.Enqueue(newVelocity);
        }
    }
}