using UnityEngine;

namespace Controller
{
    public class CompassReader : MonoBehaviour
    {
        public float smoothingFactor = 0.05f;
        
        private bool _isCompassAvailable;
        private float _rawHeading = 0f;       // Stores the current heading from the device
        private float _displayedHeading = 0f; // Stores the smoothed heading for display
        private bool _isInitialized = false;
        
        private void Start()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            if (_isInitialized) return;
            
            // Start location services 
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
            if (!_isCompassAvailable) return;
            
            _rawHeading = Input.compass.trueHeading;
            
            float angularDiff = Mathf.DeltaAngle(_displayedHeading, _rawHeading);
            
            _displayedHeading += angularDiff * smoothingFactor * Time.deltaTime * 60f;
            
            _displayedHeading = (_displayedHeading + 360f) % 360f;
        }
        
        /// <summary>
        /// Returns the raw (unsmoothed) compass heading (0-360). Returns 0 if unavailable.
        /// </summary>
        public float GetRawHeading()
        {
            if (!_isInitialized) Initialize();
            return _isCompassAvailable ? _rawHeading : 0f;
        }
        
        /// <summary>
        /// Returns the smoothed compass heading (0-360). Returns 0 if unavailable.
        /// </summary>
        public float GetHeading()
        {
            if (!_isInitialized) Initialize();
            return _isCompassAvailable ? _displayedHeading : 0f;
        }
        
        /// <summary>
        /// Returns the heading in radians for use in rotations (-2π to 0)
        /// </summary>
        public float GetHeadingRadians()
        {
            return -2f * Mathf.PI * (GetHeading() / 360f);
        }
        
        /// <summary>
        /// Cleanup when the component is destroyed
        /// </summary>
        private void OnDestroy()
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                Input.location.Stop();
            }
        }
    }
}