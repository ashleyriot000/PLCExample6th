using UnityEngine;

public class ShaftController : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("목표 분당 회전수 (RPM)")]
    public float targetRPM = 100f;

    [Tooltip("목표 속도에 도달하거나 멈출 때 걸리는 가감속 시간 (초)")]
    public float accelerationTime = 1.0f;

    [Tooltip("회전할 축 (기본: Y축)")]
    public Vector3 rotationAxis = Vector3.up;

    public Rigidbody rb;
    private float currentSpeedRadPerSec = 0f;
    private float velocitySmoothVelocity = 0f; // Mathf.SmoothDamp 내부 계산용

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.maxAngularVelocity = 600f;
        // 회전 중심이 어긋나서 덜덜 떨리는 것을 방지하려면 비활성화하는 것이 좋습니다.
        // rb.maxAngularVelocity 기본값(7 rad/s)은 고속 회전 시 제한되므로 여유 있게 늘려줍니다.
        //UpdateMaxAngularVelocity();
    }

    void FixedUpdate()
    {
        // 1. RPM을 라디안/초(rad/s)로 변환
        // 공식: RPM * 6 (초당 각도) * (Mathf.PI / 180) (라디안 변환) = RPM * 0.10472...
        float targetSpeedRadPerSec = targetRPM * 6f * Mathf.Deg2Rad;

        // 2. SmoothDamp를 사용해 현재 속도를 목표 속도로 부드럽게 보간 (가감속 구현)
        currentSpeedRadPerSec = Mathf.SmoothDamp(
            currentSpeedRadPerSec,
            targetSpeedRadPerSec,
            ref velocitySmoothVelocity,
            accelerationTime,
            Mathf.Infinity,
            Time.fixedDeltaTime
        );

        // 3. Rigidbody의 각속도(angularVelocity)에 반영
        rb.angularVelocity = rotationAxis.normalized * currentSpeedRadPerSec;
    }

    // 목표 RPM이 실시간으로 바뀔 때 maxAngularVelocity를 동적으로 갱신해주는 메서드
    public void UpdateMaxAngularVelocity()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        float maxReqRad = Mathf.Abs(targetRPM * 6f * Mathf.Deg2Rad);
        if (rb.maxAngularVelocity < maxReqRad)
        {
            rb.maxAngularVelocity = maxReqRad + 5f; // 여유분 추가
        }
    }

    public void SetRPM(float value)
    {
        targetRPM = value;
        //UpdateMaxAngularVelocity();
    }
}