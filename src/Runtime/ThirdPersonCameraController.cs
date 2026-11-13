using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

[DefaultExecutionOrder(-200)]
public class ThirdPersonCameraController : MonoBehaviour
{
    public InputActionReference lookAction;

    public float sensitivity = 0.1f;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    [Header("Shooter Facing")]
    public Transform characterRoot;
    public bool turnCharacterWithCamera = true;
    [Min(0f)] public float turnSharpness = 20f;
    public bool captureCursorOnEnable = true;

    [Header("Shoulder Aim")]
    public CinemachineCamera virtualCamera;
    public CinemachineThirdPersonFollow shoulderFollow;
    [Range(0.2f, 1f)] public float aimDistanceMultiplier = 0.6f;
    [Range(10f, 100f)] public float aimFieldOfView = 45f;
    [Min(0.01f)] public float aimNearClipPlane = 0.05f;
    [Range(0.1f, 1f)] public float aimSensitivityMultiplier = 0.7f;
    [Min(0.01f)] public float aimBlendTime = 0.18f;

    float yaw, pitch, aimBlend, defaultDistance, defaultFieldOfView, defaultNearClipPlane;
    bool enabledLookAction, capturedThisFrame, hasLensDefaults, previewCursorReleased;
    RifleHandIK aimPreviewRig;
    bool EditorAimPreviewActive => aimPreviewRig != null && aimPreviewRig.IsEditorAimPreviewActive;
    bool HasGameplayFocus
    {
        get
        {
            if (!Application.isFocused) return false;
#if UNITY_EDITOR
            var window = UnityEditor.EditorWindow.focusedWindow;
            return window != null && window.GetType().Name == "GameView";
#else
            return true;
#endif
        }
    }
    public bool GameplayInputAvailable => !EditorAimPreviewActive && HasGameplayFocus && Cursor.lockState == CursorLockMode.Locked && !capturedThisFrame;
    public Quaternion LookRotation => Quaternion.Euler(pitch, yaw, 0f);
    public Vector3 FlatForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

    void OnEnable()
    {
        previewCursorReleased = false;
        var angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, angles.x), minPitch, maxPitch);
        if (characterRoot == null)
        {
            var movement = GetComponentInParent<PunisherMovement>();
            if (movement != null) characterRoot = movement.transform;
        }
        if (characterRoot != null) aimPreviewRig = characterRoot.GetComponentInChildren<RifleHandIK>();
        enabledLookAction = lookAction != null && !lookAction.action.enabled;
        if (enabledLookAction) lookAction.action.Enable();
        if (virtualCamera == null) virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        if (shoulderFollow == null && virtualCamera != null) shoulderFollow = virtualCamera.GetComponent<CinemachineThirdPersonFollow>();
        hasLensDefaults = virtualCamera != null && shoulderFollow != null;
        if (hasLensDefaults)
        {
            defaultDistance = shoulderFollow.CameraDistance;
            defaultFieldOfView = virtualCamera.Lens.FieldOfView;
            defaultNearClipPlane = virtualCamera.Lens.NearClipPlane;
        }
        if (captureCursorOnEnable && HasGameplayFocus && !EditorAimPreviewActive) CaptureCursor();
    }

    void OnDisable()
    {
        if (enabledLookAction && lookAction != null) lookAction.action.Disable();
        if (hasLensDefaults)
        {
            shoulderFollow.CameraDistance = defaultDistance;
            var lens = virtualCamera.Lens;
            lens.FieldOfView = defaultFieldOfView; lens.NearClipPlane = defaultNearClipPlane;
            virtualCamera.Lens = lens;
        }
        aimBlend = 0f;
        ReleaseCursor();
    }

    void Update()
    {
        capturedThisFrame = false;
        if (EditorAimPreviewActive)
        {
            if (!previewCursorReleased) { ReleaseCursor(); previewCursorReleased = true; }
            ApplyLook(Vector2.zero, true, false, Time.deltaTime);
            UpdateAimLens(true, Time.deltaTime);
            return;
        }
        previewCursorReleased = false;
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (!HasGameplayFocus) { ReleaseCursor(); UpdateAimLens(false, Time.deltaTime); return; }
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
        else if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            CaptureCursor(); capturedThisFrame = true;
        }
        bool aiming = GameplayInputAvailable && mouse != null && mouse.rightButton.isPressed;
        Vector2 look = Vector2.zero;
        if (GameplayInputAvailable)
            look = lookAction != null ? lookAction.action.ReadValue<Vector2>() : mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
        ApplyLook(look, aiming, GameplayInputAvailable, Time.deltaTime);
        UpdateAimLens(aiming, Time.deltaTime);
    }

    void ApplyLook(Vector2 look, bool aiming, bool faceCharacter, float dt)
    {
        float scale = sensitivity * (aiming ? aimSensitivityMultiplier : 1f);
        // Mouse delta is already per-frame; do not multiply it by deltaTime.
        yaw = Mathf.Repeat(yaw + look.x * scale, 360f);
        pitch = Mathf.Clamp(pitch - look.y * scale, minPitch, maxPitch);
        if (turnCharacterWithCamera && characterRoot != null && faceCharacter)
            characterRoot.rotation = Quaternion.Slerp(characterRoot.rotation, Quaternion.Euler(0f, yaw, 0f),
                1f - Mathf.Exp(-Mathf.Max(0f, turnSharpness) * dt));
        // World rotation prevents yaw accumulating as the parent turns.
        transform.rotation = LookRotation;
    }

    void UpdateAimLens(bool aiming, float dt)
    {
        aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, Mathf.Max(0f, dt) / Mathf.Max(0.01f, aimBlendTime));
        if (!hasLensDefaults) return;
        float eased = Mathf.SmoothStep(0f, 1f, aimBlend);
        shoulderFollow.CameraDistance = Mathf.Lerp(defaultDistance, defaultDistance * aimDistanceMultiplier, eased);
        var lens = virtualCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(defaultFieldOfView, aimFieldOfView, eased);
        lens.NearClipPlane = Mathf.Lerp(defaultNearClipPlane, Mathf.Min(defaultNearClipPlane, aimNearClipPlane), eased);
        virtualCamera.Lens = lens;
    }

    public void CaptureCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    public void ReleaseCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    void OnApplicationFocus(bool focus) { if (!focus) ReleaseCursor(); }
}
