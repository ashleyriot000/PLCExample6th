using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    public enum UpdateType
    {
        None,
        FixedUpdate,
        Update,
        LateUpdate,
        Max
    }

    public UpdateType uType = UpdateType.FixedUpdate;
    [Header("Movement Settings")]
    public float moveSpeed = 10f;       //카메라의 이동 속도
    public float mouseSensitivity = 0.1f;   //마우스 감도
    public float panSpeed = 0.05f;      //패닝 속도

    [Header("Focus Settings")]
    public float focusDistance = 1.5f;      //현재 포커스 거리
    public float minFocusDistance = 1f;
    public float maxFocusDistance = 10f;
    public float scrollSensitivity = 0.01f;     //휠 스크롤 감도

    //입력값
    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private float zoomInput;
    private Transform focusingTarget = null;
    private bool isFocusing = false;

    private bool isRightPressed;
    private bool isLeftPressed;
    private bool isMiddlePressed;

    private void OnEnable()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
    }

    private void HandleCalculation(float delta)
    {
        if (isFocusing && focusingTarget != null)
        {
            //휠로 스크롤하면 줌 거리 조절.
            if(Mathf.Abs(zoomInput) > 0.01f)
            {
                //휠을 위로 밀면 거리가 가까워지고, 아래로 당기면 멀어짐.
                focusDistance -= zoomInput * scrollSensitivity;
                focusDistance = Mathf.Clamp(focusDistance, minFocusDistance, maxFocusDistance);
            }

            //궤도 회전(좌클릭 드래그할 때)
            if (isLeftPressed && lookInput.sqrMagnitude > 0.01f)
            {
                float x = lookInput.x * mouseSensitivity;
                float y = lookInput.y * mouseSensitivity;
                targetRotation *= Quaternion.Euler(-y, x, 0f);
                Vector3 euler = targetRotation.eulerAngles;
                targetRotation = Quaternion.Euler(euler.x, euler.y, 0f);
            }

            //최종 위치 계산 => 타겟 중심으로 회전하면서 얻어낸 거리 위치에 타겟 위치를 설정함.
            targetPosition = focusingTarget.position - (targetRotation * Vector3.forward * focusDistance);
        }
        else
        {
            if(moveInput.sqrMagnitude > 0.01f)
            {
                Vector3 dir = (transform.forward * moveInput.y) + (transform.right * moveInput.x);
                targetPosition += dir * moveSpeed * delta;
            }

            if(isRightPressed && lookInput.sqrMagnitude > 0.01f)
            {
                float x = lookInput.x * mouseSensitivity;
                float y = lookInput.y * mouseSensitivity;
                targetRotation *= Quaternion.Euler(-y, x, 0f);
                Vector3 euler = targetRotation.eulerAngles;
                targetRotation = Quaternion.Euler(euler.x, euler.y, 0f);
            }

            if(isMiddlePressed && lookInput.sqrMagnitude > 0.01f)
            {
                Vector3 pan = (transform.up * -lookInput.y + transform.right * -lookInput.x) * panSpeed;
                targetPosition -= pan;
            }
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }
    public void OnOrbit(InputValue value)
    {
        isLeftPressed = value.isPressed;
    }
    public void OnFreelook(InputValue value)
    {
        isRightPressed = value.isPressed;
    }       
    public void OnPan(InputValue value)
    {
        isMiddlePressed = value.isPressed;
    }
    public void OnZoom(InputValue value)
    {
        zoomInput = value.Get<float>();
    }

    private void FixedUpdate()
    {
        if (uType != UpdateType.FixedUpdate)
            return;

        HandleCalculation(Time.fixedDeltaTime);
        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void Update()
    {
        if (uType != UpdateType.Update)
            return;

        HandleCalculation(Time.deltaTime);
        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void LateUpdate()
    {
        if (uType != UpdateType.LateUpdate)
            return;

        HandleCalculation(Time.deltaTime);
        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }
}
