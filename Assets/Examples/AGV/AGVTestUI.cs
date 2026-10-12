using UnityEngine;
using UnityEngine.UI;

public class AGVTestUI : MonoBehaviour
{
    public InputField field;
    public Button goButton;
    public AGVController controller;
    
    void Start()
    {
        goButton.onClick.AddListener(Go);
    }

    private void Go()
    {
        Debug.Log("Go 버튼 눌림");
        controller.Go(int.Parse(field.text));
    }

}
