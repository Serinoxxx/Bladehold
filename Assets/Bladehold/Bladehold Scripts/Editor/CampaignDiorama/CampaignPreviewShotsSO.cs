using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Camera poses for <see cref="CampaignPreviewCapture" />: one hero shot per level scene, shown in the
///     campaign map tooltip. Scenes without an entry get an automatic shot from behind and above the player
///     spawn, looking the way the player faces. Re-run <b>Bladehold > Campaign > Capture Level Previews</b> after editing.
/// </summary>
[CreateAssetMenu(fileName = "CampaignPreviewShots", menuName = "Scriptable Objects/Campaign/Campaign Preview Shots")]
public class CampaignPreviewShotsSO : ScriptableObject
{
    [System.Serializable]
    public class Shot
    {
        [Tooltip("Scene file name without extension, as in CampaignNodeSO.sceneName.")]
        public string sceneName;
        public Vector3 position;
        public Vector3 euler;
        public float fieldOfView = 50f;
        [Tooltip("Multiplies the captured colours, for night levels that read too dark as a thumbnail.")]
        public float brightness = 1f;
    }

    public List<Shot> shots = new List<Shot>();

    public Shot Get(string sceneName)
    {
        foreach (Shot s in shots)
        {
            if (s != null && s.sceneName == sceneName) return s;
        }
        return null;
    }
}
