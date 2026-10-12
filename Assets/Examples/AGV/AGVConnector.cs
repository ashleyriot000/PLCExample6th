using System;
using UnityEngine;

public class AGVConnector : MXObject
{
    public AGVController controller;
    public AGVLiftController liftController;

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

    public float feedbackTime = 0.3f;

    private bool needMove = false;
    private bool needLift = false;
    private short destination;
    private short floor;
    private bool completedMove;
    private bool completedLift;
    private float remainTime;
    private float remainTime_Lift;


    void Start()
    {
        if (controller == null)
            controller = GetComponent<AGVController>();

        if (liftController == null)
            liftController = GetComponent<AGVLiftController>();

        MXRequester.Get.AddDeviceAddress(plcHB.address, PLCHeartbeat);
        MXRequester.Get.AddDeviceAddress(moveAddress.address, Move);
        MXRequester.Get.AddDeviceAddress(destinationAddress.address, SetDestination);
        MXRequester.Get.AddDeviceAddress(liftAddress.address, Lift);
        MXRequester.Get.AddDeviceAddress(floorAddress.address, SetFloor);
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
}
