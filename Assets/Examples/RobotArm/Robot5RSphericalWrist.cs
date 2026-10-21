using System;
using System.Collections.Generic;
using UnityEngine;

namespace Preliy.Flange
{
    /// <summary>
    /// Five-revolute-joint manipulator, intended for a 3+2 DOF arm.
    /// IK matches flange position (XYZ) and one flange-local tool-axis direction (2 DOF).
    /// Rotation about the tool axis is deliberately NOT constrained.
    /// Uses damped least-squares numerical IK, not the analytical 6R spherical-wrist solver.
    /// Units: FrameConfig [meters, radians]; joint values [degrees].
    /// </summary>
    [AddComponentMenu("Robotics/Robot 5R Spherical Wrist")]
    [DisallowMultipleComponent]
    [SelectionBase]
    public sealed class Robot5RSphericalWrist : Robot
    {
        private const int JointCount = 5;
        private const int FrameCount = 7; // Base + 5 joint frames + Flange
        private const int ResidualCount = 5; // XYZ + tool-axis direction (2)
        private const int SeedCount = 8;

        [Header("5R IK: flange position + tool-axis direction")]
        [Tooltip("Axis of interest expressed in the Flange frame; often +Z or +Y.")]
        [SerializeField] private Vector3 _flangeToolAxis = Vector3.forward;
        [SerializeField, Min(0.00001f)] private float _positionTolerance = 0.001f;
        [SerializeField, Range(0.1f, 10f)] private float _directionToleranceDegrees = 1f;
        [Tooltip("Relative weighting of tool-direction error (roughly meters per radian).")]
        [SerializeField, Min(0.001f)] private float _directionWeight = 0.35f;
        [SerializeField, Range(20, 300)] private int _maxIterations = 120;
        [SerializeField, Range(0.001f, 1f)] private float _damping = 0.04f;
        [SerializeField, Range(0.01f, 20f)] private float _maxJointStepDegrees = 8f;
        [SerializeField, Range(0.01f, 0.5f)] private float _differenceDegrees = 0.1f;

        private void Reset()
        {
            _frames = new List<Frame>(new Frame[FrameCount]);
            _joints = new List<TransformJoint>(new TransformJoint[JointCount]);
        }

#if UNITY_EDITOR
        [ContextMenu("Create Default Hierarchy", false, 500)]
        public void CreateDefaultHierarchy()
        {
            _frames = new List<Frame>();
            _joints = new List<TransformJoint>();
            var root = transform.GetOrCreate("Base").CreateMeshPlaceholder();
            _frames.Add(root);
            Transform parent = root.transform;

            for (int i = 0; i < JointCount; ++i)
            {
                var frame = parent.GetOrCreate($"Joint_{i + 1}").CreateMeshPlaceholder();
                var joint = frame.GetComponent<TransformJoint>();
                if (joint == null) joint = frame.gameObject.AddComponent<TransformJoint>();
                _frames.Add(frame);
                _joints.Add(joint);
                parent = frame.transform;
            }

            _frames.Add(parent.GetOrCreate("Flange"));
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

#if UNITY_EDITOR
        [ContextMenu("Verify FK to IK at Current Pose", false, 530)]
        private void VerifyCurrentPose()
        {
            var expected = ComputeForward();
            var solved = ComputeInverse(expected, Configuration.Default, SolutionIgnoreMask.None);
            if (solved.IsValid)
                Debug.Log("5R FK-to-IK check passed for current pose.", this);
            else
                Debug.LogError("5R FK-to-IK check failed: " + solved.Exception?.Message, this);
        }
#endif

        /// <summary>FK from the current scene joint values, relative to the base frame.</summary>
        public override Matrix4x4 ComputeForward()
        {
            EnsureSetup();
            var values = new float[JointCount];
            for (int i = 0; i < JointCount; ++i) values[i] = _joints[i].Position.Value;
            return ComputeForward(values);
        }

        /// <summary>FK from five angles in degrees without moving GameObjects.</summary>
        public override Matrix4x4 ComputeForward(float[] values)
        {
            EnsureSetup();
            if (values == null || values.Length < JointCount)
                throw new ArgumentException("5R FK requires five joint values.", nameof(values));
            return base.ComputeForward(values);
        }

        /// <summary>
        /// Numerical 5R IK. Configuration.Index selects one of eight starting guesses.
        /// Configuration turn fields are not supported for this custom solver.
        /// The supplied pose is the FLANGE pose in robot-base coordinates.
        /// </summary>
        public override IKSolution ComputeInverse(
            Matrix4x4 target, Configuration configuration, SolutionIgnoreMask ignoreMask)
        {
            try
            {
                EnsureSetup();
                var axis = GetToolAxis();
                var targetDirection = target.MultiplyVector(axis).normalized;
                if (targetDirection.sqrMagnitude < 0.9f)
                    return new IKSolution("Invalid flange target orientation.");

                BuildDirectionBasis(targetDirection, out var basis0, out var basis1);
                var targetPosition = target.MultiplyPoint3x4(Vector3.zero);
                int seed = Mathf.Clamp(configuration.Index, 0, SeedCount - 1);
                var values = MakeSeed(seed);

                if (!Solve(values, targetPosition, targetDirection, basis0, basis1, axis))
                    return new IKSolution(
                        "5R IK could not reach the requested flange position and tool-axis direction. " +
                        "Try another configuration index, reachable target, or tool axis.");

                // Flange's JointTarget stores six robot axes; axis 6 is unused here.
                var jointTarget = new JointTarget(
                    values[0], values[1], values[2], values[3], values[4], 0f);

                // This solver uses seed indices, not Flange's 6R turn parameters.
                var appliedConfiguration = new Configuration(0, 0, 0, seed);
                var solution = new IKSolution(jointTarget, appliedConfiguration);
                solution.Validate(this);
                return solution;
            }
            catch (Exception ex)
            {
                return new IKSolution("5R IK configuration error: " + ex.Message);
            }
        }

        /// <summary>
        /// Search several seeds for distinct valid IK solutions.
        /// 'turn' is not implemented; angles are already limited by JointConfig.Limits.
        /// </summary>
        public override List<IKSolution> ComputeInverse(
            Matrix4x4 target, bool turn, SolutionIgnoreMask ignoreMask)
        {
            var results = new List<IKSolution>();
            for (int i = 0; i < SeedCount; ++i)
            {
                var candidate = ComputeInverse(
                    target, new Configuration(0, 0, 0, i), ignoreMask);
                if (!candidate.IsValid || IsDuplicate(candidate, results)) continue;
                results.Add(candidate);
            }
            return results;
        }

        // Flange's 6R configuration index has no direct 5R equivalent.
        public override int GetConfigurationIndex(float[] jointValues) => 0;
        public override int GetConfigurationIndex() => 0;

        private void EnsureSetup()
        {
            if (_joints == null || _joints.Count != JointCount ||
                _frames == null || _frames.Count != FrameCount)
                throw new InvalidOperationException(
                    "5R requires exactly 5 Joints and 7 Frames (Base, J1..J5, Flange).");

            for (int i = 0; i < FrameCount; ++i)
                if (_frames[i] == null)
                    throw new InvalidOperationException($"Frame reference {i} is missing.");

            for (int i = 0; i < JointCount; ++i)
            {
                if (_joints[i] == null)
                    throw new InvalidOperationException($"Joint reference {i} is missing.");
                if (_joints[i].Frame != _frames[i + 1])
                    throw new InvalidOperationException($"Joint {i + 1} must share its GameObject with Frame {i + 1}.");
                if (_joints[i].Config.Type != TransformJoint.JointType.Rotation)
                    throw new InvalidOperationException($"Joint {i + 1} must be a rotation joint.");
                if (Mathf.Abs(_joints[i].Config.Factor) < 1e-6f)
                    throw new InvalidOperationException($"Joint {i + 1} has a zero joint factor.");
            }
        }

        private Vector3 GetToolAxis()
        {
            if (_flangeToolAxis.sqrMagnitude < 1e-8f)
                throw new InvalidOperationException("Flange Tool Axis cannot be zero.");
            return _flangeToolAxis.normalized;
        }

        private float[] MakeSeed(int index)
        {
            var q = new float[JointCount];
            for (int i = 0; i < JointCount; ++i)
            {
                var lim = _joints[i].Config.Limits;
                float mid = (lim.x + lim.y) * 0.5f;
                float span = lim.y - lim.x;

                if (index == 0)
                    q[i] = Mathf.Clamp(_joints[i].Position.Value, lim.x, lim.y);
                else if (index == 1)
                    q[i] = mid;
                else
                {
                    int pattern = index - 1; // 1..6, distinct start configurations
                    float sign = ((pattern >> (i % 3)) & 1) == 0 ? -1f : 1f;
                    float fraction = (index % 2 == 0) ? 0.23f : 0.32f;
                    q[i] = Mathf.Clamp(mid + sign * span * fraction, lim.x, lim.y);
                }
            }
            return q;
        }

        private bool Solve(float[] q, Vector3 targetPos, Vector3 targetDir,
            Vector3 basis0, Vector3 basis1, Vector3 flangeAxis)
        {
            var error = new float[ResidualCount];
            var movedError = new float[ResidualCount];
            var jacobian = new float[ResidualCount, JointCount];
            var delta = new double[JointCount];
            var proposal = new float[JointCount];

            for (int iteration = 0; iteration < _maxIterations; ++iteration)
            {
                float loss = Measure(q, targetPos, targetDir, basis0, basis1,
                    flangeAxis, error, out float distance, out float directionAngle);

                if (distance <= _positionTolerance &&
                    directionAngle <= _directionToleranceDegrees) return true;

                for (int j = 0; j < JointCount; ++j)
                {
                    var limits = _joints[j].Config.Limits;
                    float step = _differenceDegrees;
                    if (q[j] + step > limits.y) step = -step;
                    if (q[j] + step < limits.x)
                    {
                        for (int k = 0; k < ResidualCount; ++k) jacobian[k, j] = 0f;
                        continue;
                    }

                    q[j] += step;
                    Measure(q, targetPos, targetDir, basis0, basis1,
                        flangeAxis, movedError, out _, out _);
                    q[j] -= step;

                    float invStepRadians = 1f / (step * Mathf.Deg2Rad);
                    for (int k = 0; k < ResidualCount; ++k)
                        jacobian[k, j] = (error[k] - movedError[k]) * invStepRadians;
                }

                if (!SolveDamped(jacobian, error, _damping, delta)) return false;

                // Bound the joint-angle step to keep linearization meaningful.
                double largest = 0;
                for (int j = 0; j < JointCount; ++j)
                    
                    largest = System.Math.Max(largest, System.Math.Abs(delta[j] * Mathf.Rad2Deg));
                double scaling = largest > _maxJointStepDegrees
                    ? _maxJointStepDegrees / largest : 1.0;

                bool accepted = false;
                for (int trial = 0; trial < 9; ++trial)
                {
                    double stepScale = scaling * System.Math.Pow(0.5, trial);
                    for (int j = 0; j < JointCount; ++j)
                    {
                        var lim = _joints[j].Config.Limits;
                        proposal[j] = Mathf.Clamp(
                            q[j] + (float)(delta[j] * Mathf.Rad2Deg * stepScale),
                            lim.x, lim.y);
                    }

                    float newLoss = Measure(proposal, targetPos, targetDir,
                        basis0, basis1, flangeAxis, movedError, out _, out _);
                    if (newLoss >= loss - 1e-12f) continue;

                    Array.Copy(proposal, q, JointCount);
                    accepted = true;
                    break;
                }
                if (!accepted) break;
            }

            Measure(q, targetPos, targetDir, basis0, basis1,
                flangeAxis, error, out float finalDistance, out float finalAngle);
            return finalDistance <= _positionTolerance &&
                   finalAngle <= _directionToleranceDegrees;
        }

        private float Measure(float[] q, Vector3 targetPos, Vector3 targetDir,
            Vector3 basis0, Vector3 basis1, Vector3 flangeAxis,
            float[] error, out float distance, out float directionAngle)
        {
            Matrix4x4 pose = ComputeForward(q);
            var position = pose.MultiplyPoint3x4(Vector3.zero);
            var currentDir = pose.MultiplyVector(flangeAxis).normalized;
            var dp = targetPos - position;

            error[0] = dp.x;
            error[1] = dp.y;
            error[2] = dp.z;
            distance = dp.magnitude;

            float dot = Mathf.Clamp(Vector3.Dot(currentDir, targetDir), -1f, 1f);
            float angle = Mathf.Acos(dot); // radians
            directionAngle = angle * Mathf.Rad2Deg;

            // Signed, shortest-arc direction error in the target's tangent plane.
            Vector3 tangent = currentDir - targetDir * dot;
            float tangentLength = tangent.magnitude;
            Vector3 angularError;
            if (tangentLength > 1e-6f)
                angularError = tangent * (-angle / tangentLength);
            else
                angularError = dot >= 0 ? Vector3.zero : basis0 * Mathf.PI;

            error[3] = Vector3.Dot(angularError, basis0) * _directionWeight;
            error[4] = Vector3.Dot(angularError, basis1) * _directionWeight;

            float result = 0f;
            for (int k = 0; k < ResidualCount; ++k) result += error[k] * error[k];
            return result;
        }

        private static void BuildDirectionBasis(Vector3 dir, out Vector3 u, out Vector3 v)
        {
            Vector3 helper = Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right;
            u = Vector3.Cross(helper, dir).normalized;
            v = Vector3.Cross(dir, u).normalized;
        }

        private static bool SolveDamped(float[,] J, float[] residual,
            float damping, double[] solution)
        {
            // Solve (J^T J + lambda^2 I) * dq = J^T * residual.
            var a = new double[JointCount, JointCount + 1];
            for (int row = 0; row < JointCount; ++row)
            {
                for (int col = 0; col < JointCount; ++col)
                {
                    double sum = 0;
                    for (int k = 0; k < ResidualCount; ++k)
                        sum += J[k, row] * J[k, col];
                    if (row == col) sum += (double)damping * damping;
                    a[row, col] = sum;
                }
                double rhs = 0;
                for (int k = 0; k < ResidualCount; ++k)
                    rhs += J[k, row] * residual[k];
                a[row, JointCount] = rhs;
            }

            // Gaussian elimination with partial pivoting.
            for (int col = 0; col < JointCount; ++col)
            {
                int pivot = col;
                for (int row = col + 1; row < JointCount; ++row)
                    if (System.Math.Abs(a[row, col]) > System.Math.Abs(a[pivot, col])) pivot = row;
                if (System.Math.Abs(a[pivot, col]) < 1e-12) return false;
                if (pivot != col)
                    for (int k = col; k <= JointCount; ++k)
                        (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);

                double diag = a[col, col];
                for (int k = col; k <= JointCount; ++k) a[col, k] /= diag;
                for (int row = col + 1; row < JointCount; ++row)
                {
                    double factor = a[row, col];
                    for (int k = col; k <= JointCount; ++k)
                        a[row, k] -= factor * a[col, k];
                }
            }
            for (int row = JointCount - 1; row >= 0; --row)
            {
                double v = a[row, JointCount];
                for (int k = row + 1; k < JointCount; ++k)
                    v -= a[row, k] * solution[k];
                solution[row] = v;
            }
            return true;
        }

        private static bool IsDuplicate(IKSolution candidate, List<IKSolution> results)
        {
            foreach (var result in results)
            {
                bool identical = true;
                for (int j = 0; j < JointCount; ++j)
                {
                    if (Mathf.Abs(candidate.JointTarget[j] - result.JointTarget[j]) <= 0.5f)
                        continue;
                    identical = false;
                    break;
                }
                if (identical) return true;
            }
            return false;
        }
    }
}
