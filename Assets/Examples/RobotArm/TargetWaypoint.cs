using Preliy.Flange;
using UnityEngine;

public class TargetWaypoint : MonoBehaviour
{
    [Header("Joint Settings at this Point")]
    public float[] joints = new float[6];

    public Vector3 position;
    public Quaternion rotation;

    private TargetScheduler scheduler;

    public void Initialize(TargetScheduler scheduler, Vector3 position, Quaternion rotation, float[] joints)
    {
        this.scheduler = scheduler;
        this.joints = joints;
        this.position = position;
        this.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
    }

    private void Reset()
    {
        this.position = transform.position;
        this.rotation = transform.rotation;
        var solution = scheduler.GetSolution(transform.GetMatrix());

        this.joints = solution.JointTarget.Value;
    }
}
