using controller;
using Controller;
using model;
using UnityEngine;

namespace view
{
    public class CameraController : MonoBehaviour
    {
        private float moveSpeed = 100f;
        private float touchMoveSpeed = 3f;

        public bool freeMovement = true;
        public bool inMenu = false;
        
        private readonly float orbitDistance = 10f;  // Distance from camera to orbit point
        private readonly float cameraHeight = 40f;   // Height offset above the floor
        private Vector3 orbitPoint;        // The point we're orbiting around

        public bool compasActive = true;

        public GameObject markerPrefab;
        public GameObject positionMarker;
        private float markerUpdateTimer = 0f;
        private float markerUpdateInterval = 1f;
        private bool  markerActive = true;

        
        private float positionUpdateTimer = 0f;
        private float positionUpdateInterval = 0.5f;
        
        public Camera cam;
        public WifiManager wifiManager;
        public BuildingManager buildingManager;
        public CompassReader compass;
        public GraphManager graphManager;

        // Touch input variables
        private Vector2 touchLastPos;
        private bool isTouching = false;
        private float touchSensitivity = 1.0f; // Adjust to increase/decrease touch movement sensitivity

        void Start()
        {
            cam = Camera.main;
            SetCameraTilt(65f);
            orbitPoint = transform.position;  // Initialize orbit point to current position
        }

        void Update()
        {
            if (!inMenu)
            {
                HandleCameraRotation();
                HandleMovementOrTracking();
            }

            if (markerActive)
            {
                HandleMarkerUpdate();
            }
        }

        private void SetCameraTilt(float angle)
        {
            cam.transform.rotation = Quaternion.Euler(angle, 0f, 0f);
        }

        private void HandleCameraRotation()
        {
            float heading = compasActive ? compass.GetHeading() : graphManager.GetHeading();
            PositionCameraOrbit(heading);
            cam.transform.rotation = Quaternion.Euler(65f, heading, 0f);
        }

        private void PositionCameraOrbit(float heading)
        {
            // Convert heading to radians
            float headingRad = heading * Mathf.Deg2Rad;
            
            // Calculate the camera position around the orbit point
            float xPos = orbitPoint.x - Mathf.Sin(headingRad) * orbitDistance;
            float zPos = orbitPoint.z - Mathf.Cos(headingRad) * orbitDistance;
            float yPos = buildingManager.GetShownFloor() * 2.0f + cameraHeight;

            cam.transform.position = new Vector3(xPos, yPos, zPos);
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
            h += touchMovement.x * touchMoveSpeed;
            v += touchMovement.y * touchMoveSpeed;

            movement = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
            
            // Move the orbit point instead of directly moving the camera
            orbitPoint += movement;
            
            // Camera position will be updated in HandleCameraRotation()
            float y = buildingManager.GetShownFloor() * 2.0f + cameraHeight;
            orbitPoint = new Vector3(orbitPoint.x, y - cameraHeight, orbitPoint.z);
        }

        private Vector2 GetTouchMovementInput()
        {
            Vector2 movement = Vector2.zero;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        isTouching = true;
                        break;

                    case TouchPhase.Moved:
                        if (isTouching)
                        {
                            Vector2 delta = touch.deltaPosition;
                            movement.x = -delta.x * 0.01f;
                            movement.y = -delta.y * 0.01f;

                        }
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        isTouching = false;
                        break;
                }
            }

            return movement;
        }


        private void HandlePositionTracking()
        {
            positionUpdateTimer += Time.deltaTime;

            if (positionUpdateTimer >= positionUpdateInterval)
            {
                Position pos = wifiManager.GetPosition();
                if (pos != null)
                {
                    // Update the orbit point to the marker position
                    orbitPoint = new Vector3(pos.X, pos.Floor * 2.0f, pos.Y);
                    buildingManager.SpawnBuildingFloor(buildingManager.GetActiveBuilding().buildingName, pos.Floor);
                }
                positionUpdateTimer = 0f;
            }
        }
        
        private void HandleMarkerUpdate()
        {
            markerUpdateTimer += Time.deltaTime;

            if (markerUpdateTimer >= markerUpdateInterval)
            {
                MoveMarkerToPosition(wifiManager.GetPosition());
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
            
            bool correctFloor = buildingManager.GetShownFloor() == pos.Floor;
            positionMarker.SetActive(correctFloor);

            if (!correctFloor) return;

            positionMarker.transform.position = new Vector3(pos.X, pos.Floor * 2.0f + 1f, pos.Y);
            
            // If we're not in free movement mode, update the orbit point to the marker position
            if (!freeMovement)
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
            buildingManager.SpawnBuildingFloor(buildingManager.GetActiveBuilding().buildingName, pos.Floor);
        }
        
        public void GotoPrediction()
        {
           GotoPosition(wifiManager.GetPosition());
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