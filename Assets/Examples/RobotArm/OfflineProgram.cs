using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Preliy.Flange;
#if UNITY_EDITOR
using UnityEditor;
#endif


public enum Pattern
{
    None,
    End,        //정밀 정지
    Continue,   //블렌딩 통과
    Rotate,     //자세 회전 우선 모드
    Max
}
public enum TaskState
{
    None,
    Wait,
    Run,
    Done,
    Error,
    Max
}
public abstract class OfflineProgram : MonoBehaviour
{
    public TargetScheduler scheduler;
    public TargetWaypoint[] waypoints;

    public TaskState currentState;
    public float doneDuration = 0.3f;

    private Queue<IRobotCommand> taskQueue = new Queue<IRobotCommand>();

    private float _accelTime;
    public float AccelTime
    {
        get => _accelTime;
        set => _accelTime = value;
    }

    private float _decelTime;
    public float DeccelTime
    {
        get => _decelTime;
        set => _decelTime = value;
    }

    //단위 : mm/seconds <-> m/second
    private float _linearSpeed;
    public float LinearSpeed
    {
        get => _linearSpeed;
        set
        {
            _linearSpeed = value * 0.001f;
        }
    }
    //단위 : degree/seconds
    private float _angularSpeed;
    public float AngularSpeed
    {
        get => _angularSpeed;
        set
        {
            _angularSpeed = value;
        }
    }

    private float _jointSpeed;
    public float JointSpeed
    {
        get => _jointSpeed;
        set
        {
            _jointSpeed = value;
        }
    }

    private float _currentLinearSpeed;
    public float CurrentLinearSpeed
    {
        get => _currentLinearSpeed;
        set => _currentLinearSpeed = value;
    }
    private float _currentAngularSpeed;
    public float CurrentAngularSpeed
    {
        get => _currentAngularSpeed;
        set => _currentAngularSpeed = value;
    }

    private float _currentJointSpeed;
    public float CurrentJointSpeed
    {
        get => _currentJointSpeed;
        set => _currentJointSpeed = value;
    }

    private float doneOffTime;

    private bool isOn;
    public bool IsOn
    {
        get => isOn;
        set
        {
            if (isOn == value)
                return;

            if (isOn = value)
            {
                currentState = TaskState.Wait;
                Home();
                RunProgram();
            }
            else
            {
                currentState = TaskState.None;
            }
        }
    }

    public abstract void Home();
    private async void RunProgram()
    {
        if (currentState == TaskState.Error || currentState == TaskState.Run)
        {
            currentState = TaskState.Error;
            return;
        }

        if (taskQueue.Count == 0)
            return;

        currentState = TaskState.Run;
        Debug.LogWarning($"위치결정 기동");
        while (taskQueue.Count > 0)
        {
            await taskQueue.Dequeue().ExecuteCommand(scheduler);
        }

        if (currentState == TaskState.Run)
            currentState = TaskState.Done;

        doneOffTime = Time.time + doneDuration;
        Debug.LogWarning($"위치결정 완료");
    }


    private void Update()
    {
        if (currentState == TaskState.Done && doneOffTime < Time.time)
        {
            currentState = TaskState.Wait;
        }
    }

    public void RunProgram(int programNum)
    {
        if (!(currentState != TaskState.Wait || currentState != TaskState.Done))
        {
            Debug.LogWarning("현재 다른 프로그램이 실행 중입니다. 완료 후 호출해주세요.");
            currentState = TaskState.Error;
            return;
        }

        // 새로운 시퀀스를 담기 위해 큐 초기화
        taskQueue.Clear();

        string methodName = programNum == 0 ? "Home" : $"Program{programNum}";

        MethodInfo targetMethod = this.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (targetMethod != null)
        {
            targetMethod.Invoke(this, null);
            RunProgram();
        }
        else
        {
            Debug.LogError($"[호출 실패] 현재 스크립트에 '{methodName}' 함수가 정의되어 있지 않습니다.");
        }
    }


    protected void Wait(int millisecond)
    {
        if (Application.isPlaying && !(currentState.HasFlag(TaskState.Wait) || currentState.HasFlag(TaskState.Done)))
            return;

        taskQueue.Enqueue(new Wait(millisecond));
    }

    protected void AccTime(float accelTime, float decelTime)
    {
        if (Application.isPlaying && !(currentState.HasFlag(TaskState.Wait) || currentState.HasFlag(TaskState.Done)))
            return;

        taskQueue.Enqueue(new AccTime(this, accelTime, decelTime));
    }
    protected void Speed(float linearSpeed, float angularSpeed, float jointSpeed)
    {
        if (Application.isPlaying && !(currentState.HasFlag(TaskState.Wait) || currentState.HasFlag(TaskState.Done)))
            return;

        taskQueue.Enqueue(new Speed(this, linearSpeed, angularSpeed, jointSpeed));
    }


    protected void Connect(bool isConnected)
    {
        if (Application.isPlaying && !(currentState.HasFlag(TaskState.Wait) || currentState.HasFlag(TaskState.Done)))
            return;


        taskQueue.Enqueue(new Connect(isConnected));
    }

    protected void Action(bool isOn, int delay = 100)
    {
        if (Application.isPlaying && !(currentState.HasFlag(TaskState.Wait) || currentState.HasFlag(TaskState.Done)))
            return;

        taskQueue.Enqueue(new ToolAction(isOn, delay));
    }

    protected void MoveJ(TargetWaypoint target, float jointSpeed = 0f, Pattern pattern = Pattern.End, float zoneAngle = 0f, int dwellTime = 0)
    {
        if (Application.isPlaying && !(currentState.HasFlag(TaskState.Wait) || currentState.HasFlag(TaskState.Done)))
            return;


        taskQueue.Enqueue(new MoveJ(this, target, jointSpeed, pattern, zoneAngle, dwellTime));
    }

    protected void MoveL(TargetWaypoint target, float linearSpeed = 0f, float angularSpeed = 0f, Pattern pattern = Pattern.End, float zoneRadius = 0f, int dwellTime = 0)
    {
        if (Application.isPlaying && !(currentState.HasFlag(TaskState.Wait) || currentState.HasFlag(TaskState.Done)))
            return;

        taskQueue.Enqueue(new MoveL(this, target, linearSpeed, angularSpeed, pattern, zoneRadius, dwellTime));
    }

    protected void MoveC(TargetWaypoint viaPoint, TargetWaypoint endPoint, float linearSpeed = 0f, float angularSpeed = 0f, Pattern pattern = Pattern.End, float zoneRadius = 0f, int dwellTime = 0)
    {
        if (Application.isPlaying && !(currentState.HasFlag(TaskState.Wait) || currentState.HasFlag(TaskState.Done)))
            return;

        taskQueue.Enqueue(new MoveC(this, viaPoint, endPoint, linearSpeed, angularSpeed, pattern, zoneRadius, dwellTime));
    }
}
