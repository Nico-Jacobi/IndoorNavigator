using controller;
using model;
using UnityEngine;

namespace view
{
    public class CameraController : MonoBehaviour
    {
        public bool freeMovement;
        public float moveSpeed;
        public Camera cam = null;

        public WifiManager wifiManager;
        public BuildingManager buildingManager;
        
        // Timer for position updates in non-free movement mode
        private float positionUpdateTimer = 0f;
        private float positionUpdateInterval = 0.5f; // Update every 1 second
        
        private GameObject positionMarker;
        public GameObject markerPrefab;
        private float markerUpdateTimer = 0f;
        private float markerUpdateInterval = 1f;

        
        void Start()
        {
            moveSpeed = 20;
            cam = Camera.main;
            freeMovement = true;
            
            cam.transform.rotation = Quaternion.Euler(65f, 0f, 0f); //  45° down from above
        }

        public void GotoPosition(Position pos)
        {
            if (pos != null)
            {
                Debug.Log($"current Position {pos}");
                        
                transform.position = new Vector3(pos.X, pos.Y, pos.Floor * 2.0f);
                buildingManager.SpawnBuildingFloor(buildingManager.GetActiveBuilding().buildingName, pos.Floor);
            }
        }
        
        /// <summary>
        /// Moves the position marker to the given Position.
        /// Instantiates the marker if it doesn't exist yet.
        /// </summary>
        public void MoveMarkerToPosition(Position pos)
        {
            if (pos == null)
            {
                pos = wifiManager.GetPosition();
            }
            
            if (positionMarker == null)
            {
                positionMarker = Instantiate(markerPrefab);
                positionMarker.name = "PositionMarker";
            }

            bool correctFloor = buildingManager.GetShownFloor() == pos.Floor;
            positionMarker.SetActive(correctFloor);

            if (!correctFloor) return;

            positionMarker.transform.position = new Vector3(pos.X, pos.Floor * 2.0f + 1f, pos.Y);
        }


        
        void Update()
        {
            markerUpdateTimer += Time.deltaTime;
            if (markerUpdateTimer >= markerUpdateInterval)
            {
                Position pos = wifiManager.GetPosition();
                Debug.Log($"Predicted Position: {pos}");
                MoveMarkerToPosition(pos);
                markerUpdateTimer = 0f;
            }
            
            
            if (freeMovement)
            {
           
                float h = Input.GetAxis("Horizontal");
                float v = Input.GetAxis("Vertical");

                cam.transform.position = new Vector3(cam.transform.position.x, buildingManager.GetShownFloor() * 2.0f + 20, cam.transform.position.z);

                // Movement only in XZ plane, in world space
                Vector3 movement = moveSpeed * Time.deltaTime * new Vector3(h, 0, v);
                cam.transform.Translate(movement, Space.World);
            
                
            }
            else
            {
                Position pos = wifiManager.GetPosition();

                positionUpdateTimer += Time.deltaTime;  //timer so this doesent get updated on every frame
                if (positionUpdateTimer >= positionUpdateInterval)
                {
                    GotoPosition(pos);
                    positionUpdateTimer = 0f;
                }
            }
        }
    
        public void OnViewModeButtonPressed()
        {
            freeMovement = !freeMovement;
        }
    }
}