using System;
using UnityEngine;

public class AGVConnector : MXObject
{
    public AGVController controller;
    public AGVLiftController liftController;
    public Animator anim;
    public AGVGripperController gripper;

    [Header("이동 관련")]
    public DeviceAddress plcHB = new DeviceAddress("PLC Heartbeat");
    public DeviceAddress agvHB = new DeviceAddress("AGV Heartbeat");

    public DeviceAddress moveAddress = new DeviceAddress("AGV 이동 명령 신호");
    public DeviceAddress destinationAddress = new DeviceAddress("AGV 이동 목적지 번호");

    public DeviceAddress receiveMoveAddress = new DeviceAddress("AGV 이동 기동 완료 신호");
    public DeviceAddress moveBusyAddress = new DeviceAddress("AGV 이동 BUSY 신호");
    public DeviceAddress moveCompletedAddress = new DeviceAddress("AGV 이동 완료 신호");

    [Header("리프트 관련")]
    public DeviceAddress floorAddress = new DeviceAddress("AGV 상하 이동 목적지 번호");
    public DeviceAddress liftAddress = new DeviceAddress("AGV 상하 이동 명령 신호");

    public DeviceAddress receivedLiftAddress = new DeviceAddress("AGV 상하 이동 기동 완료 신호");
    public DeviceAddress liftBusyAddress = new DeviceAddress("AGV 상하 이동 BUSY 신호");
    public DeviceAddress completedLiftAddress = new DeviceAddress("AGV 상하 이동 완료 신호");

    [Header("그리퍼 관련")]
    public DeviceAddress getAddress = new DeviceAddress("그리퍼 겟 명령신호");
    public DeviceAddress putAddress = new DeviceAddress("그리퍼 풋 명령신호");
    public DeviceAddress pullAddress = new DeviceAddress("그리퍼 풀 명령신호");
    public DeviceAddress sendAddress = new DeviceAddress("그리퍼 샌드 명령신호");

    public DeviceAddress receivedGripperAddress = new DeviceAddress("그리퍼 기동 완료 신호");
    public DeviceAddress gripperBusyAddress = new DeviceAddress("그리퍼 BUSY 신호");
    public DeviceAddress completedGripperAddress = new DeviceAddress("그리퍼 완료 신호");


    public float feedbackTime = 0.3f;

    private bool needMove = false;
    private bool needLift = false;
    private bool needGripper = false;
    private short destination;
    private short floor;
    private bool completedMove;
    private bool completedLift;
    private bool completedGripper;
    private float remainTime;
    private float remainTime_Lift;
    private float remainTime_Gripper;
    private bool gripperBusy = false;


    void Start()
    {
        if (controller == null)
            controller = GetComponent<AGVController>();

        if (liftController == null)
            liftController = GetComponent<AGVLiftController>();

        if (anim == null)
            anim = GetComponent<Animator>();

        MXRequester.Get.AddDeviceAddress(plcHB.address, PLCHeartbeat);
        MXRequester.Get.AddDeviceAddress(moveAddress.address, Move);
        MXRequester.Get.AddDeviceAddress(destinationAddress.address, SetDestination);
        MXRequester.Get.AddDeviceAddress(liftAddress.address, Lift);
        MXRequester.Get.AddDeviceAddress(floorAddress.address, SetFloor);

        MXRequester.Get.AddDeviceAddress(getAddress.address, GetSignal);
        MXRequester.Get.AddDeviceAddress(putAddress.address, PutSignal);
        MXRequester.Get.AddDeviceAddress(pullAddress.address, PullSignal);
        MXRequester.Get.AddDeviceAddress(sendAddress.address, sendSignal);
    }

    private void sendSignal(short obj)
    {
        if (obj != 0)
        {
            if (gripperBusy)
                return;
            
            Send();
        }
        else
        {
            MXRequester.Get.AddSetDeviceRequest(receivedGripperAddress.address, 0);
        }   
    }

    private void PullSignal(short obj)
    {
        if (obj != 0)
        {
            if (gripperBusy)
                return;

            Pull();
        }
        else
        {
            MXRequester.Get.AddSetDeviceRequest(receivedGripperAddress.address, 0);
        }
    }

    private void PutSignal(short obj)
    {
        if (obj != 0)
        {
            if (gripperBusy)
                return;

            Put();
        }
        else
        {
            MXRequester.Get.AddSetDeviceRequest(receivedGripperAddress.address, 0);
        }
    }

    private void GetSignal(short obj)
    {
        if (obj != 0)
        {
            if (gripperBusy)
                return;

            Get();
        }
        else
        {
            MXRequester.Get.AddSetDeviceRequest(receivedGripperAddress.address, 0);
        }
    }

    private void SetFloor(short obj)
    {
        floor = obj;
    }

    private void Lift(short obj)
    {
        if(obj != 0)
        {
            needLift = true;
        }
        else
        {
            MXRequester.Get.AddSetDeviceRequest(receivedLiftAddress.address, 0);
        }
    }

    private void PLCHeartbeat(short obj)
    {
        MXRequester.Get.AddSetDeviceRequest(agvHB.address, obj);
    }

    private void Move(short obj)
    {
        Debug.Log($"이동 명령 => {obj}");
        if (obj != 0)
            needMove = true;
        else
            MXRequester.Get.AddSetDeviceRequest(receiveMoveAddress.address, 0);
    }

    private void SetDestination(short obj)
    {
        Debug.Log($"이동 번호 => {obj}");
        destination = obj;
    }

    private void Update()
    {
        if (controller == null)
            return;

        if(needMove && destination != 0)
        {
            controller.Go(destination);
            MXRequester.Get.AddSetDeviceRequest(receiveMoveAddress.address, 1);
            MXRequester.Get.AddSetDeviceRequest(moveBusyAddress.address, 1);
            needMove = false;
            destination = 0;
        }

        if(needLift && floor != 0)
        {
            liftController.Go(floor);
            MXRequester.Get.AddSetDeviceRequest(receivedLiftAddress.address, 1);
            MXRequester.Get.AddSetDeviceRequest(liftBusyAddress.address, 1);
            needLift = false;
            floor = 0;
        }

        if(completedMove && remainTime < Time.time)
        {
            completedMove = false;
            MXRequester.Get.AddSetDeviceRequest(moveCompletedAddress.address, 0);           
        }

        if(completedLift && remainTime_Lift < Time.time)
        {
            completedLift = false;
            MXRequester.Get.AddSetDeviceRequest(completedLiftAddress.address, 0);
        }

        if(completedGripper && remainTime_Gripper < Time.time)
        {
            completedGripper = false;
            MXRequester.Get.AddSetDeviceRequest(completedGripperAddress.address, 0);
        }
    }

    public void OnComletedMove()
    {
        completedMove = true;
        remainTime = Time.time + feedbackTime;
        MXRequester.Get.AddSetDeviceRequest(moveCompletedAddress.address, 1);
        MXRequester.Get.AddSetDeviceRequest(moveBusyAddress.address, 0);
    }

    public void OnCompletedLift()
    {
        completedLift = true;
        remainTime_Lift = Time.time + feedbackTime;
        MXRequester.Get.AddSetDeviceRequest(completedLiftAddress.address, 1);
        MXRequester.Get.AddSetDeviceRequest(liftBusyAddress.address, 0);
    }

    public void Get()
    {
        anim.SetTrigger("Get");
        MXRequester.Get.AddSetDeviceRequest(receivedGripperAddress.address, 1);
        MXRequester.Get.AddSetDeviceRequest(gripperBusyAddress.address, 1);
        gripperBusy = true;
    }
    public void GetProduct()
    {
        gripper.Grab(true);
    }

    public void Put()
    {
        Debug.Log("Put");
        anim.SetTrigger("Put");
        MXRequester.Get.AddSetDeviceRequest(receivedGripperAddress.address, 1);
        MXRequester.Get.AddSetDeviceRequest(gripperBusyAddress.address, 1);
        gripperBusy = true;
    }
    public void PutProduct()
    {
        gripper.PutProduct();
    }

    public void Pull()
    {
        anim.SetTrigger("Pull");
        MXRequester.Get.AddSetDeviceRequest(receivedGripperAddress.address, 1);
        MXRequester.Get.AddSetDeviceRequest(gripperBusyAddress.address, 1);
        gripperBusy = true;
    }
    public void PullProduct()
    {
        gripper.PullProduct();
    }

    public void Send()
    {
        anim.SetTrigger("Send");
        MXRequester.Get.AddSetDeviceRequest(receivedGripperAddress.address, 1);
        MXRequester.Get.AddSetDeviceRequest(gripperBusyAddress.address, 1);
        gripperBusy = true;
    }
    public void ReleaseProduct()
    {
        gripper.Grab(false);
    }

    public void OnCompletedGripper()
    {
        MXRequester.Get.AddSetDeviceRequest(completedGripperAddress.address, 1);
        MXRequester.Get.AddSetDeviceRequest(gripperBusyAddress.address, 0);
        gripperBusy = false;
        completedGripper = true;
        remainTime_Gripper = Time.time + feedbackTime;
    }
}
