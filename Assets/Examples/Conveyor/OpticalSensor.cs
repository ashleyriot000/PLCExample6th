using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

public class OpticalSensor : MonoBehaviour
{
    [Header("감지 조건")]
    public LayerMask detectableMask;        //감지 가능한 레이어 설정
    public string detectableTag;            //감지 가능한 태그 설정
    public string detectableName;           //감지 가능한 이름 설정
    public float detectableDistance;        //감지 가능한 거리

    [Header("선택사항")]
    public float lineWidth = 0.01f;         //레이저 굵기
    public Material DefalutMaterial;        //기본 재질
    public Material DetectedMaterial;       //감지시 재질.

    private LineRenderer line;              //레이저 렌더

    public UnityEvent<bool> onChangedDetected;      //감지 변화에 대한 콜백함수들을 담는 델리게이트
    private Vector3 detectedPoint;

    private bool hasDetected;
    public bool HasDetected
    {
        get => hasDetected;
        set
        {
            if (hasDetected == value)
                return;

            hasDetected = value;
            onChangedDetected?.Invoke(value);
        }
    }

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (line == null)
            return;

        line.useWorldSpace = true;
        line.SetPositions(new Vector3[2]
        {
            transform.position,
            transform.position + transform.forward *  detectableDistance
        });
        line.material = DefalutMaterial;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
    }

    void FixedUpdate()
    {
        //레이저 빛 알갱이 준비
        Ray ray = new Ray(transform.position, transform.forward);
        if(Physics.Raycast(ray, out RaycastHit hit, detectableDistance, detectableMask))
        {
            detectedPoint = hit.point;    

            if(!string.IsNullOrEmpty(detectableTag) && 
                hit.transform.gameObject.tag != detectableTag)
            {
                HasDetected = false;
                if (line != null)
                {
                    line.SetPosition(0, transform.position);
                    line.SetPosition(1, transform.position + transform.forward * detectableDistance);
                    line.material = DefalutMaterial;
                }

                return;
            }

            if (!string.IsNullOrEmpty(detectableName) && 
                !hit.transform.gameObject.name.Contains(detectableName))
            {
                HasDetected = false;

                if (line != null)
                {
                    line.SetPosition(0, transform.position);
                    line.SetPosition(1, transform.position + transform.forward * detectableDistance);
                    line.material = DefalutMaterial;
                }
                return;
            }

            HasDetected = true;
            if (line != null)
            {
                line.SetPosition(0, transform.position);
                line.SetPosition(1, detectedPoint);
                line.material = DetectedMaterial;
            }
        }
        else
        {
            HasDetected = false;
            if (line != null)
            {
                line.SetPosition(0, transform.position);
                line.SetPosition(1, transform.position + transform.forward * detectableDistance);
                line.material = DefalutMaterial;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if(hasDetected)
        {
            //감지되면 붉은 색 라인으로 표시하기
            Handles.color = Color.red;
            Handles.DrawLine(transform.position, detectedPoint);
        }
        else
        {
            //감지 못하면 녹색 라인으로 최대 감지 범위까지 표시하기
            Handles.color = Color.green;
            Handles.DrawLine(transform.position,
                transform.position + transform.forward * detectableDistance);
        }
    }




#endif
}
