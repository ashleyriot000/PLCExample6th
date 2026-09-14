using UnityEngine;

public class RodController : MonoBehaviour
{
    public ConfigurableJoint joint;
    public Vector3 forwardPosition;
    public Vector3 backwardPosition;

    private bool isOnForward;
    public bool IsOnForward
    {
        get => isOnForward;
        set
        {
            if (isOnForward == value)
                return;

            if (isOnForward = value)
            {
                if (isOnBackward == false)
                {
                    joint.targetPosition = forwardPosition;
                }
            }
            else
            {
                if (isOnBackward == true)
                {
                    joint.targetPosition = backwardPosition;
                }
            }
        }
    }

    private bool isOnBackward;
    public bool IsOnBackward
    {
        get => isOnBackward;
        set
        {
            if (isOnBackward == value)
                return;

            if(isOnBackward = value)
            {
                if(isOnForward == false)
                {
                    joint.targetPosition = backwardPosition;
                }
            }
            else
            {
                if (isOnForward == true)
                {
                    joint.targetPosition = forwardPosition;
                }
            }
        }
    }
}
