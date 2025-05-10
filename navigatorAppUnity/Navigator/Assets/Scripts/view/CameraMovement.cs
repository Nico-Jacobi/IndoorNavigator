using controller;
using model;
using UnityEngine;

namespace view
{
    public class CameraController : MonoBehaviour
    {

        private float moveSpeed = 100f;
        public bool freeMovement = true;
        public bool inMenu = false;
        
        private readonly float orbitDistance = 10f;  // Distance from camera to orbit point
        private readonly float cameraHeight = 40f;   // Height offset above the floor
        private Vector3 orbitPoint;        // The point we're orbiting around

        public bool compasActive = true;

        public GameObject markerPrefab;
        private GameObject positionMarker;
        private float markerUpdateTimer = 0f;
        private float markerUpdateInterval = 1f;

        private float positionUpdateTimer = 0f;
        private float positionUpdateInterval = 0.5f;
        
        public Camera cam;
        public WifiManager wifiManager;
        public BuildingManager buildingManager;
        public CompassReader compass;
        public GraphManager graphManager;



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

            HandleMarkerUpdate();
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
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(h, 0, v) * moveSpeed * Time.deltaTime;
            
            // Move the orbit point instead of directly moving the camera
            orbitPoint += movement;
            
            // Camera position will be updated in HandleCameraRotation()
            float y = buildingManager.GetShownFloor() * 2.0f + cameraHeight;
            orbitPoint = new Vector3(orbitPoint.x, y - cameraHeight, orbitPoint.z);
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
            if (pos == null) return;
            
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
        
        public Vector3 GetCrosshairPosition()
        {
            // Return the orbit point as the theoretical point we're rotating around
            return orbitPoint;
        }
    }
}