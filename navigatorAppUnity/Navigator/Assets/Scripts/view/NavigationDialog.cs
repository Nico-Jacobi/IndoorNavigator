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
        }

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

        public void Initialize(List<string> roomNames)
        {
            allOptions = new List<string>(roomNames);
            PopulateDropdownFromStrings(allOptions);
            CloseDialog();
        }

        public void ShowDialog()
        {
            Debug.Log("showing navigation dialog");
            pressedOutsideButton.gameObject.SetActive(true);
            navigationDialog.gameObject.SetActive(true);
            
            StartCoroutine(SlideDialog(navigationDialog, navigationDialog.anchoredPosition, visiblePos, slideDuration));
        }

        public void CloseDialog()
        {
            StartCoroutine(SlideDialogAndHide(navigationDialog, navigationDialog.anchoredPosition, hiddenPos, slideDuration));
        }

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

        public string GetSelectedDestination()
        {
            if (toField.options.Count == 0 || toField.value >= toField.options.Count)
                return null;

            return toField.options[toField.value].text;
        }

        private void PopulateDropdownFromStrings(List<string> options)
        {
            toField.ClearOptions();
            toField.AddOptions(options);
            startNavigationButton.interactable = options.Count > 0;
        }

        private void OnSearchChanged(string input)
        {
            string lowerInput = input.ToLower();
            List<string> matches = allOptions
                .Where(option => option.ToLower().Contains(lowerInput))
                .ToList();

            PopulateDropdownFromStrings(matches);
        }

        private void OnStartNavigationButtonPressed()
        {
            string destination = GetSelectedDestination();
            if (!string.IsNullOrEmpty(destination))
            {
                OnNavigationRequested?.Invoke(destination);
                CloseDialog();
            }
        }
        
        private void OnCloseButtonPressed()
        {
            CloseDialog();
        }
        
        private void OnCancelNavigationButtonPressed()
        {
            registry.graphManager.CancelNavigation();
            CloseDialog();
        }

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