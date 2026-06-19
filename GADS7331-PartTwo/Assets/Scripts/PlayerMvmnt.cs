using System.Collections;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMvmnt : MonoBehaviour
{
    [SerializeField] InputAction Jump;

    public Transform orientation;
    Rigidbody rb;
    [SerializeField] float jumpStrength = 10f;
    public float MoveSpeed;
    float horizontalInput;
    float verticalInput;
    [SerializeField] float overlapRadius = 1f;
    [SerializeField] LayerMask groundLayer;
    [SerializeField] bool isGrounded;
    Vector3 moveDirection;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void PlayerInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
    }

    private void Update()
    {
        PlayerInput();
        isGrounded = Physics.CheckSphere(this.transform.position, overlapRadius, groundLayer);
        LimitSpeed();
    }

    private void FixedUpdate()
    {
        PlayerMoveValues();

        if (Jump.IsPressed())
        {
            if (isGrounded == true)
            {
                rb.AddForce(Vector3.up * jumpStrength, ForceMode.Impulse);
            }
        }
    }

    private void PlayerMoveValues()
    {
        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
        rb.AddForce(moveDirection.normalized * MoveSpeed * 10f, ForceMode.Force);
    }

    private void LimitSpeed()
    {
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if(flatVel.magnitude > MoveSpeed)
        {

        }
    }

    private void OnEnable()
    {
        Jump.Enable();
    }
}
