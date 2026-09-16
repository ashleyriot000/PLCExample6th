using UnityEngine;
using UnityEngine.Splines;

public class ChainConnector : MonoBehaviour
{
    public bool connectTrigger;
    private SplineContainer container;
    public bool lockX = false;
    public bool lockY = false;
    public bool lockZ = false;
    public Vector3 lockPosition;


#if UNITY_EDITOR
    private void OnValidate()
    {
        container = GetComponent<SplineContainer>();
        container.Spline.Clear();
        int count = transform.childCount;
        for (int i = 0; i < count; ++i)
        {
            var child = transform.GetChild(i);
            var position = child.position;
            if (lockX)
                position.x = lockPosition.x;
            if (lockY)
                position.y = lockPosition.y;
            if (lockZ)
                position.z = lockPosition.z;

            var knot = new BezierKnot(position);
            container.Spline.Add(knot);
        }
        container.Spline.Closed = true;
    }
#endif
}
