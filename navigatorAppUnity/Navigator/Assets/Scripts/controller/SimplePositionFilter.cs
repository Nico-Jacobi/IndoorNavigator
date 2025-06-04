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
        
        private float lastUpdateTimeWifi = 0f; // add this field

        // Store WiFi positions with their timestamps
        private List<Vector3> wifiPositions; // x, y, floor
        private List<int> floorHistory;
        private int currentFloor;

        private float lastUpdateTimeIMU;
        private bool initialized = false;

        // Debug visualization objects
        private List<GameObject> debugSpheres;
        private GameObject estimateSphere;

        [Header("Debug Visualization")]
        public bool enableDebugVisualization = true;
        public GameObject debugSpherePrefab;

        private void Awake()
        {
            wifiPositions = new List<Vector3>();
            floorHistory = new List<int>();
            debugSpheres = new List<GameObject>();
            lastUpdateTimeIMU = Time.time;

            // Initialize positions with zero
            for (int i = 0; i < maxPositions; i++)
            {
                wifiPositions.Add(Vector3.zero);
            }

            if (enableDebugVisualization)
                SetupDebugVisualization();
        }


        public void UpdateWithWifi(Position rawWifiPrediction)
        {
            if (rawWifiPrediction == null) return;

            Vector3 newPosition = new Vector3(rawWifiPrediction.X, rawWifiPrediction.Y, rawWifiPrediction.Floor); 

            float velocity = 0f;
            float currentTime = Time.time;

            if (!initialized)
            {
                for (int i = 0; i < maxPositions; i++)
                {
                    wifiPositions[i] = newPosition;
                }
                currentFloor = rawWifiPrediction.Floor;
                initialized = true;
            }
            else
            {
                float deltaTime = currentTime - lastUpdateTimeWifi;
                if (deltaTime > 0)
                {
                    float distance = Vector3.Distance(newPosition, wifiPositions[0]);
                    velocity = distance / deltaTime;

                    walkingSpeed = walkingSpeed * 0.2f + velocity * 0.8f;   //adjusting walkink speed to the users speed

                }

                for (int i = maxPositions - 1; i > 0; i--)
                {
                    wifiPositions[i] = wifiPositions[i - 1];
                }

                wifiPositions[0] = newPosition;
            }

            lastUpdateTimeWifi = currentTime;
            UpdateFloorHistory(rawWifiPrediction.Floor);

            if (enableDebugVisualization)
                UpdateDebugVisualization();
        }


        public void UpdateWithIMU(Vector2 ignored, float headingDegrees)
        {
            
            if (!initialized) return;

            float deltaTime = Time.time - lastUpdateTimeIMU;
            if (deltaTime < minDeltaTime) return;

            float headingRad = -(headingDegrees+90) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(headingRad), Mathf.Sin(headingRad));


            Vector2 positionDelta = direction * walkingSpeed * deltaTime;

            for (int i = 0; i < maxPositions; i++)
            {
                Vector3 pos = wifiPositions[i];
                pos.x += positionDelta.x;
                pos.y += positionDelta.y;
                wifiPositions[i] = pos;
            }
            
            lastUpdateTimeIMU = Time.time;

            if (enableDebugVisualization)
                UpdateDebugVisualization();
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

            Dictionary<int, int> floorCounts = new Dictionary<int, int>();
            foreach (int floor in floorHistory)
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

        // Calculate weighted average position with exponentially decaying weights
        public Position GetEstimate()
        {
            if (!initialized)
                return new Position(0, 0, 0);

            Vector2 weightedSum = Vector2.zero;
            float totalWeight = 0f;

            for (int i = 0; i < maxPositions; i++)
            {
                float weight = Mathf.Pow(0.8f, i); // exponential decay: newest=1, oldest ~0
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
            for (int i = 0; i < maxPositions; i++)
            {
                wifiPositions[i] = Vector3.zero;
            }
            floorHistory.Clear();
            initialized = false;
            lastUpdateTimeIMU = Time.time;

            if (enableDebugVisualization)
            {
                foreach (var sphere in debugSpheres)
                {
                    sphere.SetActive(false);
                }
            }
        }

        // Debug visualization setup and update
        private void SetupDebugVisualization()
        {
            if (debugSpherePrefab == null)
            {
                Debug.LogWarning("Debug Sphere Prefab not assigned!");
                return;
            }

            for (int i = 0; i < maxPositions; i++)
            {
                var sphere = Instantiate(debugSpherePrefab);
                sphere.name = $"DebugSphere_{i}";
                sphere.transform.localScale = Vector3.one * 50f;
                sphere.SetActive(true);
                debugSpheres.Add(sphere);
            }
            Debug.Log("Setting up debug visualization");

        }

        private void UpdateDebugVisualization()
        {
            for (int i = 0; i < maxPositions; i++)
            {
                Vector3 pos = wifiPositions[i];
                Vector3 correctPos = new Vector3(pos.x, pos.z*2 +1 ,pos.y);
                debugSpheres[i].transform.position = correctPos;
                debugSpheres[i].SetActive(true);


                // Optionally color by weight (newer = brighter)
                float weight = Mathf.Pow(0.8f, i);
                Color c = Color.Lerp(Color.red, Color.green, weight); // red oldest, green newest
                var renderer = debugSpheres[i].GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = c;
                }
            }
        }


        private void OnDestroy()
        {
            if (debugSpheres != null)
            {
                foreach (var sphere in debugSpheres)
                {
                    if (sphere != null)
                        DestroyImmediate(sphere);
                }
            }
        }
    }
}
