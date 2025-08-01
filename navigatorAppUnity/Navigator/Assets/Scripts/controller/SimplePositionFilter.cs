using System.Collections.Generic;
using controller;
using model;
using UnityEngine;

namespace Controller
{
    public class SimplePositionFilter :  MonoBehaviour, PositionFilter
    {
        public Registry registry;

        public float walkingSpeed = 0f; // constant walking speed m/s 
        public float minDeltaTime = 0.001f;
        
        private float lastUpdateTimeWifi = 0f;

        // wifi positions with timestamps
        private List<Vector3> wifiPositions; // x, y, floor

        private Position lastWifiPositionRaw;
        
        // floor tracking
        private List<int> floorHistory;
        private int currentFloor;

        private float lastUpdateTimeIMU;


        private int getMaxPositions()
        {
            return  Mathf.RoundToInt(registry.settingsMenu.accuracy * 5);
        }
        
        private void Awake()
        {
            wifiPositions = new List<Vector3>();
            floorHistory = new List<int>();
            lastUpdateTimeIMU = Time.time;
            
        }

        /// <summary>
        /// Adds the new wifi prediction to the list and updates its prediction 
        /// </summary>
        public void UpdateWithWifi(Position rawWifiPrediction)
        {
            if (rawWifiPrediction == null) return;

            Vector3 newPosition = new Vector3(rawWifiPrediction.X, rawWifiPrediction.Y, rawWifiPrediction.Floor);
            float currentTime = Time.time;

            if (lastWifiPositionRaw != null)
            {
                float deltaTime = currentTime - lastUpdateTimeWifi;
                if (deltaTime > 0)
                {
                    Vector2 posA = new Vector2(newPosition.x, newPosition.y);
                    Vector2 posB = new Vector2(lastWifiPositionRaw.X, lastWifiPositionRaw.Y);

                    float distance = Vector2.Distance(posA, posB);
                    float velocity = distance / deltaTime;

                    Debug.Log($"new pos {newPosition}, old pos: {(wifiPositions.Count > 0 ? wifiPositions[^1].ToString() : "none")}");  //that indexing is crazy :D
                    Debug.Log($"distance moved according to wifi: {distance} in {deltaTime} time, which is a speed of {velocity}");

                    walkingSpeed = walkingSpeed * 0.7f + velocity * 0.3f;
                }
            }

            lastWifiPositionRaw = rawWifiPrediction;

            // Add newest position at **start** for consistency with GetEstimatedVelocity()
            wifiPositions.Insert(0, newPosition);
            if (wifiPositions.Count > getMaxPositions())
                wifiPositions.RemoveAt(wifiPositions.Count - 1);

            lastUpdateTimeWifi = currentTime;
            UpdateFloorHistory(rawWifiPrediction.Floor);
        }


        /// <summary>
        /// updates the filter with imu data
        /// (shifts all positions accordingly)
        /// </summary>
        public void UpdateWithIMU(Vector2 ignored, float headingDegrees)
        {
            if (wifiPositions.Count == 0) return;

            float deltaTime = Time.time - lastUpdateTimeIMU;
            if (deltaTime < minDeltaTime) return;

            // convert heading to movement direction
            float headingRad = -(headingDegrees+90) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));

            // move all positions by walking distance
            Vector2 positionDelta = direction * walkingSpeed * deltaTime;

            for (int i = 0; i < wifiPositions.Count; i++)
            {
                Vector3 pos = wifiPositions[i];
                pos.x += positionDelta.x;
                pos.y += positionDelta.y;
                wifiPositions[i] = pos;
            }
            
            lastUpdateTimeIMU = Time.time;
        }

        
        /// <summary>
        /// updates the floor history and makes a new floor prediction based on that
        /// </summary>
        private void UpdateFloorHistory(int newFloor)
        {
            floorHistory.Add(newFloor);
            if (floorHistory.Count > getMaxPositions())
                floorHistory.RemoveAt(0);

            UpdateFloorEstimate();
        }

        
        /// <summary>
        /// updates the floor estimate based on the current floor history
        /// </summary>
        private void UpdateFloorEstimate()
        {
            if (floorHistory.Count == 0) return;

            // take last 5 floors or fewer if less than 5
            int startIndex = Mathf.Max(0, floorHistory.Count - 5);
            var recentFloors = floorHistory.GetRange(startIndex, floorHistory.Count - startIndex);

            Dictionary<int, int> floorCounts = new Dictionary<int, int>();
            foreach (int floor in recentFloors)
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
        /// returns the current position estimation
        /// basically a weighted average of recent positions
        /// </summary>
        public Position GetEstimate()
        {
            if (wifiPositions.Count == 0) return new Position(0, 0, 0);

            Vector2 weightedSum = Vector2.zero;
            float totalWeight = 0f;

            for (int i = 0; i < wifiPositions.Count; i++)
            {
                float weight = Mathf.Pow(0.8f, i); // newer positions matter more
                Vector2 pos2D = new Vector2(wifiPositions[i].x, wifiPositions[i].y);
                weightedSum += pos2D * weight;
                totalWeight += weight;
            }

            Vector2 averagePosition = weightedSum / totalWeight;
            return new Position(averagePosition.x, averagePosition.y, currentFloor);
        }

        /// <summary>
        /// returns the estimed velocity 
        /// NOT the velocity based on imu, but based on his filters last received wifi positions
        /// </summary>
        public Vector2 GetEstimatedVelocity()
        {
            if (wifiPositions.Count < 2) return Vector2.zero;

            Vector2 newest = new Vector2(wifiPositions[0].x, wifiPositions[0].y);
            Vector2 previous = new Vector2(wifiPositions[1].x, wifiPositions[1].y);
            Vector2 direction = (newest - previous).normalized;
            return direction * walkingSpeed;
        }


       
    }
}