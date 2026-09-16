using realvirtual;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ConveyorController : MonoBehaviour
{
    public ConveyorBelt belt;           //컨베이어 외관
    public LayerMask movableMask;       //컨베이어 밸트 위에서 움직여져야 하는 레이어 설정.
    public string movableTag;           //컨베이어 밸트 위에서 움직여져야 하는 태그 설정.
    public string movableName;          //컨베이어 밸트 위에서 움직여져야 하는 게임오브젝트 이름 설정.

    public float maxSpeed = 1f;         //초당 최대 이동 속도(미터)
    public float targetSpeed;           //초당 지령 속도(미터)
    public float currentSpeed;          //현재 초당 속도
    private float _targetSpeed;         //명령 내려진 속도
    private List<Rigidbody> triggerList = new List<Rigidbody>();

    public UnityEvent<bool> onChangedForward;
    public UnityEvent<bool> onChangedReverse;

    private bool isOnForward;
    public bool IsOnForward
    {
        get => IsOnForward;
        set
        {
            if (isOnForward == value)
                return;

            if(isOnForward = value)
            {
                isOnReverse = false;
                onChangedReverse?.Invoke(false);
            }

            SetMoveDirection();
            onChangedForward?.Invoke(value);
        }
    }

    private bool isOnReverse;
    public bool IsOnReverse
    {
        get => IsOnReverse;
        set
        {
            if (isOnReverse == value)
                return;

            if (isOnReverse = value)
            {
                isOnForward = false;
                onChangedForward?.Invoke(false);
            }

            SetMoveDirection();
            onChangedReverse?.Invoke(value);
        }
    }

    private void Start()
    {
        if (belt == null)
            belt = GetComponent<ConveyorBelt>();
    }

    private void FixedUpdate()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, _targetSpeed, maxSpeed * Time.fixedDeltaTime);
        belt.speed = currentSpeed;
        Vector3 moveDelta = currentSpeed * Time.fixedDeltaTime * transform.forward;
        foreach(Rigidbody rb in triggerList)
        {
            rb.MovePosition(rb.position + moveDelta);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((movableMask.value & 1 << other.gameObject.layer) == 0)
            return;

        if (!string.IsNullOrEmpty(movableTag) && other.gameObject.tag != movableTag)
            return;

        if (!string.IsNullOrEmpty(movableName) && !other.gameObject.name.Contains(movableName))
            return;

        Rigidbody rb = other.attachedRigidbody;
        if(rb != null && !triggerList.Contains(rb))
        {
            triggerList.Add(rb);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if ((movableMask.value & 1 << other.gameObject.layer) == 0)
            return;

        if (!string.IsNullOrEmpty(movableTag) && other.gameObject.tag != movableTag)
            return;

        if (!string.IsNullOrEmpty(movableName) && !other.gameObject.name.Contains(movableName))
            return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && triggerList.Contains(rb))
        {
            triggerList.Remove(rb);
        }
    }


    public void SetTargetSpeed(float speed)
    {
        _targetSpeed = 0f;
        targetSpeed = speed;
        Debug.Log(targetSpeed);
        if(speed > maxSpeed)
        {
            if (isOnForward)
                _targetSpeed = maxSpeed;
            if (isOnReverse)
                _targetSpeed += -maxSpeed;
            return;
        }

        if (speed < 0f)
        {
            _targetSpeed = 0f;
            return;
        }

        if (isOnForward)
            _targetSpeed = speed;
        if (isOnReverse)
            _targetSpeed += -targetSpeed;
    }

    private void SetMoveDirection()
    {
        _targetSpeed = 0f;
        if (isOnForward)
            _targetSpeed = targetSpeed;
        if (isOnReverse)
            _targetSpeed += -targetSpeed;
    }
}
