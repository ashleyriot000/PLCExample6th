using UnityEngine;
using UnityEngine.UI;

public class InverterDisplayer : MonoBehaviour
{
    public InverterController controller;
    public Text hzText;

    public void OnClickSTF()
    {
        controller.IsOnSTF = !controller.IsOnSTF;
    }

    public void OnClickSTR()
    {
        controller.IsOnSTR = !controller.IsOnSTR;
    }

    public void OnClickIncrease()
    {
        controller.ChangeFrequency(controller.targetHz + 10f);
    }

    public void OnClickDecrease()
    {
        controller.ChangeFrequency(controller.targetHz - 10f);
    }


    private void Start()
    {
        controller.onChangedHz.AddListener(DisplayCurrentHz);
    }

    public void DisplayCurrentHz(float hz)
    {
        hzText.text = $"{hz:f1}Hz";
    }
}
