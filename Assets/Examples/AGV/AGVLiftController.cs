using UnityEngine;
using UnityEngine.Events;

public class AGVLiftController : MonoBehaviour
{
    public Transform lift;
    public Transform[] floors;
    public float offset;
    public float liftSpeed = 1.0f;
    public float liftDuration = 1.0f;

    private int index;
    private float height;
    private bool useAbsoluteHeight;
    private bool isArrival = true;
    private Vector3 startPos;
    private float t = 0f;

    public UnityEvent onCompletedLift;

    private void FixedUpdate()
    {
        if (isArrival)
            return;

        if(useAbsoluteHeight)
        {
            Vector3 destination = lift.localPosition;
            destination.y = height - offset;
            lift.localPosition = Vector3.MoveTowards(lift.localPosition, destination, liftSpeed * Time.fixedDeltaTime);

            if(transform.localPosition == destination)
            {
                isArrival = true;
            }
        }
        else
        {
            t += Time.fixedDeltaTime;
            Vector3 destination = lift.localPosition;
            destination.y = floors[index].localPosition.y;
            lift.localPosition = Vector3.Lerp(startPos, destination, t / liftDuration);

            if(lift.localPosition == destination)
            {
                isArrival = true;
                onCompletedLift?.Invoke();
                Debug.Log($"{index}층에 도착했습니다.");
            }
        }
    }


    public void Go(int floor)
    {
        isArrival = false;
        useAbsoluteHeight = false;
        index = floor;
        startPos = lift.localPosition;
        t = 0f;
    }

    public void Go(float height)
    {
        isArrival = false;
        useAbsoluteHeight = true;
        this.height = height;
    }
}
