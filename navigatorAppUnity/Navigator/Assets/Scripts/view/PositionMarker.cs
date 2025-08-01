using System;
using controller;
using model;
using UnityEngine;

namespace view
{
    public class PositionMarker : MonoBehaviour
    {
        public Registry registry;
        public GameObject markerPrefab;
        
        private GameObject positionMarker;
        private float markerUpdateTimer = 0f;
        private float markerUpdateInterval = 0.1f;
        private bool markerActive = true;
        private bool markerWasHidden = true;    //when this flag is set the next time the marker is shown its moved into position without animation
        private bool markerInShownBuilding = true;
        
        private float markerInShownBuildingTimer = 0f;
        private float markerInShownBuildingInterval = 1f;
        
        // Smooth marker movement variables
        private Vector3 markerStartPosition;
        private Vector3 markerTargetPosition;
        private float markerMoveTimer = 0f;
        private float markerMoveDuration = 0.5f;
        private bool markerIsMoving = false;
        
        public bool IsActive => markerActive;
        public GameObject MarkerObject => positionMarker;


        private void Start()
        {
            if (markerPrefab == null)
            {
                Debug.LogError("markerPrefab is not assigned!");
                return;
            }
            positionMarker = Instantiate(markerPrefab);
            positionMarker.name = "PositionMarker";
        }


        void Update()
        {
            markerInShownBuildingTimer += Time.deltaTime;
            if (markerInShownBuildingTimer >= markerInShownBuildingInterval)
            {
                UpdateMarkerInShownBuilding();
                markerInShownBuildingTimer = 0f;
            }

            if (markerActive && markerInShownBuilding)
            {
                HandleMarkerUpdate();
                HandleMarkerSmoothing();
            }

            if (registry.wifiPositionTracker.foundPosition)
            {
                ActivateMarker();
            }
            else
            {
                DeactivateMarker();
            }

            //Debug.Log($"IsInShownBuilding: {markerInShownBuilding}");
            //Debug.Log($"Marker Active: {markerActive}");
        }

        
        
        /// <summary>
        /// Checks if the marker's position is within the currently shown building and floor,
        /// updates visibility and resets movement state if needed.
        /// </summary>
        public void UpdateMarkerInShownBuilding()
        {
            var estimate = registry.GetPositionFilter().GetEstimate();
            markerInShownBuilding = 
                estimate.Floor == registry.buildingManager.GetShownFloor() &&
                registry.buildingManager.GetShownBuilding().buildingName == registry.buildingManager.GetActiveBuilding().buildingName;
            
            
            if (!markerInShownBuilding)
            {
                markerWasHidden = true;
            }
            //Debug.Log($"Estimate.Floor: {estimate.Floor}");
            //Debug.Log($"ShownFloor: {registry.buildingManager.GetShownFloor()}");
            //Debug.Log($"ShownBuilding: {registry.buildingManager.GetShownBuilding().buildingName}");
            //Debug.Log($"ActiveBuilding: {registry.buildingManager.GetActiveBuilding().buildingName}");

            UpdateMarkerVisibility();
        }
        
        /// <summary>
        /// Handles periodic marker position updates based on position filter estimates.
        /// </summary>
        private void HandleMarkerUpdate()
        {
            markerUpdateTimer += Time.deltaTime;

            if (markerUpdateTimer >= markerUpdateInterval)
            {
                MoveMarkerToPosition(registry.GetPositionFilter().GetEstimate());
                markerUpdateTimer = 0f;
            }
        }
        
        
        /// <summary>
        /// Moves the marker to a new position, using smooth movement unless marker was hidden.
        /// Skips movement if marker is inactive or outside shown building.
        /// </summary>
        public void MoveMarkerToPosition(Position pos)
        {
            if (pos == null || !markerActive || !markerInShownBuilding) return;
            
            bool correctFloor = registry.buildingManager.GetShownFloor() == pos.Floor;
            positionMarker.SetActive(correctFloor);

            if (!correctFloor) return;

            Vector3 newTargetPosition = new Vector3(pos.X, pos.Floor * 2.0f + 1f, pos.Y);
            
            // If marker was just activated, move instantly without smoothing
            if (markerWasHidden)
            {
                positionMarker.transform.position = newTargetPosition;
                markerIsMoving = false;
                markerWasHidden = false;
                return;
            }
            
            // Check if this is a significant position change (threshold to avoid micro-movements)
            float positionChangeThreshold = 0.1f;
            if (Vector3.Distance(positionMarker.transform.position, newTargetPosition) > positionChangeThreshold)
            {
                // Start smooth movement
                markerStartPosition = positionMarker.transform.position;
                markerTargetPosition = newTargetPosition;
                markerMoveTimer = 0f;
                markerIsMoving = true;
            }
            else if (!markerIsMoving)
            {
                // Small movement, just update directly
                positionMarker.transform.position = newTargetPosition;
            }
        }
        
        
        /// <summary>
        /// Smoothly interpolates the marker's position over time during movement animation.
        /// </summary>
        private void HandleMarkerSmoothing()
        {
            if (markerIsMoving && positionMarker != null)
            {
                markerMoveTimer += Time.deltaTime;
                float progress = markerMoveTimer / markerMoveDuration;
                
                if (progress >= 1f)
                {
                    progress = 1f;
                    markerIsMoving = false;
                }
                
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
                Vector3 currentPosition = Vector3.Lerp(markerStartPosition, markerTargetPosition, smoothProgress);
                
                positionMarker.transform.position = currentPosition;
            }
        }
        
        /// <summary>
        /// Activates the marker and updates visibility accordingly.
        /// </summary>
        public void ActivateMarker()
        {
            markerActive = true;
            UpdateMarkerVisibility();
        }
        
        
        /// <summary>
        /// Deactivates the marker, marks it as hidden, and updates visibility.
        /// </summary>
        public void DeactivateMarker()
        {
            markerActive = false;
            markerWasHidden = true;
            UpdateMarkerVisibility();
        }
        
        
        /// <summary>
        /// Updates the active state of the marker GameObject based on current active and building/floor state.
        /// </summary>
        private void UpdateMarkerVisibility()
        {
            //Debug.Log($"marker visible: {markerActive && markerInShownBuilding}");
            if (positionMarker != null)
            {
                positionMarker.SetActive(markerActive && markerInShownBuilding);
            }
        }
        
        
        /// <summary>
        /// Sets the rotation of the marker to face the given heading in degrees.
        /// </summary>
        public void SetMarkerRotation(float heading)
        {
            if (positionMarker != null)
            {
                positionMarker.transform.rotation = Quaternion.Euler(90f, heading, 0f);
            }
        }
        
        
        /// <summary>
        /// Returns the current world position of the marker.
        /// </summary>
        public Vector3 GetMarkerPosition()
        {
            if (positionMarker != null)
            {
                return positionMarker.transform.position;
            }
            return Vector3.zero;
        }
        
        
        /// <summary>
        /// Returns whether the marker is currently moving smoothly.
        /// </summary>
        public bool IsMoving()
        {
            return markerIsMoving;
        }
        
        
        /// <summary>
        /// Gets the current target position the marker is moving towards,
        /// or its current position if not moving.
        /// </summary>
        public Vector3 GetCurrentTargetPosition()
        {
            return markerIsMoving ? markerTargetPosition : GetMarkerPosition();
        }
    }
}