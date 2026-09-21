using UnityEngine;
using UnityEngine.Events;

public class RollerConveyorController : MonoBehaviour
{
    public HingeJoint[] rollers;

    public int maxRPM = 60;     //정격 회전수(분당 회전수)

    [Min(0.1f)]
    public float accelTime = 1f;    //가감속 시간

    private float maxVelocity;          //최대 속도
    private float targetVelocity;       //목표 속도
    private float currentVelocity;      //현재 속도

    public UnityEvent<bool> onChangedForward;       //정회전 이벤트 델리게이트
    public UnityEvent<bool> onChangedReverse;       //역회전 이벤트 델리게이트

    private bool isOnForward;
    public bool IsOnForward
    {
        get => isOnForward;
        set
        {
            if (isOnForward == value)
                return;

            if(isOnForward = value)
            {
                IsOnReverse = false;
            }
            CalculateTargetSpeed(isOnForward, isOnReverse);
            onChangedForward?.Invoke(value);
        }
    }
    private bool isOnReverse;
    public bool IsOnReverse
    {
        get => isOnReverse;
        set
        {
            if (isOnReverse == value)
                return;

            if(isOnReverse = value)
            {
                IsOnForward = false;
            }

            CalculateTargetSpeed(isOnForward, isOnReverse);
            onChangedReverse?.Invoke(value);
        }
    }

    private void CalculateTargetSpeed(bool isOnForward, bool isOnReverse)
    {
        if(isOnForward == isOnReverse)
        {
            targetVelocity = 0f;
            return;
        }

        if(isOnForward)
        {
            targetVelocity = maxVelocity;
            return;
        }

        targetVelocity = -maxVelocity;
    }

    private void Start()
    {
        maxVelocity = maxRPM * 6f;
    }

    private void FixedUpdate()
    {
        //현재 초당 회전각속도 구하기.
        currentVelocity = 
            Mathf.MoveTowards(currentVelocity, targetVelocity, 
            (maxVelocity / accelTime) * Time.fixedDeltaTime);

        //모든 롤러에 동일한 각속도 적용.
        foreach(var roller in rollers)
        {
            var motor = roller.motor;
            motor.targetVelocity = currentVelocity;
            roller.motor = motor;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        //초당 최대 각속도를 구하기.
        maxVelocity = maxRPM * 6f;
    }
#endif
}
