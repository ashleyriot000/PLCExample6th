using UnityEngine;
using UnityEngine.UI;


public class UnitDisplayer : MonoBehaviour
{
    public Text pulseText;
    public Text unitText;

    public void GetUnit(float unit)
    {
        unitText.text = unit.ToString();
    }

    public void GetPulse(int pulse)
    {
        pulseText.text = pulse.ToString();
    }


}
