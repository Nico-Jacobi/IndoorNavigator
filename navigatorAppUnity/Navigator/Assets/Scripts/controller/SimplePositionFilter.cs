using System.Collections.Generic;
using controller;
using model;
using UnityEngine;

namespace Controller
{
    public class SimplePositionFilter :  MonoBehaviour, PositionFilter
    {
        public Registry registry;

        public int maxPositions = 10;
        public float walkingSpeed = 0f; // constant walking speed m/s 
        public int floorHistorySize = 10;
        public float minDeltaTime = 0.001f;
        
        private float lastUpdateTimeWifi = 0f;

        // wifi positions with timestamps
        private List<Vector3> wifiPositions; // x, y, floor

        private Position lastWifiPositionRaw;
        
        // floor tracking
        private List<int> floorHistory;
        private int currentFloor;

        private float lastUpdateTimeIMU;
        private bool initialized = false;

        private void Awake()
        {
            wifiPositions = new List<Vector3>();
            floorHistory = new List<int>();
            lastUpdateTimeIMU = Time.time;

            // fill with zeros initially
            for (int i = 0; i < maxPositions; i++)
            {
                wifiPositions.Add(Vector3.zero);
            }
        }

        public void UpdateWithWifi(Position rawWifiPrediction)
        {
            if (rawWifiPrediction == null) return;
            
            Vector3 newPosition = new Vector3(rawWifiPrediction.X, rawWifiPrediction.Y, rawWifiPrediction.Floor); 

            float velocity = 0f;
            float currentTime = Time.time;

            if (!initialized)
            {
                // first wifi update - fill all positions with same value
                for (int i = 0; i < maxPositions; i++)
                {
                    wifiPositions[i] = newPosition;
                }
                currentFloor = rawWifiPrediction.Floor;
                initialized = true;
            }
            else
            {
                // calculate velocity from last update
                float deltaTime = currentTime - lastUpdateTimeWifi;
                if (deltaTime > 0 && lastWifiPositionRaw != null)
                {
                    Vector2 posA = new Vector2(newPosition.x, newPosition.y);
                    Position pos = lastWifiPositionRaw;

                    float dx = posA.x - pos.X;
                    float dy = posA.y - pos.Y;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    velocity = distance / deltaTime;

                    Debug.Log($"new pos {newPosition}, old pos: {wifiPositions[0]}");
                    Debug.Log($"distance moved according to wifi: {distance} in {deltaTime} time, which is a speed of {velocity}");

                    // smooth walking speed adjustment
                    walkingSpeed = walkingSpeed * 0.7f + velocity * 0.3f;
                    lastWifiPositionRaw = rawWifiPrediction;
                }
                else
                {
                    lastWifiPositionRaw = rawWifiPrediction;
                }

                // shift positions - newest goes to front
                for (int i = maxPositions - 1; i > 0; i--)
                {
                    wifiPositions[i] = wifiPositions[i - 1];
                }

                wifiPositions[0] = newPosition;
            }

            lastUpdateTimeWifi = currentTime;
            UpdateFloorHistory(rawWifiPrediction.Floor);
        }

        public void UpdateWithIMU(Vector2 ignored, float headingDegrees)
        {
            if (!initialized) return;

            float deltaTime = Time.time - lastUpdateTimeIMU;
            if (deltaTime < minDeltaTime) return;

            // convert heading to movement direction
            float headingRad = -(headingDegrees+90) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));

            // move all positions by walking distance
            Vector2 positionDelta = direction * walkingSpeed * deltaTime;

            for (int i = 0; i < maxPositions; i++)
            {
                Vector3 pos = wifiPositions[i];
                pos.x += positionDelta.x;
                pos.y += positionDelta.y;
                wifiPositions[i] = pos;
            }
            
            lastUpdateTimeIMU = Time.time;
        }

        private void UpdateFloorHistory(int newFloor)
        {
            floorHistory.Add(newFloor);
            if (floorHistory.Count > floorHistorySize)
                floorHistory.RemoveAt(0);

            UpdateFloorEstimate();
        }

        private void UpdateFloorEstimate()
        {
            if (floorHistory.Count == 0) return;

            // count floor occurrences
            Dictionary<int, int> floorCounts = new Dictionary<int, int>();
            foreach (int floor in floorHistory)
            {
                floorCounts[floor] = floorCounts.ContainsKey(floor) ? floorCounts[floor] + 1 : 1;
            }

            // pick most frequent floor
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

        // weighted average of recent positions
        public Position GetEstimate()
        {
            if (!initialized)
                return new Position(0, 0, 0);

            Vector2 weightedSum = Vector2.zero;
            float totalWeight = 0f;

            for (int i = 0; i < maxPositions; i++)
            {
                float weight = Mathf.Pow(0.8f, i); // newer positions matter more
                Vector2 pos2D = new Vector2(wifiPositions[i].x, wifiPositions[i].y);
                weightedSum += pos2D * weight;
                totalWeight += weight;
            }

            Vector2 averagePosition = weightedSum / totalWeight;
            return new Position(averagePosition.x, averagePosition.y, currentFloor);
        }

        public Vector2 GetEstimatedVelocity()
        {
            if (!initialized || wifiPositions.Count < 2) return Vector2.zero;

            Vector2 newest = new Vector2(wifiPositions[0].x, wifiPositions[0].y);
            Vector2 previous = new Vector2(wifiPositions[1].x, wifiPositions[1].y);
            Vector2 direction = (newest - previous).normalized;
            return direction * walkingSpeed;
        }

        public bool IsInitialized => initialized;

        public void Reset()
        {
            // clear everything
            for (int i = 0; i < maxPositions; i++)
            {
                wifiPositions[i] = Vector3.zero;
            }
            floorHistory.Clear();
            initialized = false;
            lastUpdateTimeIMU = Time.time;
        }
    }
}