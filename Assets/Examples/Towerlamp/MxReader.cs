using System;
using UnityEngine;
using UnityEngine.Events;

public class MxReader : MXObject
{
    public DeviceAddress address;
    public UnityEvent<bool> onChangedValue;

    private bool isOn;
    public bool IsOn
    {
        get => isOn;
        set
        {
            if (isOn == value)
                return;

            isOn = value;
            onChangedValue?.Invoke(value);
        }
    }

    private void Start()
    {
        if(address.useDevice)
        {
            MXRequester.Get.AddDeviceAddress(address.address, ReceiveValue);
        }
    }

    private void ReceiveValue(short value)
    {
        IsOn = value != 0;
    }
}
