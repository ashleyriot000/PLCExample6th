using System;
using UnityEngine;

public class GripperConnector : MXObject
{
    public Gripper controller;
    public DeviceAddress grabAddress = new DeviceAddress("잡기 ON/OFF");

    void Start()
    {
        if (controller == null)
            controller = GetComponent<Gripper>();

        if (grabAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(grabAddress.address, Grab);
    }

    private void Grab(short obj)
    {
        controller.Grab(obj != 0);
    }
}
