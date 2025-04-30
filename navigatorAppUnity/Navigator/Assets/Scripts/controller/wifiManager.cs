using System;
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
        public TextMeshProUGUI promptText;

        private Positioning position;

        void Start()
        {
            position = new Positioning();

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
            catch (Exception e)
            {
                Console.WriteLine("Count get android API, some functionality wont work");
            }

            
            ClosePrompt();

        }

        public Positioning GetPositioning()
        {
            return position;
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
                    //SSID = scanResult.Get<string>("SSID"),    #to make the db faster this is excluded as its not strictly necesarry
                    BSSID = scanResult.Get<string>("BSSID"),
                    level = scanResult.Get<int>("level"),
                    //frequency = scanResult.Get<int>("frequency"),
                    //capabilities = scanResult.Get<string>("capabilities"),
                    timestamp = scanResult.Get<long>("timestamp")
                };

                networks.Add(net);
            }

            return networks;
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