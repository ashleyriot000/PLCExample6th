using System.Collections.Generic;
using UnityEngine;

public class RouteManager : MonoBehaviour
{
    public Waypoint[] allWaypoints;

    //딕셔너리 - key, value 
    private Dictionary<int, Waypoint> waypointDic = new Dictionary<int, Waypoint>();

    public void InitializeWaypoints()
    {
        waypointDic.Clear();
        foreach(Waypoint w in allWaypoints)
        {
            if(waypointDic.ContainsKey(w.id))
            {
                continue;
            }

            waypointDic.Add(w.id, w);
        }

        Debug.Log($"[RouteManager]에 총 {waypointDic.Count}개의 웨이포인트가 로드되었습니다.");
    }

    /// <summary>
    /// 해당 위치 기준 가장 가까운 웨이포인트의 ID를 검색해서 반환.
    /// </summary>
    /// <param name="position">검색 위치</param>
    /// <returns>가장 가까운 웨이포인트 ID</returns>
    public int GetNearestWaypointID(Vector3 position)
    {
        if(waypointDic.Count == 0)
        {
            Debug.LogError($"[RouteManager]에 등록된 웨이포인트가 존재하지 않습니다. " +
                $"\n새로운 웨이포인트를 추가해주세요.");
            return 0;
        }

        int foundID = 0;
        float minDistance = float.MaxValue;
        foreach(var pair in waypointDic)
        {
            float distanceSqr = (pair.Value.transform.position - position).sqrMagnitude;
            if(minDistance > distanceSqr)
            {
                minDistance = distanceSqr;
                foundID = pair.Key;
            }
        }

        return foundID;
    }

    /// <summary>
    /// 목적지까지의 최단 경로 탐색. 다익스트라 알고리즘
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    private List<Waypoint> FindShortestPath(Waypoint start, Waypoint end)
    {
        Dictionary<Waypoint, float> distances = new Dictionary<Waypoint, float>();
        Dictionary<Waypoint, Waypoint> previousPoints = new Dictionary<Waypoint, Waypoint>();
        List<Waypoint> unvisited = new List<Waypoint>();

        //딕셔너리에 저장된 모든 등록 웨이포인트 순회.
        foreach(var wp in waypointDic.Values)
        {
            distances[wp] = float.MaxValue;
            previousPoints[wp] = null;
            unvisited.Add(wp);
        }

        distances[start] = 0;

        while(unvisited.Count > 0)
        {
            unvisited.Sort((a, b) => distances[a].CompareTo(distances[b]));
            Waypoint current = unvisited[0];
            unvisited.RemoveAt(0);

            if (current == end) break;
            if (distances[current] == float.MaxValue)
                break;

            foreach(var neighbor in current.neighbors)
            {
                if (!unvisited.Contains(neighbor)) continue;

                float alt = distances[current] + Vector3.Distance(current.transform.position, neighbor.transform.position);

                if(alt < distances[neighbor])
                {
                    distances[neighbor] = alt;
                    previousPoints[neighbor] = current;
                }
            }
        }

        List<Waypoint> path = new List<Waypoint>();
        Waypoint curr = end;

        while(curr != null)
        {
            path.Insert(0, curr);
            curr = previousPoints[curr];
        }

        Debug.Log(path);
        if (path.Count == 0 || path[0] != start)
            return null;

        Debug.Log(path.Count);
        return path;
    }

    private void Awake()
    {
        InitializeWaypoints();
    }

    /// <summary>
    /// AGV컨트롤러 쪽에서 최단 경로를 알아봐달라고 요청하는 함수
    /// </summary>
    /// <param name="controller">요청한 컨트롤러</param>
    /// <param name="startID">가장 가까운 웨이포인트 ID</param>
    /// <param name="endID">도착해야 할 웨이포인트 ID</param>
    /// <returns>경로 검색 성공 여부</returns>
    public void GetRoutes(AGVController controller, int startID, int endID)
    {
        if (waypointDic.Count == 0)
            return;

        if (!waypointDic.TryGetValue(startID, out Waypoint start))
        {
            return;
        }

        if (!waypointDic.TryGetValue(endID, out Waypoint end))
        {
            return;
        }

        Debug.Log("경로 탐색 시작");
        List<Waypoint> path = FindShortestPath(start, end);
        if(path == null || path.Count == 0)
            return;
        string p = string.Empty;
        foreach (var waypoint in path)
        {
            p += waypoint.id + "->";
        }

        Debug.Log($"경로 탐색 완료 {p}");
        //컨트롤러에게 최단 경로 가져다 주기.
        controller.SetRoute(path);
    }
}
