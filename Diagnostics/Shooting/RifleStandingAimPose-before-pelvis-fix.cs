using System.Collections.Generic;
using UnityEngine;

// A transform-only reference rig samples the existing standing animation graph.
// It has no renderers, colliders, weapon, gameplay scripts or automatic updates.
// Compensating at Spine leaves the walking hips and both leg chains untouched.
[AddComponentMenu("")]
public sealed class RifleStandingAimPose : MonoBehaviour
{
    RifleHandIK owner;
    Animator source, reference;
    Transform spine, referenceSpine;
    readonly List<Transform> upper = new List<Transform>();
    readonly List<Transform> referenceUpper = new List<Transform>();
    int weaponLayer;
    Vector3 aimPoint;
    float aimBlend;
    AnimatorControllerParameter[] parameters;
    Transform sourceRightHand, referenceRightHand;
    Vector3 gripPosition;
    Quaternion gripRotation;

    public static RifleStandingAimPose Create(RifleHandIK owner, Animator source)
    {
        Transform spine = source.GetBoneTransform(HumanBodyBones.Spine);
        if (spine == null || source.runtimeAnimatorController == null) return null;
        var root = new GameObject("Standing aim reference");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.SetParent(source.transform, false);
        var pose = root.AddComponent<RifleStandingAimPose>();
        pose.owner = owner;
        pose.source = source;
        pose.parameters = source.parameters;
        pose.spine = spine;
        var required = new HashSet<Transform>();
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            for (Transform bone = source.GetBoneTransform((HumanBodyBones)i);
                bone != null && bone != source.transform; bone = bone.parent)
                required.Add(bone);
        pose.CloneBones(source.transform, root.transform, required);
        pose.reference = root.AddComponent<Animator>();
        pose.reference.avatar = source.avatar;
        pose.reference.runtimeAnimatorController = source.runtimeAnimatorController;
        pose.reference.applyRootMotion = false;
        pose.reference.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        pose.reference.enabled = false;
        pose.reference.Rebind();
        pose.reference.Update(0f);
        pose.sourceRightHand = source.GetBoneTransform(HumanBodyBones.RightHand);
        pose.referenceRightHand = pose.reference.GetBoneTransform(HumanBodyBones.RightHand);
        return pose;
    }

    void CloneBones(Transform original, Transform parent, HashSet<Transform> required)
    {
        foreach (Transform child in original)
        {
            if (!required.Contains(child)) continue;
            var copy = new GameObject(child.name).transform;
            copy.gameObject.hideFlags = HideFlags.HideAndDontSave;
            copy.SetParent(parent, false);
            copy.localPosition = child.localPosition;
            copy.localRotation = child.localRotation;
            copy.localScale = child.localScale;
            if (child == spine) referenceSpine = copy;
            else if (child.IsChildOf(spine)) { upper.Add(child); referenceUpper.Add(copy); }
            CloneBones(child, copy, required);
        }
    }

    public void Apply(float weight, int layer, Vector3 target, float deltaTime)
    {
        if (reference == null || referenceSpine == null) return;
        var current = source.GetCurrentAnimatorStateInfo(0);
        float moving = current.IsName("Rifle Idle") ? 0f : 1f;
        if (source.IsInTransition(0))
        {
            float nextMoving = source.GetNextAnimatorStateInfo(0).IsName("Rifle Idle") ? 0f : 1f;
            moving = Mathf.Lerp(moving, nextMoving, Mathf.Clamp01(source.GetAnimatorTransitionInfo(0).normalizedTime));
        }
        weight = Mathf.Clamp01(weight * moving);
        if (reference.runtimeAnimatorController != source.runtimeAnimatorController)
        {
            reference.runtimeAnimatorController = source.runtimeAnimatorController;
            parameters = source.parameters;
        }
        weaponLayer = layer;
        aimPoint = target;
        aimBlend = source.GetLayerWeight(layer);
        if (owner.leftHandGrip != null && sourceRightHand != null)
        {
            gripPosition = sourceRightHand.InverseTransformPoint(owner.leftHandGrip.position);
            gripRotation = Quaternion.Inverse(sourceRightHand.rotation) * owner.leftHandGrip.rotation;
        }
        foreach (var parameter in parameters)
        {
            int id = parameter.nameHash;
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Bool:
                    reference.SetBool(id, parameter.name != "iswalking" && parameter.name != "isrunning" && source.GetBool(id));
                    break;
                case AnimatorControllerParameterType.Float: reference.SetFloat(id, source.GetFloat(id)); break;
                case AnimatorControllerParameterType.Int: reference.SetInteger(id, source.GetInteger(id)); break;
            }
        }
        if (current.IsName("Rifle Idle") && !source.IsInTransition(0))
            reference.Play(current.fullPathHash, 0, current.normalizedTime);
        else
            reference.Update(Mathf.Max(0f, deltaTime) * source.speed);
        if (weight <= 0f) { reference.Update(0f); return; }
        // Use the SAME upper state phases and transition progress as the visible
        // actor, including interrupted aim/fire blends. Only the base stays idle.
        for (int i = 1; i < source.layerCount; i++)
        {
            reference.SetLayerWeight(i, source.GetLayerWeight(i));
            var state = source.GetCurrentAnimatorStateInfo(i);
            reference.Play(state.fullPathHash, i, state.normalizedTime);
        }
        reference.Update(0f);
        for (int i = 1; i < source.layerCount; i++)
        {
            if (!source.IsInTransition(i)) continue;
            var next = source.GetNextAnimatorStateInfo(i);
            var transition = source.GetAnimatorTransitionInfo(i);
            reference.CrossFade(next.fullPathHash, transition.duration, i, next.normalizedTime, transition.normalizedTime);
        }
        reference.Update(0f);
        if (weight <= 0f) return;
        spine.SetPositionAndRotation(Vector3.Lerp(spine.position, referenceSpine.position, weight),
            Quaternion.Slerp(spine.rotation, referenceSpine.rotation, weight));
        for (int i = 0; i < upper.Count; i++)
        {
            upper[i].localPosition = Vector3.Lerp(upper[i].localPosition, referenceUpper[i].localPosition, weight);
            upper[i].localRotation = Quaternion.Slerp(upper[i].localRotation, referenceUpper[i].localRotation, weight);
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (reference == null || owner == null || layerIndex != weaponLayer) return;
        float weight = owner.followCameraAim ? aimBlend : 0f;
        reference.SetLookAtWeight(weight, owner.aimBodyWeight, owner.aimHeadWeight, 0f, .5f);
        if (weight > 0f) reference.SetLookAtPosition(aimPoint);
        bool gripAssigned = owner.leftHandGrip != null && referenceRightHand != null;
        reference.SetIKPositionWeight(AvatarIKGoal.LeftHand, gripAssigned ? Mathf.Clamp01(owner.positionWeight) : 0f);
        reference.SetIKRotationWeight(AvatarIKGoal.LeftHand, gripAssigned ? Mathf.Clamp01(owner.rotationWeight) : 0f);
        if (gripAssigned)
        {
            reference.SetIKPosition(AvatarIKGoal.LeftHand, referenceRightHand.TransformPoint(gripPosition));
            reference.SetIKRotation(AvatarIKGoal.LeftHand, referenceRightHand.rotation * gripRotation);
        }
        bool hintAssigned = gripAssigned && owner.leftElbowHint != null;
        reference.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,
            hintAssigned ? Mathf.Clamp01(owner.elbowWeight) * Mathf.Clamp01(owner.positionWeight) : 0f);
        if (hintAssigned) reference.SetIKHintPosition(AvatarIKHint.LeftElbow, owner.leftElbowHint.position);
    }

    public void Dispose()
    {
        if (reference != null) reference.enabled = false;
        if (Application.isPlaying) Destroy(gameObject);
        else DestroyImmediate(gameObject);
    }
}
