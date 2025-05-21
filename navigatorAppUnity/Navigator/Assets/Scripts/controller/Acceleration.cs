using UnityEngine;
using System.Collections.Generic;
using TMPro;

namespace controller
{
    /// <summary>
    /// Estimates movement vector (velocity) by integrating accelerometer data with simple gravity removal.
    /// Keeps a rolling log of velocity samples for up to 10 seconds.
    /// </summary>
    public class Acceleration : MonoBehaviour
    {
        private Vector3 _gravity = Vector3.zero;
        private Vector3 _velocity = Vector3.zero;
        private float _lastTime;

        private const float GravityFilterAlpha = 0.8f;
        private const float UpdateInterval = 0.1f; // 100ms

        private float _accumulatedTime = 0f;

        private const int MaxLogSize = 100; // 10 seconds / 0.1s intervals
        private readonly Queue<Vector3> _velocityLog = new Queue<Vector3>(MaxLogSize);

        
        public TMP_Text speedText; //todo remove

        
        public void Awake()
        {
            Input.gyro.enabled = true;
            _lastTime = Time.time;

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
            _accumulatedTime += deltaTime;

            if (_accumulatedTime < UpdateInterval)
                return; // wait until 100ms passed

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
        
        
        private void UpdateSpeedText()
        {
            if (speedText == null) return;

            float sumSpeed = 0f;
            foreach (var v in _velocityLog)
            {
                sumSpeed += v.magnitude;
            }

            float avgSpeed = sumSpeed / _velocityLog.Count;
            speedText.text = $"Avg Speed (last 10s): {avgSpeed:F3} m/s";
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
            _velocity = newVelocity;
            _velocityLog.Clear();
            _velocityLog.Enqueue(newVelocity);
        }
    }
}
