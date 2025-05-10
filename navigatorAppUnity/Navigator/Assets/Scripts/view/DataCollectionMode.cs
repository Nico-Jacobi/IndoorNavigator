using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using controller;
using model.Database;
using UnityEngine;
using UnityEngine.UI; // Added for Button references

namespace view
{
    public class DataCollectionMode : MonoBehaviour
    {
        private readonly List<GameObject> markers = new();

        private bool active = false;
        public SQLiteDatabase database;
        public BuildingManager buildingManager;
        public CameraController cameraController;
        public WifiManager wifiManager;
        
        public Button collectButton;
        public Button deleteButton;
        public GameObject dialogPanel;

        public LoadingSpinner spinner;
        
        public bool isCollecting = false;

        private void Start()
        {

            collectButton.onClick.AddListener(CollectAtCurrentPosition);
            deleteButton.onClick.AddListener(DeleteAtCurrentPosition);
            Deactivate();
        }
        

        
        public void Activate()
        {
            Debug.Log("Activate called");
            dialogPanel.SetActive(true);
            active = true;
            Refresh();
        }

        public void Deactivate()
        {
            active = false;
            dialogPanel.SetActive(false);
            foreach (var marker in markers)
            {
                Destroy(marker);
            }
            markers.Clear();
        }

        public void Refresh()
        {
            if (!active) return;

            foreach (var marker in markers)
            {
                Destroy(marker);
            }
            markers.Clear();
       

        List<Coordinate> coords  = database.GetCoordinatesForBuilding(buildingManager.GetActiveBuilding().buildingName)
                .Where(coord => coord.Floor == buildingManager.GetShownFloor()).ToList();

            
            foreach (var coord in coords)
            {
                Vector3 position = new(coord.X, coord.Floor * 2.0f + 1f, coord.Y);
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.transform.position = position;
                //marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // make it smaller
                marker.name = $"CoordMarker_{coord.X}_{coord.Y}";

                // Now access the Renderer after the marker is instantiated
                Renderer rend = marker.GetComponent<Renderer>();
                rend.material.color = Color.blue;  // Set the color to blue

                markers.Add(marker);
            }
        }

        public void DeleteAtCurrentPosition()
        {
            Vector3 crosshairPos = cameraController.GetCrosshairPosition();
    
            Coordinate closest = null;
            float closestDistance = float.PositiveInfinity;

            List<Coordinate> coords  = database.GetCoordinatesForBuilding(buildingManager.GetActiveBuilding().buildingName)
                .Where(coord => coord.Floor == buildingManager.GetShownFloor()).ToList();
            
            foreach (Coordinate coord in coords)
            {
                // Calculate distance using only X and Z coordinates
                float distance = Vector3.Distance(
                    new Vector3(coord.X, 0, coord.Y), 
                    new Vector3(crosshairPos.x, 0, crosshairPos.z) 
                );
    
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = coord;
                }
            }

            if (closest != null)
            {
                database.DeleteCoordinate(closest.Id);
                Refresh(); // Refresh the view to reflect the deletion
            }
        }

        
        /// <summary>
        /// Collects a WiFi data point at the current position shown by the crosshair
        /// </summary>
        public void CollectAtCurrentPosition()
        {
            if (isCollecting)
            {
                Debug.Log("Already collecting data point, please wait...");
                return;
            }
        
            // Get the position where the crosshair is pointing
            Vector3 crosshairPos = cameraController.GetCrosshairPosition();
            
            collectButton.interactable = false; 
            isCollecting = true;
            spinner.StartSpinning();
            collectButton.GetComponentInChildren<Text>().text = "";
        
            // Start the data collection coroutine
            StartCoroutine(CollectDataPointCoroutine(crosshairPos.x, crosshairPos.z,  buildingManager.GetShownFloor(),  buildingManager.GetActiveBuilding().buildingName));
        }
    
        private IEnumerator CollectDataPointCoroutine(float x, float y, int floor, string building)
        {
            Debug.Log($"Starting data collection at ({x}, {y}, Floor: {floor})");
        
            // Call the WiFi manager to create a data point with multiple measurements
            IEnumerator createPointCoroutine = wifiManager.CreateDataPoint(
                x, y, floor, building, 
                true,  // Save to database
                OnDataPointCollected  // Callback when done
            );
        
            // Wait for the coroutine to complete
            while (createPointCoroutine.MoveNext())
            {
                yield return createPointCoroutine.Current;
            }
        }
        
        
        private void OnDataPointCollected(Coordinate dataPoint)
        {
            // Handle the collected data point (e.g., update UI, etc.)
            Debug.Log($"Successfully collected data point with {dataPoint.WifiInfoMap.Count} WiFi networks");
        
            // Reset collection state
            isCollecting = false;
            Refresh();
            spinner.StopSpinning();
            collectButton.interactable = true; 
            collectButton.GetComponentInChildren<Text>().text = "Collect";

        }
    }
}