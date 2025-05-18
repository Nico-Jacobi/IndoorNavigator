using System;
using System.Collections.Generic;
using UnityEngine;

namespace Controller
{
    public class CompassReader : MonoBehaviour
    {
        public bool active = true;
        [Range(0.01f, 1.0f)]
        public float smoothingFactor = 0.0002f;

        private bool _isCompassAvailable;
        private float _rawHeading = 0f;
        private float _displayedHeading = 0f;
        private bool _isInitialized = false;

        private readonly Queue<float> _headingHistory = new Queue<float>();
        private const int MaxHistory = 20;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            Input.location.Start();
            Input.compass.enabled = true;

            _isCompassAvailable = Input.compass.enabled;

            if (_isCompassAvailable)
            {
                _rawHeading = Input.compass.trueHeading;
                _displayedHeading = _rawHeading;
                AddHeadingToHistory(_rawHeading);
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Adds a heading to the history buffer, keeping it at a max size.
        /// </summary>
        private void AddHeadingToHistory(float heading)
        {
            if (_headingHistory.Count >= MaxHistory)
                _headingHistory.Dequeue();

            _headingHistory.Enqueue(heading);
        }

        /// <summary>
        /// Computes the average heading from the history buffer.
        /// Takes into account circular nature of angles.
        /// </summary>
        private float GetAverageHeading()
        {
            if (_headingHistory.Count == 0) return 0f;

            float sinSum = 0f;
            float cosSum = 0f;

            foreach (float h in _headingHistory)
            {
                float rad = h * Mathf.Deg2Rad;
                sinSum += Mathf.Sin(rad);
                cosSum += Mathf.Cos(rad);
            }

            float avgRad = Mathf.Atan2(sinSum, cosSum);
            return (avgRad * Mathf.Rad2Deg + 360f) % 360f;
        }

        public float GetRawHeading()
        {
            if (!_isInitialized) Initialize();
            return _isCompassAvailable ? _rawHeading : 0f;
        }

        public float GetHeading()
        {
            if (!active) return 0f;
            if (!_isInitialized) Initialize();
            if (!_isCompassAvailable) return 0f;

            float newReading = Input.compass.trueHeading;
            AddHeadingToHistory(newReading);
            _rawHeading = GetAverageHeading();

            float rawRadians = -2f * Mathf.PI * (_rawHeading / 360f);
            float displayedRadians = -2f * Mathf.PI * (_displayedHeading / 360f);

            float angularDiff = ((rawRadians - displayedRadians + Mathf.PI) % (2f * Mathf.PI)) - Mathf.PI;

            displayedRadians += angularDiff * smoothingFactor;

            _displayedHeading = (-displayedRadians / (2f * Mathf.PI)) * 360f;
            _displayedHeading = (_displayedHeading + 360f) % 360f;

            return _displayedHeading;
        }

        public float GetHeadingRadians()
        {
            return -2f * Mathf.PI * (GetHeading() / 360f);
        }

        private void OnDestroy()
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                Input.location.Stop();
            }
        }
    }
}
