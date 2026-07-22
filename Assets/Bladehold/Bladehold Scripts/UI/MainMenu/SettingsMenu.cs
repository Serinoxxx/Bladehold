using UnityEngine;
using TMPro;

namespace Bladehold.UI
{
    public class SettingsMenu : MonoBehaviour
    {
        public TextMeshProUGUI resolutionText;
        public TextMeshProUGUI frameRateText;
        public TextMeshProUGUI vsyncText;
        
        private Resolution[] resolutions;
        private int currentResIndex = 0;
        
        private int[] frameRates = new int[] { 30, 60, 120, 144, 240, -1 }; // -1 is unlimited
        private int currentFrIndex = 1; // Default 60
        
        private bool vsync = true;

        void Start()
        {
            resolutions = Screen.resolutions;
            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i].width == Screen.currentResolution.width && 
                    resolutions[i].height == Screen.currentResolution.height)
                {
                    currentResIndex = i;
                    break;
                }
            }
            UpdateUI();
        }

        public void NextResolution()
        {
            if (resolutions == null || resolutions.Length == 0) return;
            currentResIndex = (currentResIndex + 1) % resolutions.Length;
            ApplySettings();
        }
        
        public void PreviousResolution()
        {
            if (resolutions == null || resolutions.Length == 0) return;
            currentResIndex--;
            if (currentResIndex < 0) currentResIndex = resolutions.Length - 1;
            ApplySettings();
        }

        public void ToggleFrameRate()
        {
            currentFrIndex = (currentFrIndex + 1) % frameRates.Length;
            ApplySettings();
        }

        public void ToggleVSync()
        {
            vsync = !vsync;
            ApplySettings();
        }

        private void ApplySettings()
        {
            if (resolutions != null && resolutions.Length > 0)
            {
                Resolution res = resolutions[currentResIndex];
                Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            }
            
            Application.targetFrameRate = frameRates[currentFrIndex];
            QualitySettings.vSyncCount = vsync ? 1 : 0;
            
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (resolutionText && resolutions != null && resolutions.Length > 0) 
                resolutionText.text = $"{resolutions[currentResIndex].width} x {resolutions[currentResIndex].height}";
            if (frameRateText) 
            {
                int fr = frameRates[currentFrIndex];
                frameRateText.text = fr == -1 ? "Unlimited" : fr.ToString();
            }
            if (vsyncText) vsyncText.text = vsync ? "On" : "Off";
        }
    }
}
