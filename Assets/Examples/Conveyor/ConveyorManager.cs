using UnityEngine;

public class ConveyorManager : MonoBehaviour
{
    public OpticalSensor[] sensors;
    public RollerConveyorController[] conveyors;

    private bool[] detectedList;
    private void Awake()
    {
        int count = sensors.Length;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            sensors[i].onChangedDetected.AddListener((x) => ChangeDetectedState(index, x));
        }

        detectedList = new bool[count];
    }


    private void ChangeDetectedState(int index, bool detected)
    {
        detectedList[index] = detected;
        int lastIndex = conveyors.Length - 1;

        for (int i = 0; i < lastIndex; i++)
        {
            conveyors[i].IsOnReverse = detectedList[i] && !detectedList[i + 1];
        }

        conveyors[lastIndex].IsOnReverse = !detectedList[lastIndex];
    }
}
