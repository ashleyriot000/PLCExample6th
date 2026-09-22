
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

public class PositioningManager : MonoBehaviour
{
    [System.Serializable]
    public class Position
    {
        public int x,y,z;

        private PositionUIDisplayer uiData;

        public PositionUIDisplayer GetUIData => uiData;

        //생성자
        public Position(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        //데이터와 연계된 UI를 연결하는 함수
        public void ConnectUI(PositionUIDisplayer uiData)
        {
            this.uiData = uiData;
        }
    }


    //ui displayer 원본
    public PositionUIDisplayer origin;

    public List<Position> positionList = new();
    public ServoAmp xAxis;
    public ServoAmp yAxis;
    public ServoAmp zAxis;

    //UI 버튼을 눌렀을 때 데이터 추가.
    public void AddData()
    {
        AddData(xAxis.GetCurrentPulse, yAxis.GetCurrentPulse, zAxis.currentPulse, true);
    }

    public void AddData(int x,  int y, int z, bool needSave = false)
    {
        Position pos = new Position(x, y, z);
        positionList.Add(pos);
        PositionUIDisplayer uiData = Instantiate(origin, transform);
        uiData.Initialize(positionList.Count, x, y, z);
        pos.ConnectUI(uiData);

        if (needSave)
            SaveData();

    }

    public void RemoveData(PositionUIDisplayer uiData)
    {
        //삭제하고 싶은 UI와 연결된 위치결정 데이터를 찾는다.
        Position pos = positionList.Find(x => x.GetUIData == uiData);
        //데이터를 찾았다면 리스트에서 지운다.
        if(pos != null)
        {
            positionList.Remove(pos);
        }

        //바뀐 리스트 순서에 맞게 UI 아이디를 수정한다.
        for (int i = 0; i < positionList.Count; i++)
        {
            positionList[i].GetUIData.ChangeIndex(i + 1);
        }
    }


    private void SaveData()
    {
        string path = Path.Combine(Application.dataPath, "PositionData.csv");

        string[] csvDatas = new string[positionList.Count + 1];
        csvDatas[0] = "Position ID, Axis X, Axis Y, Axis Z";
        for (int i = 0; i < positionList.Count; ++i)
        {
            csvDatas[i + 1] = i.ToString() + ',' +
                positionList[i].x.ToString() + ',' +
                positionList[i].y.ToString() + ',' +
                positionList[i].z.ToString();
        }

        //해당 파일 경로로 저장하기
        File.WriteAllLines(path, csvDatas);
        
    }

    private void LoadData()
    {

    }
}
