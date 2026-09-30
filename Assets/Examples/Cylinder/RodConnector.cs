using System;
using UnityEngine;
using UnityEngine.Events;

public class RodConnector : MXObject
{
    public RodController controller;
    public DeviceAddress forwardAddress = new ("실린더 전진 신호");
    public DeviceAddress reverseAddress = new ("실린더 후퇴 신호");

    public UnityEvent<bool> onChangedForward;
    public UnityEvent<bool> onChangedReverse;

    private void Start()
    {
        if(forwardAddress.useDevice)
        {
            MXRequester.Get.AddDeviceAddress(forwardAddress.address, Forward);
        }

        if(reverseAddress.useDevice)
        {
            MXRequester.Get.AddDeviceAddress(reverseAddress.address, Reverse);
        }

        if(controller == null)
        {
            controller = GetComponent<RodController>();
        }
    }

    private void Reverse(short obj)
    {
        controller.IsOnBackward = obj != 0;
        onChangedReverse?.Invoke(obj != 0);
    }

    private void Forward(short obj)
    {
        controller.IsOnForward = obj != 0;
        onChangedForward?.Invoke(obj != 0);
    }
}
