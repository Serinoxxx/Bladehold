using UnityEngine;
using TMPro;

namespace Bladehold.UI
{
    public class CharacterRotunda : MonoBehaviour
    {
        public Transform rotundaCenter;
        public GameObject[] characterModels;
        public TextMeshProUGUI characterNameText;
        public TextMeshProUGUI characterDescText;
        public string[] characterNames;
        public string[] characterDescs;
        public int currentIndex = 0;
        
        private GameObject currentInstantiatedModel;

        void Start()
        {
            Select(0);
        }

        public void Select(int index)
        {
            if (characterModels == null || characterModels.Length == 0 || index < 0 || index >= characterModels.Length) return;
            
            currentIndex = index;
            
            if (currentInstantiatedModel != null)
            {
                Destroy(currentInstantiatedModel);
            }
            
            if (characterModels[index] != null)
            {
                currentInstantiatedModel = Instantiate(characterModels[index], rotundaCenter);
                currentInstantiatedModel.transform.localPosition = Vector3.zero;
                currentInstantiatedModel.transform.localRotation = Quaternion.Euler(0, 180, 0); // Face camera
            }
            
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (characterNameText != null && characterNames != null && currentIndex < characterNames.Length)
            {
                characterNameText.text = characterNames[currentIndex];
            }
            if (characterDescText != null && characterDescs != null && currentIndex < characterDescs.Length)
            {
                characterDescText.text = $"<b>{characterNames[currentIndex]}</b>\n\n{characterDescs[currentIndex]}\n\n<color=#ffaa00>SELECTED</color>";
            }
        }
    }
}
