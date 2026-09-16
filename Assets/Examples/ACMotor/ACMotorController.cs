using UnityEngine;
using UnityEngine.Events;

public class ACMotorController : MonoBehaviour
{
    public HingeJoint shaft;        //회전축을 회전시키기 위해 필요.
    public Rigidbody rb;            //회전 저항을 수정하기 위해서 필요.

    public Vector3 torqueAxis = Vector3.forward;    //회전 방향축 설정.
    [Delayed] public float targetVelocity = 360f;    //초당 목표 회전 속도.
    [Delayed] public float torque = 1f;             //회전에 적용되는 토크값.
    [Delayed] public float damping = 0.01f;         //회전 저항값.

    public UnityEvent<bool> onChangedForward;
    public UnityEvent<bool> onChangedBackward;

    private bool isOnForward = false;       //정방향 전류On
    public bool IsOnForward
    {
        get => isOnForward;
        set
        {
            if(isOnForward == value) return;

            if(isOnForward = value)
            {
                isOnBackward = false;
                onChangedBackward?.Invoke(false);
                shaft.useMotor = true;
                shaft.axis = torqueAxis;
            }
            else if(isOnForward == isOnBackward)
            {
                shaft.useMotor = false;
            }
            onChangedForward?.Invoke(value);
        }
    }

    private bool isOnBackward = false;
    public bool IsOnBackward
    {
        get => isOnBackward;
        set
        {
            if (isOnBackward == value) return;

            if (isOnBackward = value)
            {
                isOnForward = false;
                onChangedForward?.Invoke(false);
                shaft.useMotor = true;
                shaft.axis = torqueAxis * -1f;
            }
            else if (isOnForward == isOnBackward)
            {
                shaft.useMotor = false;
            }

            onChangedBackward?.Invoke(value);
        }
    }

    private void Awake()
    {
        if(shaft == null)
        {
            shaft = GetComponent<HingeJoint>();
        }

        if(shaft != null)
        {
            shaft.motor = new JointMotor()
            {
                targetVelocity = this.targetVelocity,
                force = this.torque,
                freeSpin = true
            };
            shaft.axis = torqueAxis;
        }

        if(rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if(rb != null)
        {
            rb.angularDamping = damping;
        }
    }
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (shaft == null)
            return;

        shaft.motor = new JointMotor()
        {
            targetVelocity = this.targetVelocity,
            force = this.torque,
            freeSpin = true
        };
    }
#endif
}
