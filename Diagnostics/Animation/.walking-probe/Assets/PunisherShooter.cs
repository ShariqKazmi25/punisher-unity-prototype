using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class PunisherShooter : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Camera aimingCamera;
    public Transform muzzle;
    public ThirdPersonCameraController cameraController;
    public ParticleSystem muzzleParticles;
    public AudioSource shotAudio;
    public RifleWeaponAnimation weaponAnimation;

    [Header("Rifle")]
    [Tooltip("Hold left click to fire. Five rounds/sec matches the imported 0.2-second standing recoil cycle.")]
    [Min(0.1f)] public float roundsPerSecond = 5f;
    [Min(0f)] public float damage = 25f;
    [Min(1f)] public float range = 150f;
    [Min(0f)] public float impactImpulse = 0.5f;
    public LayerMask hitLayers = ~0;
    public bool showShotEffects = true;

    [Header("Animation")]
    public string weaponLayerName = "Weapon Actions";
    [Min(0.01f)] public float layerBlendTime = 0.1f;

    [Header("Crosshair")]
    public bool showCrosshair = true;
    [Min(1f)] public float crosshairArm = 4f;
    [Min(0f)] public float crosshairGap = 2f;
    [Min(1f)] public float crosshairThickness = 1f;

    public bool IsAiming { get; private set; }
    public bool IsFiring { get; private set; }
    public bool WeaponReady => IsAiming || IsFiring;
    public Vector3 AimPoint { get; private set; }
    public int WeaponLayerIndex { get; private set; } = -1;
    public bool IsEditorAimPreviewActive => handIK != null && handIK.IsEditorAimPreviewActive;
    public event Action<ShotInfo> ShotFired;

    public readonly struct ShotInfo
    {
        public readonly Vector3 origin, point, normal;
        public readonly Collider collider;
        public bool Hit => collider != null;
        public ShotInfo(Vector3 origin, Vector3 point, Vector3 normal, Collider collider)
        { this.origin = origin; this.point = point; this.normal = normal; this.collider = collider; }
    }

    static readonly int Aiming = Animator.StringToHash("isaiming");
    static readonly int Firing = Animator.StringToHash("isfiring");
    readonly RaycastHit[] hits = new RaycastHit[64];
    RuntimeAnimatorController validatedController;
    Animator validatedAnimator;
    bool hasAiming, hasFiring, fireHeld, waitForFireRelease = true;
    float nextShotTime, firingUntil, recoilDuration = 0.2f;
    Transform rightHand;
    RifleHandIK handIK;
    Material effectMaterial;

    void OnEnable()
    {
        ClearInput();
        nextShotTime = 0f;
        waitForFireRelease = true;
    }

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        handIK = GetComponentInChildren<RifleHandIK>();
        if (aimingCamera == null) aimingCamera = Camera.main;
        if (cameraController == null) cameraController = GetComponentInChildren<ThirdPersonCameraController>();
        if (animator != null && animator.isHuman) rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (muzzle == null)
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == "Muzzle") { muzzle = child; break; }
        ConfigureAnimator();
        ConfigureWeaponAnimation();
    }

    void ConfigureWeaponAnimation()
    {
        var settings = Resources.Load<RifleWeaponAnimationSettings>("Weapons/AR15Animation");
        if (weaponAnimation != null)
        {
            weaponAnimation.Configure(weaponAnimation.settings != null ? weaponAnimation.settings : settings);
            return;
        }
        if (settings == null) return;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "ar15") continue;
            weaponAnimation = child.GetComponent<RifleWeaponAnimation>();
            if (weaponAnimation == null) weaponAnimation = child.gameObject.AddComponent<RifleWeaponAnimation>();
            if (!weaponAnimation.Configure(settings))
                Debug.LogWarning("AR15 shooting animation could not bind to the rifle's Armature.003 hierarchy.", child);
            break;
        }
    }

    void ConfigureAnimator()
    {
        validatedAnimator = animator;
        validatedController = animator != null ? animator.runtimeAnimatorController : null;
        WeaponLayerIndex = -1;
        hasAiming = hasFiring = false;
        if (animator == null || validatedController == null) return;
        foreach (var parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Bool) continue;
            if (parameter.nameHash == Aiming) hasAiming = true;
            if (parameter.nameHash == Firing) hasFiring = true;
        }
        WeaponLayerIndex = animator.GetLayerIndex(weaponLayerName);
        foreach (var clip in validatedController.animationClips)
            if (clip.name == "Firing Rifle2") recoilDuration = clip.length;
        if (WeaponLayerIndex > 0) animator.SetLayerWeight(WeaponLayerIndex, 0f);
        if (!hasAiming || !hasFiring || WeaponLayerIndex <= 0)
            Debug.LogWarning("The Animator needs the 'Weapon Actions' layer and isaiming/isfiring Bool parameters. See Diagnostics/Shooting/ShooterSetup.md.", this);
    }

    void Update()
    {
        if (animator != validatedAnimator || (animator != null && animator.runtimeAnimatorController != validatedController))
            ConfigureAnimator();
        bool inputAvailable = Application.isFocused &&
            (cameraController == null || cameraController.GameplayInputAvailable);
        Mouse mouse = Mouse.current;
        ReadControls(IsEditorAimPreviewActive, inputAvailable && mouse != null,
            mouse != null && mouse.rightButton.isPressed,
            mouse != null && mouse.leftButton.isPressed, Time.time);
        WriteAnimationParameters();
        UpdateLayerWeight(Time.deltaTime);
    }

    void ReadControls(bool previewAim, bool inputAvailable, bool aimPressed, bool firePressed, float now)
    {
        if (previewAim)
        {
            waitForFireRelease = true;
            ClearInput();
            UpdateWeaponInput(true, false, now);
        }
        else if (!inputAvailable)
        {
            waitForFireRelease = true;
            ClearInput();
        }
        else
        {
            if (!firePressed) waitForFireRelease = false;
            UpdateWeaponInput(aimPressed, !waitForFireRelease && firePressed, now);
        }
    }

    void UpdateWeaponInput(bool aim, bool fire, float now)
    {
        IsAiming = aim;
        fireHeld = fire;
        IsFiring = fire || now < firingUntil;
    }

    void WriteAnimationParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        if (hasAiming) animator.SetBool(Aiming, WeaponReady);
        if (hasFiring) animator.SetBool(Firing, IsFiring);
    }

    void UpdateLayerWeight(float dt)
    {
        if (animator == null || WeaponLayerIndex <= 0) return;
        // Direct pose blends replace the removed raise/lower clips. Release must
        // fade back to locomotion immediately, without waiting for a lower state.
        float desired = WeaponReady ? 1f : 0f;
        animator.SetLayerWeight(WeaponLayerIndex, Mathf.MoveTowards(animator.GetLayerWeight(WeaponLayerIndex),
            desired, Mathf.Max(0f, dt) / Mathf.Max(0.01f, layerBlendTime)));
    }

    void LateUpdate()
    {
        if (aimingCamera == null) aimingCamera = Camera.main;
        if (aimingCamera == null) return;
        AimPoint = GetAimLookPoint();
        FireIfReady(Time.time);
    }

    bool FireIfReady(float now)
    {
        if (IsEditorAimPreviewActive || !fireHeld || aimingCamera == null || now + 0.00001f < nextShotTime) return false;
        nextShotTime = now + 1f / Mathf.Max(0.1f, roundsPerSecond);
        firingUntil = now + Mathf.Max(0.05f, recoilDuration);
        IsFiring = true;
        if (weaponAnimation != null) weaponAnimation.PlayShot(1f / Mathf.Max(0.1f, roundsPerSecond));
        Ray ray = aimingCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aim = ClosestHit(ray.origin, ray.direction, range, out var cameraHit)
            ? cameraHit.point : ray.origin + ray.direction * range;
        Vector3 origin = muzzle != null ? muzzle.position : rightHand != null
            ? rightHand.position : transform.position + Vector3.up * 0.5f;
        Vector3 toAim = aim - origin;
        RaycastHit shotHit = default;
        bool didHit = false;
        // A barrel poking through cover must not let the shot start beyond it.
        Vector3 chest = transform.position + Vector3.up * 0.45f;
        Vector3 toMuzzle = origin - chest;
        if (toMuzzle.sqrMagnitude > 0.0001f)
            didHit = ClosestHit(chest, toMuzzle.normalized, toMuzzle.magnitude, out shotHit);
        if (!didHit && toAim.sqrMagnitude > 0.0001f)
            didHit = ClosestHit(origin, toAim.normalized, toAim.magnitude + 0.01f, out shotHit);
        Vector3 point = didHit ? shotHit.point : aim;
        if (didHit)
        {
            shotHit.collider.GetComponentInParent<IRifleDamageable>()?.TakeDamage(damage);
            if (shotHit.rigidbody != null && shotHit.rigidbody.gameObject.activeInHierarchy && !shotHit.rigidbody.isKinematic)
                shotHit.rigidbody.AddForceAtPosition(toAim.normalized * impactImpulse, point, ForceMode.Impulse);
        }
        if (shotAudio != null) shotAudio.Play();
        if (muzzleParticles != null) muzzleParticles.Play();
        if (showShotEffects && Application.isPlaying) DrawShot(origin, point, didHit);
        ShotFired?.Invoke(new ShotInfo(origin, point, didHit ? shotHit.normal : Vector3.zero,
            didHit ? shotHit.collider : null));
        return true;
    }

    bool ClosestHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
    {
        closest = default;
        int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, hitLayers, QueryTriggerInteraction.Ignore);
        RaycastHit[] candidates = count == hits.Length
            ? Physics.RaycastAll(origin, direction, distance, hitLayers, QueryTriggerInteraction.Ignore) : hits;
        if (candidates != hits) count = candidates.Length;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var candidate = candidates[i];
            if (candidate.collider == null || candidate.collider.transform.IsChildOf(transform) || candidate.distance >= nearest) continue;
            nearest = candidate.distance; closest = candidate;
        }
        return nearest < float.PositiveInfinity;
    }

    public Vector3 GetAimLookPoint()
    {
        if (aimingCamera == null) return transform.position + transform.forward * range;
        // Use the same camera-centre surface point as the shot, including close
        // targets and Cinemachine's final shoulder offset/rotation.
        Ray ray = aimingCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return ClosestHit(ray.origin, ray.direction, range, out var hit)
            ? hit.point : ray.origin + ray.direction * range;
    }

    void DrawShot(Vector3 origin, Vector3 point, bool hit)
    {
        if (effectMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null) return;
            effectMaterial = new Material(shader) { color = new Color(1f, 0.85f, 0.4f) };
        }
        var tracer = new GameObject("Rifle tracer");
        var line = tracer.AddComponent<LineRenderer>();
        line.sharedMaterial = effectMaterial;
        line.positionCount = 2; line.useWorldSpace = true;
        line.SetPosition(0, origin); line.SetPosition(1, point);
        line.startWidth = 0.003f; line.endWidth = 0.001f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(tracer, 0.06f);
        var flash = new GameObject("Rifle flash");
        flash.transform.position = origin;
        var light = flash.AddComponent<Light>();
        light.color = new Color(1f, 0.8f, 0.4f); light.range = 0.6f; light.intensity = 1.5f;
        Destroy(flash, 0.035f);
        if (hit)
        {
            var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "Rifle impact";
            spark.transform.position = point;
            spark.transform.localScale = Vector3.one * 0.012f;
            var collider = spark.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            spark.GetComponent<Renderer>().sharedMaterial = effectMaterial;
            Destroy(spark, 0.08f);
        }
    }

    void OnGUI()
    {
        if (!showCrosshair || !Application.isFocused ||
            (cameraController != null && !cameraController.GameplayInputAvailable)) return;
        Color previous = GUI.color; GUI.color = Color.white;
        float x = Screen.width * 0.5f, y = Screen.height * 0.5f;
        float t = crosshairThickness, g = crosshairGap, a = crosshairArm;
        GUI.DrawTexture(new Rect(x - g - a, y - t * 0.5f, a, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + g, y - t * 0.5f, a, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - t * 0.5f, y - g - a, t, a), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - t * 0.5f, y + g, t, a), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    void ClearInput()
    {
        IsAiming = IsFiring = fireHeld = false;
        firingUntil = 0f;
    }

    void OnDisable()
    {
        ClearInput(); waitForFireRelease = true;
        if (weaponAnimation != null) weaponAnimation.StopShot();
        WriteAnimationParameters();
        if (animator != null && WeaponLayerIndex > 0) animator.SetLayerWeight(WeaponLayerIndex, 0f);
    }

    void OnDestroy() { if (effectMaterial != null) Destroy(effectMaterial); }
}
