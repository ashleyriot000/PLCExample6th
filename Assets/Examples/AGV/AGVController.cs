
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class AGVController : MonoBehaviour
{
    public RouteManager manager;
    public List<Waypoint> routes;
    private int routeIndex;

    public float speed = 1f;
    public float rotateSpeed = 90f;
    private bool isRotating = false;

    public UnityEvent onCompletedMove;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (routes == null || routes.Count == 0)
            return;

        isRotating = true;
    }

    void FixedUpdate()
    {
        if (routes == null || routes.Count == 0 || routeIndex >= routes.Count)
            return;

        Vector3 toward = routes[routeIndex].transform.position - transform.position;
        toward.y = 0f;

        //1. 회전 제어 단계
        if(isRotating)
        {
            if(toward.sqrMagnitude > 0.001f) 
            {
                Quaternion targetRotation = Quaternion.LookRotation(toward);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotateSpeed * Time.fixedDeltaTime);

                //오차 범위를 감안해서 목표 방향과 각도 차이가 0.0001보다 작아지면 완료처리
                if(Quaternion.Angle(transform.rotation, targetRotation) < 0.001f)
                {
                    transform.rotation = targetRotation;
                    isRotating = false;
                }
            }
            else
            {
                isRotating = false;
            }

            return;
        }

        //2. 이동 제어 단계
        transform.position = Vector3.MoveTowards(
            transform.position, 
            routes[routeIndex].transform.position, 
            speed * Time.fixedDeltaTime);

        if(Vector3.Distance(transform.position, routes[routeIndex].transform.position) < 0.001f)
        {
            transform.position = routes[routeIndex].transform.position;
            routeIndex += 1;
            isRotating = true;

            //마지막 목적지에 도달한 상태인 경우
            if(routeIndex >= routes.Count)
            {
                routes = null;
                routeIndex = 0;
                isRotating = false;
                onCompletedMove?.Invoke();
                Debug.Log($"[{gameObject.name}] 최종 목적지에 도달했습니다.");
            }
        }
    }

    public void SetRoute(List<Waypoint> routes)
    {
        this.routes = routes;
        routeIndex = 0; //새로운 경로이기 때문에 0부터 시작하도록 초기화.
        isRotating = true;
    }

    public void Go(int destinationID)
    {
        Debug.Log($"Go 명령함 => {destinationID}");
        manager.GetRoutes(this, manager.GetNearestWaypointID(transform.position), destinationID);
    }
}
