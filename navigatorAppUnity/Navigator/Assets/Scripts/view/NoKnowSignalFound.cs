using System;
using UnityEngine;
using UnityEngine.UI;

namespace view
{
    public class NoKnowSignalFound : MonoBehaviour
    {
        [SerializeField] private GameObject dialogPanel;
        [SerializeField] private Button closeButton;

        private DateTime lastShowTime = DateTime.MinValue;
        private const float cooldown = 10f; // seconds
        
        private bool shouldShow = false;
        private bool shouldHide = false;

        private void Awake()
        {
            if (dialogPanel == null)
            {
                Debug.LogError("Dialog Panel reference missing!");
            }
            else
            {
                dialogPanel.SetActive(false);
            }

            if (closeButton == null)
            {
                Debug.LogError("Close Button reference missing!");
            }
            else
            {
                closeButton.onClick.AddListener(Close);
            }

            Hide();
        }
        
        private void Update()
        {
            // handle queued UI updates on main thread
            if (shouldShow)
            {
                shouldShow = false;
                if (dialogPanel != null)
                    dialogPanel.SetActive(true);
            }
            
            if (shouldHide)
            {
                shouldHide = false;
                if (dialogPanel != null)
                    dialogPanel.SetActive(false);
            }
        }

        public void Show()
        {
            var timeSinceLastShow = (DateTime.Now - lastShowTime).TotalSeconds;
            
            if (timeSinceLastShow < cooldown)
            {
                Debug.Log("Show blocked: cooldown active.");
                return;
            }

            lastShowTime = DateTime.Now;
            shouldShow = true; // queue for main thread
        }

        public void Hide()
        {
            shouldHide = true; // queue for main thread
        }

        public void Close()
        {
            Hide();
        }
    }
}