using System.Collections;
using System.Globalization;
using System.Linq;
using controller;
using Controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using View;

namespace view
{
    public class SettingsMenu : MonoBehaviour
    {
        public Registry registry;
        
        public RectTransform menu;
        public Button closeButton;

        public SwitchButton freeMovement;
        public SwitchButton compasActive;
        public SwitchButton collectDataMode;
        public SwitchButton upDateDbSwitch;
        public SwitchButton kalmanSwitchButton;

        public TMP_InputField measureInterval;
        public TMP_InputField rollingAverageSize;

        public Button navigateButton;
        public Button gotoPositionButton;

        public Button importJson;
        public Button exportJson;

        private Vector2 hiddenPos;
        private Vector2 visiblePos;
        private float slideDuration = 0.3f;

        public float accuracy = 1.0f; // default accuracy
        
        private bool open = false;
        
        private void Start()
        {
            visiblePos = menu.anchoredPosition;
            
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            float canvasWidth = canvasRect.rect.width;
            
            hiddenPos = visiblePos + new Vector2(canvasWidth + menu.rect.width + 100f, 0);

            menu.anchoredPosition = hiddenPos;

            
            CloseMenu();
            freeMovement.OnValueChanged += HandleFreeMovementToggle;
            compasActive.OnValueChanged += HandleCompasActiveToggle;
            collectDataMode.OnValueChanged += HandleCollectDataModeToggle;
            upDateDbSwitch.OnValueChanged += HandleDbPassiveCollectionToggle;
            kalmanSwitchButton.OnValueChanged += HandleKalmanFilterToggle;
            
            freeMovement.SetValue(false);
            compasActive.SetValue(false);
            collectDataMode.SetValue(false);
            upDateDbSwitch.SetValue(false);
            kalmanSwitchButton.SetValue(true);
            
            closeButton.onClick.AddListener(CloseMenu);
            
            measureInterval.SetTextWithoutNotify(registry.wifiManager.updateInterval.ToString() + "s");
            measureInterval.onValueChanged.AddListener(OnMeasureIntervalChanged);
            rollingAverageSize.SetTextWithoutNotify(accuracy.ToString()); 
            rollingAverageSize.onValueChanged.AddListener(OnAccuracyChanged);
            
            importJson.onClick.AddListener(registry.database.PickFileAndImport);
            exportJson.onClick.AddListener(registry.database.ExportWithSimpleFilename);
        }

        public void ToggleMenu()
        {
            if (open)
                CloseMenu();
            else
                OpenMenu();
        }

        public void OpenMenu()
        {
            registry.topMenu.CloseMenu();
            registry.floatingButtons.Hide();
            open = true;
            registry.cameraController.inMenu = true;
            closeButton.gameObject.SetActive(true);
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, visiblePos, slideDuration));
        }

        public void CloseMenu()
        {
            registry.topMenu.OpenMenu();
            registry.floatingButtons.Show();
            open = false;
            registry.cameraController.inMenu = false;
            closeButton.gameObject.SetActive(false);
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, hiddenPos, slideDuration));
        }

        private IEnumerator SlideMenu(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                rect.anchoredPosition = Vector2.Lerp(from, to, time / duration);
                yield return null;
            }

            rect.anchoredPosition = to;
        }

        public void HandleFreeMovementToggle(bool freeMovementMode)
        {
            registry.cameraController.ToggleViewMode();
        }

        public void HandleCompasActiveToggle(bool compasActiveMode)
        {
            registry.cameraController.compassActive = compasActiveMode;
        }

        public void HandleCollectDataModeToggle(bool collectDataModeStart)
        {
            if (collectDataModeStart)
            {
                registry.wifiManager.isUpdating = false; //cant measure while collecting data
                //CloseMenu();
                registry.dataCollectionMode.Activate();
                navigateButton.gameObject.SetActive(false);
                gotoPositionButton.gameObject.SetActive(false);

            }
            else
            {
                registry.wifiManager.isUpdating = true;
                //CloseMenu();
                registry.dataCollectionMode.Deactivate();
                navigateButton.gameObject.SetActive(true);
                gotoPositionButton.gameObject.SetActive(true);

            }
        }

        public void HandleDbPassiveCollectionToggle(bool HandleDbPassiveCollection)
        {
            registry.positionTracker.passiveDataCollectionActive = HandleDbPassiveCollection;
        }
        
        
        private void HandleKalmanFilterToggle(bool active)
        {
            registry.kalmanFilterActive = active;
        }
        
        
        /// <summary>
        /// Handles change of measure interval string. Extracts digits, parses float, clamps or sets default.
        /// </summary>
        private void OnMeasureIntervalChanged(string interval)
        {
            float defaultValue = 2.0f;
            
            if (string.IsNullOrEmpty(interval))
            {
                Debug.LogWarning("Empty interval input, using default 1.0s");
                registry.wifiManager.updateInterval = defaultValue;
                measureInterval.SetTextWithoutNotify("1s");
                return;
            }

            string numericInterval = new string(interval.Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(numericInterval))
            {
                Debug.LogWarning($"Interval input '{interval}' contained no digits, using default 1.0s");
                registry.wifiManager.updateInterval = defaultValue;
                measureInterval.SetTextWithoutNotify("1s");
                return;
            }

            if (!float.TryParse(numericInterval, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                Debug.LogWarning($"Failed to parse interval '{numericInterval}', using default 1.0s");
                result = defaultValue;
            }

            // Avoid ridiculous update intervals
            result = Mathf.Clamp(result, 0.1f, 60f);
            registry.wifiManager.updateInterval = result;
            measureInterval.SetTextWithoutNotify($"{result}s");
        }

        /// <summary>
        /// Handles change of accuracy input string. Extracts digits + dot, parses float safely, clamps.
        /// </summary>
        private void OnAccuracyChanged(string input)
        {
            float defaultValue = 1.0f;

            if (string.IsNullOrWhiteSpace(input))
            {
                Debug.LogWarning("Empty accuracy input, using default 1.0");
                accuracy = defaultValue;
                rollingAverageSize.SetTextWithoutNotify($"{defaultValue}");
                return;
            }

            // Extract digits and dots (so "1.5abc" becomes "1.5")
            string numericOnly = new string(input.Where(c => char.IsDigit(c) || c == '.').ToArray());
            if (string.IsNullOrEmpty(numericOnly))
            {
                Debug.LogWarning($"Input '{input}' contained no digits, using default 1.0");
                accuracy = defaultValue;
                rollingAverageSize.SetTextWithoutNotify($"{defaultValue}");
                return;
            }

            if (!float.TryParse(numericOnly, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                Debug.LogWarning($"Failed to parse '{numericOnly}', using default 1.0");
                value = defaultValue;
            }

            accuracy = Mathf.Clamp(value, 0.1f, 5f);
            rollingAverageSize.SetTextWithoutNotify($"{accuracy}");
        }

        
    }
}