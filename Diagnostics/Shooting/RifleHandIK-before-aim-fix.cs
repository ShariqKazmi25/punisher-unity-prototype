using UnityEngine;

// Attach to the character model that owns the Humanoid Animator.
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class RifleHandIK : MonoBehaviour
{
    [Header("Left Hand Grip")]
    [Tooltip("An empty Transform under the rifle representing the desired left wrist position and hand orientation.")]
    public Transform leftHandGrip;
    [Range(0f, 1f)] public float positionWeight = 1f;
    [Range(0f, 1f)] public float rotationWeight = 0f;

    [Header("Optional Elbow Guide")]
    [Tooltip("An empty Transform under the character root, near the desired left elbow. Keep it outside the arm bones.")]
    public Transform leftElbowHint;
    [Range(0f, 1f)] public float elbowWeight = 0.5f;

    [Header("Camera Aim")]
    public bool followCameraAim = true;
    [Range(0f, 1f)] public float aimBodyWeight = 0.35f;
    [Range(0f, 1f)] public float aimHeadWeight = 0.6f;

    private Animator characterAnimator;
    private PunisherShooter shooter;

    private void Awake()
    {
        characterAnimator = GetComponent<Animator>();
        shooter = GetComponentInParent<PunisherShooter>();
        if (!characterAnimator.isHuman)
        {
            Debug.LogError("RifleHandIK requires a Humanoid Animator. Attach it to the Punisher model with the Animator component.", this);
            enabled = false;
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (characterAnimator == null || !characterAnimator.isHuman)
            return;

        if (shooter == null) shooter = GetComponentInParent<PunisherShooter>();
        int ikLayer = shooter != null && shooter.WeaponLayerIndex > 0 ? shooter.WeaponLayerIndex : 0;
        if (layerIndex != ikLayer) return;
        bool aiming = followCameraAim && shooter != null && shooter.WeaponReady;
        characterAnimator.SetLookAtWeight(aiming ? 1f : 0f, aimBodyWeight, aimHeadWeight, 0f, 0.5f);
        if (aiming) characterAnimator.SetLookAtPosition(shooter.GetAimLookPoint());

        bool gripAssigned = leftHandGrip != null;
        characterAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand,
            gripAssigned ? Mathf.Clamp01(positionWeight) : 0f);
        characterAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand,
            gripAssigned ? Mathf.Clamp01(rotationWeight) : 0f);

        if (gripAssigned)
        {
            characterAnimator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandGrip.position);
            characterAnimator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandGrip.rotation);
        }

        bool hintAssigned = gripAssigned && leftElbowHint != null;
        characterAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,
            hintAssigned ? Mathf.Clamp01(elbowWeight) * Mathf.Clamp01(positionWeight) : 0f);
        if (hintAssigned)
            characterAnimator.SetIKHintPosition(AvatarIKHint.LeftElbow, leftElbowHint.position);
    }

    private void OnDrawGizmosSelected()
    {
        if (leftHandGrip != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(leftHandGrip.position, 0.015f);
            Gizmos.DrawRay(leftHandGrip.position, leftHandGrip.forward * 0.08f);
        }
        if (leftElbowHint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(leftElbowHint.position, 0.02f);
        }
    }
}
