using UnityEngine;
using UnityEngine.InputSystem;

public class PunisherMovement : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Animator animator;
    public Transform cameraTransform;

    [Header("Movement")]
    public float walkSpeed = 1.5f;
    public float acceleration = 4f;
    public float deceleration = 3f;
    public float rotationSpeed = 10f;

    [Header("Gravity")]
    public float gravity = -9.81f;
    public float groundedGravity = -2f;

    private float currentSpeed = 0f;
    private float verticalVelocity = 0f;

    private Vector3 moveDirection;

    void Update()
    {
        // -------------------------
        // INPUT
        // -------------------------

        bool walkingInput =
            Keyboard.current != null &&
            Keyboard.current.wKey.isPressed;

        // Controls all Animator transitions
        animator.SetBool("iswalking", walkingInput);


        // -------------------------
        // MOVEMENT DIRECTION
        // -------------------------

        if (walkingInput)
        {
            // Move in the direction the camera is facing
            Vector3 cameraForward = cameraTransform.forward;

            // Remove vertical camera angle
            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude > 0.01f)
            {
                cameraForward.Normalize();

                moveDirection = cameraForward;


                // -------------------------
                // ROTATE CHARACTER
                // -------------------------

                Quaternion targetRotation =
                    Quaternion.LookRotation(moveDirection);

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }


        // -------------------------
        // ACCELERATION / DECELERATION
        // -------------------------

        float targetSpeed =
            walkingInput ? walkSpeed : 0f;

        float speedChangeRate =
            walkingInput ? acceleration : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed,
            targetSpeed,
            speedChangeRate * Time.deltaTime
        );


        // -------------------------
        // GRAVITY
        // -------------------------

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedGravity;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }


        // -------------------------
        // APPLY MOVEMENT
        // -------------------------

        Vector3 velocity =
            moveDirection * currentSpeed;

        velocity.y = verticalVelocity;

        controller.Move(
            velocity * Time.deltaTime
        );
    }
}