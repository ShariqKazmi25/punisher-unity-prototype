using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// Evaluate the gun before the character's final hand/aim solve (order 150).
[DefaultExecutionOrder(125)]
[ExecuteAlways, DisallowMultipleComponent]
public class RifleWeaponAnimation : MonoBehaviour
{
    public RifleWeaponAnimationSettings settings;
    public bool IsPlaying { get; private set; }
    public float NormalizedTime => duration > 0f ? Mathf.Clamp01(elapsed / duration) : 0f;

    PlayableGraph graph;
    AnimationClipPlayable clipPlayable;
    Transform body;
    Transform[] bones;
    Vector3[] restPositions, restScales;
    Quaternion[] restRotations;
    Animator weaponAnimator;
    bool addedAnimator;
    float elapsed, duration;

    public bool Configure(RifleWeaponAnimationSettings source)
    {
        if (graph.IsValid()) return settings == source;
        settings = source;
        if (source == null || source.shootingClip == null || source.shootingClip.legacy) return false;
        body = transform.Find(source.bodyBonePath);
        if (body == null) return false;
        bones = body.parent.GetComponentsInChildren<Transform>(true);
        restPositions = new Vector3[bones.Length]; restScales = new Vector3[bones.Length];
        restRotations = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            restPositions[i] = bones[i].localPosition; restRotations[i] = bones[i].localRotation;
            restScales[i] = bones[i].localScale;
        }
        weaponAnimator = GetComponent<Animator>();
        if (weaponAnimator == null) { weaponAnimator = gameObject.AddComponent<Animator>(); addedAnimator = true; }
        weaponAnimator.applyRootMotion = false;
        weaponAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        graph = PlayableGraph.Create("AR15 shooting");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        clipPlayable = AnimationClipPlayable.Create(graph, source.shootingClip);
        clipPlayable.SetApplyFootIK(false); clipPlayable.SetApplyPlayableIK(false); clipPlayable.SetSpeed(0d);
        var output = AnimationPlayableOutput.Create(graph, "AR15", weaponAnimator);
        output.SetSourcePlayable(clipPlayable);
        graph.Play();
        RestorePose();
        BindMarkers();
        return true;
    }

    public void PlayShot(float shotInterval)
    {
        if (!enabled || (!graph.IsValid() && !Configure(settings))) return;
        // Complete a whole cycle before the next round, without slowing sparse shots.
        duration = Mathf.Min(settings.shootingClip.length, Mathf.Max(0.01f, shotInterval));
        elapsed = 0f; IsPlaying = true;
        clipPlayable.SetTime(0d); clipPlayable.SetDone(false); graph.Evaluate(0f);
    }

    void LateUpdate()
    {
        BindMarkers();
        Advance(Time.deltaTime);
    }

    void Advance(float dt)
    {
        if (!IsPlaying || !graph.IsValid()) return;
        elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0f, dt));
        if (elapsed >= duration) { StopShot(); return; }
        clipPlayable.SetTime(NormalizedTime * settings.shootingClip.length);
        graph.Evaluate(0f);
    }

    void BindMarkers()
    {
        if (body == null) return;
        var ik = GetComponentInParent<RifleHandIK>();
        if (ik != null && ik.leftHandGrip != null && ik.leftHandGrip.IsChildOf(transform))
        {
            // Set this before reparenting; the anatomical barrel axis belongs to
            // ar15, not to the differently oriented internal Bone transform.
            if (ik.weaponTransform == null) ik.weaponTransform = transform;
            AttachMarker(ik.leftHandGrip);
            if (ik.stockContact != null) AttachMarker(ik.stockContact);
        }
        var shooter = GetComponentInParent<PunisherShooter>();
        if (shooter != null && shooter.muzzle != null && shooter.muzzle.IsChildOf(transform)) AttachMarker(shooter.muzzle);
    }

    void AttachMarker(Transform marker)
    {
        if (marker != body && marker.parent == transform) marker.SetParent(body, true);
    }

    public void StopShot()
    {
        IsPlaying = false;
        RestorePose();
    }

    void RestorePose()
    {
        if (bones == null) return;
        for (int i = 0; i < bones.Length; i++)
            if (bones[i] != null)
            {
                bones[i].localPosition = restPositions[i]; bones[i].localRotation = restRotations[i];
                bones[i].localScale = restScales[i];
            }
    }

    void OnDisable() { StopShot(); }
    void OnDestroy()
    {
        StopShot();
        if (graph.IsValid()) graph.Destroy();
        if (addedAnimator && weaponAnimator != null)
        {
            if (Application.isPlaying) Destroy(weaponAnimator);
            else DestroyImmediate(weaponAnimator);
        }
    }
}
