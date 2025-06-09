using controller;
using UnityEngine;
using UnityEngine.UI;

namespace view
{
    public class FloatingButtons : MonoBehaviour
    {
        public Registry registry;
        
        public Button startNavigationButton;
        public Button gotoPositionButton;

        private void Awake()
        {
            startNavigationButton.onClick.AddListener(OnStartNavigationClicked);
            gotoPositionButton.onClick.AddListener(OnGotoPositionClicked);
        }
        
        /// <summary>
        /// Shows the floating buttons
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the floating buttons
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Toggles the visibility of the floating buttons
        /// </summary>
        public void Toggle()
        {
            gameObject.SetActive(!gameObject.activeInHierarchy);
        }

        /// <summary>
        /// Triggered when the Start Navigation button is clicked.
        /// </summary>
        private void OnStartNavigationClicked()
        {
            registry.navigationDialog.ShowDialog();
        }

        /// <summary>
        /// Triggered when the Go To Position button is clicked.
        /// </summary>
        private void OnGotoPositionClicked()
        {
            registry.cameraController.GotoPrediction();
        }
    }
}