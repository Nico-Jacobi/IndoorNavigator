using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using model;
using model.Database;
using UnityEngine;

namespace controller
{
    /// <summary>
    /// Handles position tracking and prediction based on WiFi signals.
    /// </summary>
    public class PositionTracker : MonoBehaviour
    {

        public Registry registry;
        
        public int rollingAverageLength = 10;
        
        private List<Position> positions;
        
        // Cache for data points to avoid frequent database calls
        private Dictionary<string, List<Coordinate>> buildingDataPointsCache = new Dictionary<string, List<Coordinate>>();
        private string lastBuilding = string.Empty;

        void Start()
        {
            positions = new List<Position>();
            positions.Add(new Position(0, 0, 3)); // Default starting position
        }

        /// <summary>
        /// Gets the current position based on rolling average of recent predictions.
        /// </summary>
        /// <returns>The averaged position.</returns>
        public Position GetPosition()
        {
            if (positions == null || positions.Count == 0)
                return null;

            // Use a more efficient approach for calculating average
            int count = Math.Min(rollingAverageLength, positions.Count);
            var lastPositions = positions.Skip(positions.Count - count).ToList();

            float avgX = 0, avgY = 0, avgFloor = 0;
            foreach (var pos in lastPositions)
            {
                avgX += pos.X;
                avgY += pos.Y;
                avgFloor += pos.Floor;
            }
            
            return new Position(
                avgX / count, 
                avgY / count, 
                (int)Math.Round(avgFloor / count)
            );
        }

        /// <summary>
        /// Update the position based on WiFi measurements.
        /// </summary>
        /// <param name="wifiNetworks">The current WiFi measurement.</param>
        /// <returns>A coroutine that updates the position.</returns>
        public IEnumerator UpdatePosition(Coordinate wifiNetworks)
        {
            if (wifiNetworks == null || wifiNetworks.WifiInfoMap.Count == 0)
            {
                Debug.Log("Wifi data is empty -> no position update possible");
                yield break;
            }

            // Update building information
            registry.buildingManager.updateBuilding(wifiNetworks);
            string currentBuilding = registry.buildingManager.GetActiveBuilding().buildingName;
            
            // Get cached data points for current building
            List<Coordinate> dataPoints = GetDataPointsForBuilding(currentBuilding);
            if (dataPoints.Count == 0)
            {
                Debug.LogWarning("No recorded data found for this building.");
                yield break;
            }

            // Run computationally expensive comparison on a worker thread
            Task<Position> positionTask = Task.Run(() => {
                // Sort coordinates by similarity - limited to 100 closest matches for performance
                var sorted = dataPoints
                    .OrderBy(coord => coord.CompareWifiSimilarity(wifiNetworks))
                    .Take(100)
                    .ToList();

                // Interpolate using exponential weighting
                float weightedX = 0, weightedY = 0, weightedFloor = 0, totalWeight = 0;
                int actualLength = sorted.Count;

                for (int i = 0; i < actualLength; i++)
                {
                    float weight = (float) Math.Pow(1.2, actualLength - i); // Exponential weighting
                    weightedX += sorted[i].X * weight;
                    weightedY += sorted[i].Y * weight;
                    weightedFloor += sorted[i].Floor * weight;
                    totalWeight += weight;
                }

                float finalX = weightedX / totalWeight;
                float finalY = weightedY / totalWeight;
                float finalFloor = weightedFloor / totalWeight;

                return new Position(finalX, finalY, (int)Math.Round(finalFloor));
            });

            // Wait for task to complete without blocking the main thread
            while (!positionTask.IsCompleted)
            {
                yield return null;
            }

            Position prediction = positionTask.Result;
            Debug.Log($"Predicted Position: X={prediction.X:F2}, Y={prediction.Y:F2}, Floor={prediction.Floor}");

            // Update positions list with the new prediction
            positions.Add(prediction);
            
            // Limit size of positions list
            if (positions.Count > rollingAverageLength * 5)
            {
                positions.RemoveAt(0);
            }
        }

        /// <summary>
        /// Loads data points from database and caches them.
        /// </summary>
        /// <param name="buildingName">The name of the building.</param>
        /// <returns>List of coordinates for the building.</returns>
        private List<Coordinate> GetDataPointsForBuilding(string buildingName)
        {
            if (lastBuilding != buildingName || !buildingDataPointsCache.ContainsKey(buildingName))
            {
                // Load and cache data points for this building
                lastBuilding = buildingName;
                buildingDataPointsCache[buildingName] = registry.database.GetCoordinatesForBuilding(buildingName);
                Debug.Log($"Loaded {buildingDataPointsCache[buildingName].Count} data points for building {buildingName}");
            }
            
            return buildingDataPointsCache[buildingName];
        }

        /// <summary>
        /// Clears the cache for a specific building or all buildings.
        /// </summary>
        /// <param name="buildingName">The building name to clear, or null for all buildings.</param>
        public void ClearCache(string buildingName = null)
        {
            if (buildingName == null)
            {
                buildingDataPointsCache.Clear();
                lastBuilding = string.Empty;
                Debug.Log("Cleared all building data point caches");
            }
            else if (buildingDataPointsCache.ContainsKey(buildingName))
            {
                buildingDataPointsCache.Remove(buildingName);
                if (lastBuilding == buildingName)
                    lastBuilding = string.Empty;
                Debug.Log($"Cleared cache for building {buildingName}");
            }
        }
    }
}