using System.Collections.Generic;
using UnityEngine;

namespace Preliy.Flange
{

    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class TargetScheduler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Controller _controller;

        public Controller GetController => _controller;

        [Header("Target Configuration")]
        [SerializeField]
        private Configuration _configuration;

        [Header("External Joints")]
        [SerializeField]
        private float _e1;
        [SerializeField]
        private float _e2;
        [SerializeField]
        private float _e3;
        [SerializeField]
        private float _e4;
        [SerializeField]
        private float _e5;
        [SerializeField]
        private float _e6;

        [Header("Tool Management")]
        [Tooltip("로봇 끝단(보통 J6)의 실제 Transform을 할당하세요.")]
        public Transform mountPoint;
        [Tooltip("연결할 툴(프리팹 또는 씬 오브젝트)들의 리스트입니다.")]
        public List<ToolParent> availableTools = new List<ToolParent>();
        [Tooltip("현재 로봇에 연결된 툴입니다.")]
        public ToolParent currentAttachedTool = null;
        [HideInInspector] public int toolIndexToConnect = 0; // 에디터에서 입력받을 인덱스

        [Header("Settings")]
        [SerializeField]
        private bool _showErrorMassage;

        [Header("Sequence Manager")]
        public Transform waypointContainer; // 생성된 웨이포인트들이 담길 부모 폴더
        public List<TargetWaypoint> waypoints = new List<TargetWaypoint>(); // 시퀀스 리스트

        [HideInInspector]
        [SerializeField]
        private Property<Matrix4x4> _target = new();

        private void OnEnable()
        {
            _target.OnValueChanged += JumpToTarget;
        }

        private void Update()
        {
            _target.Value = transform.GetMatrix();
        }

        private void OnDisable()
        {
            _target.OnValueChanged -= JumpToTarget;
        }

        private void JumpToTarget(Matrix4x4 target)
        {
            if (_controller == null) return;

            JumpTo(_controller, target, SolutionIgnoreMask.All, _showErrorMassage);
        }

        public void SyncTransformQuietly(Vector3 position, Quaternion rotation)
        {
            // 1. IK 연산 이벤트(JumpToTarget)가 터지는 것을 막기 위해 리스너 임시 해제
            _target.OnValueChanged -= JumpToTarget;

            // 2. 스케줄러 오브젝트 이동
            transform.SetPositionAndRotation(position, rotation);

            // 3. 내부 프로퍼티 값 갱신 (리스너가 없으므로 IK가 실행되지 않음)
            _target.Value = transform.GetMatrix();

            // 4. 다음 MoveL, MoveC 명령을 위해 이벤트 리스너 원상 복구
            _target.OnValueChanged += JumpToTarget;
        }

        /// <summary>
        /// Jump to specific <see cref="CartesianTarget"/>
        /// </summary>
        /// <param name="controller"> <see cref="Controller"/></param>
        /// <param name="target">Target pose in world space</param>
        /// <param name="ignoreMask"> <see cref="SolutionIgnoreMask"/></param>
        /// <param name="showErrorMassage">Show error message, if error occured</param>
        private void JumpTo(Controller controller, Matrix4x4 target, SolutionIgnoreMask ignoreMask, bool showErrorMassage = true)
        {
            var externalJoints = new ExtJoint(_e1, _e2, _e3, _e4, _e5, _e6);
            var solution = controller.Solver.ComputeInverse(target, controller.Tool.Value, _configuration, externalJoints, ignoreMask);
            controller.Solver.TryApplySolution(solution, showErrorMassage);
        }

        public IKSolution GetSolution(Matrix4x4 target)
        {
            var externalJoints = new ExtJoint(_e1, _e2, _e3, _e4, _e5, _e6);
            return _controller.Solver.ComputeInverse(target, _controller.Tool.Value, _configuration, externalJoints, SolutionIgnoreMask.All);
        }

        public void SetJointAngleFromInspector(int jointIndex, float angle)
        {
            MechanicalGroup group = _controller?.MechanicalGroup;
            if (group == null || group.Joints.Count == 0) return;

            Matrix4x4 before = group.ComputeForward();
            // 1. 조인트 실제 각도(Position.Value) 변경
            group.SetJoint(jointIndex, angle);


            // 2. 순기구학(FK) 연산 수행
            Matrix4x4 lastPose;
            if (currentAttachedTool != null)
            {
                Transform tcp = currentAttachedTool.Point;
                transform.SetPositionAndRotation(tcp.position, tcp.rotation);
            }
            else
            {
                lastPose = _controller.MechanicalGroup.ComputeForward();
                // 3. 스케줄러(브러시)의 게임 오브젝트를 해당 위치로 순간이동시킵니다.
                transform.SetMatrix(lastPose);
            }
        }

        // ==========================================
        // 새 웨이포인트(타겟) 생성 로직
        // ==========================================
        public void CreateNewWaypoint()
        {
            if (waypointContainer == null)
            {
                GameObject container = new GameObject("Waypoints");
                container.transform.position = Vector3.zero;
                waypointContainer = container.transform;
            }

            GameObject wpObj = new GameObject($"Target_{waypoints.Count:00}");
            wpObj.transform.SetParent(waypointContainer);

            // 현재 스케줄러의 위치와 회전값 복사
            TargetWaypoint wp = wpObj.AddComponent<TargetWaypoint>();
            wp.Initialize(this, transform.position, transform.rotation, JointExtension.GetJointValues(_controller.MechanicalGroup.Joints));
            waypoints.Add(wp);
            Debug.Log($"새 타겟 [{wpObj.name}] 이 생성되어 웨이포인트 리스트에 추가되었습니다.");
        }

        public void ClearWaypoints()
        {
            foreach (var w in waypoints)
            {
                DestroyImmediate(w.gameObject);
            }
            waypoints.Clear();
            Debug.Log("모든 웨이포인트가 제거되었습니다.");
        }

        public void JumpToWaypoint(int index)
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                Debug.LogWarning("저장된 웨이포인트 시퀀스가 없습니다.");
                return;
            }

            if (index < 0 || index >= waypoints.Count)
            {
                Debug.LogWarning($"유효하지 않은 인덱스입니다. (0 ~ {waypoints.Count - 1} 사이의 값을 입력하세요.)");
                return;
            }

            TargetWaypoint wp = waypoints[index];
            if (wp == null) return;

            // 1. 스케줄러의 위치와 회전값을 웨이포인트와 동일하게 맞춤
            this.transform.position = wp.transform.position;
            this.transform.rotation = wp.transform.rotation;

            // 3. 바뀐 Transform과 세팅을 즉시 로봇 기구학 연산에 반영 (에디터 갱신)
            //JumpToTarget(this.transform.GetMatrix());
            //Debug.Log($"스케줄러가 웨이포인트 [{index} : {wp.name}] => {transform.position}:{transform.eulerAngles} 위치로 동기화되었습니다.");
        }

        // ==========================================
        // 홈 위치 복귀 및 동기화 로직
        // ==========================================
        public void ReturnToHome()
        {
            if (_controller == null)
            {
                Debug.LogWarning("Controller가 연결되지 않아 Home 위치를 계산할 수 없습니다.");
                return;
            }

            // 1. Controller의 홈 자세로 만듭니다.
            _controller.MechanicalGroup.LoadState();

            // 2. [매우 중요] 로봇이 홈 자세가 된 후의 TCP(끝단) 위치를 스케줄러로 가져와야 합니다.
            Matrix4x4 lastPose;
            if (currentAttachedTool != null)
            {
                Transform tcp = currentAttachedTool.Point;
                transform.SetPositionAndRotation(tcp.position, tcp.rotation);
            }
            else
            {
                lastPose = _controller.MechanicalGroup.ComputeForward();
                // Matrix4x4에서 위치(Position)와 회전(Rotation)을 추출합니다.
                Vector3 homePos = lastPose.GetColumn(3); // 4번째 열이 Position

                // Matrix에서 회전값 추출 (Unity 표준 수학 연산)
                Vector3 forward = lastPose.GetColumn(2);
                Vector3 upwards = lastPose.GetColumn(1);
                Quaternion homeRot = Quaternion.LookRotation(forward, upwards);

                // 3. 스케줄러(브러시)의 게임 오브젝트를 해당 위치로 순간이동시킵니다.
                transform.SetPositionAndRotation(homePos, homeRot);
            }


            Debug.Log("스케줄러와 로봇이 Home 위치로 동기화되었습니다.");
        }

        // ==========================================
        // 툴 부착 (Connect) 로직
        // ==========================================

        public void ConnectTool()
        {
            if (availableTools.Count == 0)
            {
                Debug.LogWarning("유효하지 않은 툴 인덱스이거나 비어있는 슬롯입니다.");
                return;
            }

            for (int i = 0; i < availableTools.Count; ++i)
            {
                if (ConnectTool(i))
                    break;
            }
        }
        public bool ConnectTool(int index)
        {
            if (mountPoint == null)
            {
                Debug.LogWarning("로봇 끝단(End Effector Mount Point)이 지정되지 않았습니다.");
                return false;
            }

            if ((availableTools[index].transform.position - mountPoint.position).magnitude > 0.01f)
            {
                return false;
            }

            // 기존에 연결된 툴이 있다면 연결 취소
            if (currentAttachedTool != null)
            {
                Debug.LogWarning("툴이 이미 연결되어 연결을 취소합니다.");
                return false;
            }

            ToolParent toolToConnect = availableTools[index];

            // 1. 유니티 계층구조상 로봇 끝단의 자식으로 설정하여 함께 움직이도록 함
            toolToConnect.transform.SetParent(mountPoint);

            // 2. 물리적 위치(Mount Point)에 딱 맞게 정렬
            toolToConnect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            currentAttachedTool = toolToConnect;
            _controller.Tool.Value = index + 1;

            //3. 새 연결 툴의 TCP위치로 위치,회전값 동기화.
            Transform tcp = currentAttachedTool.Point;
            SyncTransformQuietly(tcp.position, tcp.rotation);
            Debug.LogWarning($"{currentAttachedTool.gameObject.name} 툴이 로봇에 연결되었습니다.");

            return true;
        }

        // ==========================================
        // 툴 분리 (Release) 로직
        // ==========================================
        public void ReleaseTool()
        {
            // 1. 로봇 끝단과의 부모-자식 관계를 끊어냄 (보통 작업대나 툴 스탠드에 내려놓음)
            // 여기서는 씬 최상단(null)으로 분리합니다.
            if (currentAttachedTool != null)
            {
                string toolName = currentAttachedTool.gameObject.name;
                currentAttachedTool.transform.SetParent(null);
                Debug.LogWarning($"[{toolName}] 툴이 로봇에서 분리되었습니다.");
            }

            // 2. 현재 부착 툴 데이터 삭제
            currentAttachedTool = null;
            _controller.Tool.Value = 0;
            toolIndexToConnect = 0;

            //3. 가장 마지막 조인트의 TCP위치,회전값 동기화.
            Matrix4x4 lastPose = _controller.MechanicalGroup.ComputeForward();
            Vector3 pEnd = lastPose.GetColumn(3); // 4번째 열이 Position
                                                  // Matrix에서 회전값 추출 (Unity 표준 수학 연산)
            Vector3 forward = lastPose.GetColumn(2);
            Vector3 upwards = lastPose.GetColumn(1);
            Quaternion rEnd = Quaternion.LookRotation(forward, upwards);

            // 3. 스케줄러(브러시)의 게임 오브젝트를 해당 위치로 순간이동시킵니다.
            SyncTransformQuietly(pEnd, rEnd);
        }

        //연결된 툴 사용(켜기/끄기)
        public void ActTool(bool isOn)
        {
            currentAttachedTool.IsOn = isOn;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            JumpToTarget(transform.GetMatrix());
        }

        [HideInInspector]
        public int targetWaypointIndex = 0;

        // 씬 뷰에서 웨이포인트들의 이동 경로와 방향(회전값)을 시각화
        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Count == 0) return;

            // 기즈모 축의 길이 설정 (로봇 크기에 맞춰 조절하세요)
            float axisLength = 0.15f;
            float lineThickness = 4f; // 선 굵기

            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;

                Vector3 pos = waypoints[i].transform.position;
                Quaternion rot = waypoints[i].transform.rotation;

                // 1. 중심점 표시 (작은 흰색 구)
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(pos, 0.01f);

                // 2. 방향(Orientation) 표시 기즈모 (MechanicalUnit과 동일한 방식)

                // X축 (Red)
                UnityEditor.Handles.color = Color.red;
                UnityEditor.Handles.DrawAAPolyLine(lineThickness, pos, pos + rot * Vector3.right * axisLength);

                // Y축 (Green)
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.DrawAAPolyLine(lineThickness, pos, pos + rot * Vector3.up * axisLength);

                // Z축 (Blue) - 보통 로봇의 툴 끝단이 향하는 방향(TCP 방향)
                UnityEditor.Handles.color = Color.blue;
                UnityEditor.Handles.DrawAAPolyLine(lineThickness, pos, pos + rot * Vector3.forward * axisLength);

                // 3. 몇 번째 타겟인지 보기 쉽도록 라벨 띄우기
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = Color.yellow;
                labelStyle.fontSize = 11;
                UnityEditor.Handles.Label(pos + Vector3.up * 0.05f, $" T{i}", labelStyle);
            }
        }
#endif
    }
}
