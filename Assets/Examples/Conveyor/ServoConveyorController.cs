using realvirtual;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ServoConveyorController : MonoBehaviour
{
    public ServoPositioningData positioningData;
    public int axisNum = 0;
    public ConveyorBelt belt;

    public UnityEvent<int> onCompletedPositioning;

    private float stopScroll = 0f;
    private bool isServoOn = false;
    private bool isForward = true;
    private bool keepRotate = false;
    private int currentPosition = 0;

    private List<Rigidbody> triggerList = new List<Rigidbody>();

    void Update()
    {
        if(keepRotate == false)
        {
            if((isForward && belt.scroll >= stopScroll) || (!isForward && belt.scroll <= stopScroll))
            {
                belt.speed = 0f;
                if (currentPosition != 0)
                    onCompletedPositioning?.Invoke(
                        positioningData.axes[axisNum].stepDataList[currentPosition - 1].mCode);

                currentPosition = 0;
            }
        }
    }

    private void FixedUpdate()
    {
        foreach(Rigidbody r in triggerList)
        {
            r.MovePosition(r.position + transform.forward * belt.speed * Time.fixedDeltaTime);
        }
    }

    public void ServoOn(bool isON)
    {
        isServoOn = isON;
        if(isON == false)
        {
            belt.speed = 0f;
        }
    }

    public int StartPositioning(int positionNum)
    {
        if (!isServoOn)
            return 0;

        if(positionNum < 1)
        {
            Debug.LogError("잘못된 포지셔닝 번호를 입력했습니다.");
            return 0;
        }

        if(positionNum > positioningData.axes[axisNum].stepDataList.Count)
        {
            Debug.LogError("잘못된 포지셔닝 번호를 입력했습니다.");
            return 0;
        }

        currentPosition = positionNum;
        var data = positioningData.axes[axisNum].stepDataList[currentPosition - 1];

        if(data.method == ControlMethod.Forward_Speed || data.method == ControlMethod.Reverse_Speed)
        {
            keepRotate = true;
            if(data.method == ControlMethod.Forward_Speed)
            {
                isForward = true;
                belt.speed = data.commandSpeed;
            }
            else
            {
                isForward = false;
                belt.speed = -data.commandSpeed;
            }
        }
        else
        {
            keepRotate = false;
            stopScroll = belt.scroll + data.positioningAddress;

            if(data.positioningAddress > 0f)
            {
                isForward = true;
                belt.speed = data.commandSpeed;
            }
            else
            {
                isForward = false;
                belt.speed = -data.commandSpeed;
            }
        }

        return data.mCode;
    }

    public void Stop()
    {
        if (isServoOn == false)
            return;

        currentPosition = 0;
        keepRotate = false;
        belt.speed = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerList.Contains(other.attachedRigidbody))
            return;

        triggerList.Add(other.attachedRigidbody);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!triggerList.Contains(other.attachedRigidbody))
            return;

        triggerList.Remove(other.attachedRigidbody);
    }
}
