using System;
using UnityEngine;

public class AGVConnector : MXObject
{
    public AGVController controller;

    public DeviceAddress plcHB = new DeviceAddress("PLC Heartbeat");
    public DeviceAddress agvHB = new DeviceAddress("AGV Heartbeat");

    public DeviceAddress moveAddress = new DeviceAddress("AGV 이동 명령 신호");
    public DeviceAddress destinationAddress = new DeviceAddress("AGV 이동 목적지 번호");

    public DeviceAddress receiveMoveAddress = new DeviceAddress("AGV 이동 기동 완료 신호");
    public DeviceAddress moveBusyAddress = new DeviceAddress("AGV 이동 BUSY 신호");
    public DeviceAddress moveCompletedAddress = new DeviceAddress("AGV 이동 완료 신호");
    public float feedbackTime = 0.3f;

    private bool needMove = false;
    private short destination;
    private bool completedMove;
    private float remainTime;


    void Start()
    {
        if (controller == null)
            controller = GetComponent<AGVController>();

        MXRequester.Get.AddDeviceAddress(plcHB.address, PLCHeartbeat);
        MXRequester.Get.AddDeviceAddress(moveAddress.address, Move);
        MXRequester.Get.AddDeviceAddress(destinationAddress.address, SetDestination);
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

        if(completedMove && remainTime < Time.time)
        {
            completedMove = false;
            MXRequester.Get.AddSetDeviceRequest(moveCompletedAddress.address, 0);           
        }
    }

    public void OnComletedMove()
    {
        completedMove = true;
        remainTime = Time.time + feedbackTime;
        MXRequester.Get.AddSetDeviceRequest(moveCompletedAddress.address, 1);
        MXRequester.Get.AddSetDeviceRequest(moveBusyAddress.address, 0);
    }
}
