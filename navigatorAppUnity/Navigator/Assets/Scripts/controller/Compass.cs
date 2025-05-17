using UnityEngine;

namespace Controller
{
    public class CompassReader : MonoBehaviour
    {
        public bool active = true;

        [Tooltip("Larger value = more responsive, smaller = smoother")]
        [Range(0f, 1f)]
        public float smoothingFactor = 0.02f;

        private bool _isCompassAvailable;
        private float _rawHeading = 0f;
        private float _displayedHeading = 0f;
        private bool _isInitialized = false;

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
            }

            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isCompassAvailable || !active) return;

            _rawHeading = Input.compass.trueHeading;
            float rawRadians = -2f * Mathf.PI * (_rawHeading / 360f);
            float displayedRadians = -2f * Mathf.PI * (_displayedHeading / 360f);

            float angularDiff = Mathf.Repeat(rawRadians - displayedRadians + Mathf.PI, 2 * Mathf.PI) - Mathf.PI;

            displayedRadians += angularDiff * smoothingFactor;

            _displayedHeading = (360f + (-displayedRadians * 360f / (2f * Mathf.PI))) % 360f;
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
            return _isCompassAvailable ? _displayedHeading : 0f;
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
