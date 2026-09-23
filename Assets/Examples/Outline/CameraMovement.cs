using UnityEngine;
using UnityEngine.EventSystems;
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
    public float lerpSpeed = 0.1f;      //선형 보간 수치

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
                targetPosition += pan;
            }
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        if (moveInput.sqrMagnitude > 0.01f)
            StopFocus();
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
        if(isRightPressed = value.isPressed)
            StopFocus();
    }       
    public void OnPan(InputValue value)
    {
        isMiddlePressed = value.isPressed;
    }
    public void OnZoom(InputValue value)
    {
        zoomInput = value.Get<float>();
    }

    public void OnFocus()
    {
        //마우스 커서가 UI 오브젝트 위에 있는 상황에서는 무시한다
        //if (EventSystem.current.IsPointerOverGameObject())
        //    return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if(Physics.Raycast(ray, out RaycastHit hit))
        {
            focusingTarget = hit.transform;
            isFocusing = true;

            //포커싱 하는 순간 포커스된 오브젝트를 정면으로 쳐다보게끔 하는 코드.
            Vector3 direction = focusingTarget.position - transform.position;
            targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            focusDistance = Mathf.Clamp(direction.magnitude, minFocusDistance, maxFocusDistance);
            targetPosition = focusingTarget.position - (targetRotation * Vector3.forward * focusDistance);
        }
    }

    public void StopFocus()
    {
        if (isFocusing == false)
            return;

        isFocusing = false;
        targetPosition = transform.position;
        focusingTarget = null;
    }

    private void LerpUpdate(float delta)
    {
        //선형 보간(Lerp)을 이용해서 부드러운 이동 및 회전을 하도록 추가.
        Vector3 position = Vector3.Lerp(transform.position, targetPosition, delta * lerpSpeed);
        Quaternion rotation = Quaternion.Slerp(transform.rotation, targetRotation, delta* lerpSpeed);
        transform.SetPositionAndRotation(position, rotation);
    }

    private void FixedUpdate()
    {
        if (uType != UpdateType.FixedUpdate)
            return;

        HandleCalculation(Time.fixedDeltaTime);
        LerpUpdate(Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (uType != UpdateType.Update)
            return;

        HandleCalculation(Time.deltaTime);
        LerpUpdate(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (uType != UpdateType.LateUpdate)
            return;

        HandleCalculation(Time.deltaTime);
        LerpUpdate(Time.deltaTime);
    }

    public void MoveToDestination(Transform destination)
    {
        StopFocus();

        targetPosition = destination.position;
        targetRotation = destination.rotation;
    }
}
