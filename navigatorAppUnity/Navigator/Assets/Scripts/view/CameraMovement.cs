using controller;
using Controller;
using model;
using UnityEngine;

namespace view
{
    public class CameraController : MonoBehaviour
    {
        public Registry registry;
        
        private float moveSpeed = 100f;
        private float touchMoveSpeed = 1.36f;  // 1/cos(cameraAngle) -1 to to have it feel natural

        public bool freeMovement = true;
        public bool inMenu = false;
        public bool compassActive = false;
        
        private float orbitDistance = 10f;  // Distance from camera to orbit point
        private float cameraHeight = 30f;   // Height offset above the floor
        private Vector3 orbitPoint;        // The point we're orbiting around
        
        private float minCameraHeight = 10f;
        private float maxCameraHeight = 100f;
        private float zoomSpeed = 0.1f;

        
        public GameObject markerPrefab;
        public GameObject positionMarker;
        private float markerUpdateTimer = 0f;
        private float markerUpdateInterval = 0.1f;
        private bool markerActive = true;
        
        // Smooth marker movement variables
        private Vector3 markerStartPosition;
        private Vector3 markerTargetPosition;
        private float markerMoveTimer = 0f;
        private float markerMoveDuration = 0.5f; // 1 second smooth transition
        private bool markerIsMoving = false;
        
        private float positionUpdateTimer = 0f;
        private float positionUpdateInterval = 0.5f;

        private float cameraAngle = 45f;
        
        // Touch input variables
        private Vector2 touchLastPos;
        private bool isTouching = false;
        
        private float currentHeading = 0;
        
        

        void Start()
        {
            registry.cam = Camera.main;
            SetCameraTilt(cameraAngle);
            orbitPoint = transform.position;  // Initialize orbit point to current position
            
        }

        void Update()
        {
            if (!inMenu)
            {
                HandleCameraRotation();
                HandleMovementOrTracking();
                HandleTouchZoom();
            }

            if (markerActive)
            {
                HandleMarkerUpdate();
                HandleMarkerSmoothing();
            }
        }

        private void SetCameraTilt(float angle)
        {
            registry.cam.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
        }

        private void HandleCameraRotation()
        {
            float compass_heading = registry.compassReader.GetHeading() + 180;
            
            float newHeading = 0;
            if (!freeMovement && !compassActive)
            {
                newHeading = registry.graphManager.GetHeading();
            }
            
            if (compassActive)
            {
                newHeading = compass_heading;
            }

            currentHeading = newHeading;
            
            

            if (positionMarker != null)
            {
                positionMarker.transform.rotation = Quaternion.Euler(90f, compass_heading, 0f);
            }

            
            
            
            PositionCameraOrbit(newHeading);
            registry.cam.transform.rotation = Quaternion.Euler(cameraAngle, newHeading, 0f);
        }

      
        private void HandleMovementOrTracking()
        {
            if (freeMovement)
            {
                HandleFreeMovement();
            }
            else
            {
                HandlePositionTracking();
            }
        }
        

        // Rotate a 2D vector by the current heading to make movement relative to camera direction
        private Vector2 RotateTouchVectorByHeading(Vector2 input)
        {
            // Convert heading to radians, negate to rotate in correct direction
            float headingRad = -currentHeading * Mathf.Deg2Rad;
            
            // Create rotation matrix
            float cosHeading = Mathf.Cos(headingRad);
            float sinHeading = Mathf.Sin(headingRad);
            
            // Apply rotation to input vector
            return new Vector2(
                input.x * cosHeading - input.y * sinHeading,
                input.x * sinHeading + input.y * cosHeading
            );
        }

        private Vector2 GetTouchMovementInput()
        {
            Vector2 currentMovement = Vector2.zero;

            if (Input.touchCount == 0)
            {
                isTouching = false;
                return Vector2.zero;
            }

            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    isTouching = true;
                    touchLastPos = touch.position;
                    return Vector2.zero;

                case TouchPhase.Moved:
                    if (isTouching)
                    {
                        Vector2 delta = touch.position - touchLastPos;
                        touchLastPos = touch.position;

                        currentMovement = new Vector2(
                            -delta.x * 0.005f,
                            -delta.y * 0.005f
                        );
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isTouching = false;
                    break;
            }

            return currentMovement;
        }
        
        private void HandleTouchZoom()
        {
            if (Input.touchCount == 2)
            {
                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                Vector2 touch0PrevPos = touch0.position - touch0.deltaPosition;
                Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;

                float prevMagnitude = (touch0PrevPos - touch1PrevPos).magnitude;
                float currentMagnitude = (touch0.position - touch1.position).magnitude;

                float difference = currentMagnitude - prevMagnitude;

                cameraHeight -= difference * zoomSpeed;
                cameraHeight = Mathf.Clamp(cameraHeight, minCameraHeight, maxCameraHeight);
                
                touchMoveSpeed = 1.36f * (cameraHeight / 40f); //todo find right value
            }
        }

        
        
        private void HandleMarkerUpdate()
        {
            markerUpdateTimer += Time.deltaTime;

            if (markerUpdateTimer >= markerUpdateInterval)
            {
                MoveMarkerToPosition(registry.GetPositionFilter().GetEstimate());
                markerUpdateTimer = 0f;
            }
        }
        


        public void MoveMarkerToPosition(Position pos)
        {
            if (pos == null || !markerActive) return;
            
            if (positionMarker == null)
            {
                positionMarker = Instantiate(markerPrefab);
                positionMarker.name = "PositionMarker";
            }
            
            bool correctFloor = registry.buildingManager.GetShownFloor() == pos.Floor;
            positionMarker.SetActive(correctFloor);

            if (!correctFloor) return;

            Vector3 newTargetPosition = new Vector3(pos.X, pos.Floor * 2.0f + 1f, pos.Y);
            
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
            
            // If we're not in free movement mode and not currently smoothing, update orbit point immediately for large jumps
            if (!freeMovement && !markerIsMoving)
            {
                orbitPoint = new Vector3(pos.X, pos.Floor * 2.0f, pos.Y);
            }
        }
        
       
        public void GotoPrediction()
        {
           GotoPosition(registry.GetPositionFilter().GetEstimate());
        }

        public void ToggleViewMode()
        {
            freeMovement = !freeMovement;
    
            // When switching to tracking mode, immediately update orbit point to current marker position
            if (!freeMovement && positionMarker != null)
            {
                // Set orbit point to same position as marker
                orbitPoint = positionMarker.transform.position;
            }
        }
        
        /// <summary>
        /// Activates the position marker if it's assigned.
        /// </summary>
        public void ActivateMarker()
        {
            markerActive = true;
            if (positionMarker != null)
            {
                positionMarker.SetActive(true);
            }
        }

        /// <summary>
        /// Deactivates the position marker if it's assigned.
        /// </summary>
        public void DeactivateMarker()
        {
            markerActive = false;
            if (positionMarker != null)
            {
                positionMarker.SetActive(false);
            }
        }
        
        
        // The key insight: The orbit point should be where the camera's center ray hits the target plane

        // Replace your PositionCameraOrbit method:
        private void PositionCameraOrbit(float heading)
        {
            // Convert heading to radians
            float headingRad = heading * Mathf.Deg2Rad;
            
            // Calculate the camera position around the orbit point
            float xPos = orbitPoint.x - Mathf.Sin(headingRad) * orbitDistance;
            float zPos = orbitPoint.z - Mathf.Cos(headingRad) * orbitDistance;
            float yPos = orbitPoint.y + cameraHeight;

            registry.cam.transform.position = new Vector3(xPos, yPos, zPos);
        }

        // Add this new method to calculate where the camera center ray hits the target plane:
        private Vector3 CalculateGroundIntersection(Vector3 cameraPos, Vector3 cameraForward, float targetY)
        {
            // Ray from camera pointing forward
            // We want to find where this ray intersects the plane at height targetY
            
            // Ray equation: point = cameraPos + t * cameraForward
            // Plane equation: y = targetY
            // Solve for t: targetY = cameraPos.y + t * cameraForward.y
            // t = (targetY - cameraPos.y) / cameraForward.y
            
            if (Mathf.Abs(cameraForward.y) < 0.001f) // Avoid division by zero
                return new Vector3(cameraPos.x, targetY, cameraPos.z);
            
            float t = (targetY - cameraPos.y) / cameraForward.y;
            
            Vector3 intersection = cameraPos + t * cameraForward;
            return new Vector3(intersection.x, targetY, intersection.z);
        }

        // Update GetCrosshairPosition to return the actual center of screen in world coords:
        public Vector3 GetCrosshairPosition()
        {
            if (registry.cam == null) return orbitPoint;
            
            // Get the camera's forward direction
            Vector3 cameraForward = registry.cam.transform.forward;
            Vector3 cameraPos = registry.cam.transform.position;
            
            // Calculate where the center ray hits the current floor level, height should be z
            float targetY = registry.buildingManager.GetShownFloor() * 2.0f + 2f; // Same as marker height
            
            Vector3 pos = CalculateGroundIntersection(cameraPos, cameraForward, targetY);
            pos.y = registry.buildingManager.GetShownFloor() * 2.0f + 1f;
            
            return pos;
        }

        // Update HandleFreeMovement to maintain proper relationship:
        private void HandleFreeMovement()
        {
            Vector3 movement = Vector3.zero;

            // Handle keyboard input (WASD)
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            
            // Handle touch input
            Vector2 touchMovement = GetTouchMovementInput();
            
            // Apply touch movement, with heading compensation
            if (touchMovement.magnitude > 0)
            {
                Vector2 rotatedMovement = RotateTouchVectorByHeading(touchMovement);
                h += rotatedMovement.x * touchMoveSpeed;
                v += rotatedMovement.y * touchMoveSpeed;
            }
            else
            {
                h += touchMovement.x * touchMoveSpeed;
                v += touchMovement.y * touchMoveSpeed;
            }

            movement = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
            
            // Move the orbit point horizontally
            orbitPoint += movement;
            
            // Keep orbit point at the intersection level (where camera looks)
            float targetY = registry.buildingManager.GetShownFloor() * 2.0f + 1f;
            orbitPoint = new Vector3(orbitPoint.x, targetY, orbitPoint.z);
        }

        // Update HandlePositionTracking:
        private void HandlePositionTracking()
        {
            positionUpdateTimer += Time.deltaTime;

            if (positionUpdateTimer >= positionUpdateInterval)
            {
                Position pos = registry.GetPositionFilter().GetEstimate();
                if (pos != null)
                {
                    // Set orbit point to marker level (this is where we want the camera to look)
                    orbitPoint = new Vector3(pos.X, pos.Floor * 2.0f + 1f, pos.Y);
                    registry.buildingManager.SpawnBuildingFloor(registry.buildingManager.GetActiveBuilding().buildingName, pos.Floor);
                }
                positionUpdateTimer = 0f;
            }
        }

        public void GotoPosition(Position pos)
        {
            if (pos == null) return;

            Debug.Log($"Current Position: {pos}");
    
            // The target position we want to show at 2/3 down the screen
            Vector3 targetPosition = new Vector3(pos.X, pos.Floor * 2.0f + 1f, pos.Y);
    
            // Calculate where the orbit point should be so that targetPosition appears at 2/3 down screen
            // Using the camera's current heading and tilt
            float headingRad = currentHeading * Mathf.Deg2Rad;
            float tiltRad = cameraAngle * Mathf.Deg2Rad;
    
            // Calculate offset for 2/3 position (0.5 would be center, 0.33 moves it up toward 2/3 down)
            float screenPositionFactor = 0.33f; // This positions target at 2/3 down the screen
            float horizontalOffset = (cameraHeight / Mathf.Tan(tiltRad)) * screenPositionFactor;
    
            // Apply the offset in the direction the camera is facing
            Vector3 offsetDirection = new Vector3(Mathf.Sin(headingRad), 0, Mathf.Cos(headingRad));
            orbitPoint = targetPosition - offsetDirection * horizontalOffset;
    
            registry.buildingManager.SpawnBuildingFloor(registry.buildingManager.GetActiveBuilding().buildingName, pos.Floor);
        }

        // The HandleMarkerSmoothing method stays mostly the same:
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
                
                // If we're not in free movement mode, update the orbit point smoothly too
                if (!freeMovement)
                {
                    // Keep orbit point at same height as marker (this keeps the marker centered)
                    orbitPoint = Vector3.Lerp(markerStartPosition, markerTargetPosition, smoothProgress);
                }
            }
        }

        // Update OnViewModeButtonPressed:
        public void OnViewModeButtonPressed()
        {
            freeMovement = !freeMovement;
            
            // When switching to tracking mode, set orbit point to marker position
            if (!freeMovement && positionMarker != null)
            {
                orbitPoint = positionMarker.transform.position;
            }
            else if (freeMovement)
            {
                // When switching to free movement, set orbit point to current crosshair position
                orbitPoint = GetCrosshairPosition();
            }
        }
        
    }
}