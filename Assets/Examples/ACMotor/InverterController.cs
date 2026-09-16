using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class InverterController : MonoBehaviour
{
    //회전축 열거형
    public enum RotateAxis
    {
        None,
        XAxis,
        YAxis,
        ZAxis,
        Max
    }

    [Header("인버터 파라미터(Settings)")]
    public RotateAxis axis = RotateAxis.ZAxis;
    [Delayed] public float maxFrequency = 60f;  //최대 주파수
    [Delayed] public float maxRPM = 1800f;      //정격 회전수
    [Delayed] public float accelTime = 1.0f;    //가속시간(0 -> 최대 회전속도까지 도달하는데 걸리는 시간
    [Delayed] public float decelTime = 1.0f;    //감속시간(최대 -> 0으로 감속하는데 걸리는 시간

    [Header("제어 입력")]
    public bool STF = false;        //정회전 신호
    public bool STR = false;        //역회전 신호

    [Delayed] public float targetHz = 0f;     //지령 주파수
    [SerializeField] private float currentHz;       //현재 주파수
    [SerializeField] private float currentRPM;      //현재 RPM

    public float GetCurrentHz => currentHz;     //현재 주파수 Get 프로퍼티
    public float GetCurrentRPM => currentRPM;   //현재 RPM Get 프로퍼티

    public Rigidbody shaft;
    public UnityEvent<bool> onChangedSTF;       //정방향 신호 변화에 따른 콜백함수를 담는 델리게이트
    public UnityEvent<bool> onChangedSTR;       //역방향 신호 변화에 따른 콜백함수를 담는 델리게이트
    public UnityEvent<float> onChangedHz;       //현재 Hz변화에 따른 콜백함수를 담는 델리게이트
    public UnityEvent<float> onChangedRPM;      //현재 RPM변화에 따른 콜백함수를 담는 델리게이트

    //정방향 신호 변화 프로퍼티
    public bool IsOnSTF
    {
        get => STF;
        set
        {
            if (STF == value)
                return;

            if(STF = value)
            {
                STR = false;
                onChangedSTR?.Invoke(STR);
            }

            onChangedSTF?.Invoke(STF);
        }
    }

    //역방향 신호 변화 프로퍼티
    public bool IsOnSTR
    {
        get => STR;
        set
        {
            if (STR == value)
                return;

            if (STR = value)
            {
                STF = false;
                onChangedSTF?.Invoke(STF);
            }

            onChangedSTR?.Invoke(STR);
        }
    }

    public float CurrentHz
    {
        get => currentHz;
        set
        {
            if (currentHz == value)
                return;

            currentHz = value;
            onChangedHz?.Invoke(Mathf.Abs(value));
        }
    }

    public float CurrentRPM
    {
        get => currentRPM;
        set
        {
            if (currentRPM == value)
                return;

            currentRPM = value;
            onChangedRPM?.Invoke(value);
        }
    }


    private void Awake()
    {
        if (shaft == null)
            shaft = GetComponent<Rigidbody>();
        if(shaft != null)
        {
            shaft.maxAngularVelocity = 6000f;
            shaft.constraints = RigidbodyConstraints.FreezePosition;

            switch (axis)
            {
                case RotateAxis.XAxis:
                    shaft.constraints |= 
                        RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
                    break;
                case RotateAxis.YAxis:
                    shaft.constraints |=
                        RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                    break;
                case RotateAxis.ZAxis:
                    shaft.constraints |=
                        RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationX;
                    break;
            }

            shaft.useGravity = false;
            shaft.automaticCenterOfMass = false;
            shaft.automaticInertiaTensor = false;
            shaft.inertiaTensorRotation = Quaternion.identity;
            shaft.inertiaTensor = Vector3.one;
        }
    }

    private void FixedUpdate()
    {
        //최종적인 타겟 주파수를 알아내고
        float finalTargetHz = 0f;

        if (STF && !STR) finalTargetHz = targetHz;
        else if (STR && !STF) finalTargetHz = -targetHz;

        //가감속 로직을 통해서 현재 주파수를 알아내기
        float rampRate = maxFrequency / (finalTargetHz != 0 ? accelTime : decelTime);
        CurrentHz = Mathf.MoveTowards(CurrentHz, finalTargetHz, rampRate * Time.fixedDeltaTime);

        //Hz -> RPM -> Rad/s 변환
        //RPM = (120 * 최대주파수) / 극수
        CurrentRPM = (CurrentHz / maxFrequency) * maxRPM;
        float radPerSec = currentRPM * Mathf.PI / 30f;

        Vector3 rotateAxis = axis switch
        {
            RotateAxis.XAxis => transform.right,
            RotateAxis.YAxis => transform.up,
            RotateAxis.ZAxis => transform.forward,
            _ => Vector3.zero
        };

        //초당 회전각도를 적용하기.
        shaft.angularVelocity = rotateAxis * radPerSec;
    }


    public void ChangeFrequency(float frequency)
    {
        targetHz = Mathf.Clamp(frequency, 0f, maxFrequency);
    }
}
