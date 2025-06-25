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

        public RectTransform collectIcon;
        public LoadingSpinner spinner;
        public GameObject CrosshairMarkerPrefab; 

        public bool isCollecting = false;

        // Animation properties
        public float slideDuration = 0.4f;
        private RectTransform panelRect;
        private Vector2 visiblePos;
        private Vector2 hiddenPos;
        private Coroutine currentSlideCoroutine;
        private bool isAnimating = false;

        private void Start()
        {
            collectButton.onClick.AddListener(CollectAtCurrentPosition);
            deleteButton.onClick.AddListener(DeleteAtCurrentPosition);

            crosshairMarker = Instantiate(CrosshairMarkerPrefab);
            crosshairMarker.transform.rotation = Quaternion.Euler(90, 0, 0);
            crosshairMarker.SetActive(false);

            // Initialize slide animation
            InitializeSlideAnimation();

            Deactivate();
        }

        private void InitializeSlideAnimation()
        {
            if (managePointsDialogPanel != null)
            {
                panelRect = managePointsDialogPanel.GetComponent<RectTransform>();
                if (panelRect == null)
                {
                    Debug.LogError("managePointsDialogPanel must have a RectTransform component!");
                    return;
                }

                // Store the visible position (current position)
                visiblePos = panelRect.anchoredPosition;
                
                // Calculate hidden position (slide down off-screen)
                // Adjust this value based on your panel height and desired effect
                float panelHeight = panelRect.rect.height;
                hiddenPos = visiblePos + new Vector2(0, -panelHeight - 50f);

                // Start in hidden position
                panelRect.anchoredPosition = hiddenPos;
                managePointsDialogPanel.SetActive(true); // Keep active for animations
            }
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
            if (isAnimating) return;

            Debug.Log("Activate called");
            active = true;
            
            //deactivate the normal pos marker
            registry.positionMarker.DeactivateMarker();

            // Show crosshair marker
            if (crosshairMarker != null)
            {
                crosshairMarker.SetActive(true);
                crosshairMarker.transform.position = registry.cameraController.GetCrosshairPosition();
            }

            // Slide panel up from bottom
            SlideUp();
            
            Refresh();
        }

        public void Deactivate()
        {
            if (isAnimating) return;

            Debug.Log("Data collection mode de-activated");
            active = false;
            
            
            crosshairMarker.SetActive(false);
            registry.positionMarker.ActivateMarker();
            
            foreach (var marker in markers)
            {
                Destroy(marker);
            }
            markers.Clear();

            // Slide panel down
            SlideDown();
        }

        private void SlideUp()
        {
            if (panelRect == null) return;

            if (currentSlideCoroutine != null)
            {
                StopCoroutine(currentSlideCoroutine);
            }

            currentSlideCoroutine = StartCoroutine(SlideToPosition(visiblePos));
        }

        private void SlideDown()
        {
            if (panelRect == null) return;

            if (currentSlideCoroutine != null)
            {
                StopCoroutine(currentSlideCoroutine);
            }

            currentSlideCoroutine = StartCoroutine(SlideToPosition(hiddenPos));
        }

        private IEnumerator SlideToPosition(Vector2 targetPos)
        {
            isAnimating = true;
            Vector2 startPos = panelRect.anchoredPosition;
            float time = 0f;

            // Ensure panel is active during animation
            if (!managePointsDialogPanel.activeInHierarchy)
            {
                managePointsDialogPanel.SetActive(true);
            }

            while (time < slideDuration)
            {
                time += Time.deltaTime;
                float t = time / slideDuration;
                
                // Use smooth easing for more natural movement
                t = Mathf.SmoothStep(0f, 1f, t);
                
                panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            // Ensure final position is exact
            panelRect.anchoredPosition = targetPos;
            
            // If sliding down to hidden position, deactivate panel
            if (Vector2.Distance(targetPos, hiddenPos) < 0.1f)
            {
                managePointsDialogPanel.SetActive(false);
            }

            isAnimating = false;
            currentSlideCoroutine = null;
        }

        public void Refresh()
        {
            if (!active) return;

            foreach (var marker in markers)
            {
                Destroy(marker);
            }
            markers.Clear();
       
            List<Coordinate> coords = registry.database.GetCoordinatesForBuilding(registry.buildingManager.GetShownBuilding().buildingName)
                .Where(coord => coord.Floor == registry.buildingManager.GetShownFloor()).ToList();

            foreach (var coord in coords)
            {
                Vector3 position = new(coord.X, coord.Floor * 2.0f + 1f, coord.Y);

                GameObject marker = Instantiate(WifiMarkerPrefab, position, Quaternion.identity);
                marker.name = $"CoordMarker_{coord.X}_{coord.Y}";

                markers.Add(marker);
            }
        }

        public void DeleteAtCurrentPosition()
        {
            Vector3 crosshairPos = registry.cameraController.GetCrosshairPosition();
    
            Coordinate closest = null;
            float closestDistance = float.PositiveInfinity;

            List<Coordinate> coords = registry.database.GetCoordinatesForBuilding(registry.buildingManager.GetShownBuilding().buildingName)
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
            collectIcon.gameObject.SetActive(false);
            
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
            
            string buildingName = registry.buildingManager.GetShownBuilding().buildingName;
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
            collectIcon.gameObject.SetActive(true);

            Debug.Log($"Successfully collected data point with {dataPoint.WifiInfoMap.Count} WiFi networks");
        
            // Reset collection state
            isCollecting = false;
            Refresh();
            spinner.StopSpinning();
            collectButton.interactable = true; 
            collectButtonText.text = "Collect";
        }

        // Public methods to manually control animation (useful for debugging)
        public void ForceSlideUp()
        {
            SlideUp();
        }

        public void ForceSlideDown()
        {
            SlideDown();
        }
        
        private void OnDestroy()
        {
            if (crosshairMarker != null)
            {
                Destroy(crosshairMarker);
            }

            // Stop any running animations
            if (currentSlideCoroutine != null)
            {
                StopCoroutine(currentSlideCoroutine);
            }
        }
    }
}