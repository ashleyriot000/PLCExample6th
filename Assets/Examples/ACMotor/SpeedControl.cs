using UnityEngine;
using UnityEngine.UI;

public class SpeedControl : MonoBehaviour
{
    public ShaftController shaft;
    public Text rpmText;

    public void SetRPM(float value)
    {
        if (shaft == null)
            return;

        shaft.SetRPM(value);
        rpmText.text = $"RPM : {value}";
    }

}
