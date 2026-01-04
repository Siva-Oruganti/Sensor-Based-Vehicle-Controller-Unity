using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    // WHEELS
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider WheelColliderFrontLeft;
    [SerializeField] private WheelCollider WheelColliderFrontRight;
    [SerializeField] private WheelCollider WheelColliderBackLeft;
    [SerializeField] private WheelCollider WheelColliderBackRight;

    [Header("Wheel Meshes")]
    [SerializeField] private Transform TransformWheelFrontLeft;
    [SerializeField] private Transform TransformWheelFrontRight;
    [SerializeField] private Transform TransformWheelBackLeft;
    [SerializeField] private Transform TransformWheelBackRight;

    // SENSORS
    [Header("Sensors")]
    [SerializeField] private Transform RaycastSensorFront;
    [SerializeField] private Transform RaycastSensorLeft;
    [SerializeField] private Transform RaycastSensorRight;

    // kept intentionally (faculty requirement)
    [SerializeField] private Transform EulerAnglesSensor;

    // MOVEMENT SETTINGS
    [Header("Movement Settings")]
    [SerializeField] private float motorTorque = 650f;
    [SerializeField] private float maxSteerAngle = 13f;
    [SerializeField] private float maxSteerSpeed = 35f;

    // SPEED LIMITS
    [Header("Speed Limits")]
    [SerializeField] private float flatSpeed = 2f;
    [SerializeField] private float uphillSpeed = 2f;
    [SerializeField] private float downhillSpeed = 1f;

    // SLOPE DETECTION
    [Header("Slope Detection")]
    [SerializeField] private float slopeCheckDistance = 3f;
    [SerializeField] private float minSlopeAngle = 5f;
    [SerializeField] private float minDownwardSpeed = 0.05f;
    [SerializeField] private float downhillConfirmTime = 0f;

    // SENSOR SETTINGS
    [Header("Sensor Settings")]
    [SerializeField] private float roadCheckDistance = 10f;
    [SerializeField] private float obstacleCheckDistance = 10f;
    [SerializeField] private float roadRayAngle = 78f;

    [Header("Obstacle Ray Angles")]
    [SerializeField] private float[] obstacleAngles = { -30f, -15f, 0f, 15f, 30f };

    // PHYSICS
    [Header("Physics")]
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);
    [SerializeField] private LayerMask roadLayer;
    [SerializeField] private LayerMask obstacleLayer;

    private Rigidbody rb;

    // STATE
    private float targetSteerAngle;
    private float currentSteerAngle;
    private float currentSpeedLimit;
    private float targetSpeedLimit;
    private float previousY;
    private float downhillTimer;

    // INIT
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass += centerOfMassOffset;

        previousY = transform.position.y;
        currentSpeedLimit = flatSpeed;
        targetSpeedLimit = flatSpeed;
    }

    // AUTO LAYER SETUP
#if UNITY_EDITOR
    private void Reset()
    {
        AutoSetupLayers();
    }

    private void AutoSetupLayers()
    {
        roadLayer = LayerMask.GetMask("Road");
        obstacleLayer = LayerMask.GetMask("Obs");
    }
#endif

    // FIXED UPDATE
    private void FixedUpdate()
    {
        DetectSlopeAndSetSpeed();
        HandleSensors();
        LimitSpeed();
        SmoothSteering();
        ApplyMovement();
        UpdateWheelMeshes();
    }

    // SLOPE → SPEED
    private void DetectSlopeAndSetSpeed()
    {
        float currentY = transform.position.y;
        float deltaY = currentY - previousY;
        bool movingDown = deltaY < -minDownwardSpeed;

        if (Physics.Raycast(transform.position, Vector3.down,
            out RaycastHit hit, slopeCheckDistance, roadLayer))
        {
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);

            if (slopeAngle > minSlopeAngle && !movingDown)
            {
                targetSpeedLimit = uphillSpeed;
            }
            else if (movingDown)
            {
                downhillTimer += Time.fixedDeltaTime;
                if (downhillTimer >= downhillConfirmTime)
                    targetSpeedLimit = downhillSpeed;
            }
            else
            {
                downhillTimer = 0f;
                targetSpeedLimit = flatSpeed;
            }
        }

        previousY = currentY;
        currentSpeedLimit = targetSpeedLimit;
    }

    // SENSOR LOGIC
    private void HandleSensors()
    {
        targetSteerAngle = 0f;

        Vector3 leftRoadDir =
            Quaternion.AngleAxis(-roadRayAngle, transform.forward) * Vector3.down;
        Vector3 rightRoadDir =
            Quaternion.AngleAxis(roadRayAngle, transform.forward) * Vector3.down;

        bool roadLeft = Physics.Raycast(
            RaycastSensorLeft.position, leftRoadDir, roadCheckDistance, roadLayer);

        bool roadRight = Physics.Raycast(
            RaycastSensorRight.position, rightRoadDir, roadCheckDistance, roadLayer);

        float avoidanceSteer = 0f;
        bool obstacleDetected = false;

        foreach (float angle in obstacleAngles)
        {
            Vector3 dir =
                Quaternion.AngleAxis(angle, transform.up) *
                RaycastSensorFront.forward;

            if (Physics.Raycast(
                RaycastSensorFront.position, dir,
                obstacleCheckDistance, obstacleLayer))
            {
                obstacleDetected = true;
                avoidanceSteer += -angle / 30f;
            }
        }

        if (obstacleDetected)
        {
            targetSteerAngle = Mathf.Clamp(
                avoidanceSteer * maxSteerAngle,
                -maxSteerAngle, maxSteerAngle);
            return;
        }

        if (!roadLeft && roadRight)
            targetSteerAngle = maxSteerAngle;
        else if (!roadRight && roadLeft)
            targetSteerAngle = -maxSteerAngle;
    }

    // SPEED LIMIT
    private void LimitSpeed()
    {
        Vector3 Velocity = rb.velocity;
        Vector3 flatVel = new Vector3(Velocity.x, 0f, Velocity.z);

        if (flatVel.magnitude > currentSpeedLimit)
        {
            Vector3 limited = flatVel.normalized * currentSpeedLimit;
            rb.velocity = new Vector3(limited.x, Velocity.y, limited.z);
        }
    }

    // STEERING
    private void SmoothSteering()
    {
        float step = maxSteerSpeed * Time.fixedDeltaTime;
        currentSteerAngle =
            Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, step);
    }

    // APPLY
    private void ApplyMovement()
    {
        WheelColliderFrontLeft.steerAngle = currentSteerAngle;
        WheelColliderFrontRight.steerAngle = currentSteerAngle;

        WheelColliderBackLeft.motorTorque = motorTorque;
        WheelColliderBackRight.motorTorque = motorTorque;
    }

    // WHEEL VISUALS
    private void UpdateWheelMeshes()
    {
        UpdateWheel(WheelColliderFrontLeft, TransformWheelFrontLeft);
        UpdateWheel(WheelColliderFrontRight, TransformWheelFrontRight);
        UpdateWheel(WheelColliderBackLeft, TransformWheelBackLeft);
        UpdateWheel(WheelColliderBackRight, TransformWheelBackRight);
    }

    private void UpdateWheel(WheelCollider col, Transform t)
    {
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        t.position = pos;
        t.rotation = rot;
    }
}