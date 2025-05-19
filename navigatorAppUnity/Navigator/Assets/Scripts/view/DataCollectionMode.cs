using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using controller;
using model.Database;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace view
{
    public class DataCollectionMode : MonoBehaviour
    {
        private readonly List<GameObject> markers = new();
        private GameObject crosshairMarker; 
        public GameObject WifiMarkerPrefab; 

        private bool active = false;

        public Registry registry;
        
        public Button collectButton;
        public TMP_Text collectButtonText;
        public Button deleteButton;
        public GameObject managePointsDialogPanel;
        public bool compasActive = false;

        public LoadingSpinner spinner;
        public GameObject CrosshairMarkerPrefab; 

        public bool isCollecting = false;
        

        private void Start()
        {
            collectButton.onClick.AddListener(CollectAtCurrentPosition);
            deleteButton.onClick.AddListener(DeleteAtCurrentPosition);

            crosshairMarker = Instantiate(CrosshairMarkerPrefab);

            //crosshairMarker.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f); // Adjust size
            crosshairMarker.transform.rotation = Quaternion.Euler(90, 0, 0);
            crosshairMarker.SetActive(false); // Initially hidden

            Deactivate();
        }

        
        private void Update()
        {
            // Update crosshair when active
            if (active && crosshairMarker != null)
            {
                Vector3 crosshairPos = registry.cameraController.GetCrosshairPosition();
                crosshairPos.y += 0.2f;
                crosshairMarker.transform.position = crosshairPos;
            }
        }
        
        public void Activate()
        {
            Debug.Log("Activate called");
            managePointsDialogPanel.SetActive(true);
            active = true;
            registry.cameraController.DeactivateMarker();

            // Show crosshair marker
            if (crosshairMarker != null)
            {
                crosshairMarker.SetActive(true);
                crosshairMarker.transform.position = registry.cameraController.GetCrosshairPosition();
            }
            
            Refresh();
        }

        public void Deactivate()
        {
            Debug.Log("Data collection mode de-activated");
            active = false;
            managePointsDialogPanel.SetActive(false);
            
            if (registry.cameraController != null && registry.cameraController.positionMarker != null)
            {
                registry.cameraController.positionMarker.SetActive(true);
            }
            
            crosshairMarker.SetActive(false);
            registry.cameraController.ActivateMarker();
            
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
       
            List<Coordinate> coords = registry.database.GetCoordinatesForBuilding(registry.buildingManager.GetActiveBuilding().buildingName)
                .Where(coord => coord.Floor == registry.buildingManager.GetShownFloor()).ToList();

            foreach (var coord in coords)
            {
                Vector3 position = new(coord.X, coord.Floor * 2.0f + 1f, coord.Y);

                GameObject marker = Instantiate(WifiMarkerPrefab, position, Quaternion.identity);

                //marker.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                marker.name = $"CoordMarker_{coord.X}_{coord.Y}";

                markers.Add(marker);
            }


        }

        public void DeleteAtCurrentPosition()
        {
            registry.database.PrintDatabase();
            Vector3 crosshairPos = registry.cameraController.GetCrosshairPosition();
    
            Coordinate closest = null;
            float closestDistance = float.PositiveInfinity;

            List<Coordinate> coords = registry.database.GetCoordinatesForBuilding(registry.buildingManager.GetActiveBuilding().buildingName)
                .Where(coord => coord.Floor == registry.buildingManager.GetShownFloor()).ToList();
            
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
                registry.database.DeleteCoordinate(closest.Id);
                Refresh(); 
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

            // Null checks
            if (registry.cameraController == null) Debug.LogError("cameraController is NULL");
            if (collectButton == null) Debug.LogError("collectButton is NULL");
            if (spinner == null) Debug.LogError("spinner is NULL");
            if (registry.buildingManager == null) Debug.LogError("buildingManager is NULL");

            Vector3 crosshairPos = registry.cameraController.GetCrosshairPosition();
            
            collectButton.interactable = false;
            isCollecting = true;


            spinner.StartSpinning();
            collectButtonText.text = "";
            
            string buildingName = registry.buildingManager.GetActiveBuilding().buildingName;
            int floor = registry.buildingManager.GetShownFloor();
            
            StartCoroutine(CollectDataPointCoroutine(
                crosshairPos.x, crosshairPos.z,
                floor,
                buildingName
            ));
        }

    
        private IEnumerator CollectDataPointCoroutine(float x, float y, int floor, string building)
        {
            Debug.Log($"Starting data collection at ({x}, {y}, Floor: {floor})");
        
            IEnumerator createPointCoroutine = registry.wifiManager.CreateDataPoint(
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
            Debug.Log($"Successfully collected data point with {dataPoint.WifiInfoMap.Count} WiFi networks");
        
            // Reset collection state
            isCollecting = false;
            Refresh();
            spinner.StopSpinning();
            collectButton.interactable = true; 
            collectButtonText.text = "Collect";
        }
        
        private void OnDestroy()
        {
            if (crosshairMarker != null)
            {
                Destroy(crosshairMarker);
            }
        }
    }
}