using UnityEngine;

public class RouteMover : MonoBehaviour
{
    public enum MoveType
    {
        None,
        Once,
        Loop,
        Roundtrip,
        Max
    }

    public Transform[] destinations;
    public MoveType mType = MoveType.Once;
    public int repeatCount = 0;
    public float moveSpeed = 3f;
    public bool isAscending;

    private int index;
    private int remainCount;

    private bool isDone;

    private void Start()
    {
        if(destinations.Length > 1)
        {
            transform.position = isAscending ? 
                destinations[0].position : destinations[destinations.Length - 1].position;

            index = isAscending ? 1 : destinations.Length - 2;
            

            remainCount = repeatCount == 0 ? -1 : repeatCount;
        }
    }

    private int Repeat(int value, int length)
    {
        return length == 0 ? 0 : (value % length + length) % length;
    }

    private int Roundtrip(int value, int length)
    {
        if (isAscending && value >= length)
        {
            isAscending = false;
            return length - 1;
        }
        else if (!isAscending && value < 0)
        {
            isAscending = true;
            return 1;
        }

        return value;
    }

    private void Update()
    {
        if (destinations.Length < 2)
            return;

        if (isDone)
            return;

        transform.position = Vector3.MoveTowards(transform.position, destinations[index].position, moveSpeed * Time.deltaTime);
        if(transform.position == destinations[index].position)
        {
            if (mType != MoveType.Once && index == 0 && remainCount == 0)
            {
                isDone = true;
                return;
            }

            index = mType switch
            {
                MoveType.Once => isAscending ? index + 1 : index - 1,
                MoveType.Loop => Repeat(isAscending ? index + 1 : index - 1, destinations.Length),
                MoveType.Roundtrip => Roundtrip(isAscending ? index + 1 : index - 1, destinations.Length),
                _ => -1
            };

            if (isAscending && index >= destinations.Length || !isAscending && index < 0)
            {
                isDone = true;
                return;
            }

            if (repeatCount == 0)
                return;

            if(index == 0)
            {
                remainCount--;
            }
        }
    }
}
