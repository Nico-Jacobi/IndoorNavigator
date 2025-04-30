using System.Collections.Generic;
using model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace controller
{
    /// <summary>
    /// Minimal WiFi Manager for Android via Unity.
    /// </summary>
    public class WifiManager : MonoBehaviour
    {
        private AndroidJavaObject wifiManager;
        private AndroidJavaObject context;

        public GameObject promptPanel;
        public Button settingsButton;
        public Button discardButton;  
        public TextMeshProUGUI  promptText;

        void Start()
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            RequestLocationPermission();
            ClosePrompt();
            wifiManager = context.Call<AndroidJavaObject>("getSystemService", "wifi");
        }

        // Check if location is activated
        private bool IsLocationEnabled()
        {
            AndroidJavaObject locationManager = context.Call<AndroidJavaObject>("getSystemService", "location");
            return locationManager.Call<bool>("isProviderEnabled", "gps") || locationManager.Call<bool>("isProviderEnabled", "network");
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
            permissionActivity.Call("requestPermissions", new string[] { "android.permission.ACCESS_FINE_LOCATION" }, 1);
        }

        /// <summary>
        /// Returns a list of nearby WiFi SSIDs.
        /// </summary>
        public List<WifiNetwork> GetAvailableNetworks()
        {
            var networks = new List<WifiNetwork>();

            if (wifiManager == null)
            {
                Debug.LogWarning("WifiManager not initialized.");
                return networks;
            }

            wifiManager.Call<bool>("startScan");

            AndroidJavaObject scanResults = wifiManager.Call<AndroidJavaObject>("getScanResults");
            int size = scanResults.Call<int>("size");

            for (int i = 0; i < size; i++)
            {
                AndroidJavaObject scanResult = scanResults.Call<AndroidJavaObject>("get", i);
                WifiNetwork net = new WifiNetwork
                {
                    SSID = scanResult.Get<string>("SSID"),
                    BSSID = scanResult.Get<string>("BSSID"),
                    level = scanResult.Get<int>("level"),
                    frequency = scanResult.Get<int>("frequency"),
                    capabilities = scanResult.Get<string>("capabilities"),
                    timestamp = scanResult.Get<long>("timestamp")
                };

                networks.Add(net);
            }

            return networks;
        }

        public void onButtonPressed()
        {
            if (!IsLocationEnabled())
            {
                Debug.LogWarning("Location is disabled. WiFi scan might return empty.");
                PromptUserToEnableLocation();
            }
            
            Debug.Log("Debug button pressed");
            List<WifiNetwork> networks = GetAvailableNetworks();
            foreach (WifiNetwork net in networks)
            {
                Debug.Log(net.ToString());
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
                promptPanel.SetActive(false);  // Hide the prompt panel
                Debug.Log("Prompt closed by user.");
            }
        }
    }
}
