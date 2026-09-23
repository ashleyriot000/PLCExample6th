using UnityEngine;
using UnityEngine.InputSystem;

public class CursorChanger : MonoBehaviour
{
    public Texture2D leftCursor;
    public Vector2 leftHotSpot;
    public Texture2D rightCursor;
    public Vector2 rightHotSpot;
    public Texture2D middleCursor;
    public Vector2 middleHotSpot;

    public void OnOrbit(InputValue value)
    {
        Cursor.SetCursor(value.isPressed ? leftCursor : null, leftHotSpot, CursorMode.Auto);
    }

    public void OnFreelook(InputValue value)
    {
        Cursor.SetCursor(value.isPressed ? rightCursor : null, rightHotSpot, CursorMode.Auto);
    }

    public void OnPan(InputValue value)
    {
        Cursor.SetCursor(value.isPressed ? middleCursor : null, middleHotSpot, CursorMode.Auto);
    }
}
