using System.Collections;
using System.Linq;
using controller;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using View;

namespace view
{
    public class SettingsMenu : MonoBehaviour
    {
        public CameraController cameraController;
        public BuildingManager buildingManager;
        public GraphManager graphManager;
        public WifiManager wifiManager;
        public DataCollectionMode dataCollectionMode;
        public SQLiteDatabase database;
        
        public RectTransform menu;
        public Button closeButton;

        public SwitchButton freeMovement;
        public SwitchButton compasActive;
        public SwitchButton collectDataMode;
        public TMP_InputField measureInterval;
        public TMP_InputField rollingAverageSize;

        public Button navigateButton;
        
        public Button importJson;
        public Button exportJson;

        private Vector2 hiddenPos;
        private Vector2 visiblePos;
        private float slideDuration = 0.3f;

        private void Start()
        {
            visiblePos = menu.anchoredPosition;
            hiddenPos = visiblePos + new Vector2(menu.rect.width, 0); // offscreen to the right
            menu.anchoredPosition = hiddenPos;

            
            CloseMenu();
            freeMovement.OnValueChanged += HandleFreeMovementToggle;
            compasActive.OnValueChanged += HandleCompasActiveToggle;
            collectDataMode.OnValueChanged += HandleCollectDataModeToggle;
            
            freeMovement.SetValue(false);
            compasActive.SetValue(false);
            collectDataMode.SetValue(false);
            
            closeButton.onClick.AddListener(CloseMenu);
            
            measureInterval.SetTextWithoutNotify(wifiManager.updateInterval.ToString() + "s");
            measureInterval.onValueChanged.AddListener(OnMeasureIntervalChanged);
            rollingAverageSize.SetTextWithoutNotify(wifiManager.rollingAverageLength.ToString());
            rollingAverageSize.onValueChanged.AddListener(OnRollingAverageSizeChanged);
            
            importJson.onClick.AddListener(database.PickFileAndImport);
            exportJson.onClick.AddListener(database.ExportWithSimpleFilename);
        }

        public void OpenMenu()
        {
            cameraController.inMenu = true;
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, visiblePos, slideDuration));
        }

        public void CloseMenu()
        {
            cameraController.inMenu = false;
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
            cameraController.freeMovement = !freeMovementMode;
        }

        public void HandleCompasActiveToggle(bool compasActiveMode)
        {
            cameraController.compasActive = compasActiveMode;
        }

        public void HandleCollectDataModeToggle(bool collectDataModeStart)
        {
            if (collectDataModeStart)
            {
                wifiManager.isUpdating = false; //cant measure while collecting data
                CloseMenu();
                dataCollectionMode.Activate();
                navigateButton.gameObject.SetActive(false);
            }
            else
            {
                wifiManager.isUpdating = true;
                CloseMenu();
                dataCollectionMode.Deactivate();
                navigateButton.gameObject.SetActive(true);

            }
        }
        
        private void OnMeasureIntervalChanged(string interval)
        {
            string numericInterval = new string(interval.Where(char.IsDigit).ToArray());
            wifiManager.updateInterval = float.Parse(numericInterval);
            string intervalWithSeconds = numericInterval + "s";
            measureInterval.SetTextWithoutNotify(intervalWithSeconds);
        }

        
        private void OnRollingAverageSizeChanged(string number)
        {
            wifiManager.rollingAverageLength = int.Parse(number);

        }
        
    }
}