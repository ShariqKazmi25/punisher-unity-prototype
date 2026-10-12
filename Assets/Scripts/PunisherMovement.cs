using UnityEngine;
using UnityEngine.InputSystem;

public class PunisherMovement : MonoBehaviour
{
    public CharacterController controller;
    public Animator animator;
    public Transform cameraTransform;

    [Header("Movement")]
    public float walkSpeed = 1.5f;
    public float acceleration = 6f;
    public float deceleration = 4f;
    public float rotationSpeed = 10f;

    [Header("Gravity")]
    public float gravity = -9.81f;
    public float groundedGravity = -2f;

    private float currentSpeed;
    private float verticalVelocity;

    private Vector3 moveDirection;

    void Update()
    {
        bool walkingInput = Keyboard.current.wKey.isPressed;

        animator.SetBool("iswalking", walkingInput);

        // When W is pressed, update movement direction
        if (walkingInput)
        {
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward.Normalize();

            moveDirection = forward;

            // Rotate player toward movement
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(moveDirection);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        // Smooth acceleration / stopping
        float targetSpeed = walkingInput ? walkSpeed : 0f;

        float speedChangeRate =
            walkingInput ? acceleration : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            speedChangeRate * Time.deltaTime
        );

        // Gravity
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedGravity;
        else
            verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity =
            moveDirection * currentSpeed;

        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }
}