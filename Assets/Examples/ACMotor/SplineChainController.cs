using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class SplineChainController : MonoBehaviour
{
    public enum UpdateType
    {
        None,
        FixedUpdate,
        Update,
        LateUpdate
    }

    [Header("연결 요소")]
    [SerializeField] private SplineContainer container;
    [SerializeField] private GameObject[] linkPrefabs;
    [SerializeField] private Transform[] connectedGears;
    [SerializeField] private Transform connectedShaft;
    [SerializeField] private Vector3 connectedGearRatio;
    [SerializeField] private UpdateType uType = UpdateType.FixedUpdate;


    [Header("기어 및 체인 설정")]
    [SerializeField, Delayed] private int gearTeethCount = 28;
    [SerializeField, Delayed] private float linkLength = 0.5f;
    [SerializeField, Delayed] private Vector3 rotateAxis = Vector3.forward;
    [SerializeField, Delayed] private Vector3 chainDirection = Vector3.forward;
    [SerializeField, Delayed] private float motorRotationSpeed = 90f;


    private List<Transform> spawnedLinks = new List<Transform>();
    private float gearCircumReferencePerRotation;
    private float distancePerDegree;
    private float currentDistanceOffset;
    private float totalSplineLength;
    private int totalLinkCount;
    private Vector3 prevRotation;

    private void Start()
    {
        if(container == null || linkPrefabs == null || linkPrefabs.Length < 2)
        {
            Debug.LogError("필수 요소가 제대로 세팅되지 않았습니다.");
            return;
        }

        gearCircumReferencePerRotation = gearTeethCount * linkLength;
        distancePerDegree = gearCircumReferencePerRotation / 360f;
        totalSplineLength = container.CalculateLength();
        totalLinkCount = Mathf.FloorToInt(totalSplineLength / linkLength);
        SpawnChain();
        UpdateChainPositions();

        if(connectedShaft != null)
        {
            prevRotation = connectedShaft.eulerAngles;
        }
    }

    private void SpawnChain()
    {
        for (int i = 0; i < totalLinkCount; i++)
        {
            GameObject chain = Instantiate(linkPrefabs[i % linkPrefabs.Length], container.transform);
            spawnedLinks.Add(chain.transform);
        }
    }

    private void UpdateChainPositions()
    {
        for (int i = 0; i < spawnedLinks.Count; ++i)
        {
            float targetDistance = (i * linkLength) + currentDistanceOffset;
            targetDistance = ((targetDistance % totalSplineLength) + totalSplineLength) % totalSplineLength;

            float t = targetDistance / totalSplineLength;

            //SplineContainer 컴포넌트는 기본적으로 로컬 스페이스 기준으로 좌표를 반환한다.
            Vector3 localPosition = container.EvaluatePosition(t);
            Vector3 localForward = container.EvaluateTangent(t);
            Vector3 up = Vector3.Cross(chainDirection, localForward).normalized;

            spawnedLinks[i].localPosition = localPosition;
            if(localForward != Vector3.zero)
            {
                spawnedLinks[i].localRotation = Quaternion.LookRotation(localForward, up);
            }
        }
    }

    private void FixedUpdate()
    {
        if (uType != UpdateType.FixedUpdate)
            return;

        CalculateAndRotate(Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (uType != UpdateType.Update)
            return;

        CalculateAndRotate(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (uType != UpdateType.LateUpdate)
            return;

        CalculateAndRotate(Time.deltaTime);
    }

    private void CalculateAndRotate(float deltaTime)
    {
        float movedDistance;
        float rotationAmount;

        //회전량 계산
        if (connectedShaft != null)
        {
            Vector3 currentRotation = connectedShaft.eulerAngles;
            Vector3 deltaRotation;
            deltaRotation.x = Mathf.DeltaAngle(prevRotation.x, currentRotation.x);
            deltaRotation.y = Mathf.DeltaAngle(prevRotation.y, currentRotation.y);
            deltaRotation.z = Mathf.DeltaAngle(prevRotation.z, currentRotation.z);

            //보정된 회전 차이값과 기어비를 내적(Dot)해서 최종 회전량을 산출
            rotationAmount = Vector3.Dot(deltaRotation, connectedGearRatio);
            prevRotation = currentRotation;

        }
        else
        {
            rotationAmount = motorRotationSpeed * deltaTime;
        }

        

        //모터 기어 회전
        foreach(var gear in connectedGears)
        {
            gear.Rotate(rotateAxis, rotationAmount);
        }

        //체인 이동 거리 계산
        movedDistance = rotationAmount * distancePerDegree;
        currentDistanceOffset = Mathf.Repeat(currentDistanceOffset + movedDistance, totalSplineLength);

        //체인의 실제 위치로 갱신
        UpdateChainPositions();
    }
}
