using System;
using UnityEngine;
using UnityEngine.UI;

public class AGVTestUI : MonoBehaviour
{
    public InputField field;
    public InputField liftField;
    public InputField liftField2;
    public Button goButton;
    public Button liftButton;
    public Button liftButton2;

    public AGVController controller;
    public AGVLiftController lift;
    
    void Start()
    {
        goButton.onClick.AddListener(Go);
        liftButton.onClick.AddListener(Lift);
        liftButton2.onClick.AddListener(Lift2);
    }

    private void Lift2()
    {
        Debug.Log("Lift 버튼 눌림");
        lift.Go(float.Parse(liftField2.text));
    }

    private void Lift()
    {
        Debug.Log("Lift 버튼 눌림");
        lift.Go(int.Parse(liftField.text));
    }

    private void Go()
    {
        Debug.Log("Go 버튼 눌림");
        controller.Go(int.Parse(field.text));
    }

}
