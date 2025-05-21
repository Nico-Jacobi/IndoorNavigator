using System.Collections.Generic;
using controller;
using model;
using UnityEngine;

namespace Controller
{
    public class KalmanFilter : MonoBehaviour
    {

        public Registry registry;

        // Store relative estimated positions (0 = latest Wi-Fi fix)
        private List<Vector2> _positionHistory;
        private List<int> lastFloors;

        public int WeightedAverageLength = 10;

        private Position lastRawPosition;
        private float lastUpdatedTimestamp;
 
        
        private void Awake()
        {
            lastUpdatedTimestamp = 0;
            _positionHistory = new List<Vector2>();
            lastFloors = new List<int>();
        }
        
        
        
        /// <summary>
        /// Resets position history based on a new Wi-Fi fix.
        /// </summary>
        public void UpdateWithWifi(Position rawWifiPrediction)
        {
            if (rawWifiPrediction == null || registry?.accelerationController == null || registry.compassReader == null)  //at startup / position not active / error earlier
            {
                lastUpdatedTimestamp = Time.time;
                return;
            }
            
            Vector2 wifiPosition = new Vector2(rawWifiPrediction.X, rawWifiPrediction.Y);
            
            Debug.Log(WeightedAverageLength);
            
            lastFloors.Add(rawWifiPrediction.Floor);
            while (lastFloors.Count > WeightedAverageLength)
            {
                lastFloors.RemoveAt(0);
                _positionHistory.RemoveAt(0);
            }


            Debug.Log(1);
            Debug.Log(rawWifiPrediction.Y);

            Vector3[] velocities = registry.accelerationController.GetVelocityLog(); // oldest -> newest
            float elapsedTime = Time.time - lastUpdatedTimestamp; // total seconds elapsed

            Position lastPos = GetLatestEstimate();
            if (lastPos == null)
            {
                lastPos = rawWifiPrediction;    // sets the velocity to 0 at the start 
            }
            Vector3 deltaPosition = new Vector3(rawWifiPrediction.X - lastPos.X, rawWifiPrediction.Y - lastPos.Y, 0);
            Vector3 estimatedVelocity = deltaPosition / elapsedTime; 

            Debug.Log(2);

            Debug.Log(estimatedVelocity);
            
            registry.accelerationController.ResetVelocity(estimatedVelocity);

            IReadOnlyList<float> headings = registry.compassReader.RecentHeadings(); // 0s -> now

            
            int stepsRounded = Mathf.RoundToInt(elapsedTime * 10f); // 100ms steps
            
            Debug.Log(3);

            
            // for the first prediction this is not run, so the elapsedTimeSteps being wrong is no problem 
            for (int i = stepsRounded - 1; i >= 0; i--)
            {
                
                float headingRad = headings[i] * Mathf.Deg2Rad;
                Vector2 dir = new(Mathf.Cos(headingRad), Mathf.Sin(headingRad));

                float speed = new Vector2(velocities[i].x, velocities[i].y).magnitude;
                Vector2 delta =  speed * 0.1f * dir; // 0.1s timestep, the speed is in m/s
                

                for (int pIndex = 0; pIndex < _positionHistory.Count; pIndex++)
                {
                    // the older the position the more noise is included
                    _positionHistory[pIndex] += delta;
                }
            }
            
            lastUpdatedTimestamp = Time.time;
            lastRawPosition = rawWifiPrediction;
            _positionHistory.Add(wifiPosition);
        }

        /// <summary>
        /// Get full history of estimated positions (oldest to newest).
        /// </summary>
        public IReadOnlyList<Vector2> GetAllEstimates()
        {
            return _positionHistory.AsReadOnly();
        }

        public Position GetLatestEstimate()
        {
            return lastRawPosition;
        }
        
        public Position GetEstimate()
        {
            
            if (_positionHistory.Count == 0)
                return new Position(0, 0, 0); // no data available
            
            
            int count = _positionHistory.Count; // this can be different from WeightedAverageLength at start or change of the value
            float totalWeight = count * (count + 1) / 2f; // sum 1..n

            Vector2 weightedSum = Vector2.zero;
            int weightedFloorSum = 0;

            for (int i = 0; i < count; i++)
            {
                int weight = i + 1; // oldest = 1, newest = n
                weightedSum += _positionHistory[i] * weight;
                weightedFloorSum += lastFloors[i] * weight;
            }

            Vector2 avgPos = weightedSum / totalWeight;
            int avgFloor = Mathf.RoundToInt(weightedFloorSum / totalWeight);


            return new Position(avgPos.x, avgPos.y, avgFloor);
        }

    }
}
