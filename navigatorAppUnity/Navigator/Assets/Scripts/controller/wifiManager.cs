using System;
using System.Collections.Generic;
using System.Linq;
using model;
using model.Database;
using model.Database.Plugins;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace controller
{
    /// <summary>
    /// Implementation of the WifiManager android api.
    /// </summary>
    public class WifiManager : MonoBehaviour
    {
        private AndroidJavaObject wifiManager;
        private AndroidJavaObject context;

        public SQLiteDatabase database;
        public string currentBuilding = null;
        
        public GameObject promptPanel;
        public Button settingsButton;
        public Button discardButton;
        public TextMeshProUGUI promptText;
        
        private List<Position> positions;   //contains last x raw predictions, removes old ones, but may contain more than rollingAverageLength
        public int rollingAverageLength = 10;

        void Start()
        {
            positions = new List<Position>();
            positions.Add(new Position(0,0,3));

            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    // this throws an error if not on android
                    context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    RequestLocationPermission();
                    wifiManager = context.Call<AndroidJavaObject>("getSystemService", "wifi");

                }
            }
            catch (AndroidJavaException)
            {
                Debug.LogAssertion("Couldn't get android API, some functionality wont work");
            }

            
            ClosePrompt();

        }

        public Position GetPosition()
        {
            //todo maybe exponential average here to? 
            //also use compass to get direction and from the points and the last ones get the speed to make better predicton
            
            if (positions == null || positions.Count == 0)
                return new Position(0, 0, 0);

            int count = Math.Min(rollingAverageLength, positions.Count);
            var lastPositions = positions.Skip(positions.Count - count).ToList();

            double avgX = lastPositions.Average(p => p.X);
            double avgY = lastPositions.Average(p => p.Y);
            int avgFloor = (int)Math.Round(lastPositions.Average(p => p.Floor));

            return new Position(avgX, avgY, avgFloor);
        }



        // Check if location is activated
        private bool IsLocationEnabled()
        {
            AndroidJavaObject locationManager = context.Call<AndroidJavaObject>("getSystemService", "location");
            return locationManager.Call<bool>("isProviderEnabled", "gps") ||
                   locationManager.Call<bool>("isProviderEnabled", "network");
        }

        // Popup to turn on the location
        public void PromptUserToEnableLocation()
        {
            if (promptPanel == null || promptText == null || discardButton == null)
            {
                Debug.LogError("UI components not assigned.");
                return;
            }

            Debug.Log("Location not active, prompting user...");
            promptPanel.SetActive(true);
            promptText.text = "Location services are disabled. Please enable GPS in your device settings.";

            // Set up button listeners
            settingsButton.onClick.AddListener(OpenLocationSettings);
            discardButton.onClick.AddListener(ClosePrompt);
        }


        // Request coarse location privileges (just the app's privileges, not if it's actually on)
        private void RequestLocationPermission()
        {
            Debug.Log("Requested permission ACCESS_FINE_LOCATION");
            AndroidJavaObject unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject permissionActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            permissionActivity.Call("requestPermissions", new string[] { "android.permission.ACCESS_FINE_LOCATION" },
                1);
        }

        /// <summary>
        /// Returns a dummy Coordinate with WiFiInfos from current scan.
        /// </summary>
        private Coordinate GetScannedCoordinate()
        {
            var coord = new Coordinate
            {
                X = 0.0f,               // Dummy values
                Y = 0.0f,               
                Floor = -1,            
                BuildingName = "CurrentMeasurement" 
            };

            if (wifiManager == null)
            {
                Debug.LogWarning("WifiManager not initialized.");
                return coord;
            }

            wifiManager.Call<bool>("startScan");

            AndroidJavaObject scanResults = wifiManager.Call<AndroidJavaObject>("getScanResults");
            int size = scanResults.Call<int>("size");

            for (int i = 0; i < size; i++)
            {
                AndroidJavaObject scanResult = scanResults.Call<AndroidJavaObject>("get", i);
                WifiInfo wifiInfo = new WifiInfo
                {
                    Bssid = scanResult.Get<string>("BSSID"),
                    SignalStrength = scanResult.Get<int>("level")   //gets normalized
                    // CoordinateId will be set later when inserted into the DB
                };

                coord.WifiInfos.Add(wifiInfo);
            }

            if (size == 0)
            {
                if (!IsLocationEnabled())
                {
                    PromptUserToEnableLocation();
                }
            }

            return coord;
        }


     /*
SSID: I'm watching you, BSSID: 2c:91:ab:59:83:5f, Level: -66, Frequency: 5500, Capabilities: [WPA2-PSK-CCMP][RSN-PSK+SAE-CCMP][ESS][WPS], Timestamp: 1667079169109
SSID: I'm watching you, BSSID: 2c:91:ab:59:83:5e, Level: -60, Frequency: 2462, Capabilities: [WPA2-PSK-CCMP][RSN-PSK+SAE-CCMP][ESS][WPS], Timestamp: 1667079169100
SSID: MagentaWLAN-E52G, BSSID: ac:b6:87:5b:a5:fe, Level: -93, Frequency: 2462, Capabilities: [WPA2-PSK-CCMP][RSN-PSK+SAE-CCMP][ESS][WPS], Timestamp: 1667079169095
SSID: , BSSID: fe:65:de:b4:99:92, Level: -49, Frequency: 2462, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][ESS][WPS], Timestamp: 1667079169103
SSID: I'm watching you, BSSID: 7e:8a:20:08:f5:d2, Level: -83, Frequency: 5745, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][ESS], Timestamp: 1667079169082
SSID: Gastzugang Jacobi, BSSID: 7e:8a:20:08:f5:d3, Level: -81, Frequency: 2412, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][ESS], Timestamp: 1667079169075
SSID: , BSSID: 8a:8a:20:08:f5:d2, Level: -83, Frequency: 5745, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][ESS], Timestamp: 1667079169090
SSID: , BSSID: 86:8a:20:08:f5:d2, Level: -83, Frequency: 5745, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][ESS], Timestamp: 1667079169088
SSID: , BSSID: 86:8a:20:08:f5:d3, Level: -71, Frequency: 2412, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][ESS], Timestamp: 1667079169080
SSID: Gastzugang Jacobi, BSSID: 2e:91:ab:59:83:5e, Level: -60, Frequency: 2462, Capabilities: [WPA2-PSK-CCMP][RSN-PSK+SAE-CCMP][ESS][WPS], Timestamp: 1667079169098
SSID: , BSSID: 82:8a:20:08:f5:d3, Level: -79, Frequency: 2412, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][ESS], Timestamp: 1667079169077
SSID: Gastzugang Jacobi, BSSID: 82:8a:20:08:f5:d2, Level: -83, Frequency: 5745, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][ESS], Timestamp: 1667079169085
SSID: Gastzugang Jacobi, BSSID: 2e:91:ab:59:83:5f, Level: -66, Frequency: 5500, Capabilities: [WPA2-PSK-CCMP][RSN-PSK+SAE-CCMP][ESS][WPS], Timestamp: 1667079169105
SSID: DTUBI-93415950, BSSID: 54:f2:9f:81:fb:a7, Level: -88, Frequency: 2437, Capabilities: [WPA2-PSK-CCMP][RSN-PSK-CCMP][WPA-PSK-CCMP][ESS], Timestamp: 1667079169093
*/

        //makes a single wifiscan and appends the position to the prediction
        public void UpdateLocation()
        {
            
            Coordinate wifiNetworks = GetScannedCoordinate();
            if (wifiNetworks.WifiInfoMap.Count == 0)
            {
                Debug.Log("Wifi data is empty -> no wifi signals around or some error (eg location not active / no privileges / no android)");
            }
            
            //updating currentBuilding
            if (currentBuilding == null)    
            {
                UpdateCurrentBuilding(wifiNetworks);
                if (currentBuilding == null)
                {
                    Debug.LogWarning("Could not determine building. Aborting location update.");
                    return;
                }
            }

   

            List<Coordinate> dataPoints = database.GetCoordinatesForBuilding(currentBuilding);
            if (dataPoints.Count == 0)
            {
                Debug.LogWarning("No recorded data found for this building.");
                return;
            }

            // Sort coordinates by similarity
            var sorted = dataPoints
                .OrderBy(coord => coord.CompareWifiSimilarity(wifiNetworks))
                .Take(100)
                .ToList();

            // Interpolate using exponential weighting
            double weightedX = 0, weightedY = 0, weightedFloor = 0, totalWeight = 0;
            int actualLength = sorted.Count;

            for (int i = 0; i < actualLength; i++)
            {
                double weight = Math.Pow(1.2, actualLength - i); // Exponential weighting
                weightedX += sorted[i].X * weight;
                weightedY += sorted[i].Y * weight;
                weightedFloor += sorted[i].Floor * weight;
                totalWeight += weight;
            }

            double finalX = weightedX / totalWeight;
            double finalY = weightedY / totalWeight;
            double finalFloor = weightedFloor / totalWeight;

            Position prediction = new Position(finalX, finalY, (int)Math.Round(finalFloor));
            Debug.Log($"Predicted Position: X={finalX:F2}, Y={finalY:F2}, Floor={Math.Round(finalFloor)}");


            positions = positions.Append(prediction).ToList();
            
            if (positions.Count > rollingAverageLength*5)   //cache some 
            {
                positions.RemoveAt(0);
            }
        }
        
        private void UpdateCurrentBuilding(Coordinate wifiNetworks)
        {
            Dictionary<string, int> buildingCount = new();

            foreach (string bssid in wifiNetworks.WifiInfoMap.Keys)
            {
                string building = database.GetBuildingForBssid(bssid);
                if (building != null)
                {
                    if (!buildingCount.TryAdd(building, 1))
                        buildingCount[building]++;
                }
            }

            if (buildingCount.Count > 0)
            {
                // Pick the building with the most matches
                string mostLikelyBuilding = buildingCount
                    .OrderByDescending(kv => kv.Value)
                    .First().Key;

                //wifiNetworks.BuildingName = mostLikelyBuilding; //this is never used but it`s nice to have
                currentBuilding = mostLikelyBuilding;
            }
            else
            {
                Debug.Log("Wifi data doesn't match any recorded building");
            }
        }

     

        // Opens the location settings for the user to enable GPS
        private void OpenLocationSettings()
        {
            ClosePrompt();

            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            //has to run on main thread
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                AndroidJavaClass settingsClass = new AndroidJavaClass("android.provider.Settings");
                string actionLocationSettings = settingsClass.GetStatic<string>("ACTION_LOCATION_SOURCE_SETTINGS");

                AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", actionLocationSettings);
                activity.Call("startActivity", intent);
            }));
        }

        // Closes the prompt when the discard button is clicked
        private void ClosePrompt()
        {
            if (promptPanel != null)
            {
                promptPanel.SetActive(false); // Hide the prompt panel
                Debug.Log("Prompt closed by user.");
            }
        }
    }
}