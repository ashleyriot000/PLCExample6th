using System;
using UnityEngine;
using UnityEngine.Events;

public class ACMotorConnector : MXObject
{
    public ACMotorController controller;
    public DeviceAddress forwardAddress = new("모터 정회전 신호");
    public DeviceAddress reverseAddress = new("모터 역회전 신호");

    public UnityEvent<bool> onChangedForward;
    public UnityEvent<bool> onChangedReverse;

    
    void Start()
    {
        if (controller == null)
        {
            controller = GetComponent<ACMotorController>();
        }

        if (forwardAddress.useDevice)
        {
            MXRequester.Get.AddDeviceAddress(forwardAddress.address, OnChangedForward);
        }

        if (reverseAddress.useDevice)
        {
            MXRequester.Get.AddDeviceAddress(reverseAddress.address, OnChangedReverse);
        }
    }

    private void OnChangedReverse(short obj)
    {
        controller.IsOnBackward = obj != 0;
        onChangedReverse?.Invoke(obj != 0);
    }

    private void OnChangedForward(short obj)
    {
        controller.IsOnForward = obj != 0;
        onChangedForward?.Invoke(obj != 0);
    }
}
