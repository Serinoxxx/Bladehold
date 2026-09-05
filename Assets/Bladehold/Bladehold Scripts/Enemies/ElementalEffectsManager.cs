using UnityEngine;

public class ElementalEffectsManager : MonoBehaviour
{
    public static ElementalEffectsManager Instance { get; private set; }

    [Header("Status VFX Prefabs")]
    public GameObject fireStatusVfx;
    public GameObject iceStatusVfx;
    public GameObject frozenStatusVfx;
    public GameObject discordRingVfx;

    [Header("Duo Synergy Explode VFX Prefabs")]
    public GameObject thermalShockVfx;
    public GameObject plasmaOverloadVfx;
    public GameObject superconductorVfx;

    [Header("Audio")]
    public AudioClip statusAppliedSfx;
    public AudioClip frozenSfx;
    public AudioClip thermalShockSfx;
    public AudioClip plasmaOverloadSfx;
    public AudioClip superconductorSfx;
    public AudioClip discordAppliedSfx;

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
