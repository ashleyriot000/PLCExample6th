using System;
using Unity.Hierarchy;
using UnityEngine;
using UnityEngine.Events;

//이 스크립트를 사용하는데 반드시 필요한 다른 컴포넌트를 자동으로 붙여줌.
[RequireComponent(typeof(ConfigurableJoint))]
public class ServoAmp : MonoBehaviour
{

    //서보 앰프의 구동방식 열거형
    public enum AmpType
    {
        Linear,
        Rotary
    }

    //컨트롤 단위 열거형
    public enum ControlUnit
    {
        mm,     //직선 이동의 거리단위
        degree, //회전의 각도 단위
        pulse   //서보 모터의 분행능의 1이동.
    }
    //서보 앰프의 상태 열거형
    public enum AmpState
    {
        IDLE,
        Jogging, //수동 조작 상태
        Positioning,    //원하는 위치로 자동 이동하는 상태
        Homing_Search,  //원점을 찾는 상태
        Homing_Retry,   //원점을 못찾아서 반대방향에서 다시 시도
        Homing_Creep,   //원점 근처까지 왔음.
        Error,          //에러 고장
        Stop
    }

    [Header("[1] 기구 및 단위 설정")]
    public AmpType ampType = AmpType.Linear;
    public ControlUnit controlUnit = ControlUnit.mm;

    [Header("[2] 기구 파라미터")]
    public float motorResolution = 131072f;  //서보 모터의 분해능
    public float gearRatio = 1.0f;           //연결된 기어의 감속비
    public float ballscrewLead = 10f;        //서보모터 1회전당 전진 길이.

    [Header("[3] 속도 파라미터")]
    public float maxSpeed = 500f;           //위치 결정시 최대 속도(mm/Sec)
    public float jogSpeed = 100f;           //수동 운전시 속도(mm/Sec)
    public int defaultHomingDirection = 1;  //원점복귀 기본 방향
    public float homingHighSpeed = 100f;    //원점복귀시 최대 속도
    public float homingCreepSpeed = 20f;    //원점 복귀시 정밀(저속) 이동 속도

    [Header("[4] 가감속 시간")]
    public float accelTime = 100f;
    public float inPosWidth = 0.1f;         //타겟 포지션의 도착 오차 범위

    [Header("모니터링")]
    public float currentPos_Unit;           //설정한 단위로 표현
    public int currentPulse;                //서보앰프의 펄스값으로 표현
    public UnityEvent<int> onChangedPulse;  //펄스값이 변경될 때 콜백함수를 담는 이벤트 델리게이트
    public UnityEvent<float> onChangedPos;  //위치값이 변경될 때 콜백함수         //
    public UnityEvent<bool> onChangedReady; //준비 상태값이 변경될 때 콜백
    public UnityEvent<bool> onChangedError; //에러 상태값이 변경될 때 콜백
    public UnityEvent<bool> onChangedBusy;  //바쁨 상태값이 변경될 때 콜백
    public UnityEvent<bool> onChangedStop;  //정지 상태값이 변경될 때 콜백
    public UnityEvent<bool> onCompletedOPR; //원점복귀 완료될 때 콜백
    public UnityEvent onCompletedPositioning;   //위치결정이 완료될 때 콜백

    public int GetCurrentPulse
    {
        get => currentPulse;
        private set
        {
            if (currentPulse == value)
                return;

            currentPulse = value;
            onChangedPulse?.Invoke(value);
        }
    }

    public float GetCurrentUnit
    {
        get => currentPos_Unit;
        private set
        {
            if (currentPos_Unit == value)
                return;

            currentPos_Unit = value;
            onChangedPos?.Invoke(value);
        }
    }

    [Header("센서 입력")]
    private bool isOnLimitSensorPositive = false; //상한 리미트 센서의 감지여부
    private bool isOnLimitSensorNegative = false; //하한 리미트 센서의 감지여부
    private bool isOnProximityDOG = false;        //근점 도그 센서의 감지 여부

    public bool IsOnLSP
    {
        get => isOnLimitSensorPositive;
        set => isOnLimitSensorPositive = value;
    }

    public bool IsOnLSN
    {
        get => isOnLimitSensorNegative;
        set => isOnLimitSensorNegative = value;
    }

    public bool IsOnPDOG
    {
        get => isOnProximityDOG;
        set => isOnProximityDOG = value;
    }

    [Header("상태")]
    private bool isReady = false;       //명령을 받을 수 있는 상태 여부
    private bool isBusy = false;        //명령 수행중 여부
    private bool inPosition = false;    //위치결정 오차범위 안에 들어왔는지 여부
    private bool isError = false;       //에러 상태 여부
    private bool opr_Complete = false;  //원점 찾은 상태 여부
    private bool isStopped = false;     //정지상태 여부
    
    public bool IsReady
    {
        get => isReady;
        set
        {
            if (isReady == value)
                return;

            if(isReady = value)
            {
                SetJointForce(100000f);
            }
            else
            {
                SetJointForce(0f);
            }

            onChangedReady?.Invoke(value);
        }
    }

    public bool IsError
    {
        get => isError;
        private set
        {
            if (isError == value)
                return;

            isError = value;
            onChangedError?.Invoke(value);
        }
    }

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (isBusy == value)
                return;

            isBusy = value;
            onChangedBusy?.Invoke(value);
        }
    }

    public bool IsJogging 
    {
        get => cmd_JogForward | cmd_JogReverse;
    }

    public bool IsStopped
    {
        get => isStopped;
        set
        {
            if (isStopped == value)
                return;

            if(isStopped = value)
            {
                //정지했을 때 상태값 바꾸기.
                currentState = AmpState.Stop;
                cmd_StartPos = false;
                cmd_StartOPR = false;
                cmd_JogForward = false;
                cmd_JogReverse = false;
                inPosition = false;
                IsBusy = false;
            }
            else
            {
                currentState = AmpState.IDLE;
            }
            onChangedStop?.Invoke(value);
        }
    }

    public bool OPRComplete
    {
        get => opr_Complete;
        private set
        {
            if (opr_Complete == value)
                return;

            opr_Complete = value;
            onCompletedOPR?.Invoke(value);
        }
    }

    [Header("지령 저장 변수")]
    private bool cmd_ServoOn;   //서보 모터에 전원을 켜라는 명령을 받으면 true;
    private bool cmd_StartPos;  //지정 위치로 이동하라는 명령을 받으면 true;
    private bool cmd_StartOPR;  //원점 복귀 명령 받으면 true;
    private bool cmd_PreStartOPR;   //원점 복귀 명령을 이전에 내렸었는지 여부를 저장.
    private bool cmd_JogForward;    //수동 조그 전진 명령을 받으면 true;
    private bool cmd_JogReverse;    //수동 조그 후진 명령을 받으면 true;
    private int cmd_TargetPulse;    //목표 위치의 펄스값.

    private ConfigurableJoint slider;   //실제 이동을 제어하는 컴포넌트
    private Rigidbody rb;               //중력과 마찰값을 설정하기 위해 필요한 컴포넌트.
    private float currentVelocity_Unit; //현재 이동/회전 속도(초당 속도)
    private float internalTarget_Unit;  //서보 내부의 실제 위치.

    [SerializeField] private AmpState currentState = AmpState.IDLE;
    private bool homingHitDog = false;  //원점복귀시 근점도그에 감지여부
    private int homingDir = -1;
    private float homeOffset_Unit = 0f; //원점을 찾은 이후의 위치보정값.

    private void Awake()
    {
        slider = GetComponent<ConfigurableJoint>();
        rb = GetComponent<Rigidbody>();

        rb.automaticCenterOfMass = false;
        rb.automaticInertiaTensor = false;
        rb.linearDamping = 35f;

        //시작 위치를 계산
        float startPos = (ampType == AmpType.Linear) ?
            -transform.localPosition.z * 1000f : transform.localRotation.eulerAngles.x;

        //물리적인 위치 알아내기.
        currentPulse = PhysToPulse(startPos);
        internalTarget_Unit = PulseToUnit(currentPulse);
        homeOffset_Unit = 0f;
    }

    //모터의 힘(Torque)을 조절하는 함수
    private void SetJointForce(float force)
    {
        if(force > 0.1f)
        {
            rb.linearDamping = 0f;
            rb.useGravity = false;
        }
        else
        {
            rb.linearDamping = 35f;
            rb.useGravity = true;
        }

        JointDrive drive = new JointDrive
        {
            positionSpring = force,
            positionDamper = 100f,
            maximumForce = float.MaxValue,
            useAcceleration = true
        };

        if (ampType == AmpType.Linear)
            slider.xDrive = drive;
        else
            slider.angularXDrive = drive;
    }

    //계산된 위치값을 실제 물리 위치값으로 변환해서 적용하는 함수.
    private void ApplyPhysics(float value)
    {
        //작동방식이 선형일 경우
        if(ampType == AmpType.Linear)
        {
            float mmValue = value;
            if(controlUnit == ControlUnit.pulse)
            {
                mmValue = (value / motorResolution) * gearRatio * ballscrewLead;
            }

            //조인트에 목표 위치값을 적용.
            slider.targetPosition = new Vector3(mmValue / 1000f, 0, 0);
        }
        //작동방식이 회전 타입일 경우
        else
        {
            float degValue = value;
            if (controlUnit == ControlUnit.pulse)
            {
                degValue = (value / motorResolution) * gearRatio * 360f;
            }

            //조인트에 목표 회전값 적용.
            slider.targetRotation = Quaternion.Euler(degValue, 0, 0);
        }
    }

    //펄스 -> 단위(mm/degree) 변환
    public float PulseToUnit(int currentPulse)
    {
        //입력받은 펄스값 / 모터 1회전당 펄스값 => 실제 회전수.
        float revs = (float)currentPulse / motorResolution;
        //서보 모터의 회전수 * 기어 감속비 => 샤프트의 회전수
        float shaftRevs = revs * gearRatio;
        float unitValue = 0f;

        if(controlUnit == ControlUnit.mm)
        {
            unitValue = shaftRevs * ballscrewLead;
        }
        else if(controlUnit == ControlUnit.degree)
        {
            unitValue = shaftRevs * 360f;
        }
        else
        {
            unitValue = currentPulse;
        }

        return unitValue + homeOffset_Unit;
    }

    //단위(mm/degree) -> Pulse 변환
    public int PhysToPulse(float physicalVelocity)
    {
        //현재 이동값 - 원점 보정값 => 원점 기준 이동값
        float relativePos = physicalVelocity - homeOffset_Unit;

        //각 제어방식에 맞게 샤프트의 회전수를 구하고
        float shaftRevs = (controlUnit == ControlUnit.mm) ? relativePos / ballscrewLead : relativePos / 360f;

        //샤프트 회전수 / 기어비 => 서보모터가 회전해야할 실제 회전수
        float motorRevs = shaftRevs / gearRatio;

        return (int)(motorRevs * motorResolution);
    }



    private void SetupHoming()
    {
        //원점 복귀 명령을 수행하기 위해 세팅되어야 하는 값을 설정.
        currentState = AmpState.Homing_Search;
        homingDir = defaultHomingDirection;
        OPRComplete = false;
        inPosition = false;
        homingHitDog = false;
        IsBusy = true;
    }

    private void CompletedHoming()
    {
        //현재 속도를 0으로 즉시 멈추고 위치 고정.
        currentVelocity_Unit = 0f;

        //현재 위치를 기억해서 여기가 원점이라는 걸 저장.
        homeOffset_Unit = internalTarget_Unit;

        //내외부에 원점이 확인됐음을 알림
        OPRComplete = true;
        currentState = AmpState.IDLE;
        IsBusy = false;
    }

    public void ServoOn(bool isOn)
    {
        if (cmd_ServoOn = isOn)
        {
            //시작 위치를 계산
            float startPos = (ampType == AmpType.Linear) ?
                -transform.localPosition.z * 1000f : transform.localRotation.eulerAngles.x;

            //물리적인 위치 알아내기.
            currentPulse = PhysToPulse(startPos);
            internalTarget_Unit = PulseToUnit(currentPulse);
            homeOffset_Unit = internalTarget_Unit;

            ApplyPhysics(internalTarget_Unit);
        }
        else 
        {
            IsReady = false;
            IsBusy = false;
            inPosition = false;
            IsError = false;
            OPRComplete = false;
            cmd_StartOPR = false;
            cmd_StartPos = false;
            cmd_PreStartOPR = false;
            cmd_JogForward = false;
            cmd_JogReverse = false;
            cmd_TargetPulse = 0;
            currentState = AmpState.IDLE;
        }
    }

    //해당 위치로 이동하는 함수
    public void Positioning(int pulse)
    {
        if (!IsReady || IsStopped || IsError)
            return;

        if(IsBusy)
        {
            IsError = true;
            return;
        }

        cmd_TargetPulse = pulse;
        cmd_StartPos = true;
        IsBusy = true;
    }

    //수동 전진 이동 함수
    public void JogForward(bool isOn)
    {
        if (!IsReady || IsStopped || IsError)
            return;

        if (IsBusy)
        {
            IsError = true;
            return;
        }

        cmd_JogForward = isOn;
    }
    //수동 후진 이동 함수
    public void JogReverse(bool isOn)
    {
        if (!IsReady || IsStopped || IsError)
            return;

        if (IsBusy)
        {
            IsError = true;
            return;
        }

        cmd_JogReverse = isOn;
    }

    //원점 복귀 명령 함수
    public void Homing()
    {
        if (!IsReady || IsStopped || IsError)
            return;

        if (IsBusy)
        {
            IsError = true;
            return;
        }
        Debug.Log("Start Opr");
        cmd_StartOPR = true;
    }

    //에러 리셋 함수
    public void ErrorReset()
    {
        if(isError)
        {
            IsError = false;
            IsBusy = false;

            if (currentState != AmpState.Stop)
                currentState = AmpState.IDLE;

            cmd_StartOPR = false;
            cmd_JogForward = false;
            cmd_JogReverse = false;
            cmd_StartPos = false;
            cmd_TargetPulse = 0;
        }
    }

    private void FixedUpdate()
    {
        if (slider == null)
            return;

        if(!cmd_ServoOn)
        {
            IsReady = false;
            currentPos_Unit = 0f;
            return;
        }

        IsReady = true;

        bool isRisingEdge_OPR = cmd_StartOPR && !cmd_PreStartOPR;
        cmd_PreStartOPR = cmd_StartOPR;
        cmd_StartOPR = false;

        float targetVelocity = 0f;

        switch(currentState)
        {
            case AmpState.IDLE:
                if (cmd_JogForward || cmd_JogReverse)
                    currentState = AmpState.Jogging;
                else if (cmd_StartPos)
                    currentState = AmpState.Positioning;
                else if(isRisingEdge_OPR)
                {
                    if (opr_Complete)
                        Positioning(0);
                    else
                        SetupHoming();
                }
                break;
            case AmpState.Jogging:
                if (cmd_JogForward)
                    targetVelocity = jogSpeed;
                else if (cmd_JogReverse)
                    targetVelocity = -jogSpeed;
                else
                    currentState = AmpState.IDLE;
                break;
            case AmpState.Positioning:
                float targetPos = PulseToUnit(cmd_TargetPulse);
                float distance = targetPos - internalTarget_Unit;
                if(Mathf.Abs(distance) <= inPosWidth)
                {
                    targetVelocity = 0f;

                    //강제로 위치와 속도를 고정(떨림 현상 방지)
                    currentVelocity_Unit = 0f;
                    internalTarget_Unit = targetPos;
                    ApplyPhysics(internalTarget_Unit);

                    IsBusy = false;
                    inPosition = true;
                    cmd_StartPos = false;
                    currentState = AmpState.IDLE;
                    onCompletedPositioning?.Invoke();
                }
                else
                {
                    inPosition = false;
                    if(Mathf.Abs(distance) < 2.0f)
                    {
                        targetVelocity = distance > 1f ?
                            Mathf.Sign(distance) * 10f : Mathf.Sign(distance); 
                    }
                    else
                    {
                        //제동거리 예측
                        float stopDist = (Mathf.Abs(currentVelocity_Unit) * accelTime * 0.001f);
                        //제동거리보다 남은 거리가 짧으면 속도를 대폭 감소
                        if(Mathf.Abs(distance) < stopDist)
                        {
                            targetVelocity = distance * 2.0f;
                        }
                        else
                        {
                            targetVelocity = Mathf.Sign(distance) * maxSpeed;
                        }
                    }
                }
                break;
            case AmpState.Homing_Search:
                //원점을 모르는 상태 -> 찾아다녀야 함.
                targetVelocity = homingHighSpeed * homingDir;
                if(isOnProximityDOG)
                {
                    currentState = AmpState.Homing_Creep;       //저속모드로 전환.
                    homingHitDog = true;
                }
                else if((homingDir == -1) && isOnLimitSensorNegative || (homingDir == 1) && isOnLimitSensorPositive)
                {
                    currentVelocity_Unit = 0f;
                    homingDir = -homingDir;
                    currentState = AmpState.Homing_Retry;
                }
                break;
            case AmpState.Homing_Retry:
                targetVelocity = homingHighSpeed * homingDir;
                if(isOnProximityDOG)
                {
                    currentState = AmpState.Homing_Creep;
                    homingHitDog = true;
                }
                break;
            case AmpState.Homing_Creep:
                targetVelocity = homingCreepSpeed * defaultHomingDirection;
                if(!isOnProximityDOG && homingHitDog)
                {
                    CompletedHoming();
                    targetVelocity = 0f;
                }
                break;
            case AmpState.Stop:
                targetVelocity = 0f;
                break;
        }

        bool isHoming = currentState == AmpState.Homing_Search ||
            currentState == AmpState.Homing_Retry ||
            currentState == AmpState.Homing_Creep;

        //정방향으로 이동중인데 상한 리미트 센서가 감지되면
        if(isOnLimitSensorPositive && targetVelocity > 0f)
        {
            //정지시키고
            targetVelocity = 0f;
            //원점복귀중인지 확인
            if(!isHoming)
            {
                IsError = true;
                IsBusy = false;
                currentState = AmpState.Error;
            }
        }

        //역방향으로 이동중인데 하한 리미트 센서가 감지되면
        if (isOnLimitSensorNegative && targetVelocity < 0f)
        {
            //정지시키고
            targetVelocity = 0f;
            //원점복귀중인지 확인
            if (!isHoming)
            {
                IsError = true;
                IsBusy = false;
                currentState = AmpState.Error;
            }
        }

        //가감속 적용하기
        float referenceSpeed = (currentState == AmpState.Jogging) ? jogSpeed : maxSpeed;
        float accelRate = referenceSpeed / (accelTime * 0.001f); //초당 속도 변화량
        //현재 초당 이동 속도
        currentVelocity_Unit = Mathf.MoveTowards(currentVelocity_Unit, targetVelocity, accelRate * Time.fixedDeltaTime);

        //위치 적분
        internalTarget_Unit += currentVelocity_Unit * Time.fixedDeltaTime;

        //실제 물리적인 위치 적용하기
        ApplyPhysics(internalTarget_Unit);

        GetCurrentUnit = internalTarget_Unit - homeOffset_Unit;
        GetCurrentPulse = PhysToPulse(internalTarget_Unit);
    }
}
