using UnityEngine;

public class WeldingProgram : OfflineProgram
{
    public override void Home()
    {
        this.AccTime(0.2f, 0.2f);
        this.Speed(200f, 60f, 30f);
        this.MoveJ(waypoints[0]);
    }

    public void Program1()
    {
        //1. 속도 설정

        //2. 용접툴 위치로 이동.
        this.MoveJ(waypoints[1]);
        //3. 용접툴 부착.
        this.Connect(true);
        //4. 1초 대기
        this.Wait(1000);
        //5. 작업대 상단 위치로 이동( -> 3 -> 4 -> 5)
        this.MoveL(waypoints[3]);
        this.MoveL(waypoints[4]);
        this.MoveL(waypoints[5]);
        //5-1. 용접툴 켜기
        this.Action(true, 100);

        //6. 실린더 4개의 꼭지점을 시계방향으로 한번씩 찍으면서 이동(-> 우 -> 하 -> 좌 -> 상)
        this.MoveL(waypoints[6]);
        this.MoveL(waypoints[7]);
        this.MoveL(waypoints[8]);
        this.MoveL(waypoints[5]);
        this.Wait(500);

        this.MoveL(waypoints[6], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[7], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[8], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[5]);
        this.Wait(500);

        this.MoveC(waypoints[6], waypoints[7], 200f, 60f, Pattern.Continue);
        this.MoveC(waypoints[8], waypoints[5]);
        this.Action(false);
        this.Wait(300);
        //7. 작업대 상단 위치로 이동(-> 4 -> 3)
        this.MoveJ(waypoints[4]);
        this.MoveJ(waypoints[3]);
        //8. 용접툴 거치대 위치로 이동(2)
        this.MoveJ(waypoints[2]);
        //9. 용접툴 연결 해제
        this.Connect(false);

        //10. 원점으로 복귀
        this.MoveJ(waypoints[0]);


        this.MoveJ(waypoints[10]);
        this.Connect(true);
        this.Wait(100);

        this.MoveL(waypoints[10]);
        this.MoveJ(waypoints[16]);
        this.MoveL(waypoints[12]);

        this.Action(true, 200);
        this.MoveL(waypoints[16], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[15], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[14]);

        this.MoveL(waypoints[13]);
        this.Action(false, 150);
        this.MoveL(waypoints[14]);
        this.Wait(1000);

        this.MoveL(waypoints[13]);
        this.Action(true, 200);

        this.MoveL(waypoints[14], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[15], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[16], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[12]);
        this.Action(false, 150);

        this.MoveL(waypoints[16], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[10], 200f, 60f, Pattern.Continue);
        this.MoveL(waypoints[11]);
        this.Connect(false);

        this.MoveJ(waypoints[0]);
    }

    public void Program2()
    {

    }
}
