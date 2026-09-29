using UnityEngine;
using ActUtlType64Lib;
using UnityEditor.Experimental.GraphView;

public class LampConnector : MonoBehaviour
{
    private ActUtlType64 connector;
    public SignController green;
    public SignController yellow;
    public SignController red;

    private bool sButton;
    public bool StartButton
    {
        get => sButton;
        set
        {
            if (sButton == value)
                return;

            sButton = value;
            connector.WriteDeviceRandom2("X7", 1, (short)(value ? 1 : 0));
        }
    }

    private bool eButton;
    public bool EmergencyButton
    {
        get => eButton;
        set
        {
            if (eButton == value)
                return;

            eButton = value;
            connector.WriteDeviceRandom2("X8", 1, (short)(value ? 1 : 0));
        }
    }

    private bool connected;





    void Start()
    {
        connector = new ActUtlType64();
        connector.ActLogicalStationNumber = 1;
        int ret;
        if((ret = connector.Open()) == 0)
        {
            Debug.Log("PLC 시뮬레이터와 연결에 성공했습니다.");
            connected = true;
        }
        else
        {
            Debug.LogError($"PLC 시뮬레이터와 연결에 실패했습니다. => {ret:x16}");
        }
    }

    private void OnDestroy()
    {
        if(connector.Close() == 0)
        {
            Debug.Log("성공적으로 PLC 시뮬레이터와 연결해제했습니다.");
        }
        else
        {
            Debug.LogError("PLC 시뮬레이터와 연결해제에 실패했습니다.");
        }
    }

    short[] readValues = new short[3];
    void Update()
    {
        if (connected == false)
            return;
        int ret;
        if ((ret = connector.ReadDeviceRandom2("Y16\nY17\nY18", 3, out readValues[0])) == 0)
        {
            green.IsOn = readValues[0] != 0;
            yellow.IsOn = readValues[1] != 0;
            red.IsOn = readValues[2] != 0;
        }
        else
        {
            Debug.LogError($"읽기 실패 => {ret:x16}");
        }
    }
}
