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
            measureInterval.onEndEdit.AddListener(OnMeasureIntervalCommitted);
            rollingAverageSize.SetTextWithoutNotify(accuracy.ToString()); 
            rollingAverageSize.onEndEdit.AddListener(OnAccuracyInputCommitted);
            
            importJson.onClick.AddListener(registry.database.PickFileAndImport);
            exportJson.onClick.AddListener(registry.database.ExportWithSimpleFilename);
        }

        /// <summary>
        /// toggles the menu (open if close, vice versa)
        /// </summary>
        public void ToggleMenu()
        {
            if (open)
                CloseMenu();
            else
                OpenMenu();
        }

        /// <summary>
        /// Opens the menu
        /// </summary>
        public void OpenMenu()
        {
            registry.topMenu.CloseMenu();
            registry.floatingButtons.Hide();
            open = true;
            registry.cameraController.inMenu = true;
            closeButton.gameObject.SetActive(true);
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, visiblePos, slideDuration));
        }

        /// <summary>
        /// Closes the menu
        /// </summary>
        public void CloseMenu()
        {
            registry.topMenu.OpenMenu();
            registry.floatingButtons.Show();
            open = false;
            registry.cameraController.inMenu = false;
            closeButton.gameObject.SetActive(false);
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, hiddenPos, slideDuration));
        }

        /// <summary>
        /// Slides the menu to the given position
        /// </summary>
        /// <param name="rect">The menu</param>
        /// <param name="from">The starting position</param>
        /// <param name="to">The ending position</param>
        /// <param name="duration">The duration of the slide</param>
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

        /// <summary>
        /// changes the view mode of the camera
         /// </summary>
        public void HandleFreeMovementToggle(bool freeMovementMode)
        {
            registry.cameraController.ToggleViewMode();
        }

        /// <summary>
        /// changes the view mode of the camera
        /// </summary>
        public void HandleCompasActiveToggle(bool compasActiveMode)
        {
            registry.cameraController.compassActive = compasActiveMode;
        }

        /// <summary>
        /// toggles collect data mode
        /// </summary>
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

        /// <summary>
        /// Toggles the passive data collection
        /// </summary>
        public void HandleDbPassiveCollectionToggle(bool HandleDbPassiveCollection)
        {
            registry.wifiPositionTracker.passiveDataCollectionActive = HandleDbPassiveCollection;
        }
        
        
        /// <summary>
        /// Toggles the kalman filter (otherwise simple filter will be used)
        /// </summary>
        private void HandleKalmanFilterToggle(bool active)
        {
            registry.kalmanFilterActive = active;
        }
        
        /// <summary>
        /// validates and sets the measure interval
        /// </summary>
        private void OnMeasureIntervalCommitted(string input)
        {
            const float defaultValue = 2.0f;

            input = input?.Trim() ?? "";

            if (string.IsNullOrEmpty(input))
            {
                Debug.LogWarning("Empty interval input on commit, using default 2.0s");
                SetMeasureInterval(defaultValue);
                return;
            }

            // Try parse directly, no weird digit extraction mid-typing.
            if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                Debug.LogWarning($"Invalid interval input '{input}', using default 2.0s");
                SetMeasureInterval(defaultValue);
                return;
            }

            // Clamp between 0.1 and 60 seconds, like a sane overlord.
            SetMeasureInterval(Mathf.Clamp(value, 0.1f, 60f));
        }

        /// <summary>
        /// sets the measure interval in seconds
        /// </summary>
        private void SetMeasureInterval(float value)
        {
            registry.wifiManager.updateInterval = value;
            measureInterval.SetTextWithoutNotify($"{value}s");
        }


        /// <summary>
        /// Call this when the user finishes editing (e.g. OnEndEdit event).
        /// Validates and clamps the accuracy input once.
        /// </summary>
        private void OnAccuracyInputCommitted(string input)
        {
            const float defaultValue = 1.0f;

            // Trim whitespace, no need for fancy extraction mid-typing.
            input = input?.Trim() ?? "";

            if (string.IsNullOrEmpty(input))
            {
                Debug.LogWarning("Empty accuracy input on commit, using default 1.0");
                SetAccuracy(defaultValue);
                return;
            }

            // Try parse directly, no weird char extraction mid-input.
            if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                Debug.LogWarning($"Invalid accuracy input '{input}', using default 1.0");
                SetAccuracy(defaultValue);
                return;
            }

            // Clamp and set.
            SetAccuracy(Mathf.Clamp(value, 0.1f, 5f));
        }

        /// <summary>
        /// Updates accuracy and the UI text without triggering input events again.
        /// </summary>
        private void SetAccuracy(float value)
        {
            accuracy = value;
            rollingAverageSize.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));
        }

        
    }
}