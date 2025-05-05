using UnityEngine;


namespace controller
{
    
    public class CompassReader : MonoBehaviour
    {
        private bool _isCompassAvailable;

        private void Start()
        {
            // Try to start location services needed for compass to work
            Input.location.Start();

            Input.compass.enabled = true;
            _isCompassAvailable = Input.compass.enabled; //&& Input.compass.headingAccuracy >= 0;
        }

        /// <summary>
        /// Returns compass heading (0-360). Returns 0 if unavailable or inaccurate.
        /// </summary>
        public float GetHeading() => _isCompassAvailable ? Input.compass.trueHeading : 0f;

    }


}