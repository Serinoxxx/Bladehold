using UnityEngine;
using TMPro;

namespace Bladehold.UI
{
    public class CharacterRotunda : MonoBehaviour
    {
        public Transform rotundaCenter;
        public GameObject[] characterModels;
        public TextMeshProUGUI characterNameText;
        public string[] characterNames;
        public int currentIndex = 0;
        public float radius = 3f;
        public float rotateSpeed = 5f;
        
        private float currentAngle = 0f;
        private float targetAngle = 0f;

        void Start()
        {
            if (characterModels == null || characterModels.Length == 0) return;
            
            float angleStep = 360f / characterModels.Length;
            for (int i = 0; i < characterModels.Length; i++)
            {
                if (characterModels[i] == null) continue;
                float angle = i * angleStep;
                Vector3 pos = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * radius, 0, Mathf.Cos(angle * Mathf.Deg2Rad) * radius);
                characterModels[i].transform.localPosition = pos;
                // Face outwards
                characterModels[i].transform.localRotation = Quaternion.LookRotation(pos);
            }
            
            UpdateUI();
        }

        void Update()
        {
            if (characterModels == null || characterModels.Length == 0) return;
            
            float angleStep = 360f / characterModels.Length;
            targetAngle = currentIndex * angleStep; // Positive to rotate models in opposite direction of index increase
            
            currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * rotateSpeed);
            rotundaCenter.localRotation = Quaternion.Euler(0, currentAngle, 0);
        }

        public void Next()
        {
            currentIndex = (currentIndex + 1) % characterModels.Length;
            UpdateUI();
        }

        public void Previous()
        {
            currentIndex--;
            if (currentIndex < 0) currentIndex = characterModels.Length - 1;
            UpdateUI();
        }
        
        private void UpdateUI()
        {
            if (characterNameText != null && characterNames != null && currentIndex < characterNames.Length)
            {
                characterNameText.text = characterNames[currentIndex];
            }
        }
    }
}
