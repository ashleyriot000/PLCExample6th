using UnityEngine.Events;

public class ConveyorConnector : MXObject
{
    public ConveyorController controller;
    public DeviceAddress forwardAddress = new DeviceAddress("컨베이어 정회전 신호");
    public DeviceAddress reverseAddress = new DeviceAddress("컨베이어 역회전 신호");

    public UnityEvent<bool> onChangedForward;
    public UnityEvent<bool> onChangedReverse;
    
    void Start()
    {
        if (forwardAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(forwardAddress.address, OnChangedForward);

        if (reverseAddress.useDevice)
            MXRequester.Get.AddDeviceAddress(reverseAddress.address, OnChangedReverse);

        if (controller == null)
            controller = GetComponent<ConveyorController>();
    }

    private void OnChangedReverse(short obj)
    {
        controller.IsOnForward = obj != 0;
        onChangedForward?.Invoke(obj != 0);
    }

    private void OnChangedForward(short obj)
    {
        controller.IsOnReverse = obj != 0;
        onChangedReverse?.Invoke(obj != 0);
    }
}
