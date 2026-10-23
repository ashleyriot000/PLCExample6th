using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class Rotater : MonoBehaviour
{
    public Vector3 rotateAxis = Vector3.up;
    public float rotateSpeed = 360f;

    public bool needRotate = false;

    public Text speedText;


    //처음 시작할 때 한번만 호출.
    void Start()
    {
        Debug.Log($"{gameObject.name}이고, {transform.position}({transform.localPosition})");
    }

    //매프레임마다 한번씩 호출.
    void Update()
    {   
        if(needRotate)
        {
            transform.Rotate(rotateAxis * rotateSpeed * Time.deltaTime);
        }
    }

    public void SetRotateSpeed(float speed)
    {
        rotateSpeed = speed;
        speedText.text = $"카메라 회전 속도 : {speed}˚";
    }

}