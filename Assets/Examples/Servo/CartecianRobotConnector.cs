using System;
using UnityEditor;
using UnityEngine;

public class CartecianRobotConnector : MXObject
{
    public ServoAmp axis1;
    public ServoAmp axis2;
    public ServoAmp axis3;

    public PositioningManager manager;
    public float feedbackTime = 0.3f;

    public DeviceAddress plcReadyAddress = new DeviceAddress("PLC Ready");
    public DeviceAddress moduleReady = new DeviceAddress("Module Ready");
    public DeviceAddress servoAllOnAddress = new DeviceAddress("Servo All On 신호");
    public DeviceAddress servoAllReadyAddress = new DeviceAddress("Servo All Ready");

    public DeviceAddress axis1JogFWDAddress = new DeviceAddress("1축 JOG 전진 신호");
    public DeviceAddress axis1JogRVSAddress = new DeviceAddress("1축 JOG 후진 신호");
    public DeviceAddress axis2JogFWDAddress = new DeviceAddress("2축 JOG 전진 신호");
    public DeviceAddress axis2JogRVSAddress = new DeviceAddress("2축 JOG 후진 신호");
    public DeviceAddress axis3JogFWDAddress = new DeviceAddress("3축 JOG 전진 신호");
    public DeviceAddress axis3JogRVSAddress = new DeviceAddress("3축 JOG 후진 신호");
    public DeviceAddress axis1PosAddress = new DeviceAddress("1축 위치 결정 기동 신호");
    public DeviceAddress axis2PosAddress = new DeviceAddress("2축 위치 결정 기동 신호");
    public DeviceAddress axis3PosAddress = new DeviceAddress("3축 위치 결정 기동 신호");
    public DeviceAddress axis1PosNumAddress = new DeviceAddress("1축 위치 결정 번호");
    public DeviceAddress axis2PosNumAddress = new DeviceAddress("2축 위치 결정 번호");
    public DeviceAddress axis3PosNumAddress = new DeviceAddress("3축 위치 결정 번호");
    public DeviceAddress axis1StopAddress = new DeviceAddress("1축 정지 신호");
    public DeviceAddress axis2StopAddress = new DeviceAddress("2축 정지 신호");
    public DeviceAddress axis3StopAddress = new DeviceAddress("3축 정지 신호");
    public DeviceAddress axis1RstAddress = new DeviceAddress("1축 리셋 신호");
    public DeviceAddress axis2RstAddress = new DeviceAddress("2축 리셋 신호");
    public DeviceAddress axis3RstAddress = new DeviceAddress("3축 리셋 신호");

    //피드백 신호
    public DeviceAddress axis1ReceivedAddress = new DeviceAddress("1축 기동 완료 피드백");
    public DeviceAddress axis2ReceivedAddress = new DeviceAddress("2축 기동 완료 피드백");
    public DeviceAddress axis3ReceivedAddress = new DeviceAddress("3축 기동 완료 피드백");
    public DeviceAddress axis1BusyAddress = new DeviceAddress("1축 BUSY 피드백");
    public DeviceAddress axis2BusyAddress = new DeviceAddress("2축 BUSY 피드백");
    public DeviceAddress axis3BusyAddress = new DeviceAddress("3축 BUSY 피드백");
    public DeviceAddress axis1ErrorAddress = new DeviceAddress("1축 ERROR 피드백");
    public DeviceAddress axis2ErrorAddress = new DeviceAddress("2축 ERROR 피드백");
    public DeviceAddress axis3ErrorAddress = new DeviceAddress("3축 ERROR 피드백");
    public DeviceAddress axis1CompletedAddress = new DeviceAddress("1축 위치 결정 완료 피드백");
    public DeviceAddress axis2CompletedAddress = new DeviceAddress("2축 위치 결정 완료 피드백");
    public DeviceAddress axis3CompletedAddress = new DeviceAddress("3축 위치 결정 완료 피드백");
    private bool haveToExcuteAxis1; //1축 기동 명령을 받은 상태 여부.
    private int axis1Positioning;   //1축 위치 결정 번호
    private bool completedAxis1Positioning; //1축 위치 결정 완료 여부
    private float remainAxis1Completed;  //1축 위치 결정 완료 OFF 시간 저장.
    private bool haveToExcuteAxis2; //2축 기동 명령을 받은 상태 여부.
    private int axis2Positioning;   //2축 위치 결정 번호
    private bool completedAxis2Positioning; //2축 위치 결정 완료 여부
    private float remainAxis2Completed;  //2축 위치 결정 완료 OFF 시간 저장.
    private bool haveToExcuteAxis3; //3축 기동 명령을 받은 상태 여부.
    private int axis3Positioning;   //3축 위치 결정 번호
    private bool completedAxis3Positioning; //3축 위치 결정 완료 여부
    private float remainAxis3Completed;  //3축 위치 결정 완료 OFF 시간 저장.

    //프로퍼티
    private bool axis1Busy;
    public bool Axis1BUSY
    {
        get => axis1Busy;
        set
        {
            if (axis1Busy == value)
                return;

            axis1Busy = value;
            if (axis1BusyAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis1BusyAddress.address, (short)(value ? 1 : 0));
        }
    }

    private bool axis2Busy;
    public bool Axis2BUSY
    {
        get => axis2Busy;
        set
        {
            if (axis2Busy == value)
                return;

            axis2Busy = value;
            if (axis2BusyAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis2BusyAddress.address, (short)(value ? 1 : 0));
        }
    }

    private bool axis3Busy;
    public bool Axis3BUSY
    {
        get => axis3Busy;
        set
        {
            if (axis3Busy == value)
                return;

            axis3Busy = value;
            if (axis3BusyAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis3BusyAddress.address, (short)(value ? 1 : 0));
        }
    }

    private bool axis1Error;
    public bool Axis1ERROR
    {
        get => axis1Error;
        set
        {
            if (axis1Error == value)
                return;

            axis1Error = value;
            if (axis1ErrorAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis1ErrorAddress.address, (short)(value ? 1 : 0));
        }
    }
    private bool axis2Error;
    public bool Axis2ERROR
    {
        get => axis2Error;
        set
        {
            if (axis2Error == value)
                return;

            axis2Error = value;
            if (axis2ErrorAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis2ErrorAddress.address, (short)(value ? 1 : 0));
        }
    }

    private bool axis3Error;
    public bool Axis3ERROR
    {
        get => axis3Error;
        set
        {
            if (axis3Error == value)
                return;

            axis3Error = value;
            if (axis3ErrorAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis3ErrorAddress.address, (short)(value ? 1 : 0));
        }
    }

    public void OnCompletedAxis1Positioning()
    {
        completedAxis1Positioning = true;
        remainAxis1Completed = Time.time + feedbackTime;
        if (axis1CompletedAddress.useDevice)
            MXRequester.Get.AddSetDeviceRequest(axis1CompletedAddress.address, 1);
    }
    public void OnCompletedAxis2Positioning()
    {
        completedAxis2Positioning = true;
        remainAxis2Completed = Time.time + feedbackTime;
        if (axis2CompletedAddress.useDevice)
            MXRequester.Get.AddSetDeviceRequest(axis2CompletedAddress.address, 1);
    }
    public void OnCompletedAxis3Positioning()
    {
        completedAxis3Positioning = true;
        remainAxis3Completed = Time.time + feedbackTime;
        if (axis3CompletedAddress.useDevice)
            MXRequester.Get.AddSetDeviceRequest(axis3CompletedAddress.address, 1);
    }

    void Start()
    {
        if (plcReadyAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(plcReadyAddress.address, PLCReady);
        if (servoAllOnAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(servoAllOnAddress.address, ServoAllOn);

        if (axis1JogFWDAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis1JogFWDAddress.address, Axis1JogFWD);
        if (axis1JogRVSAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis1JogRVSAddress.address, Axis1JogRVS);
        if (axis2JogFWDAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis2JogFWDAddress.address, Axis2JogFWD);
        if (axis2JogRVSAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis2JogRVSAddress.address, Axis2JogRVS);
        if (axis3JogFWDAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis3JogFWDAddress.address, Axis3JogFWD);
        if (axis3JogRVSAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis3JogRVSAddress.address, Axis3JogRVS);

        if (axis1PosAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis1PosAddress.address, StartAxis1Positioning);
        if (axis2PosAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis2PosAddress.address, StartAxis2Positioning);
        if (axis3PosAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis3PosAddress.address, StartAxis3Positioning);

        if (axis1PosNumAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis1PosNumAddress.address, SetAxis1Positioning);
        if (axis2PosNumAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis2PosNumAddress.address, SetAxis2Positioning);
        if (axis3PosNumAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis3PosNumAddress.address, SetAxis3Positioning);

        if (axis1StopAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis1StopAddress.address, StopAxis1);
        if (axis2StopAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis2StopAddress.address, StopAxis2);
        if (axis3StopAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis3StopAddress.address, StopAxis3);

        if (axis1RstAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis1RstAddress.address, ResetAxis1);
        if (axis2RstAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis2RstAddress.address, ResetAxis2);
        if (axis3RstAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(axis3RstAddress.address, ResetAxis3);
    }

    private void PLCReady(short obj)
    {
        Debug.Log($"[{plcReadyAddress.address}] PLC {(obj != 0 ? "" : "Not")} Ready!!!");

        if(moduleReady.useDevice)
        {
            MXRequester.Get.AddSetDeviceRequest(moduleReady.address, obj);
            Debug.Log($"[{moduleReady.address}] Module {(obj != 0 ? "" : "Not")} Ready!!!");
        }
    }

    private void ServoAllOn(short obj)
    {
        if (servoAllReadyAddress.useDevice)
            MXRequester.Get.AddSetDeviceRequest(servoAllReadyAddress.address, obj);

        axis1.ServoOn(obj != 0);
        axis2.ServoOn(obj != 0);
        axis3.ServoOn(obj != 0);
    }

    private void Axis1JogFWD(short obj)
    {
        axis1.JogForward(obj != 0);
    }

    private void Axis1JogRVS(short obj)
    {
        axis1.JogReverse(obj != 0);
    }

    private void Axis2JogFWD(short obj)
    {
        axis2.JogForward(obj != 0);
    }

    private void Axis2JogRVS(short obj)
    {
        axis2.JogReverse(obj != 0);
    }

    private void Axis3JogFWD(short obj)
    {
        axis3.JogForward(obj != 0);
    }

    private void Axis3JogRVS(short obj)
    {
        axis3.JogReverse(obj != 0);
    }

    private void StartAxis1Positioning(short obj)
    {
        if(obj != 0)
        {
            haveToExcuteAxis1 = true;
        }
        else
        {
            if(axis1ReceivedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis1ReceivedAddress.address, 0);
        }
    }

    private void StartAxis2Positioning(short obj)
    {
        if (obj != 0)
        {
            haveToExcuteAxis2 = true;
        }
        else
        {
            if (axis2ReceivedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis2ReceivedAddress.address, 0);
        }
    }

    private void StartAxis3Positioning(short obj)
    {
        if (obj != 0)
        {
            haveToExcuteAxis3 = true;
        }
        else
        {
            if (axis3ReceivedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis3ReceivedAddress.address, 0);
        }
    }

    private void SetAxis1Positioning(short obj)
    {
        axis1Positioning = obj;
    }

    private void SetAxis2Positioning(short obj)
    {
        axis2Positioning = obj;
    }

    private void SetAxis3Positioning(short obj)
    {
        axis3Positioning = obj;
    }

    private void StopAxis1(short obj)
    {
        axis1.IsStopped = obj != 0;
    }

    private void StopAxis2(short obj)
    {
        axis2.IsStopped = obj != 0;
    }

    private void StopAxis3(short obj)
    {
        axis3.IsStopped = obj != 0;
    }

    private void ResetAxis1(short obj)
    {
        if (obj != 0)
            axis1.ErrorReset();
    }

    private void ResetAxis2(short obj)
    {
        if (obj != 0)
            axis2.ErrorReset();
    }

    private void ResetAxis3(short obj)
    {
        if (obj != 0)
            axis3.ErrorReset();
    }

    void Update()
    {
        if(haveToExcuteAxis1 && axis1Positioning != 0)
        {
            if(!axis1.OPRComplete && axis1Positioning == 9001)
            {
                //원점찾기
                axis1.Homing();
            }
            else if(axis1.OPRComplete && axis1Positioning == 9002)
            {
                //원점 고속 복귀.
                axis1.Homing();
            }
            else
            {
                axis1.Positioning(manager.positionList[axis1Positioning - 1].x);
            }

            haveToExcuteAxis1 = false;
            if (axis1ReceivedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis1ReceivedAddress.address, 1);
        }
        if (haveToExcuteAxis2 && axis2Positioning != 0)
        {
            if (!axis2.OPRComplete && axis2Positioning == 9001)
            {
                //원점찾기
                axis2.Homing();
            }
            else if (axis2.OPRComplete && axis2Positioning == 9002)
            {
                //원점 고속 복귀.
                axis2.Homing();
            }
            else
            {
                axis2.Positioning(manager.positionList[axis2Positioning - 1].y);
            }

            haveToExcuteAxis2 = false;
            if (axis2ReceivedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis2ReceivedAddress.address, 1);
        }
        if (haveToExcuteAxis3 && axis3Positioning != 0)
        {
            if (!axis3.OPRComplete && axis3Positioning == 9001)
            {
                //원점찾기
                axis3.Homing();
            }
            else if (axis3.OPRComplete && axis3Positioning == 9002)
            {
                //원점 고속 복귀.
                axis3.Homing();
            }
            else
            {
                axis3.Positioning(manager.positionList[axis3Positioning - 1].z);
            }

            haveToExcuteAxis3 = false;
            if (axis3ReceivedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis3ReceivedAddress.address, 1);
        }

        if(completedAxis1Positioning && remainAxis1Completed < Time.time)
        {
            if (axis1CompletedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis1CompletedAddress.address, 0);

            completedAxis1Positioning = false;
        }
        if (completedAxis2Positioning && remainAxis2Completed < Time.time)
        {
            if (axis2CompletedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis2CompletedAddress.address, 0);

            completedAxis2Positioning = false;
        }
        if (completedAxis3Positioning && remainAxis3Completed < Time.time)
        {
            if (axis3CompletedAddress.useDevice)
                MXRequester.Get.AddSetDeviceRequest(axis3CompletedAddress.address, 0);

            completedAxis3Positioning = false;
        }
    }
}
