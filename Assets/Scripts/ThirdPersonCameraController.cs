using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    public InputActionReference lookAction;

    public float sensitivity = 0.1f;
    public float minPitch = -30f;
    public float maxPitch = 70f;

    private float yaw;
    private float pitch;

    void OnEnable()
    {
        lookAction.action.Enable();
    }

    void OnDisable()
    {
        lookAction.action.Disable();
    }

    void Update()
    {
        Vector2 look = lookAction.action.ReadValue<Vector2>();

        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}