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
        private float cameraHeight = 40f;   // Height offset above the floor
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
        
        // Touch input variables
        private Vector2 touchLastPos;
        private bool isTouching = false;
        
        private float currentHeading = 0;
        
        

        void Start()
        {
            registry.cam = Camera.main;
            SetCameraTilt(65f);
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
            registry.cam.transform.rotation = Quaternion.Euler(65f, newHeading, 0f);
        }

        private void PositionCameraOrbit(float heading)
        {
            // Convert heading to radians
            float headingRad = heading * Mathf.Deg2Rad;
            
            // Calculate the camera position around the orbit point
            float xPos = orbitPoint.x - Mathf.Sin(headingRad) * orbitDistance;
            float zPos = orbitPoint.z - Mathf.Cos(headingRad) * orbitDistance;
            float yPos = registry.buildingManager.GetShownFloor() * 2.0f + cameraHeight;

            registry.cam.transform.position = new Vector3(xPos, yPos, zPos);
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
                // Rotate the touch movement vector to account for camera heading
                Vector2 rotatedMovement = RotateTouchVectorByHeading(touchMovement);
                h += rotatedMovement.x * touchMoveSpeed;
                v += rotatedMovement.y * touchMoveSpeed;
            }
            else
            {
                // No touch movement, use regular keyboard input
                h += touchMovement.x * touchMoveSpeed;
                v += touchMovement.y * touchMoveSpeed;
            }

            movement = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
            
            orbitPoint += movement;
            
            float y = registry.buildingManager.GetShownFloor() * 2.0f + cameraHeight;
            orbitPoint = new Vector3(orbitPoint.x, y - cameraHeight, orbitPoint.z);
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



        private void HandlePositionTracking()
        {
            positionUpdateTimer += Time.deltaTime;

            if (positionUpdateTimer >= positionUpdateInterval)
            {
                Position pos = registry.kalmanFilter.GetEstimate();
                if (pos != null)
                {
                    // Update the orbit point to the marker position
                    orbitPoint = new Vector3(pos.X, pos.Floor * 2.0f, pos.Y);
                    registry.buildingManager.SpawnBuildingFloor(registry.buildingManager.GetActiveBuilding().buildingName, pos.Floor);
                }
                positionUpdateTimer = 0f;
            }
        }
        
        private void HandleMarkerUpdate()
        {
            markerUpdateTimer += Time.deltaTime;

            if (markerUpdateTimer >= markerUpdateInterval)
            {
                MoveMarkerToPosition(registry.kalmanFilter.GetEstimate());
                markerUpdateTimer = 0f;
            }
        }

        private void HandleMarkerSmoothing()
        {
            if (markerIsMoving && positionMarker != null)
            {
                markerMoveTimer += Time.deltaTime;
                float progress = markerMoveTimer / markerMoveDuration;
                
                if (progress >= 1f)
                {
                    // Movement complete
                    progress = 1f;
                    markerIsMoving = false;
                }
                
                // Smooth interpolation using ease-in-out curve
                float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
                Vector3 currentPosition = Vector3.Lerp(markerStartPosition, markerTargetPosition, smoothProgress);
                
                positionMarker.transform.position = currentPosition;
                
                // If we're not in free movement mode, update the orbit point smoothly too
                if (!freeMovement)
                {
                    Vector3 orbitTarget = new Vector3(markerTargetPosition.x, markerTargetPosition.y - 1f, markerTargetPosition.z);
                    Vector3 orbitStart = new Vector3(markerStartPosition.x, markerStartPosition.y - 1f, markerStartPosition.z);
                    orbitPoint = Vector3.Lerp(orbitStart, orbitTarget, smoothProgress);
                }
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
        
        public void GotoPosition(Position pos)
        {
            if (pos == null) return;

            Debug.Log($"Current Position: {pos}");
            
            // Update the orbit point
            orbitPoint = new Vector3(pos.X, pos.Floor * 2.0f, pos.Y);
            
            // Handle camera positioning will be done in Update()
            registry.buildingManager.SpawnBuildingFloor(registry.buildingManager.GetActiveBuilding().buildingName, pos.Floor);
        }
        
        public void GotoPrediction()
        {
           GotoPosition(registry.kalmanFilter.GetEstimate());
        }

        public void OnViewModeButtonPressed()
        {
            freeMovement = !freeMovement;
            
            // When switching to tracking mode, immediately update orbit point to current marker position
            if (!freeMovement && positionMarker != null)
            {
                orbitPoint = new Vector3(
                    positionMarker.transform.position.x,
                    positionMarker.transform.position.y - 1f, // Adjust for marker height offset
                    positionMarker.transform.position.z
                );
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
        
        public Vector3 GetCrosshairPosition()
        {
            // Return the orbit point as the theoretical point we're rotating around
            return orbitPoint;
        }
    }
}