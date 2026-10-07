using System;
using UnityEngine;

public enum MCodeType
{
    WithMode,
    AfterMode
}

public class ServoConveyorConnector : MonoBehaviour
{
    public ServoConveyorController controller;

    public DeviceAddress plcReadyAddress = new DeviceAddress("PLC Ready");
    public DeviceAddress servoAllOnAddress = new DeviceAddress("Servo All On");

    public DeviceAddress moduleReady = new DeviceAddress("Module Ready");
    public DeviceAddress axisAllReady = new DeviceAddress("Axis All Ready");

    public DeviceAddress startPositioningAddress = new DeviceAddress("위치 결정 기동 신호");
    public DeviceAddress positioningNumAddress = new DeviceAddress("위치 결정 번호");
    public DeviceAddress stopAddress = new DeviceAddress("정지 신호");

    public DeviceAddress receivedAddress = new DeviceAddress("기동 완료 신호");
    public DeviceAddress busyAddress = new DeviceAddress("BUSY 신호");
    public DeviceAddress completedAddress = new DeviceAddress("위치 결정 완료 신호");
    public DeviceAddress mCodeOnAddress = new DeviceAddress("M코드 발동 신호");
    public DeviceAddress mCodeAddress = new DeviceAddress("M코드 저장 주소");
    public DeviceAddress mCodeReleaseAddress = new DeviceAddress("M코드 해제 신호");

    public float feedbackTime = 0.3f;
    public MCodeType mCodeType = MCodeType.AfterMode;

    private bool receivedPositioning;
    private bool haveToExcute;
    private bool completedPosition = false;
    private int currentPosition = 0;
    private float remainFeedbackTime;
    private bool isOnMCode;

    void Start()
    {
        if (plcReadyAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(plcReadyAddress.address, PLCReady);
        if (servoAllOnAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(servoAllOnAddress.address, ServoOn);
        if (startPositioningAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(startPositioningAddress.address, StartPositioning);
        if (positioningNumAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(positioningNumAddress.address, SetPositioning);
        if (stopAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(stopAddress.address, Stop);
        if (mCodeReleaseAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(mCodeReleaseAddress.address, ReleaseMCode);
    }

    private void PLCReady(short obj)
    {
        Debug.Log($"PLC {(obj != 0 ? "" : "Not")} Ready!!!");
        if (moduleReady.useDevice)
            MXRequester.Get.AddSetDeviceRequest(moduleReady.address, obj);
    }

    private void ServoOn(short obj)
    {
        if (axisAllReady.useDevice)
            MXRequester.Get.AddSetDeviceRequest(axisAllReady.address, obj);

        controller.ServoOn(obj != 0);
    }

    private void StartPositioning(short obj)
    {
        if (isOnMCode)
            return;

        if(obj != 0)
        {
            haveToExcute = true;
        }
        else
        {
            if (receivedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(receivedAddress.address, 0);
        }
    }

    private void SetPositioning(short obj)
    {
        currentPosition = obj;
    }

    private void Stop(short obj)
    {
        if(obj == 1)
        {
            controller.Stop();
            if (busyAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(busyAddress.address, 0);
        }
    }

    private void ReleaseMCode(short obj)
    {
        if(obj == 1)
        {
            isOnMCode = false;
            if (mCodeAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(mCodeAddress.address, 0);
            if (mCodeOnAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(mCodeOnAddress.address, 0);
            if (mCodeReleaseAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(mCodeReleaseAddress.address, 0);
        }
    }

    public void OnCompletedPositioning(int mcode)
    {
        if (completedAddress.useDevice)
            MXRequester.Get.AddSetDeviceRequest(completedAddress.address, 1);

        if(mCodeType == MCodeType.AfterMode && mCodeOnAddress.useDevice && mcode != 0)
        {
            isOnMCode = true;
            MXRequester.Get.AddSetDeviceRequest(mCodeOnAddress.address, 1);
        }

        if(mCodeType == MCodeType.AfterMode && mCodeAddress.useDevice && mcode != 0)
        {
            MXRequester.Get.AddSetDeviceRequest(mCodeAddress.address, (short)mcode);
        }

        if (busyAddress.useDevice)
            MXRequester.Get.AddSetDeviceRequest(busyAddress.address, 0);

        completedPosition = true;
        remainFeedbackTime = Time.time + feedbackTime;
    }

    void Update()
    {
        if(haveToExcute && currentPosition != 0)
        {
            int mcode = controller.StartPositioning(currentPosition);
            if (receivedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(receivedAddress.address, 1);
            if (busyAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(busyAddress.address, 1);

            if (mCodeType == MCodeType.WithMode && mcode != 0)
            {
                isOnMCode = true;
                if (mCodeOnAddress.useDevice)
                    MXRequester.Get.AddSetDeviceRequest(mCodeOnAddress.address, 1);
                if (mCodeAddress.useDevice)
                    MXRequester.Get.AddSetDeviceRequest(mCodeAddress.address, (short)mcode);
            }

            haveToExcute = false;
        }

        if(completedPosition && remainFeedbackTime < Time.time)
        {
            completedPosition = false;
            if (completedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(completedAddress.address, 0);
        }
    }
}
