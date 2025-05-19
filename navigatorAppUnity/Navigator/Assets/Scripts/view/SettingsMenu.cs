using System.Collections;
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
        public TMP_InputField measureInterval;
        public TMP_InputField rollingAverageSize;

        public Button navigateButton;
        public Button gotoPositionButton;

        public Button importJson;
        public Button exportJson;

        private Vector2 hiddenPos;
        private Vector2 visiblePos;
        private float slideDuration = 0.3f;

        private void Start()
        {
            visiblePos = menu.anchoredPosition;
            hiddenPos = visiblePos + new Vector2(menu.rect.width * 1.3f, 0); // offscreen to the right
            menu.anchoredPosition = hiddenPos;

            
            CloseMenu();
            freeMovement.OnValueChanged += HandleFreeMovementToggle;
            compasActive.OnValueChanged += HandleCompasActiveToggle;
            collectDataMode.OnValueChanged += HandleCollectDataModeToggle;
            
            freeMovement.SetValue(false);
            compasActive.SetValue(false);
            collectDataMode.SetValue(false);
            
            closeButton.onClick.AddListener(CloseMenu);
            
            measureInterval.SetTextWithoutNotify(registry.wifiManager.updateInterval.ToString() + "s");
            measureInterval.onValueChanged.AddListener(OnMeasureIntervalChanged);
            rollingAverageSize.SetTextWithoutNotify(registry.positionTracker.rollingAverageLength.ToString());
            rollingAverageSize.onValueChanged.AddListener(OnRollingAverageSizeChanged);
            
            importJson.onClick.AddListener(registry.database.PickFileAndImport);
            exportJson.onClick.AddListener(registry.database.ExportWithSimpleFilename);
        }

        public void OpenMenu()
        {
            registry.cameraController.inMenu = true;
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, visiblePos, slideDuration));
        }

        public void CloseMenu()
        {
            print(registry);
            print(registry.cameraController);
            registry.cameraController.inMenu = false;
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
            registry.cameraController.freeMovement = !freeMovementMode;
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
                CloseMenu();
                registry.dataCollectionMode.Activate();
                navigateButton.gameObject.SetActive(false);
                gotoPositionButton.gameObject.SetActive(false);

            }
            else
            {
                registry.wifiManager.isUpdating = true;
                CloseMenu();
                registry.dataCollectionMode.Deactivate();
                navigateButton.gameObject.SetActive(true);
                gotoPositionButton.gameObject.SetActive(true);

            }
        }
        
        private void OnMeasureIntervalChanged(string interval)
        {
            string numericInterval = new string(interval.Where(char.IsDigit).ToArray());
            registry.wifiManager.updateInterval = float.Parse(numericInterval);
            string intervalWithSeconds = numericInterval + "s";
            measureInterval.SetTextWithoutNotify(intervalWithSeconds);
        }

        
        private void OnRollingAverageSizeChanged(string number)
        {
            registry.positionTracker.rollingAverageLength = int.Parse(number);

        }
        
    }
}