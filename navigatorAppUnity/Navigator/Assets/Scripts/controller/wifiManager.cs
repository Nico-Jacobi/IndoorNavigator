using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using model;
using model.Database;
using model.Database.Plugins;
using view;

namespace controller
{
    /// <summary>
    /// Implementation of the WifiManager android api.
    /// </summary>
    public class WifiManager : MonoBehaviour
    {
        private AndroidJavaObject wifiManager;
        private AndroidJavaObject context;

        public Registry registry;
        
        public bool isUpdating = false;    //set to true to stop the scanning, otherwise to coroutine will use this
        public float updateInterval = 2f;  //seconds between scans (if wifi scans are throttled (android 12+ default) set to 30)
        
        // Added to track scan completion status
        private bool scanInProgress = false;
        private float scanTimeout = 5.0f; // Maximum time to wait for scan results in seconds

        void Start()
        {
            InitializeAndroidWifi();
            SetupLocationPromptDialog();
        }

        private void InitializeAndroidWifi()
        {
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
        }

        private void SetupLocationPromptDialog()
        {
            if (registry.locationPromptDialog != null)
            {
                // Subscribe to dialog events
                registry.locationPromptDialog.OnSettingsButtonClicked += OpenLocationSettings;
                registry.locationPromptDialog.OnDiscardButtonClicked += OnDiscardPrompt;
                
                // Set the prompt text
                registry.locationPromptDialog.SetPromptText("Location services are disabled. Please enable GPS in your device settings.");
            }
            else
            {
                Debug.LogError("LocationPromptDialog reference missing in WifiManager!");
            }
        }

        // Check if location is activated
        private bool IsLocationEnabled()
        {
            if (context == null) return false;
            
            AndroidJavaObject locationManager = context.Call<AndroidJavaObject>("getSystemService", "location");
            return locationManager.Call<bool>("isProviderEnabled", "gps") ||
                   locationManager.Call<bool>("isProviderEnabled", "network");
        }

        // Show the location prompt dialog
        public void PromptUserToEnableLocation()
        {
            if (registry.locationPromptDialog != null)
            {
                registry.locationPromptDialog.Open();
            }
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
                // scan will still return the old values, so it still "works"
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
        
        /// <summary>
        /// Collects multiple WiFi measurements and returns a dictionary with max signal strength for each BSSID.
        /// </summary>
        /// <param name="numberOfScans">Number of scans to perform (default: 10)</param>
        /// <param name="filter">Whether to filter networks by SSID (optional)</param>
        /// <param name="allowedSSIDs">List of allowed SSIDs when filtering (optional)</param>
        /// <returns>Coroutine yielding a dictionary with BSSID as key and WifiInfo as value</returns>
        public IEnumerator CollectWifiDataMultipleMeasurements(int numberOfScans = 20, bool filter = false, List<string> allowedSSIDs = null)
        {
                
            if (allowedSSIDs == null)
                allowedSSIDs = new List<string>();
                
            Debug.Log($"Starting multiple WiFi measurements: {numberOfScans} scans");
            
            // Dictionary to store all scan results: BSSID -> List of SignalStrengths
            Dictionary<string, List<float>> bssidSignalStrengths = new Dictionary<string, List<float>>();
            Dictionary<string, string> bssidToSsid = new Dictionary<string, string>();
            
            // Mark that we're performing multiple scans to prevent other scanning operations
            isUpdating = true;
            
            for (int i = 0; i < numberOfScans; i++)
            {
                Debug.Log($"Performing scan {i+1} of {numberOfScans}");
                
                // Get a single scan result
                IEnumerator scanCoroutine = GetScannedCoordinateAsync();
                Coordinate scanResult = null;
                
                // Execute the scan coroutine
                while (scanCoroutine.MoveNext())
                {
                    if (scanCoroutine.Current is Coordinate)
                    {
                        scanResult = scanCoroutine.Current as Coordinate;
                    }
                    yield return scanCoroutine.Current;
                }
                
                // Process scan results if available
                if (scanResult != null && scanResult.WifiInfos.Count > 0)
                {
                    foreach (var wifiInfo in scanResult.WifiInfos)
                    {
                        string bssid = wifiInfo.Bssid;
                        
                        if (!bssidSignalStrengths.ContainsKey(bssid))
                        {
                            bssidSignalStrengths[bssid] = new List<float>();
                        }
                        bssidSignalStrengths[bssid].Add(wifiInfo.SignalStrength);
                    }
                }
                
                // this has to be changed to 30 if collecting with a phone that only allows 4/2min
                // (although that would make a scan take forever)
                yield return new WaitForSeconds(0.1f); 
            }
            
            // Create result dictionary using max signal strength for each BSSID
            Dictionary<string, WifiInfo> aggregatedData = new Dictionary<string, WifiInfo>();
            
            foreach (var entry in bssidSignalStrengths)
            {
                string bssid = entry.Key;
                List<float> strengths = entry.Value;
                
                // Skip this BSSID if filtering is enabled and SSID is not in allowed list
                if (filter && 
                    bssidToSsid.ContainsKey(bssid) &&
                    allowedSSIDs.Count > 0 && 
                    !allowedSSIDs.Contains(bssidToSsid[bssid]))
                {
                    continue;
                }
                
                // Find max signal strength
                float maxStrength = strengths.Max();
                
                // Create WiFi info object with max signal strength
                WifiInfo wifiInfo = new WifiInfo
                {
                    Bssid = bssid,
                    SignalStrength = maxStrength
                    // Could add more properties like SSID if needed
                };
                
                aggregatedData[bssid] = wifiInfo;
            }
            
            Debug.Log($"Completed multiple WiFi measurements with {aggregatedData.Count} networks");
            
            // Create a new coordinate with the aggregated data
            var aggregatedCoordinate = new Coordinate
            {
                X = 0.0f,
                Y = 0.0f,
                Floor = -1,
                BuildingName = "MultiScanMeasurement"
            };
            
            foreach (var wifiInfo in aggregatedData.Values)
            {
                aggregatedCoordinate.WifiInfos.Add(wifiInfo);
            }
            
            isUpdating = false;
            
            yield return aggregatedCoordinate;
        }
        
        /// <summary>
        /// Creates a DataPoint with multiple WiFi measurements.
        /// This can be used to create reference points for the database.
        /// </summary>
        /// <param name="x">X-coordinate of the point</param>
        /// <param name="y">Y-coordinate of the point</param>
        /// <param name="floor">Floor number</param>
        /// <param name="buildingName">Name of the building</param>
        /// <param name="saveToDatabase">Whether to save the data point to the database</param>
        /// <param name="onComplete">Action to call when measurement is complete with the created coordinate</param>
        /// <returns>Coroutine yielding a Coordinate object with aggregated WiFi data</returns>
        public IEnumerator CreateDataPoint(float x, float y, int floor, string buildingName, bool saveToDatabase = false, Action<Coordinate> onComplete = null)
        {
            Debug.Log($"Creating data point at X={x}, Y={y}, Floor={floor} in {buildingName}");
            
            // Get WiFi data using multiple measurements
            Coordinate wifiData = null;
            IEnumerator multiScanCoroutine = CollectWifiDataMultipleMeasurements();
            
            // Execute the coroutine
            while (multiScanCoroutine.MoveNext())
            {
                // If the current result is a Coordinate, store it
                if (multiScanCoroutine.Current is Coordinate)
                {
                    wifiData = multiScanCoroutine.Current as Coordinate;
                }
                yield return multiScanCoroutine.Current;
            }
            
            if (wifiData == null || wifiData.WifiInfoMap.Count == 0)
            {
                Debug.LogWarning("Failed to collect WiFi data for this data point");
                yield break;
            }
            
            // Create a new coordinate with the collected WiFi data and specified position
            Coordinate dataPoint = new Coordinate
            {
                X = x,
                Y = y,
                Floor = floor,
                BuildingName = buildingName,
                WifiInfos = wifiData.WifiInfos // Copy WiFi infos from the scan result
            };
            
            Debug.Log($"Created data point with {dataPoint.WifiInfoMap.Count} WiFi networks");
            
            // Add to database if requested
            if (saveToDatabase && registry.database != null)
            {
                registry.database.InsertCoordinateWithWifiInfos(dataPoint);
                Debug.Log($"Saved data point to database for building {buildingName}");
                
                // Clear position tracker cache for this building to refresh data
                if (registry.positionTracker != null)
                {
                    registry.positionTracker.ClearCache(buildingName);
                }
            }
            
            // Call completion callback if provided
            onComplete?.Invoke(dataPoint);
            
            yield return dataPoint;
        }
        
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

        private IEnumerator UpdateLocationAsync()
        {
            Coordinate wifiNetworks = null;
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
            
            if (wifiNetworks == null || wifiNetworks.WifiInfoMap.Count == 0)
            {
                Debug.Log("Wifi data is empty -> no wifi signals around or some error (eg location not active / no privileges / no android)");
                yield break;
            }

            // Update position using the position tracker
            if (registry.positionTracker != null)
            {
                yield return StartCoroutine(registry.positionTracker.UpdatePosition(wifiNetworks));
            }
            else
            {
                Debug.LogError("PositionTracker reference is missing in WifiManager");
            }
        }

        // Event handler for settings button click
        private void OpenLocationSettings()
        {
            // Close the dialog first
            if (registry.locationPromptDialog != null)
            {
                registry.locationPromptDialog.Close();
            }

            if (context == null) return;

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

        // Event handler for discard button click
        private void OnDiscardPrompt()
        {
            if (registry.locationPromptDialog != null)
            {
                registry.locationPromptDialog.Close();
            }
            Debug.Log("Location prompt dismissed by user.");
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            if (registry.locationPromptDialog != null)
            {
                registry.locationPromptDialog.OnSettingsButtonClicked -= OpenLocationSettings;
                registry.locationPromptDialog.OnDiscardButtonClicked -= OnDiscardPrompt;
            }
        }
    }
}