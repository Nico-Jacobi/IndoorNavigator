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

        // to smooth distplay of the heading
        private readonly Queue<float> _headingHistory = new Queue<float>();
        private const int MaxHistory = 20;

        // for position estimation via dead reconing
        // For 10 seconds @ 100ms intervals = 100 entries max
        private const float RecordInterval = 0.1f;
        private const float HistoryDuration = 10f;
        private readonly List<(float time, float heading)> _recentHeadings = new List<(float, float)>();
        private float _timeSinceLastRecord = 0f;

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

        private void Update()
        {
            if (!active || !_isInitialized || !_isCompassAvailable) return;

            _timeSinceLastRecord += Time.deltaTime;
            if (_timeSinceLastRecord >= RecordInterval)
            {
                _timeSinceLastRecord = 0f;
                float currentHeading = GetHeading();
                float currentTime = Time.time;
                RecordHeading(currentTime, currentHeading);
            }
        }

        /// <summary>
        /// Adds a heading to the smoothing history buffer.
        /// </summary>
        private void AddHeadingToHistory(float heading)
        {
            if (_headingHistory.Count >= MaxHistory)
                _headingHistory.Dequeue();

            _headingHistory.Enqueue(heading);
        }
        
        
        /// <summary>
        /// Returns the list of recorded headings in the last 10 seconds with timestamps.
        /// </summary>
        public IReadOnlyList<float> RecentHeadings()
        {
            const int targetCount = 100;
            float currentTime = Time.time;
            var result = new float[targetCount];

            // Fill with 0 initially
            for (int i = 0; i < targetCount; i++) result[i] = 0f;

            int index = targetCount - 1;
            // Traverse recent headings from the end (latest first)
            for (int i = _recentHeadings.Count - 1; i >= 0 && index >= 0; i--)
            {
                if (currentTime - _recentHeadings[i].time <= HistoryDuration)
                {
                    result[index] = _recentHeadings[i].heading;
                    index--;
                }
            }

            // If less than 100, remaining are 0 as already initialized
            return result;
        }


        /// <summary>
        /// Keeps a rolling record of headings for the last 10 seconds.
        /// </summary>
        private void RecordHeading(float time, float heading)
        {
            _recentHeadings.Add((time, heading));

            // Remove any entries older than 10 seconds
            while (_recentHeadings.Count > 0 && (time - _recentHeadings[0].time) > HistoryDuration)
            {
                _recentHeadings.RemoveAt(0);
            }
        }


        /// <summary>
        /// Computes the average heading from the smoothing buffer (circular angle aware).
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

        /// <summary>
        /// Returns the raw heading of the compass.
         /// </summary>
        public float GetRawHeading()
        {
            if (!_isInitialized) Initialize();
            return _isCompassAvailable ? _rawHeading : 0f;
        }

        /// <summary>
        /// Returns the smoothed heading of the compass.
         /// </summary>
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

            float angularDiff = Mathf.DeltaAngle(displayedRadians * Mathf.Rad2Deg, rawRadians * Mathf.Rad2Deg) * Mathf.Deg2Rad;

            displayedRadians += angularDiff * smoothingFactor;

            _displayedHeading = (-displayedRadians / (2f * Mathf.PI)) * 360f;
            _displayedHeading = (_displayedHeading + 360f) % 360f;

            return (_displayedHeading) % 360f;
        }

        /// <summary>
        /// returns the smoothed heading of the compas in radiants
        /// </summary>
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
