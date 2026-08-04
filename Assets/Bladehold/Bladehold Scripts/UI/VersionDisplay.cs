using UnityEngine;
using TMPro;

public class VersionDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text versionText;

    private void Start()
    {
        if (versionText == null)
        {
            versionText = GetComponent<TMP_Text>();
        }

        if (versionText != null)
        {
            versionText.text = "v" + Application.version;
        }
    }
}
