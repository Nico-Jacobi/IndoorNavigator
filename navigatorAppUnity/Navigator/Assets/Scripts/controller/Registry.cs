using Controller;
using UnityEngine;
using view;

namespace controller
{
    public class Registry : MonoBehaviour
    {
        public bool kalmanFilterActive = true;
        
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
        public SimplePositionFilter simplePositionFilter;
        
        public NoKnowSignalFound noKnowSignalFoundDialog;
        public SettingsMenu settingsMenu;
        public TopMenu topMenu;
        public NavigationDialog navigationDialog;
        public FloatingButtons floatingButtons;

        public PositionFilter GetPositionFilter(bool getInactive = false)
        {
            if (getInactive)
            {
                return kalmanFilterActive ? simplePositionFilter : kalmanFilter;
            }

            return kalmanFilterActive ? kalmanFilter : simplePositionFilter;
        }

        
       
    }
}