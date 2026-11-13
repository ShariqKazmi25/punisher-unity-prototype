using UnityEngine;

// Attach to the character model that owns the Humanoid Animator.
[DefaultExecutionOrder(150)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class RifleHandIK : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Editor Aim Preview")]
    [Tooltip("While Play Mode is running, hold aim and free the mouse so Scene view/Inspector tuning updates live. Turn off to resume normal controls.")]
    public bool previewAimInSceneView;
#endif
    public bool IsEditorAimPreviewActive
    {
        get
        {
#if UNITY_EDITOR
            return Application.isPlaying && isActiveAndEnabled && previewAimInSceneView;
#else
            return false;
#endif
        }
    }

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

    [Header("Moving Aim Posture")]
    [Tooltip("Steady the pelvis and torso together using the standing aim/fire pose. Leg IK preserves the animated footfalls and weapon fitting.")]
    public bool stabilizeMovingAim = true;
    [Range(0f, 1f)] public float movingAimStability = 1f;
    [Tooltip("Remove the walking pelvis sway beneath the standing torso. Foot positions are retained with leg IK.")]
    [Range(0f, 1f)] public float movingPelvisStability = 1f;

    [Header("Shouldered Aim Pose")]
    public bool shoulderStockWhileAiming = true;
    [Tooltip("Optional stock contact target, preferably under the chest/clavicle, outside either arm chain.")]
    public Transform shoulderAnchor;
    [Tooltip("Fallback from the right shoulder joint, in character axes: X right, Y up, Z forward.")]
    public Vector3 shoulderOffset = new Vector3(-0.015f, -0.025f, 0.055f);
    [Tooltip("Optional point at the centre of the rifle's butt pad.")]
    public Transform stockContact;
    [Tooltip("Measured butt-pad centre relative to this AR15 root, before its runtime reparenting.")]
    public Vector3 ar15StockLocalPosition = new Vector3(0f, 0.44194f, 5.51587f);
    [Tooltip("Slide the aiming wrist back along the handguard (world units). Idle grip is unchanged.")]
    [Min(0f)] public float supportHandSlideBack = 0.04f;

    [Header("Aiming Elbows")]
    public bool guideAimingElbows = true;
    [Tooltip("Optional aiming-only left elbow pole. The older Left Elbow Hint also affects idle IK.")]
    public Transform leftAimElbowHint;
    [Tooltip("Optional right elbow pole, outside the arm chain. Left elbow uses Left Elbow Hint above.")]
    public Transform rightElbowHint;
    [Tooltip("Fallback left elbow pole from its shoulder, in character axes.")]
    public Vector3 leftAimElbowOffset = new Vector3(-0.04f, -0.14f, 0.10f);
    [Tooltip("Fallback right elbow pole from its shoulder, in character axes.")]
    public Vector3 rightAimElbowOffset = new Vector3(0.08f, -0.16f, 0.02f);
    [Range(0f, 1f)] public float aimingElbowHintWeight = 1f;
    [Tooltip("Bend is measured from straight: 0 means locked out, 90 means a right angle.")]
    [Range(0f, 90f)] public float minimumLeftElbowBend = 35f;
    [Range(30f, 160f)] public float maximumLeftElbowBend = 135f;
    [Range(0f, 90f)] public float minimumRightElbowBend = 30f;
    [Range(30f, 160f)] public float maximumRightElbowBend = 90f;

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
    private RifleStandingAimPose standingAimPose;

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
        UpdateRig(Time.deltaTime);
    }

    private void UpdateRig(float deltaTime)
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
        if (stockContact == null && shoulderStockWhileAiming && weaponTransform != null && weaponTransform.name == "ar15")
        {
            stockContact = new GameObject("StockContact").transform;
            stockContact.SetParent(weaponTransform, false);
            stockContact.localPosition = ar15StockLocalPosition;
        }
        if (calibratedGrip != leftHandGrip) { calibratedGrip = leftHandGrip; hasIdleGrip = false; }
        float blend = AimBlend;
        if (stabilizeMovingAim && movingAimStability > 0f && shooter.WeaponLayerIndex > 0)
        {
            if (standingAimPose == null)
                standingAimPose = RifleStandingAimPose.Create(this, characterAnimator);
            if (standingAimPose != null)
                standingAimPose.Apply(blend * movingAimStability, shooter.WeaponLayerIndex, shooter.GetAimLookPoint(), deltaTime);
        }
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
        Vector3 target = SupportGripPosition();
        Quaternion rotation = leftHandGrip.rotation * (useIdle ? idleGripRotation : Quaternion.identity);
        Vector3 handPosition = Vector3.Lerp(leftHand.position, target, blend * aimingPositionWeight);
        Quaternion handRotation = Quaternion.Slerp(leftHand.rotation, rotation, blend * aimingRotationWeight);
        Vector3 leftPole = ElbowPole(false);
        SolveArm(leftUpperArm, leftLowerArm, leftHand, handPosition, leftPole,
            guideAimingElbows ? blend * aimingElbowHintWeight : leftElbowHint != null ? elbowWeight : 0f);
        leftHand.rotation = handRotation;
        if (preserveIdleFingerPose && hasIdleGrip)
            for (int i = 0; i < fingers.Length; i++)
                fingers[i].localRotation = Quaternion.Slerp(fingers[i].localRotation, idleFingerRotations[i], blend);
    }

    float AimBlend => shooter.WeaponLayerIndex > 0
        ? characterAnimator.GetLayerWeight(shooter.WeaponLayerIndex) : shooter.WeaponReady ? 1f : 0f;

    private void OnDisable()
    {
        if (standingAimPose != null) standingAimPose.Dispose();
        standingAimPose = null;
    }

    void AlignBarrel(Vector3 aimPoint, float blend)
    {
        if (weaponTransform == null || rightUpperArm == null || rightLowerArm == null || rightHand == null || localBarrelDirection.sqrMagnitude < 0.0001f) return;
        Quaternion initialUpper = rightUpperArm.rotation, initialLower = rightLowerArm.rotation, initialHand = rightHand.rotation;
        Transform tip = barrelTip != null ? barrelTip : shooter.muzzle;
        // Aim the rigid rifle from the trigger hand, then choose a wrist position
        // inside BOTH arm reach spheres. Moving only the shoulder can push the
        // support grip outside the left arm's reach, even with full IK weight.
        for (int i = 0; i < 24; i++)
        {
            Vector3 previousWrist = rightHand.position;
            Vector3 origin = tip != null ? tip.position : weaponTransform.position;
            Vector3 direction = aimPoint - origin;
            if (direction.sqrMagnitude < 0.0001f) break;
            Vector3 forward = tip != null ? tip.forward : weaponTransform.TransformDirection(localBarrelDirection);
            Quaternion correction = Quaternion.FromToRotation(forward, direction);
            rightHand.rotation = Quaternion.RotateTowards(initialHand, correction * rightHand.rotation, maxAimCorrection);
            if (leftHandGrip != null && leftUpperArm != null && leftLowerArm != null && leftHand != null)
            {
                Vector3 grip = SupportGripPosition();
                Vector3 span = grip - rightHand.position;
                ArmReach(rightUpperArm, rightLowerArm, rightHand, minimumRightElbowBend, maximumRightElbowBend, out float rightMinimum, out float rightReach);
                ArmReach(leftUpperArm, leftLowerArm, leftHand, minimumLeftElbowBend, maximumLeftElbowBend, out float leftMinimum, out float leftReach);
                Vector3 wrist = rightHand.position;
                if (shoulderStockWhileAiming && stockContact != null) wrist += ShoulderPosition() - stockContact.position;
                for (int projection = 0; projection < 8; projection++)
                {
                    wrist = ProjectReach(wrist, rightUpperArm.position, rightMinimum, rightReach);
                    Vector3 centre = leftUpperArm.position - span;
                    wrist = ProjectReach(wrist, centre, leftMinimum, leftReach);
                }
                Quaternion rotation = rightHand.rotation;
                SolveArm(rightUpperArm, rightLowerArm, rightHand, wrist, ElbowPole(true),
                    guideAimingElbows ? aimingElbowHintWeight : rightElbowHint != null ? elbowWeight : 0f);
                rightHand.rotation = rotation;
            }
            Vector3 finalOrigin = tip != null ? tip.position : weaponTransform.position;
            Vector3 finalForward = (tip != null ? tip.forward : weaponTransform.TransformDirection(localBarrelDirection)).normalized;
            Vector3 finalDirection = (aimPoint - finalOrigin).normalized;
            if ((rightHand.position - previousWrist).sqrMagnitude < .0000000001f &&
                Vector3.Dot(finalForward, finalDirection) > 0f && Vector3.Cross(finalForward, finalDirection).sqrMagnitude < .00000001f) break;
        }
        Quaternion solvedUpper = rightUpperArm.rotation, solvedLower = rightLowerArm.rotation, solvedHand = rightHand.rotation;
        rightUpperArm.rotation = Quaternion.Slerp(initialUpper, solvedUpper, Mathf.Clamp01(blend));
        rightLowerArm.rotation = Quaternion.Slerp(initialLower, solvedLower, Mathf.Clamp01(blend));
        rightHand.rotation = Quaternion.Slerp(initialHand, solvedHand, Mathf.Clamp01(blend));
    }

    Vector3 SupportGripPosition()
    {
        Vector3 position = preserveIdleGrip && hasIdleGrip ? leftHandGrip.TransformPoint(idleGripPosition) : leftHandGrip.position;
        Vector3 forward = barrelTip != null ? barrelTip.forward : weaponTransform != null
            ? weaponTransform.TransformDirection(localBarrelDirection).normalized : transform.forward;
        return position - forward * Mathf.Max(0f, supportHandSlideBack);
    }

    Vector3 ShoulderPosition() => shoulderAnchor != null ? shoulderAnchor.position
        : rightUpperArm.position + transform.TransformVector(shoulderOffset);

    Vector3 ElbowPole(bool right)
    {
        Transform hint = right ? rightElbowHint : leftAimElbowHint != null ? leftAimElbowHint : leftElbowHint;
        if (hint != null) return hint.position;
        Transform shoulder = right ? rightUpperArm : leftUpperArm;
        return shoulder.position + transform.TransformVector(right ? rightAimElbowOffset : leftAimElbowOffset);
    }

    static void ArmReach(Transform upper, Transform lower, Transform hand, float minimumBend, float maximumBend, out float minimum, out float maximum)
    {
        float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, hand.position);
        float nearAngle = Mathf.Clamp(maximumBend, Mathf.Clamp(minimumBend, 0f, 160f), 160f) * Mathf.Deg2Rad;
        float farAngle = Mathf.Clamp(minimumBend, 0f, 160f) * Mathf.Deg2Rad;
        minimum = Mathf.Sqrt(Mathf.Max(0f, a * a + b * b + 2f * a * b * Mathf.Cos(nearAngle)));
        maximum = Mathf.Sqrt(Mathf.Max(0f, a * a + b * b + 2f * a * b * Mathf.Cos(farAngle))) - .00001f;
        minimum = Mathf.Min(minimum, maximum);
    }

    static Vector3 ProjectReach(Vector3 position, Vector3 centre, float minimum, float maximum)
    {
        Vector3 delta = position - centre;
        float distance = delta.magnitude;
        if (distance < .000001f) return centre + Vector3.forward * minimum;
        return centre + delta * (Mathf.Clamp(distance, minimum, maximum) / distance);
    }

    [ContextMenu("Create Aim Pose Targets")]
    void CreateAimPoseTargets()
    {
        var a = GetComponent<Animator>();
        if (!gameObject.scene.IsValid() || a == null || !a.isHuman) return;
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RecordObject(this, "Create rifle aim targets");
#endif
        Transform parent = a.GetBoneTransform(HumanBodyBones.UpperChest);
        if (parent == null) parent = a.GetBoneTransform(HumanBodyBones.Chest);
        if (parent == null) parent = transform;
        Transform rightShoulder = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform leftShoulder = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (rightShoulder == null || leftShoulder == null) return;
        if (shoulderAnchor == null) shoulderAnchor = CreatePoseTarget("StockShoulderTarget", parent,
            rightShoulder.position + transform.TransformVector(shoulderOffset));
        if (leftAimElbowHint == null) leftAimElbowHint = CreatePoseTarget("LeftAimElbowTarget", parent,
            leftShoulder.position + transform.TransformVector(leftAimElbowOffset));
        if (rightElbowHint == null) rightElbowHint = CreatePoseTarget("RightAimElbowTarget", parent,
            rightShoulder.position + transform.TransformVector(rightAimElbowOffset));
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    static Transform CreatePoseTarget(string name, Transform parent, Vector3 position)
    {
        var target = new GameObject(name);
#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(target, "Create rifle aim target");
#endif
        target.transform.SetParent(parent, false); target.transform.position = position;
        return target.transform;
    }

    Quaternion AimCorrection(Vector3 forward, Vector3 direction, float weight)
    {
        Quaternion correction = Quaternion.FromToRotation(forward, direction);
        return Quaternion.Slerp(Quaternion.identity,
            Quaternion.RotateTowards(Quaternion.identity, correction, maxAimCorrection), Mathf.Clamp01(weight));
    }

    static void SolveArm(Transform upperArm, Transform lowerArm, Transform hand, Vector3 target, Vector3 hintPosition, float hintWeight)
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
        if (hintWeight > 0f)
            bend = Vector3.Lerp(bend.normalized, Vector3.ProjectOnPlane(hintPosition - root, direction).normalized, Mathf.Clamp01(hintWeight));
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
        var a = GetComponent<Animator>();
        if (a == null || !a.isHuman) return;
        Transform right = rightUpperArm != null ? rightUpperArm : a.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform left = leftUpperArm != null ? leftUpperArm : a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (right == null || left == null) return;
        Vector3 shoulderPoint = shoulderAnchor != null ? shoulderAnchor.position : right.position + transform.TransformVector(shoulderOffset);
        Gizmos.color = new Color(1f, .6f, 0f);
        Gizmos.DrawWireSphere(shoulderPoint, .012f);
        if (stockContact != null) Gizmos.DrawLine(stockContact.position, shoulderPoint);
        Gizmos.color = Color.magenta;
        Vector3 leftPole = leftAimElbowHint != null ? leftAimElbowHint.position : leftElbowHint != null
            ? leftElbowHint.position : left.position + transform.TransformVector(leftAimElbowOffset);
        Gizmos.DrawWireSphere(leftPole, .012f); Gizmos.DrawLine(left.position, leftPole);
        Gizmos.color = Color.green;
        Vector3 rightPole = rightElbowHint != null ? rightElbowHint.position : right.position + transform.TransformVector(rightAimElbowOffset);
        Gizmos.DrawWireSphere(rightPole, .012f); Gizmos.DrawLine(right.position, rightPole);
    }
}
