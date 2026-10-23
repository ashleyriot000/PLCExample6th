using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Preliy.Flange
{
    /// <summary>
    /// Independent TCP controller for Robot5RSphericalWrist (Flange v1.0.11).
    /// Solves the actual TCP position (3 DOF) and one TCP-local tool direction
    /// (2 DOF). Roll around that direction is unconstrained.
    ///
    /// This is NOT derived from Flange.Controller because its 6R-centered
    /// editor workflow computes flange targets by removing a full 6D tool pose.
    /// Attach to the robot root; do not also drive the same arm with Controller.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Robotics/Robot 5R TCP Controller")]
    public sealed class Robot5RController : MonoBehaviour
    {
        private const int AxisCount = 5;
        private const int ResidualCount = 5;

        [Header("References")]
        [SerializeField] private Robot5RSphericalWrist _robot;
        [Tooltip("An empty Transform representing the real tool tip. Must be under the Flange frame.")]
        [SerializeField] private Transform _tcp;

        [Header("TCP Direction Constraint")]
        [Tooltip("TCP-local unit axis whose WORLD direction the IK must match (e.g. 0,0,1). Rotation around this axis is free.")]
        [SerializeField] private Vector3 _toolDirectionInTcp = Vector3.forward;

        [Header("Target (World Coordinate System)")]
        [SerializeField] private Vector3 _targetPositionWorld;
        [Tooltip("Only the direction of Tool Direction In Tcp is constrained; other Euler rotation is not guaranteed.")]
        [SerializeField] private Vector3 _targetEulerWorld;
        [SerializeField] private bool _showSceneHandles = true;
        [SerializeField] private bool _showGizmos = true;

        [Header("Numerical IK")]
        [SerializeField, Min(0.000001f)] private float _positionTolerance = 0.001f;
        [SerializeField, Range(0.1f, 10f)] private float _directionToleranceDeg = 1f;
        [Tooltip("Direction error weight, in equivalent meters per radian.")]
        [SerializeField, Min(0.001f)] private float _directionWeight = 0.35f;
        [SerializeField, Range(20, 300)] private int _maxIterations = 120;
        [SerializeField, Range(0.001f, 1f)] private float _damping = 0.04f;
        [SerializeField, Range(0.1f, 30f)] private float _maxStepDegrees = 8f;
        [SerializeField, Range(0.01f, 0.5f)] private float _finiteDifferenceDegrees = 0.1f;
        [SerializeField, Range(1, 16)] private int _seedCount = 8;

        public Robot5RSphericalWrist Robot => _robot;
        public Transform Tcp => _tcp;
        public Vector3 TargetPositionWorld => _targetPositionWorld;
        public Quaternion TargetRotationWorld => Quaternion.Euler(_targetEulerWorld);
        public Vector3 ToolDirectionInTcp => _toolDirectionInTcp.normalized;
        public bool ShowSceneHandles => _showSceneHandles;
        public string LastResult { get; private set; } = "Not solved yet";

        private void Reset()
        {
            _robot = GetComponent<Robot5RSphericalWrist>();
        }

        public void SetTargetWorld(Vector3 position, Quaternion orientation)
        {
            _targetPositionWorld = position;
            _targetEulerWorld = orientation.eulerAngles;
        }

        /// <summary>Assign a TCP marker to the Flange or create one if absent.</summary>
        [ContextMenu("Create / Assign TCP Marker")]
        public void CreateTcpMarker()
        {
            if (_robot == null) _robot = GetComponent<Robot5RSphericalWrist>();
            if (_robot == null || _robot.Frames == null || _robot.Frames.Count != 7 ||
                _robot.Frames[6] == null)
            {
                Debug.LogError("First assign Robot5RSphericalWrist and create its 7-frame hierarchy.", this);
                return;
            }

            if (_tcp == null)
            {
                var flange = _robot.Frames[6].transform;
                var existing = flange.Find("TCP");
                if (existing != null) _tcp = existing;
                else
                {
                    var marker = new GameObject("TCP");
#if UNITY_EDITOR
                    if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(marker, "Create TCP marker");
#endif
                    marker.transform.SetParent(flange, false);
                    marker.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    _tcp = marker.transform;
                }
            }
            CaptureActualTcpAsTarget();
#if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
        }

        [ContextMenu("Use Actual TCP as Target")]
        public void CaptureActualTcpAsTarget()
        {
            if (_tcp == null)
            {
                Debug.LogError("Assign a TCP marker first.", this);
                return;
            }
            SetTargetWorld(_tcp.position, _tcp.rotation);
            LastResult = "Target captured from scene TCP";
        }

        public void SetJoint(int index, float degrees)
        {
            ValidateReferences();
            if (index < 0 || index >= AxisCount) throw new ArgumentOutOfRangeException(nameof(index));
            var joint = _robot.Joints[index];
            var limits = joint.Config.Limits;
            joint.Position.Value = Mathf.Clamp(degrees, limits.x, limits.y);
        }

        /// <summary>
        /// Predicted TCP world pose based only on the Flange FK and the marker's
        /// flange-relative transform. Does not read the current marker world pose.
        /// </summary>
        public Matrix4x4 ComputeTcpWorld()
        {
            ValidateReferences();
            return ComputeTcpWorld(GetCurrentJointValues(), GetFlangeToTcp());
        }

        /// <summary>
        /// Check the FK model against the actual Unity hierarchy. A large error
        /// means incorrect DH/frame settings or scene hierarchy and cannot be fixed
        /// by using a different controller alone.
        /// </summary>
        [ContextMenu("Diagnose FK vs Scene")]
        public void DiagnoseFkAlignment()
        {
            try
            {
                ValidateReferences();
                var expectedFlange = _robot.transform.localToWorldMatrix * _robot.ComputeForward(GetCurrentJointValues());
                var actualFlange = _robot.Frames[6].transform;
                var expectedTcp = expectedFlange * GetFlangeToTcp();

                float flangePosMm = Vector3.Distance(expectedFlange.GetColumn(3), actualFlange.position) * 1000f;
                float flangeRotDeg = Quaternion.Angle(expectedFlange.rotation, actualFlange.rotation);
                float tcpPosMm = Vector3.Distance(expectedTcp.GetColumn(3), _tcp.position) * 1000f;
                float tcpRotDeg = Quaternion.Angle(expectedTcp.rotation, _tcp.rotation);

                LastResult = $"FK/Scene: Flange {flangePosMm:F2} mm, {flangeRotDeg:F2} deg; " +
                             $"TCP {tcpPosMm:F2} mm, {tcpRotDeg:F2} deg";
                if (flangePosMm > 1f || flangeRotDeg > 1f)
                    Debug.LogWarning(LastResult + " — Fix joint Frame configs, hierarchy, or scale before IK.", this);
                else
                    Debug.Log(LastResult, this);
            }
            catch (Exception ex)
            {
                LastResult = "Diagnosis failed: " + ex.Message;
                Debug.LogError(LastResult, this);
            }
        }

        /// <summary>
        /// Solve WORLD target directly at the TCP, avoiding the off-axis-tool error
        /// caused by 'remove full tool offset -> unconstrained flange roll' workflows.
        /// Returns false and leaves the joints unchanged if no converged solution exists.
        /// </summary>
        [ContextMenu("Solve TCP Target")]
        public bool TrySolveTarget()
        {
            try
            {
                ValidateReferences();
                var axis = _toolDirectionInTcp.normalized;
                if (axis.sqrMagnitude < 0.9f)
                    throw new InvalidOperationException("Tool Direction In Tcp must be non-zero.");

                Matrix4x4 flangeToTcp = GetFlangeToTcp();
                Quaternion targetRotation = Quaternion.Euler(_targetEulerWorld);
                Vector3 desiredDirection = (targetRotation * axis).normalized;
                BuildTangentBasis(desiredDirection, out var u, out var v);

                float bestPositionMm = float.PositiveInfinity;
                float bestDirectionDeg = float.PositiveInfinity;
                float[] current = GetCurrentJointValues();

                // A controller cannot repair a mismatch between the numerical
                // DH model and Unity's actual frame hierarchy.
                var initialPredicted = ComputeTcpWorld(current, flangeToTcp);
                var fkPositionGap = Vector3.Distance(
                    initialPredicted.GetColumn(3), _tcp.position);
                var fkRotationGap = Quaternion.Angle(
                    initialPredicted.rotation, _tcp.rotation);
                if (fkPositionGap > 0.002f || fkRotationGap > 1f)
                    throw new InvalidOperationException(
                        $"FK and scene disagree ({fkPositionGap * 1000f:F2} mm, " +
                        $"{fkRotationGap:F2} deg). Run Diagnose FK vs Scene and correct frames first.");

                for (int seed = 0; seed < _seedCount; ++seed)
                {
                    float[] q = CreateSeed(seed, current);
                    if (Iterate(q, flangeToTcp, _targetPositionWorld,
                        desiredDirection, axis, u, v,
                        out float errorM, out float angleDeg))
                    {
                        for (int i = 0; i < AxisCount; ++i) SetJoint(i, q[i]);
                        float actualError = Vector3.Distance(_tcp.position, _targetPositionWorld);
                        float actualDirectionError = Vector3.Angle(
                            _tcp.rotation * axis, desiredDirection);
                        if (actualError > _positionTolerance ||
                            actualDirectionError > _directionToleranceDeg)
                        {
                            for (int i = 0; i < AxisCount; ++i) SetJoint(i, current[i]);
                            throw new InvalidOperationException(
                                $"Solved pose does not match actual TCP " +
                                $"({actualError * 1000f:F2} mm, {actualDirectionError:F2} deg). " +
                                "Original joints restored; check hierarchy and TCP marker.");
                        }
                        LastResult = $"IK succeeded: actual TCP position {actualError * 1000f:F2} mm, " +
                                     $"direction {actualDirectionError:F2} deg (seed {seed})";
#if UNITY_EDITOR
                        if (!Application.isPlaying) EditorUtility.SetDirty(this);
#endif
                        return true;
                    }

                    if (errorM * 1000f < bestPositionMm)
                    {
                        bestPositionMm = errorM * 1000f;
                        bestDirectionDeg = angleDeg;
                    }
                }

                LastResult = $"IK failed: target may be unreachable or frame data incorrect. " +
                             $"Best attempted position error {bestPositionMm:F2} mm, " +
                             $"direction {bestDirectionDeg:F2} deg. Joints unchanged.";
                Debug.LogWarning(LastResult, this);
                return false;
            }
            catch (Exception ex)
            {
                LastResult = "IK setup error: " + ex.Message;
                Debug.LogError(LastResult, this);
                return false;
            }
        }

        public bool TrySolveWorldPose(Vector3 targetPosition, Quaternion targetRotation)
        {
            SetTargetWorld(targetPosition, targetRotation);
            return TrySolveTarget();
        }

        private void ValidateReferences()
        {
            if (_robot == null) _robot = GetComponent<Robot5RSphericalWrist>();
            if (_robot == null) throw new InvalidOperationException("Robot5RSphericalWrist is missing.");
            if (_robot.Joints == null || _robot.Joints.Count != AxisCount ||
                _robot.Frames == null || _robot.Frames.Count != 7)
                throw new InvalidOperationException("Expected 5 joints and 7 frames: Base, J1..J5, Flange.");
            for (int i = 0; i < 7; ++i)
                if (_robot.Frames[i] == null)
                    throw new InvalidOperationException($"Frame reference {i} is null.");
            for (int i = 0; i < AxisCount; ++i)
            {
                var joint = _robot.Joints[i];
                if (joint == null || joint.Frame != _robot.Frames[i + 1])
                    throw new InvalidOperationException($"J{i + 1} must share Frame {i + 1}.");
                if (joint.Config.Type != TransformJoint.JointType.Rotation)
                    throw new InvalidOperationException($"J{i + 1} must be a revolute joint.");
            }
            if (_tcp == null) throw new InvalidOperationException("Assign TCP Transform under Flange.");
            if (_tcp != _robot.Frames[6].transform &&
                !_tcp.IsChildOf(_robot.Frames[6].transform))
                throw new InvalidOperationException("TCP must be a child of the Flange frame.");
        }

        private float[] GetCurrentJointValues()
        {
            var q = new float[AxisCount];
            for (int i = 0; i < AxisCount; ++i) q[i] = _robot.Joints[i].Position.Value;
            return q;
        }

        private Matrix4x4 GetFlangeToTcp()
        {
            Transform flange = _robot.Frames[6].transform;
            var localPosition = flange.InverseTransformPoint(_tcp.position);
            var localRotation = Quaternion.Inverse(flange.rotation) * _tcp.rotation;
            return Matrix4x4.TRS(localPosition, localRotation, Vector3.one);
        }

        private Matrix4x4 ComputeTcpWorld(float[] q, Matrix4x4 flangeToTcp)
        {
            return _robot.transform.localToWorldMatrix * _robot.ComputeForward(q) * flangeToTcp;
        }

        private float[] CreateSeed(int seed, float[] current)
        {
            var result = new float[AxisCount];
            for (int j = 0; j < AxisCount; ++j)
            {
                var range = _robot.Joints[j].Config.Limits;
                float mid = (range.x + range.y) * 0.5f;
                if (seed == 0)
                    result[j] = Mathf.Clamp(current[j], range.x, range.y);
                else if (seed == 1)
                    result[j] = mid;
                else
                {
                    int bits = seed - 2;
                    float sign = ((bits >> (j % 4)) & 1) == 0 ? -1f : 1f;
                    result[j] = Mathf.Clamp(mid + sign * (range.y - range.x) * 0.26f,
                        range.x, range.y);
                }
            }
            return result;
        }

        private static void BuildTangentBasis(Vector3 desired, out Vector3 u, out Vector3 v)
        {
            Vector3 helper = Mathf.Abs(desired.y) < 0.9f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(helper, desired).normalized;
            v = Vector3.Cross(desired, u).normalized;
        }

        private float Residual(float[] q, Matrix4x4 offset, Vector3 goalPos,
            Vector3 goalDir, Vector3 axis, Vector3 u, Vector3 v,
            float[] e, out float posError, out float dirError)
        {
            var pose = ComputeTcpWorld(q, offset);
            Vector3 position = pose.GetColumn(3);
            Vector3 actualDir = pose.MultiplyVector(axis).normalized;
            Vector3 dp = goalPos - position;
            posError = dp.magnitude;
            e[0] = dp.x; e[1] = dp.y; e[2] = dp.z;

            float dot = Mathf.Clamp(Vector3.Dot(actualDir, goalDir), -1f, 1f);
            float angle = Mathf.Acos(dot);
            dirError = angle * Mathf.Rad2Deg;
            Vector3 tangent = actualDir - goalDir * dot;
            float len = tangent.magnitude;
            Vector3 directionError = len > 1e-6f
                ? tangent * (-angle / len)
                : (dot >= 0f ? Vector3.zero : u * Mathf.PI);
            e[3] = Vector3.Dot(directionError, u) * _directionWeight;
            e[4] = Vector3.Dot(directionError, v) * _directionWeight;

            float loss = 0f;
            for (int k = 0; k < ResidualCount; ++k) loss += e[k] * e[k];
            return loss;
        }

        private bool Iterate(float[] q, Matrix4x4 offset, Vector3 goalPos, Vector3 goalDir,
            Vector3 axis, Vector3 u, Vector3 v, out float posError, out float dirError)
        {
            var e = new float[ResidualCount];
            var moved = new float[ResidualCount];
            var jac = new float[ResidualCount, AxisCount];
            var delta = new double[AxisCount];
            var proposal = new float[AxisCount];

            for (int iter = 0; iter < _maxIterations; ++iter)
            {
                float loss = Residual(q, offset, goalPos, goalDir, axis, u, v,
                    e, out posError, out dirError);
                if (posError <= _positionTolerance && dirError <= _directionToleranceDeg)
                    return true;

                // J = -d(error)/d(angle_rad); q itself is stored in degrees.
                for (int j = 0; j < AxisCount; ++j)
                {
                    Vector2 lim = _robot.Joints[j].Config.Limits;
                    float dDeg = _finiteDifferenceDegrees;
                    if (q[j] + dDeg > lim.y) dDeg = -dDeg;
                    if (q[j] + dDeg < lim.x)
                    {
                        for (int k = 0; k < ResidualCount; ++k) jac[k, j] = 0;
                        continue;
                    }
                    q[j] += dDeg;
                    Residual(q, offset, goalPos, goalDir, axis, u, v,
                        moved, out _, out _);
                    q[j] -= dDeg;
                    float invRad = 1f / (dDeg * Mathf.Deg2Rad);
                    for (int k = 0; k < ResidualCount; ++k)
                        jac[k, j] = (e[k] - moved[k]) * invRad;
                }

                if (!DampedLeastSquares(jac, e, _damping, delta)) break;

                double largestDegrees = 0;
                for (int j = 0; j < AxisCount; ++j)
                    largestDegrees = System.Math.Max(largestDegrees,
                        System.Math.Abs(delta[j] * Mathf.Rad2Deg));
                double baseScale = largestDegrees > _maxStepDegrees
                    ? _maxStepDegrees / largestDegrees : 1.0;

                bool improved = false;
                for (int trial = 0; trial < 9; ++trial)
                {
                    double scale = baseScale * System.Math.Pow(0.5, trial);
                    for (int j = 0; j < AxisCount; ++j)
                    {
                        var lim = _robot.Joints[j].Config.Limits;
                        proposal[j] = Mathf.Clamp(q[j] +
                            (float)(delta[j] * Mathf.Rad2Deg * scale), lim.x, lim.y);
                    }
                    float nextLoss = Residual(proposal, offset, goalPos, goalDir,
                        axis, u, v, moved, out _, out _);
                    if (nextLoss >= loss - 1e-12f) continue;
                    Array.Copy(proposal, q, AxisCount);
                    improved = true;
                    break;
                }
                if (!improved) break;
            }

            Residual(q, offset, goalPos, goalDir, axis, u, v,
                e, out posError, out dirError);
            return posError <= _positionTolerance && dirError <= _directionToleranceDeg;
        }

        private static bool DampedLeastSquares(float[,] j, float[] error,
            float lambda, double[] result)
        {
            // (J^T J + lambda^2 I) dq = J^T e, 5x5 linear system.
            var a = new double[AxisCount, AxisCount + 1];
            for (int row = 0; row < AxisCount; ++row)
            {
                for (int col = 0; col < AxisCount; ++col)
                {
                    double sum = 0;
                    for (int k = 0; k < ResidualCount; ++k)
                        sum += j[k, row] * j[k, col];
                    if (row == col) sum += (double)lambda * lambda;
                    a[row, col] = sum;
                }
                double rhs = 0;
                for (int k = 0; k < ResidualCount; ++k)
                    rhs += j[k, row] * error[k];
                a[row, AxisCount] = rhs;
            }

            for (int col = 0; col < AxisCount; ++col)
            {
                int pivot = col;
                for (int row = col + 1; row < AxisCount; ++row)
                    if (System.Math.Abs(a[row, col]) > System.Math.Abs(a[pivot, col]))
                        pivot = row;
                if (System.Math.Abs(a[pivot, col]) < 1e-12) return false;
                if (pivot != col)
                    for (int k = col; k <= AxisCount; ++k)
                    {
                        double temp = a[col, k];
                        a[col, k] = a[pivot, k];
                        a[pivot, k] = temp;
                    }

                double diagonal = a[col, col];
                for (int k = col; k <= AxisCount; ++k) a[col, k] /= diagonal;
                for (int row = col + 1; row < AxisCount; ++row)
                {
                    double factor = a[row, col];
                    for (int k = col; k <= AxisCount; ++k)
                        a[row, k] -= factor * a[col, k];
                }
            }

            for (int row = AxisCount - 1; row >= 0; --row)
            {
                double value = a[row, AxisCount];
                for (int k = row + 1; k < AxisCount; ++k)
                    value -= a[row, k] * result[k];
                result[row] = value;
            }
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showGizmos || _tcp == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_tcp.position, 0.012f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_targetPositionWorld, 0.012f);
            Gizmos.DrawLine(_tcp.position, _targetPositionWorld);
            var axis = _toolDirectionInTcp.sqrMagnitude > 1e-8f
                ? _toolDirectionInTcp.normalized : Vector3.forward;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(_tcp.position, _tcp.rotation * axis * 0.15f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(_targetPositionWorld, Quaternion.Euler(_targetEulerWorld) * axis * 0.15f);
        }
    }
}

#if UNITY_EDITOR
namespace Preliy.Flange.Editor
{
    [CustomEditor(typeof(Robot5RController))]
    internal sealed class Robot5RControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (Robot5RController)target;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("5R TCP", EditorStyles.boldLabel);
            if (controller.Tcp != null)
            {
                EditorGUILayout.Vector3Field("Actual TCP (world m)", controller.Tcp.position);
                EditorGUILayout.Vector3Field("Actual TCP rotation", controller.Tcp.eulerAngles);
            }
            EditorGUILayout.HelpBox(
                "IK solves TCP XYZ and the selected tool-axis direction only. " +
                "It cannot enforce full 6D orientation. The TCP marker must be below Flange.",
                MessageType.Info);
            EditorGUILayout.LabelField("Last result", controller.LastResult, EditorStyles.wordWrappedLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create TCP Marker")) controller.CreateTcpMarker();
            if (GUILayout.Button("Capture Actual TCP"))
            {
                Undo.RecordObject(controller, "Capture 5R target");
                controller.CaptureActualTcpAsTarget();
                EditorUtility.SetDirty(controller);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Diagnose FK vs Scene")) controller.DiagnoseFkAlignment();
            if (GUILayout.Button("Solve TCP Target"))
            {
                if (controller.Robot != null)
                {
                    foreach (var joint in controller.Robot.Joints)
                    {
                        if (joint == null) continue;
                        Undo.RecordObject(joint, "Solve 5R IK");
                        Undo.RecordObject(joint.transform, "Solve 5R IK");
                    }
                }
                controller.TrySolveTarget();
                EditorUtility.SetDirty(controller);
            }
            EditorGUILayout.EndHorizontal();

            if (controller.Robot != null && controller.Robot.Joints.Count == 5)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Joint FK control (degrees)", EditorStyles.boldLabel);
                for (int i = 0; i < 5; ++i)
                {
                    var joint = controller.Robot.Joints[i];
                    if (joint == null) continue;
                    var limits = joint.Config.Limits;
                    EditorGUI.BeginChangeCheck();
                    float next = EditorGUILayout.Slider($"J{i + 1}", joint.Position.Value,
                        limits.x, limits.y);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(joint, $"Set J{i + 1}");
                        Undo.RecordObject(joint.transform, $"Set J{i + 1}");
                        controller.SetJoint(i, next);
                        EditorUtility.SetDirty(joint);
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            var controller = (Robot5RController)target;
            if (!controller.ShowSceneHandles || controller.Tcp == null) return;
            var targetPosition = controller.TargetPositionWorld;
            var targetRotation = controller.TargetRotationWorld;
            EditorGUI.BeginChangeCheck();
            Vector3 position = Handles.PositionHandle(targetPosition, targetRotation);
            Quaternion rotation = Handles.RotationHandle(targetRotation, position);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(controller, "Move 5R TCP target");
                if (controller.Robot != null)
                    foreach (var joint in controller.Robot.Joints)
                    {
                        if (joint == null) continue;
                        Undo.RecordObject(joint, "Move 5R TCP target");
                        Undo.RecordObject(joint.transform, "Move 5R TCP target");
                    }
                controller.SetTargetWorld(position, rotation);
                controller.TrySolveTarget();
                EditorUtility.SetDirty(controller);
            }
            Handles.Label(position, "5R Target TCP");
        }
    }
}
#endif
