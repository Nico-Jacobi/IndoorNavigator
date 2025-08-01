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
        
        /// <summary>
        /// Initializes the building dropdown list with available building names from BuildingManager.
        /// Sets the dropdown to the currently active building.
        /// </summary>
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

        /// <summary>
        /// Toggles the top menu visibility by sliding it in or out.
        /// </summary>
        public void ToggleMenu()
        {
            if (open)
                CloseMenu();
            else
                OpenMenu();
        }

        /// <summary>
        /// Slides the menu into visible position with animation.
        /// </summary>
        public void OpenMenu()
        {
            open = true;
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, visiblePos, slideDuration));
        }

        /// <summary>
        /// Slides the menu out to hidden position with animation.
        /// </summary>
        public void CloseMenu()
        {
            open = false;
            StartCoroutine(SlideMenu(menu, menu.anchoredPosition, hiddenPos, slideDuration));
        }

        /// <summary>
        /// Coroutine that animates sliding a RectTransform from one position to another over a duration.
        /// </summary>
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
        /// Updates the building dropdown selection to match the active building in BuildingManager.
        /// </summary>
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

        /// <summary>
        /// Updates UI elements: floor text, floor button interactivity, and building dropdown selection.
        /// </summary>
        public void UpdateUI()
        {
            UpdateFloorText();
            UpdateFloorButtons();
            SetBuildingDropdownToActive();
        }

        /// <summary>
        /// Updates the current room display text.
        /// </summary>
        public void UpdateCurrentRoomDisplay(string roomName)
        {
            if (currentRoomText != null)
            {
                currentRoomText.text = roomName;
            }
        }

        /// <summary>
        /// Updates the floor display text to show the current floor number.
        /// </summary>
        private void UpdateFloorText()
        {
            if (currentFloorText != null && registry.buildingManager != null)
            {
                int currentFloor = registry.buildingManager.GetShownFloor();
                currentFloorText.text = $"Floor: {currentFloor}";
            }
        }

        /// <summary>
        /// Updates the interactable state of floor up/down buttons based on BuildingManager limits.
        /// </summary>
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

        /// <summary>
        /// Event handler for settings button click, toggles the settings menu visibility.
        /// </summary>
        private void OnSettingsClicked()
        {
            registry.settingsMenu.ToggleMenu();
        }

        /// <summary>
        /// Event handler for increase floor button click, decreases the current floor in BuildingManager.
        /// </summary>
        private void OnFloorDownClicked()
        {
            //Debug.Log("Floor down button clicked");
            if (registry.buildingManager != null)
            {
                registry.buildingManager.DecreaseFloor();
            }
        }
        /// <summary>
        /// Event handler for increase floor button click, increases the current floor in BuildingManager.
        /// </summary>
        private void OnFloorUpClicked()
        {
            //Debug.Log("Floor up button clicked");
            if (registry.buildingManager != null)
            {
                registry.buildingManager.IncreaseFloor();
            }
        }

        /// <summary>
        /// Event handler for building dropdown change, switches active building and resets camera position.
        /// </summary>
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