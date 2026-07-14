using UnityEngine;

[CreateAssetMenu(fileName = "DodgeDashSO", menuName = "Scriptable Objects/DodgeDashSO")]
public class DodgeDashSO : ScriptableObject
{
    [Tooltip("The elf dodges while within this distance of the player AND inside the player's facing cone (i.e. while it reads as 'targeted').")]
    public float triggerDistance = 5f;

    [Tooltip("Min dot product of the player's forward vs. the direction to this elf for the elf to count as targeted (the Parry facing-cone shape, reversed).")]
    [Range(-1f, 1f)] public float targetedDotThreshold = 0.7f;

    [Tooltip("World distance covered by one dodge.")]
    public float dashDistance = 3.5f;

    [Tooltip("Seconds one dodge takes (a short burst strafe via NavMeshAgent.Move).")]
    public float dashSeconds = 0.25f;

    [Tooltip("Minimum seconds between dodges.")]
    public float dodgeCooldown = 2f;
}
