using UnityEngine;

public class WeldingTool : ToolParent
{
    public GameObject effect;
    protected override void OnChangedValue(bool isOn)
    {
        if (effect == null)
            return;

        effect.SetActive(isOn);
    }
}
