using System;
using System.Collections.Generic;
using System.Linq;
using controller;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace view
{
    public class NavigationDialog : MonoBehaviour
    {
        Registry registry;
        
        [Header("UI Components")] public TMP_Dropdown toField;
        public RectTransform navigationDialog;
        public TMP_InputField searchField;
        public Button startNavigationButton;
        public Button closeButton;

        private List<string> allOptions;

        public event Action<string> OnNavigationRequested;

        void Start()
        {
            searchField.onValueChanged.AddListener(OnSearchChanged);
            startNavigationButton.onClick.AddListener(OnStartNavigationButtonPressed);
            closeButton.onClick.AddListener(OnCloseButtonPressed);

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
            navigationDialog.gameObject.SetActive(true);
        }

        public void CloseDialog()
        {
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