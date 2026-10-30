using UnityEngine;

// Attach to the character model that owns the Humanoid Animator.
[DefaultExecutionOrder(150)]
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

    [Header("Aiming Grip")]
    [Tooltip("Keep the weapon-relative wrist fit from the first settled idle pose when aiming.")]
    public bool preserveIdleGrip = true;
    [Tooltip("Keep the idle finger curl on this fitted rifle while the weapon layer is active.")]
    public bool preserveIdleFingerPose = true;
    [Range(0f, 1f)] public float aimingPositionWeight = 1f;
    [Range(0f, 1f)] public float aimingRotationWeight = 1f;

    [Header("Barrel Alignment")]
    [Tooltip("The rifle root under the right hand. Automatically resolves from LeftHandGrip's parent.")]
    public Transform weaponTransform;
    [Tooltip("Optional barrel-tip marker. Uses PunisherShooter.Muzzle when assigned.")]
    public Transform barrelTip;
    [Tooltip("Create a runtime Muzzle marker for this AR15 if none was assigned.")]
    public bool createAR15Muzzle = true;
    [Tooltip("Measured tip of the current AR15 in its root's local coordinates.")]
    public Vector3 ar15MuzzleLocalPosition = new Vector3(0.00055f, 0.68964f, -5.96547f);
    [Tooltip("Barrel direction in the rifle root's local space. The imported AR15 points along -Z.")]
    public Vector3 localBarrelDirection = Vector3.back;
    [Range(0f, 120f)] public float maxAimCorrection = 85f;
    [Tooltip("The Mixamo head's local face direction.")]
    public Vector3 localHeadForward = Vector3.forward;

    Transform rightUpperArm, rightLowerArm, rightHand, leftUpperArm, leftLowerArm, leftHand, head;
    Transform calibratedGrip;
    Vector3 idleGripPosition;
    Quaternion idleGripRotation = Quaternion.identity;
    bool hasIdleGrip;
    Transform[] fingers;
    Quaternion[] idleFingerRotations;

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
            return;
        }
        rightUpperArm = characterAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = characterAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        leftUpperArm = characterAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArm = characterAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = characterAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        head = characterAnimator.GetBoneTransform(HumanBodyBones.Head);
        var fingerBones = new System.Collections.Generic.List<Transform>();
        for (int bone = (int)HumanBodyBones.LeftThumbProximal; bone <= (int)HumanBodyBones.RightLittleDistal; bone++)
        {
            Transform finger = characterAnimator.GetBoneTransform((HumanBodyBones)bone);
            if (finger != null) fingerBones.Add(finger);
        }
        fingers = fingerBones.ToArray(); idleFingerRotations = new Quaternion[fingers.Length];
        if (weaponTransform == null && leftHandGrip != null) weaponTransform = leftHandGrip.parent;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (characterAnimator == null || !characterAnimator.isHuman)
            return;

        if (shooter == null) shooter = GetComponentInParent<PunisherShooter>();
        int ikLayer = shooter != null && shooter.WeaponLayerIndex > 0 ? shooter.WeaponLayerIndex : 0;
        if (layerIndex != ikLayer) return;
        float aimBlend = followCameraAim && shooter != null ? AimBlend : 0f;
        characterAnimator.SetLookAtWeight(aimBlend, aimBodyWeight, aimHeadWeight, 0f, 0.5f);
        if (aimBlend > 0f) characterAnimator.SetLookAtPosition(shooter.GetAimLookPoint());

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

    private void LateUpdate()
    {
        if (characterAnimator == null || !characterAnimator.enabled || !characterAnimator.isHuman) return;
        if (shooter == null) shooter = GetComponentInParent<PunisherShooter>();
        if (shooter == null) return;
        if (weaponTransform == null && leftHandGrip != null) weaponTransform = leftHandGrip.parent;
        if (barrelTip == null)
        {
            barrelTip = shooter.muzzle;
            if (barrelTip == null && createAR15Muzzle && weaponTransform != null && weaponTransform.name == "ar15")
            {
                barrelTip = new GameObject("Muzzle").transform;
                barrelTip.SetParent(weaponTransform, false);
                barrelTip.localPosition = ar15MuzzleLocalPosition;
                barrelTip.localRotation = Quaternion.LookRotation(localBarrelDirection);
            }
        }
        if (shooter.muzzle == null && barrelTip != null) shooter.muzzle = barrelTip;
        if (calibratedGrip != leftHandGrip) { calibratedGrip = leftHandGrip; hasIdleGrip = false; }
        float blend = AimBlend;
        if (blend <= 0.0001f)
        {
            // Capture after the native IK solve, so the existing idle weights
            // and the user's weapon fitting remain the reference pose.
            if (!hasIdleGrip && leftHandGrip != null && leftHand != null &&
                !characterAnimator.IsInTransition(0) && characterAnimator.GetCurrentAnimatorStateInfo(0).IsName("Rifle Idle"))
            {
                idleGripPosition = leftHandGrip.InverseTransformPoint(leftHand.position);
                idleGripRotation = Quaternion.Inverse(leftHandGrip.rotation) * leftHand.rotation;
                for (int i = 0; i < fingers.Length; i++) idleFingerRotations[i] = fingers[i].localRotation;
                hasIdleGrip = true;
            }
            return;
        }
        Vector3 aimPoint = shooter.GetAimLookPoint();
        if (followCameraAim)
        {
            AlignBarrel(aimPoint, blend);
            if (head != null && localHeadForward.sqrMagnitude > 0.0001f)
            {
                Vector3 direction = aimPoint - head.position;
                if (direction.sqrMagnitude > 0.0001f)
                    head.rotation = AimCorrection(head.TransformDirection(localHeadForward), direction, blend) * head.rotation;
            }
        }
        if (leftHandGrip == null || leftHand == null || leftUpperArm == null || leftLowerArm == null) return;
        // The barrel/right arm and native torso IK have now finished moving the
        // rifle. Solve the support arm against THIS frame's final grip pose.
        bool useIdle = preserveIdleGrip && hasIdleGrip;
        Vector3 target = useIdle ? leftHandGrip.TransformPoint(idleGripPosition) : leftHandGrip.position;
        Quaternion rotation = leftHandGrip.rotation * (useIdle ? idleGripRotation : Quaternion.identity);
        Vector3 handPosition = Vector3.Lerp(leftHand.position, target, blend * aimingPositionWeight);
        Quaternion handRotation = Quaternion.Slerp(leftHand.rotation, rotation, blend * aimingRotationWeight);
        SolveArm(leftUpperArm, leftLowerArm, leftHand, handPosition, leftElbowHint, elbowWeight);
        leftHand.rotation = handRotation;
        if (preserveIdleFingerPose && hasIdleGrip)
            for (int i = 0; i < fingers.Length; i++)
                fingers[i].localRotation = Quaternion.Slerp(fingers[i].localRotation, idleFingerRotations[i], blend);
    }

    float AimBlend => shooter.WeaponLayerIndex > 0
        ? characterAnimator.GetLayerWeight(shooter.WeaponLayerIndex) : shooter.WeaponReady ? 1f : 0f;

    void AlignBarrel(Vector3 aimPoint, float blend)
    {
        if (weaponTransform == null || rightUpperArm == null || rightLowerArm == null || rightHand == null || localBarrelDirection.sqrMagnitude < 0.0001f) return;
        Quaternion initialUpper = rightUpperArm.rotation, initialLower = rightLowerArm.rotation, initialHand = rightHand.rotation;
        Transform tip = barrelTip != null ? barrelTip : shooter.muzzle;
        // Aim the rigid rifle from the trigger hand, then choose a wrist position
        // inside BOTH arm reach spheres. Moving only the shoulder can push the
        // support grip outside the left arm's reach, even with full IK weight.
        for (int i = 0; i < 12; i++)
        {
            Vector3 origin = tip != null ? tip.position : weaponTransform.position;
            Vector3 direction = aimPoint - origin;
            if (direction.sqrMagnitude < 0.0001f) break;
            Vector3 forward = tip != null ? tip.forward : weaponTransform.TransformDirection(localBarrelDirection);
            Quaternion correction = Quaternion.FromToRotation(forward, direction);
            rightHand.rotation = Quaternion.RotateTowards(initialHand, correction * rightHand.rotation, maxAimCorrection);
            if (leftHandGrip != null && leftUpperArm != null && leftLowerArm != null && leftHand != null)
            {
                Vector3 grip = preserveIdleGrip && hasIdleGrip ? leftHandGrip.TransformPoint(idleGripPosition) : leftHandGrip.position;
                Vector3 span = grip - rightHand.position;
                float rightReach = Vector3.Distance(rightUpperArm.position, rightLowerArm.position) + Vector3.Distance(rightLowerArm.position, rightHand.position) - .0001f;
                float leftReach = Vector3.Distance(leftUpperArm.position, leftLowerArm.position) + Vector3.Distance(leftLowerArm.position, leftHand.position) - .0001f;
                Vector3 wrist = rightHand.position;
                for (int projection = 0; projection < 8; projection++)
                {
                    wrist = rightUpperArm.position + Vector3.ClampMagnitude(wrist - rightUpperArm.position, rightReach);
                    Vector3 centre = leftUpperArm.position - span;
                    wrist = centre + Vector3.ClampMagnitude(wrist - centre, leftReach);
                }
                Quaternion rotation = rightHand.rotation;
                SolveArm(rightUpperArm, rightLowerArm, rightHand, wrist, null, 0f);
                rightHand.rotation = rotation;
            }
        }
        Quaternion solvedUpper = rightUpperArm.rotation, solvedLower = rightLowerArm.rotation, solvedHand = rightHand.rotation;
        rightUpperArm.rotation = Quaternion.Slerp(initialUpper, solvedUpper, Mathf.Clamp01(blend));
        rightLowerArm.rotation = Quaternion.Slerp(initialLower, solvedLower, Mathf.Clamp01(blend));
        rightHand.rotation = Quaternion.Slerp(initialHand, solvedHand, Mathf.Clamp01(blend));
    }

    Quaternion AimCorrection(Vector3 forward, Vector3 direction, float weight)
    {
        Quaternion correction = Quaternion.FromToRotation(forward, direction);
        return Quaternion.Slerp(Quaternion.identity,
            Quaternion.RotateTowards(Quaternion.identity, correction, maxAimCorrection), Mathf.Clamp01(weight));
    }

    static void SolveArm(Transform upperArm, Transform lowerArm, Transform hand, Vector3 target, Transform hint, float hintWeight)
    {
        Vector3 root = upperArm.position;
        float upperLength = Vector3.Distance(root, lowerArm.position);
        float lowerLength = Vector3.Distance(lowerArm.position, hand.position);
        Vector3 delta = target - root;
        if (upperLength < 0.00001f || lowerLength < 0.00001f || delta.sqrMagnitude < 0.00000001f) return;
        float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.00001f,
            upperLength + lowerLength - 0.00001f);
        Vector3 direction = delta.normalized;
        Vector3 bend = Vector3.ProjectOnPlane(lowerArm.position - root, direction);
        if (hint != null)
            bend = Vector3.Lerp(bend, Vector3.ProjectOnPlane(hint.position - root, direction), Mathf.Clamp01(hintWeight));
        if (bend.sqrMagnitude < 0.00000001f) bend = Vector3.ProjectOnPlane(Vector3.down, direction);
        if (bend.sqrMagnitude < 0.00000001f) bend = Vector3.Cross(direction, Vector3.right);
        float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
        Vector3 elbow = root + direction * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        upperArm.rotation = Quaternion.FromToRotation(lowerArm.position - root, elbow - root) * upperArm.rotation;
        Vector3 reachable = root + direction * distance;
        lowerArm.rotation = Quaternion.FromToRotation(hand.position - lowerArm.position,
            reachable - lowerArm.position) * lowerArm.rotation;
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
