using controller;
using model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace view
{
    public class TopMenu : MonoBehaviour
    {
        public Registry registry;

        public Button settingsButton;
        public TMP_Dropdown buildingField;
        public Button increaseFloorButton;
        public Button decreaseFloorButton;
        public TMP_Text currentFloorText;
        public TMP_Text currentRoomText;

        private void Start()
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
            decreaseFloorButton.onClick.AddListener(OnFloorDownClicked);
            increaseFloorButton.onClick.AddListener(OnFloorUpClicked);
            buildingField.onValueChanged.AddListener(OnBuildingChanged);

            // Initialize the building dropdown after BuildingManager has loaded
            InitializeBuildingDropdown();
        }

        private void InitializeBuildingDropdown()
        {
            if (registry.buildingManager != null)
            {
                buildingField.ClearOptions();
                buildingField.AddOptions(registry.buildingManager.GetAvailableBuildingNames());
                
                // Set default to h4 if available
                SetBuildingDropdownToActive();
            }
        }

        private void SetBuildingDropdownToActive()
        {
            var activeBuilding = registry.buildingManager.GetActiveBuilding();
            if (activeBuilding != null)
            {
                int index = buildingField.options.FindIndex(option => option.text == activeBuilding.buildingName);
                if (index >= 0)
                {
                    buildingField.SetValueWithoutNotify(index);
                    buildingField.RefreshShownValue();
                }
            }
        }

        public void UpdateUI()
        {
            UpdateFloorText();
            UpdateFloorButtons();
            SetBuildingDropdownToActive();
        }

        public void UpdateCurrentRoomDisplay(string roomName)
        {
            if (currentRoomText != null)
            {
                currentRoomText.text = roomName;
            }
        }

        private void UpdateFloorText()
        {
            if (currentFloorText != null && registry.buildingManager != null)
            {
                int currentFloor = registry.buildingManager.GetShownFloor();
                currentFloorText.text = $"Floor: {currentFloor}";
            }
        }

        private void UpdateFloorButtons()
        {
            if (registry.buildingManager != null)
            {
                if (increaseFloorButton != null)
                {
                    increaseFloorButton.interactable = registry.buildingManager.CanIncreaseFloor();
                }

                if (decreaseFloorButton != null)
                {
                    decreaseFloorButton.interactable = registry.buildingManager.CanDecreaseFloor();
                }
            }
        }

        private void OnSettingsClicked()
        {
            registry.settingsMenu.OpenMenu();
        }

        private void OnFloorDownClicked()
        {
            Debug.Log("Floor down button clicked");
            if (registry.buildingManager != null)
            {
                registry.buildingManager.DecreaseFloor();
            }
        }

        private void OnFloorUpClicked()
        {
            Debug.Log("Floor up button clicked");
            if (registry.buildingManager != null)
            {
                registry.buildingManager.IncreaseFloor();
            }
        }

        private void OnBuildingChanged(int index)
        {
            if (index >= 0 && index < buildingField.options.Count)
            {
                string selectedBuilding = buildingField.options[index].text;
                Debug.Log($"Building changed to index {index}: {selectedBuilding}");
                
                if (registry.buildingManager != null)
                {
                    registry.buildingManager.ChangeBuilding(selectedBuilding);
                }
            }
        }
    }
}