using System;
using System.Collections;
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
        public string currentBuilding;
        
        public GameObject promptPanel;
        public Button settingsButton;
        public Button discardButton;
        public TextMeshProUGUI promptText;
        
        private List<Position> positions;   //contains last x raw predictions, removes old ones, but may contain more than rollingAverageLength
        public int rollingAverageLength = 10;

        
        private bool isUpdating = false;    //set to true to stop the scanning, otherwise to coroutine will use this
        private float updateInterval = 2f;  //seconds between scans (if wifi scans are throttled (android 12+ default) set to 30)
        
        // Added to track scan completion status
        private bool scanInProgress = false;
        private float scanTimeout = 5.0f; // Maximum time to wait for scan results in seconds

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
                    StartCoroutine(UpdateLocationContinuously());
                }
            }
            catch //(AndroidJavaException)
            {
                Debug.LogAssertion("Couldn't get android API, some functionality wont work");
            }
            promptText.text = "Location services are disabled. Please enable GPS in your device settings.";
            settingsButton.onClick.AddListener(OpenLocationSettings);
            discardButton.onClick.AddListener(ClosePrompt);
            promptPanel.SetActive(false);
        }

        public Position GetPosition()
        {
            //todo maybe exponential average here to? 
            //also use compass to get direction and from the points and the last ones get the speed to make better predicton
            
            if (positions == null || positions.Count == 0)
                return null;

            int count = Math.Min(rollingAverageLength, positions.Count);
            var lastPositions = positions.Skip(positions.Count - count).ToList();

            float avgX = lastPositions.Average(p => p.X);
            float avgY = lastPositions.Average(p => p.Y);
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
            promptPanel.SetActive(true);
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
        /// Starts a WiFi scan and returns a coroutine that will yield a Coordinate with WiFiInfos from scan.
        /// </summary>
        private IEnumerator GetScannedCoordinateAsync()
        {
            // Create a new coordinate object with dummy values
            var coord = new Coordinate
            {
                X = 0.0f,
                Y = 0.0f,
                Floor = -1,
                BuildingName = "CurrentMeasurement" 
            };
            
            // Check if a scan is already in progress
            if (scanInProgress)
            {
                Debug.Log("A scan is already in progress, waiting for it to complete...");
                yield return new WaitUntil(() => !scanInProgress);
            }
            
            scanInProgress = true;
            
            // Start the WiFi scan
            bool scanStarted = wifiManager.Call<bool>("startScan");
            Debug.Log($"WiFi scan started: {scanStarted}");
            
            if (!scanStarted)
            {
                Debug.LogWarning("Failed to start WiFi scan. This could be due to throttling on Android 9+");
                scanInProgress = false;
                yield return coord; // Return empty coordinate
            }
            
            // Wait a short time for the scan to complete
            float timeWaited = 0;
            while (timeWaited < scanTimeout)
            {
                yield return new WaitForSeconds(0.2f);
                timeWaited += 0.2f;
                
                // Check if scan results are available
                AndroidJavaObject scanResults = wifiManager.Call<AndroidJavaObject>("getScanResults");
                int size = scanResults.Call<int>("size");
                
                if (size > 0)
                {
                    Debug.Log($"Scan completed with {size} WiFi networks found");
                    
                    // Process scan results
                    for (int i = 0; i < size; i++)
                    {
                        AndroidJavaObject scanResult = scanResults.Call<AndroidJavaObject>("get", i);
                        WifiInfo wifiInfo = new WifiInfo
                        {
                            Bssid = scanResult.Get<string>("BSSID"),
                            SignalStrength = scanResult.Get<int>("level")   // Gets normalized
                            // CoordinateId will be set later when inserted into the DB
                        };

                        coord.WifiInfos.Add(wifiInfo);
                    }
                    
                    scanInProgress = false;
                    yield return coord;
                    yield break;
                }
            }
            
            // If we reached here, the scan timed out
            Debug.LogWarning("WiFi scan timed out");
            scanInProgress = false;
            
            // Check if location is enabled, if no networks were found
            if (!IsLocationEnabled())
            {
                PromptUserToEnableLocation();
            }
            
            yield return coord;
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
     
     // Coroutine that ensures UpdateLocation is called continuously but waits for the previous call to finish.
     private IEnumerator UpdateLocationContinuously()
     {
         while (true)
         {
             // Check if UpdateLocation is already running
             if (!isUpdating)
             {
                 isUpdating = true;
     
                 yield return StartCoroutine(UpdateLocationAsync());

                 yield return new WaitForSeconds(updateInterval);
                 isUpdating = false;
             }
             else
             {
                 yield return null;
             }
         }
     }

        // Modified to be a coroutine that waits for scan results
        private IEnumerator UpdateLocationAsync()
        {
            // Start the scan coroutine
            Coordinate wifiNetworks = null;
            
            // Create a custom coroutine to handle getting the coordinate
            IEnumerator scanCoroutine = GetScannedCoordinateAsync();
            
            // Execute the coroutine
            while (scanCoroutine.MoveNext())
            {
                // If the current result is a Coordinate, store it
                if (scanCoroutine.Current is Coordinate)
                {
                    wifiNetworks = scanCoroutine.Current as Coordinate;
                }
                yield return scanCoroutine.Current;
            }
            
            // Check if we got any results
            if (wifiNetworks == null || wifiNetworks.WifiInfoMap.Count == 0)
            {
                if (wifiNetworks != null)
                {
                    UpdateCurrentBuilding(wifiNetworks);    //in case the user just entered a building, then the error is negligible
                }
                Debug.Log("Wifi data is empty -> no wifi signals around or some error (eg location not active / no privileges / no android)");
                yield break;
            }
            
            //updating currentBuilding
            if (currentBuilding == null)    
            {
                UpdateCurrentBuilding(wifiNetworks);
                if (String.IsNullOrEmpty(currentBuilding))
                {
                    Debug.LogWarning("Could not determine building. Aborting location update.");
                    yield break;
                }
            }

            
            List<Coordinate> dataPoints = database.GetCoordinatesForBuilding(currentBuilding);
            if (dataPoints.Count == 0)
            {
                Debug.LogWarning("No recorded data found for this building.");
                yield break;
            }

            // Sort coordinates by similarity
            var sorted = dataPoints
                .OrderBy(coord => coord.CompareWifiSimilarity(wifiNetworks))
                .Take(100)
                .ToList();

            // Interpolate using exponential weighting
            float weightedX = 0, weightedY = 0, weightedFloor = 0, totalWeight = 0;
            int actualLength = sorted.Count;

            for (int i = 0; i < actualLength; i++)
            {
                float weight = (float) Math.Pow(1.2, actualLength - i); // Exponential weighting
                weightedX += sorted[i].X * weight;
                weightedY += sorted[i].Y * weight;
                weightedFloor += sorted[i].Floor * weight;
                totalWeight += weight;
            }

            float finalX = weightedX / totalWeight;
            float finalY = weightedY / totalWeight;
            float finalFloor = weightedFloor / totalWeight;

            Position prediction = new Position(finalX, finalY, (int)Math.Round(finalFloor));
            Debug.Log($"Predicted Position: X={finalX:F2}, Y={finalY:F2}, Floor={Math.Round(finalFloor)}");


            positions = positions.Append(prediction).ToList();
            
            if (positions.Count > rollingAverageLength*5)   //cache some 
            {
                positions.RemoveAt(0);
            }
            
            Debug.Log($"{positions.Count} predictons saved");
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
                Debug.Log($"set current building to {currentBuilding}");
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