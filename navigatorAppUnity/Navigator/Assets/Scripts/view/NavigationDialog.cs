using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using controller;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace view
{
    public class NavigationDialog : MonoBehaviour
    {
        public Registry registry;
        
        public TMP_Dropdown toField;
        public RectTransform navigationDialog;
        public TMP_InputField searchField;
        public Button startNavigationButton;
        public Button cancelButton;
        public Button pressedOutsideButton;

        private List<string> allOptions;
        private Vector2 hiddenPos;
        private Vector2 visiblePos;
        private float slideDuration = 0.3f;

        public event Action<string> OnNavigationRequested;

        void Start()
        {
            searchField.onValueChanged.AddListener(OnSearchChanged);
            startNavigationButton.onClick.AddListener(OnStartNavigationButtonPressed);
            cancelButton.onClick.AddListener(OnCancelNavigationButtonPressed);
            pressedOutsideButton.onClick.AddListener(OnCloseButtonPressed);

            // Initialize positions
            InitializePositions();
            
            List<string> allRoomNames = new List<string>(registry.buildingManager.GetActiveGraph().allRoomsNames);
            registry.navigationDialog.Initialize(allRoomNames);
        }

        /// <summary>
        /// initializes the dialog (so its is hidden off screen)
        /// </summary>
        private void InitializePositions()
        {
            visiblePos = navigationDialog.anchoredPosition;
            
            // Calculate hidden position - move completely off-screen to the right
            Canvas canvas = GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            float canvasWidth = canvasRect.rect.width;
            
            hiddenPos = visiblePos + new Vector2(canvasWidth + navigationDialog.rect.width + 100f, 0);
            
            // Start hidden
            navigationDialog.anchoredPosition = hiddenPos;
        }

        /// <summary>
        /// Loads room names into dropdown and hides the dialog.
        /// </summary>
        public void Initialize(List<string> roomNames)
        {
            allOptions = new List<string>(roomNames);
            PopulateDropdownFromStrings(allOptions);
            CloseDialog();
        }

        /// <summary>
        /// Shows and slides the dialog into view.
        /// </summary>
        public void ShowDialog()
        {
            Debug.Log("showing navigation dialog");
            pressedOutsideButton.gameObject.SetActive(true);
            navigationDialog.gameObject.SetActive(true);
            
            StartCoroutine(SlideDialog(navigationDialog, navigationDialog.anchoredPosition, visiblePos, slideDuration));
        }

        /// <summary>
        /// Slides dialog out and hides it.
        /// </summary>
        public void CloseDialog()
        {
            StartCoroutine(SlideDialogAndHide(navigationDialog, navigationDialog.anchoredPosition, hiddenPos, slideDuration));
        }

        /// <summary>
        /// Smoothly slides the dialog between two positions over a duration.
        /// </summary>
        private IEnumerator SlideDialog(RectTransform rect, Vector2 from, Vector2 to, float duration)
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
        /// Slides dialog out and disables it after animation completes.
        /// </summary>
        private IEnumerator SlideDialogAndHide(RectTransform rect, Vector2 from, Vector2 to, float duration)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                rect.anchoredPosition = Vector2.Lerp(from, to, time / duration);
                yield return null;
            }
            rect.anchoredPosition = to;
            
            // Hide after animation completes
            pressedOutsideButton.gameObject.SetActive(false);
            navigationDialog.gameObject.SetActive(false);
        }

        /// <summary>
        /// Returns the currently selected destination from the dropdown.
        /// </summary>
        public string GetSelectedDestination()
        {
            if (toField.options.Count == 0 || toField.value >= toField.options.Count)
                return null;

            return toField.options[toField.value].text;
        }

        /// <summary>
        /// Populates dropdown with given options and updates start button interactability.
        /// </summary>
        private void PopulateDropdownFromStrings(List<string> options)
        {
            toField.ClearOptions();
            toField.AddOptions(options);
            startNavigationButton.interactable = options.Count > 0;
        }

        /// <summary>
        /// Filters dropdown options based on search input.
        /// </summary>
        private void OnSearchChanged(string input)
        {
            string lowerInput = input.ToLower();
            List<string> matches = allOptions
                .Where(option => option.ToLower().Contains(lowerInput))
                .ToList();

            PopulateDropdownFromStrings(matches);
        }

        /// <summary>
        /// Handles the start navigation button press and triggers navigation event.
        /// </summary>
        private void OnStartNavigationButtonPressed()
        {
            string destination = GetSelectedDestination();
            if (!string.IsNullOrEmpty(destination))
            {
                OnNavigationRequested?.Invoke(destination);
                CloseDialog();
            }
        }
        
        /// <summary>
        /// Handles closing the dialog when the close button is pressed.
        /// </summary>
        private void OnCloseButtonPressed()
        {
            CloseDialog();
        }
        
        /// <summary>
        /// Cancels navigation and closes the dialog.
        /// </summary>
        private void OnCancelNavigationButtonPressed()
        {
            registry.graphManager.CancelNavigation();
            CloseDialog();
        }

        
        /// <summary>
        /// Refreshes dropdown options and reapplies search filter if active.
        /// </summary>
        /// <param name="newOptions">New list of options.</param>
        public void RefreshOptions(List<string> newOptions)
        {
            allOptions = new List<string>(newOptions);

            // Preserve current search if active
            string currentSearch = searchField.text;
            if (!string.IsNullOrEmpty(currentSearch))
            {
                OnSearchChanged(currentSearch);
            }
            else
            {
                PopulateDropdownFromStrings(allOptions);
            }
        }
    }
}