using System;
using System.Collections;
using System.Collections.Generic;
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

        public RectTransform menu;
        public Button settingsButton;
        public TMP_Dropdown buildingField;
        public Button increaseFloorButton;
        public Button decreaseFloorButton;
        public TMP_Text currentFloorText;
        public TMP_Text currentRoomText;

       
        private Vector2 hiddenPos;
        private Vector2 visiblePos;
        private float slideDuration = 0.3f;

        private bool open = false;
        
        private void Start()
        {
            visiblePos = menu.anchoredPosition;
            
            // just move up by menu height + small padding to hide it
            hiddenPos = visiblePos + new Vector2(0, menu.rect.height + 50f);

            menu.anchoredPosition = hiddenPos;

            settingsButton.onClick.AddListener(OnSettingsClicked);
            decreaseFloorButton.onClick.AddListener(OnFloorDownClicked);
            increaseFloorButton.onClick.AddListener(OnFloorUpClicked);
            buildingField.onValueChanged.AddListener(OnBuildingChanged);

            // Initialize the building dropdown after BuildingManager has loaded
            InitializeBuildingDropdown();
        }

        private void InitializeBuildingDropdown()
        {
            List<string> buildingNames = registry.buildingManager.GetAvailableBuildingNames();
    
            // Clear and set options directly
            buildingField.options.Clear();
    
            foreach (string name in buildingNames)
            {
                buildingField.options.Add(new TMP_Dropdown.OptionData(name));
            }
    
            // Force refresh the dropdown display
            buildingField.RefreshShownValue();
    
            Debug.Log($"found {buildingNames.Count} building names");
    
            // Debug: Print all option texts to verify they're set correctly
            for (int i = 0; i < buildingField.options.Count; i++)
            {
                Debug.Log($"Option {i}: {buildingField.options[i].text}");
            }
    
            // Set default to active building if available
            SetBuildingDropdownToActive();
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
            open = true;
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, visiblePos, slideDuration));
        }

        public void CloseMenu()
        {
            open = false;
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
        
        private void SetBuildingDropdownToActive()
        {
            var activeBuilding = registry.buildingManager.GetShownBuilding();
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
            registry.settingsMenu.ToggleMenu();
        }

        private void OnFloorDownClicked()
        {
            //Debug.Log("Floor down button clicked");
            if (registry.buildingManager != null)
            {
                registry.buildingManager.DecreaseFloor();
            }
        }

        private void OnFloorUpClicked()
        {
            //Debug.Log("Floor up button clicked");
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
                registry.cameraController.GotoPosition(new Position(0,0,registry.buildingManager.GetShownFloor()));

                registry.buildingManager.ChangeBuilding(selectedBuilding);
                
            }
        }
    }
}