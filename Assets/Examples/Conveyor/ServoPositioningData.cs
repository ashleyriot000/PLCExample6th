using System.Collections.Generic;
using UnityEngine;

//1. 동작패턴
public enum OperationPattern
{
    End,        //지정된 위치로 이동한 뒤, 해당 스텝을 완전히 종료.
    Continue,   //지정된 위치로 이동한 뒤, 멈추지 않고 다음 스텝의 목표 속도로 변속하며 다음 스텝으로 전환.
    Location    //지정된 위치로 이동한 뒤, 멈추고 Dwell 시간동한 정지후에 다음 스텝으로 전환.
}

//2. 제어 방식
public enum ControlMethod
{
    ABS_Linear, //절대 위치값으로 이동하는 방식. 위치에 도착하면 종료.
    INC_Linear, //상대 위치값으로 이동하는 방식. 위치에 도착하면 종료.
    Forward_Speed,  //위치가 아닌 속도(정회전)로만 제어하는 방식. 해당 속도에 도달 후 무한 유지.
    Reverse_Speed   //위치가 아닌 속도(역회전)로만 제어하는 방식. 해당 속도에 도달 후 무한 유지.
}

[System.Serializable]
public struct PositioningData
{
    public OperationPattern pattern;
    public ControlMethod method;
    public float positioningAddress;
    public float commandSpeed;
    public float dwellTime;
    public ushort mCode;
}

[System.Serializable]
public class AxisData
{
    public string axisName = "new Axis";
    public List<PositioningData> stepDataList = new List<PositioningData>();
}

[CreateAssetMenu(fileName = "new PositioningData", menuName = "DigitalTwin/PositioningData")]
public class ServoPositioningData : ScriptableObject
{
    public List<AxisData> axes = new List<AxisData>();
}
