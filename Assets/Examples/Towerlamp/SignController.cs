using UnityEngine;
using UnityEngine.Events;

public class SignController : MonoBehaviour
{
    public MeshRenderer meshRenderer;
    public UnityEvent<bool> onChangedValue;

    private bool isOn = false;
    
    //프로퍼티
    public bool IsOn
    {
        get => isOn;
        set
        {
            if (isOn == value)
                return;

            isOn = value;
            TurnOn(value);
            onChangedValue?.Invoke(value);
        }
    }

    private void TurnOn(bool isOn)
    {
        if (meshRenderer == null)
            return;

        if(isOn)
            meshRenderer.material.EnableKeyword("_EMISSION");
        else
            meshRenderer.material.DisableKeyword("_EMISSION");
    }


    void Start()
    {
        if(meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();


        //시작 상태와 동일한 램프 상태로 만들기
        if(meshRenderer != null)
        {
            TurnOn(isOn);
        }
    }
}
