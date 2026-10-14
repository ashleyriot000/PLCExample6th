using Preliy.Flange;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;


public interface IRobotCommand
{
    Task ExecuteCommand(TargetScheduler scheduler);
}

public class Wait : IRobotCommand
{
    private int ms;

    public Wait(int milliseconds)
    {
        ms = milliseconds;
    }
    public async Task ExecuteCommand(TargetScheduler scheduler)
    {
        await Task.Delay(ms);
        Debug.LogWarning($"{ms}ms 대기완료");
    }
}

public class AccTime : IRobotCommand
{
    private OfflineProgram program;
    private float accelTime;
    private float decelTime;

    public AccTime(OfflineProgram program, float accelTime, float decelTime)
    {
        this.program = program;
        this.accelTime = accelTime;
        this.decelTime = decelTime;
    }

    public async Task ExecuteCommand(TargetScheduler scheduler)
    {
        program.AccelTime = accelTime;
        program.DeccelTime = decelTime;

        await Task.Yield();
    }
}

public class Speed : IRobotCommand
{
    private OfflineProgram program;
    private float linearSpeed;
    private float angularSpeed;
    private float jointSpeed;

    public Speed(OfflineProgram program, float linearSpeed, float angularSpeed, float jointSpeed)
    {
        this.program = program;
        this.linearSpeed = linearSpeed;
        this.angularSpeed = angularSpeed;
        this.jointSpeed = jointSpeed;
    }
    public async Task ExecuteCommand(TargetScheduler scheduler)
    {
        program.LinearSpeed = linearSpeed;
        program.AngularSpeed = angularSpeed;
        program.JointSpeed = jointSpeed;
        Debug.LogWarning($"TCP 최대 이동 속도 : {linearSpeed}mm/s, TCP 최대 회전 속도 : {angularSpeed}°/s, 조인트 최대 회전 속도 : {jointSpeed}°/s로 변경되었습니다.");
        await Task.Yield();
    }
}

public class Connect : IRobotCommand
{
    private bool connected;
    public Connect(bool connected)
    {
        this.connected = connected;
    }

    public async Task ExecuteCommand(TargetScheduler scheduler)
    {
        if (connected)
            scheduler.ConnectTool();
        else
            scheduler.ReleaseTool();

        await Task.Delay(100);
    }
}

public class ToolAction : IRobotCommand
{
    private bool isOn;
    private int delay;
    public ToolAction(bool isOn, int delay)
    {
        this.isOn = isOn;
        this.delay = delay;
    }

    public async Task ExecuteCommand(TargetScheduler scheduler)
    {
        scheduler.ActTool(isOn);

        await Task.Delay(delay);
    }
}
public class MoveJ : IRobotCommand
{
    private OfflineProgram program;
    private TargetWaypoint target;
    private float jointSpeed;
    private Pattern pattern;
    private float zoneAngle;
    private int dwellTime;
    public MoveJ(OfflineProgram program, TargetWaypoint target, float jointSpeed, Pattern pattern, float zoneAngle, int dwellTime)
    {
        this.program = program;
        this.target = target;
        this.jointSpeed = jointSpeed;
        this.pattern = pattern;
        this.zoneAngle = zoneAngle;
        this.dwellTime = dwellTime;
    }

    public async Task ExecuteCommand(TargetScheduler scheduler)
    {
        if (jointSpeed > 0f)
            program.JointSpeed = jointSpeed;

        MechanicalGroup group = scheduler.GetController.MechanicalGroup;
        int count = group.Joints.Count;
        //x : 시작 각도, y : 끝 각도, z : deltaAngle, w : 목표 속도
        float4[] joints = new float4[count];
        float[] currentJoints = new float[count];
        var solution = scheduler.GetSolution(target.transform.GetMatrix());

        float maxDiff = 0f;
        int maxDiffIndex = -1;
        for (int i = 0; i < count; i++)
        {
            currentJoints[i] = joints[i].x = group.Joints[i].Position.Value;
            joints[i].y = solution.JointTarget[i];

            // 가장 많이 회전해야 하는 관절의 각도 차이를 구함 (지배축 산출)
            joints[i].z = Mathf.Abs(joints[i].y - joints[i].x);
            if (joints[i].z > maxDiff)
            {
                maxDiff = joints[i].z;
                maxDiffIndex = i;
            }
        }

        //컨티뉴 모드 처리
        if (pattern.HasFlag(Pattern.Continue))
            maxDiff = Mathf.Max(0f, maxDiff - zoneAngle);

        float duration = maxDiff / program.JointSpeed;
        for (int i = 0; i < count; ++i)
        {
            joints[i].w = joints[i].z / duration;
        }

        jointSpeed = maxDiff / duration;
        float startJointSpeed = program.CurrentJointSpeed;
        float accelTime = program.AccelTime;
        float decelTime = program.DeccelTime;

        // 가감속 회전 각도
        float accelAngle = accelTime * jointSpeed * 0.5f;
        float decelAngle = decelTime * jointSpeed * 0.5f;

        float accelAngleRate = jointSpeed / accelTime;
        float decelAngleRate = jointSpeed / decelTime;
        float maxAngleRate = jointSpeed * 2f;

        Vector3 prevPosition = scheduler.transform.position;
        Quaternion prevRotation = scheduler.transform.rotation;

        float currentRotatedAngle = 0f;
        while (currentRotatedAngle < maxDiff)
        {
            float dt = Time.deltaTime;
            float remainingAngle = maxDiff - currentRotatedAngle;
            //회전 가감속 프로파일 제어

            if (remainingAngle < decelAngle && pattern == Pattern.End)
            {
                program.CurrentJointSpeed = Mathf.MoveTowards(program.CurrentJointSpeed, 0, decelAngleRate * dt);
                if (program.CurrentJointSpeed < 1f) program.CurrentJointSpeed = 1f;
            }
            else if (currentRotatedAngle < accelAngle)
            {
                program.CurrentJointSpeed = Mathf.MoveTowards(program.CurrentJointSpeed, jointSpeed, accelAngleRate * dt);
            }
            else
            {
                program.CurrentJointSpeed = Mathf.MoveTowards(program.CurrentJointSpeed, jointSpeed, maxAngleRate * dt);
            }

            // 이번 프레임동안 회전한 각도 계산
            //수학적으로 제곱근(Square Root, sqrt{x})을 구하는 공식.
            //값이 커질 수록 초반에는 빠르게 커지지만 1에 가까워질수록 커지는 속도가 둔화되는 효과
            currentRotatedAngle += program.CurrentJointSpeed * dt;
            float t = pattern == Pattern.Rotate ? Mathf.Sqrt(currentRotatedAngle / maxDiff) : currentRotatedAngle / maxDiff;

            for (int i = 0; i < count; ++i)
            {
                currentJoints[i] = Mathf.Lerp(joints[i].x, joints[i].y, t);
            }
            group.Joints.SetJointValues(currentJoints);

            Transform tcp = null;
            if (scheduler.currentAttachedTool != null)
            {
                tcp = scheduler.currentAttachedTool.Point;
                scheduler.SyncTransformQuietly(tcp.position, tcp.rotation);
            }
            else
            {
                tcp = scheduler.mountPoint;
                scheduler.SyncTransformQuietly(target.position, target.rotation);
            }

            program.CurrentLinearSpeed = (tcp.position - prevPosition).magnitude / dt;
            program.CurrentAngularSpeed = Quaternion.Angle(tcp.rotation, prevRotation) / dt;
            await Task.Yield();
        }

        if (pattern == Pattern.End)
        {
            program.CurrentJointSpeed = 0f;
            program.CurrentLinearSpeed = 0f;
            program.CurrentAngularSpeed = 0f;

            if (dwellTime > 0f)
                await Task.Delay(dwellTime);
        }
    }
}

public class MoveL : IRobotCommand
{
    private OfflineProgram program;
    private TargetWaypoint target;
    private float linearSpeed;
    private float angularSpeed;
    private int dwellTime;
    private Pattern pattern;
    private float zoneRadius;

    public MoveL(OfflineProgram program, TargetWaypoint target, float linearSpeed, float angularSpeed, Pattern pattern, float zoneRadius, int dwellTime)
    {
        this.program = program;
        this.target = target;
        this.linearSpeed = linearSpeed;
        this.angularSpeed = angularSpeed;
        this.pattern = pattern;
        this.zoneRadius = zoneRadius;
        this.dwellTime = dwellTime;

    }
    public async Task ExecuteCommand(TargetScheduler scheduler)
    {
        if (linearSpeed > 0f)
            program.LinearSpeed = linearSpeed;

        if (angularSpeed > 0f)
            program.AngularSpeed = angularSpeed;

        Vector3 startPoint = scheduler.transform.position;
        Quaternion startAngle = scheduler.transform.rotation;

        Vector3 endPoint = target.position;
        Quaternion endAngle = target.rotation;
        //1. 전체 이동 거리와 회전 각도 산출
        float totalDistance = Vector3.Distance(startPoint, endPoint);
        float totalAngle = Quaternion.Angle(startAngle, endAngle);

        //2. Continue모드 처리
        if (pattern == Pattern.Continue && zoneRadius > 0f)
            totalDistance = Mathf.Max(0f, totalDistance - zoneRadius);

        //3. 설정 속도로 이동/회전했을 때 필요한 순수 소요시간 계산
        float linearDuration = totalDistance / program.LinearSpeed;
        float angularDuration = totalAngle / program.AngularSpeed;

        float duration = Mathf.Max(linearDuration, angularDuration);

        //4. 도착 지점에 동시에 도달하도록 동기화. 더 빠른쪽의 속도를 늦춤(Scale down)
        linearSpeed = totalDistance / duration;
        angularSpeed = totalAngle / duration;

        //3. 선형 및 회전 속도 모멘텀과 목표 속도 설정
        float startLinearSpeed = program.CurrentLinearSpeed;
        float startAngularSpeed = program.CurrentAngularSpeed;

        float accelTime = program.AccelTime;
        float decelTime = program.DeccelTime;

        float currentTraveledDistance = 0f;
        float currentRotatedAngle = 0f;

        // accelTime * maxLinearSpeed: 만약 속도가 처음부터 최고 속도로 즉시 점프해서 일정하게 유지되었다고 
        // 가정했을 때 이동했을 전체 면적(직사각형 넓이).
        // 0.5f (삼각형의 넓이 공식): 속도가 0부터 시작해서 직선으로 서서히 증가(Mathf.MoveTowards)하므로, 
        // 그 그래프는 직사각형이 아니라 직각삼각형 모양이 됨. 
        // 따라서 직사각형 넓이의 절반인 삼각형의 넓이를 구하기 위해 0.5를 곱해주는 것
        // 0 -> 목표속도까지 가속했을 때 이동하는 거리
        float accelDistance = accelTime * linearSpeed * 0.5f;
        // 목표속도 -> 0까지 감속했을 때 이동하는 거리
        float decelDistance = decelTime * linearSpeed * 0.5f;

        float accelRate = linearSpeed / accelTime;
        float decelRate = linearSpeed / decelTime;
        float maxAccelRate = linearSpeed * 2.0f;

        // 가감속 회전 각도
        float accelAngle = accelTime * angularSpeed * 0.5f;
        float decelAngel = decelTime * angularSpeed * 0.5f;

        float accelAngleRate = angularSpeed / accelTime;
        float decelAngleRate = angularSpeed / decelTime;
        float maxAngleRate = angularSpeed * 2f;

        MechanicalGroup group = scheduler.GetController.MechanicalGroup;
        float[] prevJoints = group.Joints.GetJointValues();

        while (currentTraveledDistance < totalDistance || currentRotatedAngle < totalAngle)
        {
            float dt = Time.deltaTime;
            float remainingDistance = totalDistance - currentTraveledDistance;
            float remainingAngle = totalAngle - currentRotatedAngle;


            //선형 가감속 프로파일 제어      
            if (remainingDistance < decelDistance && pattern == Pattern.End)
            {
                program.CurrentLinearSpeed = Mathf.MoveTowards(program.CurrentLinearSpeed, 0f, decelRate * dt);
                if (program.CurrentLinearSpeed < 0.01f) program.CurrentLinearSpeed = 0.01f;
            }
            else if (currentTraveledDistance < accelDistance)
            {
                program.CurrentLinearSpeed = Mathf.MoveTowards(program.CurrentLinearSpeed, linearSpeed, accelRate * dt);
            }
            else
            {
                program.CurrentLinearSpeed = Mathf.MoveTowards(program.CurrentLinearSpeed, linearSpeed, maxAccelRate * dt);
            }



            //회전 가감속 프로파일 제어      
            if (remainingAngle < decelAngel && pattern == Pattern.End)
            {
                program.CurrentAngularSpeed = Mathf.MoveTowards(program.CurrentAngularSpeed, 0, decelAngleRate * dt);
                if (program.CurrentAngularSpeed < 0.5f) program.CurrentAngularSpeed = 0.5f;
            }
            else if (currentRotatedAngle < accelAngle)
            {
                program.CurrentAngularSpeed = Mathf.MoveTowards(program.CurrentAngularSpeed, angularSpeed, accelAngleRate * dt);
            }
            else
            {
                program.CurrentAngularSpeed = Mathf.MoveTowards(program.CurrentAngularSpeed, angularSpeed, maxAngleRate * dt);
            }



            // 이번 프레임 이동 거리 계산
            currentTraveledDistance += program.CurrentLinearSpeed * dt;
            currentRotatedAngle += program.CurrentAngularSpeed * dt;

            Vector3 currentPos = Vector3.MoveTowards(startPoint, endPoint, currentTraveledDistance);

            //Mathf.Pow(masterRatio, 0.5f)는 수학적으로 제곱근(Square Root, sqrt{x})을 구하는 공식.
            //값이 커질 수록 초반에는 빠르게 커지지만 1에 가까워질수록 커지는 속도가 둔화되는 효과
            Quaternion currentRot = pattern == Pattern.Rotate ?
                Quaternion.Slerp(startAngle, endAngle, Mathf.Pow(currentRotatedAngle / totalAngle, 0.5f))
                : Quaternion.RotateTowards(startAngle, endAngle, currentRotatedAngle);
            scheduler.transform.SetPositionAndRotation(currentPos, currentRot);

            float maxDelta = 0f;
            for (int i = 0; i < 6; i++)
            {
                float current = group.Joints[i].Position.Value;
                float delta = Mathf.Abs(current - prevJoints[i]);
                prevJoints[i] = current;

                if (delta > maxDelta)
                    maxDelta = delta;

                // 다음 프레임 비교를 위해 갱신
            }
            program.CurrentJointSpeed = maxDelta > 0f ? maxDelta / dt : 0f;
            await Task.Yield();
        }

        scheduler.transform.SetPositionAndRotation(endPoint, endAngle);
        if (pattern == Pattern.End)
        {
            program.CurrentLinearSpeed = 0f;
            program.CurrentAngularSpeed = 0f;

            if (dwellTime > 0f)
                await Task.Delay(dwellTime);
        }
    }
}


public class MoveC : IRobotCommand
{
    private OfflineProgram program;
    private TargetWaypoint viaPoint;
    private TargetWaypoint endPoint;
    private float linearSpeed;
    private float angularSpeed;
    private Pattern pattern;
    private float zoneRadius;
    private int dwellTime;

    public MoveC(OfflineProgram program, TargetWaypoint viaPoint, TargetWaypoint endPoint, float linearSpeed, float angularSpeed, Pattern pattern, float zoneRadius, int dwellTime)
    {
        this.program = program;
        this.viaPoint = viaPoint;
        this.endPoint = endPoint;
        this.linearSpeed = linearSpeed;
        this.angularSpeed = angularSpeed;
        this.pattern = pattern;
        this.zoneRadius = zoneRadius;
        this.dwellTime = dwellTime;
    }

    public async Task ExecuteCommand(TargetScheduler scheduler)
    {
        if (linearSpeed > 0f)
            program.LinearSpeed = linearSpeed;

        if (angularSpeed > 0f)
            program.AngularSpeed = angularSpeed;

        if (linearSpeed > 0f)
            program.LinearSpeed = linearSpeed;

        if (angularSpeed > 0f)
            program.AngularSpeed = angularSpeed;

        // 1. 3개의 핵심 포인트 좌표 및 회전값 추출
        Quaternion rStart = scheduler.transform.rotation;
        Quaternion rEnd = endPoint.rotation;

        // 2. 3점을 지나는 원의 중심(Center) 계산
        Vector3 center = CalculateCircleCenter(scheduler.transform.position, viaPoint.position, endPoint.position, out Vector3 normal);
        if (float.IsInfinity(center.x) || float.IsNaN(center.x))
        {
            Debug.LogWarning("세 점이 일직선에 가까워 원호(Arc)를 생성할 수 없습니다. 모션을 종료합니다.");
            return;
        }

        // 3. 중심점을 기준으로 시작 벡터와 종료 벡터 도출
        Vector3 vStart = scheduler.transform.position - center;
        Vector3 vEnd = endPoint.position - center;

        // 5. 경유지(Via) 방향을 향하는 정확한 전체 회전 각도(Signed Angle) 계산
        float arcAngle = Vector3.SignedAngle(vStart, vEnd, normal);

        // SignedAngle이 음수가 나왔다는 것은 최단거리가 역방향이라는 의미이므로, 
        // 360도를 더해 무조건 경유지 방향을 거쳐가는 거대한 원호(Major Arc)로 강제 변환
        if (arcAngle < 0f)
        {
            arcAngle += 360f;
        }

        // 6. 이동해야 할 원호의 실제 길이(Arc Length) 계산
        float radius = vStart.magnitude;
        float arcLength = (arcAngle / 360f) * 2f * Mathf.PI * radius;
        if (pattern == Pattern.Continue && zoneRadius > 0f)
            arcLength -= zoneRadius;

        float totalAngle = Quaternion.Angle(rStart, rEnd);
        // 7. 소요 시간 계산

        float arcDuration = arcLength / program.LinearSpeed;
        float angleDuration = totalAngle / program.AngularSpeed;
        float duration = arcDuration > angleDuration ? arcDuration : angleDuration;

        //3. 선형 및 회전 속도 모멘텀과 목표 속도 설정
        float startLinearSpeed = program.CurrentLinearSpeed;
        float startAngularSpeed = program.CurrentAngularSpeed;

        linearSpeed = arcLength / duration;
        angularSpeed = totalAngle / duration;

        float accelTime = program.AccelTime;
        float decelTime = program.DeccelTime;
        // 0 -> 목표속도까지 가속했을 때 이동하는 거리
        float accelDistance = accelTime * linearSpeed * 0.5f;
        // 목표속도 -> 0까지 감속했을 때 이동하는 거리
        float decelDistance = decelTime * linearSpeed * 0.5f;

        float accelRate = linearSpeed / accelTime;
        float decelRate = linearSpeed / decelTime;
        float maxAccelRate = program.LinearSpeed * 2.0f;

        // 가감속 회전 각도
        float accelAngle = accelTime * angularSpeed * 0.5f;
        float decelAngle = decelTime * angularSpeed * 0.5f;

        float accelAngleRate = angularSpeed / accelTime;
        float decelAngleRate = angularSpeed / decelTime;
        float maxAngleRate = angularSpeed * 2f;

        float currentTraveledDistance = 0f;
        float currentRotatedAngle = 0f;

        MechanicalGroup group = scheduler.GetController.MechanicalGroup;
        float[] prevJoints = group.Joints.GetJointValues();

        // 8. Cartesian Space(직교 공간) 원호 보간 루프
        while (currentTraveledDistance < arcLength || currentRotatedAngle < totalAngle)
        {
            float dt = Time.deltaTime;
            float remainingDistance = arcLength - currentTraveledDistance;
            float remainingAngle = totalAngle - currentRotatedAngle;
            //선형 가감속 프로파일 제어      
            if (remainingDistance < decelDistance && pattern == Pattern.End)
            {
                program.CurrentLinearSpeed = Mathf.MoveTowards(program.CurrentLinearSpeed, 0f, decelRate * dt);
                if (program.CurrentLinearSpeed < 0.01f) program.CurrentLinearSpeed = 0.01f;
            }
            else if (currentTraveledDistance < accelDistance)
            {
                program.CurrentLinearSpeed = Mathf.MoveTowards(program.CurrentLinearSpeed, linearSpeed, accelRate * dt);
            }
            else
            {
                program.CurrentLinearSpeed = Mathf.MoveTowards(program.CurrentLinearSpeed, linearSpeed, maxAccelRate * dt);
            }



            //회전 가감속 프로파일 제어      
            if (remainingAngle < decelAngle && pattern == Pattern.End)
            {
                program.CurrentAngularSpeed = Mathf.MoveTowards(program.CurrentAngularSpeed, 0f, decelAngleRate * dt);
                if (program.CurrentAngularSpeed < 0.5f) program.CurrentAngularSpeed = 0.5f;
            }
            else if (currentRotatedAngle < accelAngle)
            {
                program.CurrentAngularSpeed = Mathf.MoveTowards(program.CurrentAngularSpeed, angularSpeed, accelAngleRate * dt);
            }
            else
            {
                program.CurrentAngularSpeed = Mathf.MoveTowards(program.CurrentAngularSpeed, angularSpeed, maxAngleRate * dt);
            }

            //현재 가감속된 속도를 기반의 위치/회전값 적용.
            currentTraveledDistance += program.CurrentLinearSpeed * dt;
            currentRotatedAngle += program.CurrentAngularSpeed * dt;

            //이동한 Arc거리
            float currentAngle = arcAngle * (currentTraveledDistance / arcLength);
            Quaternion arcRotation = Quaternion.AngleAxis(currentAngle, normal);
            Vector3 currentArcPos = center + (arcRotation * vStart);

            Quaternion currentRot = pattern == Pattern.Rotate ?
                    Quaternion.Slerp(rStart, rEnd, Mathf.Pow(currentRotatedAngle / totalAngle, 0.5f))
                    : Quaternion.RotateTowards(rStart, rEnd, currentRotatedAngle);


            // 역기구학 솔버에 현재 프레임의 원호 궤적 매트릭스 전달
            scheduler.transform.SetPositionAndRotation(currentArcPos, currentRot);


            //조인트의 현재 회전량 확인 및 적용
            float maxDelta = 0f;
            for (int i = 0; i < 6; i++)
            {
                float current = group.Joints[i].Position.Value;
                float delta = Mathf.Abs(current - prevJoints[i]);
                prevJoints[i] = current;

                if (delta > maxDelta)
                    maxDelta = delta;
            }
            program.CurrentJointSpeed = maxDelta > 0f ? maxDelta / dt : 0f;

            await Task.Yield();
        }

        scheduler.transform.SetPositionAndRotation(endPoint.position, endPoint.rotation);
        if (pattern == Pattern.End)
        {
            program.CurrentLinearSpeed = 0f;
            program.CurrentAngularSpeed = 0f;
            program.CurrentJointSpeed = 0f;

            if (dwellTime > 0f)
                await Task.Delay(dwellTime);
        }
    }


    /// <summary>
    /// 3D 공간에서 세 점을 지나는 삼각형의 외심(Circumcenter)을 계산하는 기하학 함수
    /// </summary>
    private Vector3 CalculateCircleCenter(Vector3 a, Vector3 b, Vector3 c, out Vector3 normal)
    {
        Vector3 v1 = b - a;
        Vector3 v2 = c - a;

        // 두 벡터의 외적을 통해 세 점이 이루는 평면의 법선 벡터(Normal)를 구함
        normal = Vector3.Cross(v1, v2);

        // 세 점이 일직선상에 있어 삼각형을 이루지 못하는 경우 (예외 처리)
        if (normal.sqrMagnitude < 0.00001f)
        {
            Debug.LogWarning("세 점이 일직선상에 있어 원호를 그릴 수 없습니다. 중간 좌표를 반환합니다.");
            return Vector3.Lerp(a, c, 0.5f);
        }

        // 3D 외심 계산 공식 적용
        Vector3 center = a + Vector3.Cross(v1.sqrMagnitude * v2 - v2.sqrMagnitude * v1, normal) / (2f * normal.sqrMagnitude);

        return center;
    }
}
