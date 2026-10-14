using Preliy.Flange;

public abstract class ToolParent : Tool
{
    private bool _isOn = false;

    public bool IsOn
    {
        get => _isOn;
        set
        {
            if (_isOn == value)
                return;

            OnChangedValue(_isOn = value);
        }
    }

    protected abstract void OnChangedValue(bool isOn);
}
