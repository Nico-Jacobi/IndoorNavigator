using Controller;
using UnityEngine;
using view;

namespace controller
{
    public class Registry : MonoBehaviour
    {
        public SQLiteDatabase database;
        public Camera cam;
        public WifiManager wifiManager;
        public BuildingManager buildingManager;
        public CompassReader compassReader;
        public GraphManager graphManager;
        public PositionTracker positionTracker;
        public CameraController cameraController;
        public DataCollectionMode dataCollectionMode;
        public Acceleration accelerationController;
        public KalmanFilter kalmanFilter;

    }
}