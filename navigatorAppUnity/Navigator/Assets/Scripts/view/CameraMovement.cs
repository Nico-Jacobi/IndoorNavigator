using controller;
using model;
using UnityEngine;

namespace view
{
    public class CameraController : MonoBehaviour
    {
        public bool freeMovement = true;
        public float moveSpeed = 5f;
        public float lookSpeed = 2f;
        public Camera cam = null;

        public WifiManager wifiManager;
        public BuildingManager buildingManager;
        
        // Timer for position updates in non-free movement mode
        private float positionUpdateTimer = 0f;
        private float positionUpdateInterval = 1.0f; // Update every 1 second
        
        void Start()
        {
            cam = Camera.main;
        }
        
        void Update()
        {
            // Move
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
    
            // Look
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;
            
            if (freeMovement)
            {
                Vector3 movement = moveSpeed * Time.deltaTime * new Vector3(h, 0, v);
                transform.Translate(movement);
                cam.transform.Rotate(0, mouseX, 0);
                cam.transform.Rotate(-mouseY, 0, 0);  
            }
            else
            {
                
                positionUpdateTimer += Time.deltaTime;  //timer so this doesent get updated on every frame
                if (positionUpdateTimer >= positionUpdateInterval)
                {
                    Position pos = wifiManager.GetPosition();
                    if (pos != null)
                    {
                        transform.Translate(new Vector3(pos.X, pos.Y, pos.Floor * 2.0f));
                        buildingManager.SpawnBuildingFloor(wifiManager.currentBuilding, pos.Floor);
                    }
                    
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