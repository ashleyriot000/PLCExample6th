
using System;
using System.Collections.Generic;
using System.IO;
using Unity.VisualScripting;
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

        SaveData();
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
        string path = Path.Combine(Application.dataPath, "PositionData.csv");
        if(!File.Exists(path))
        {
            Debug.LogError("파일이 존재하지 않아 불러올 수 없습니다.");
            return;
        }

        string[] csvDatas = File.ReadAllLines(path);
        if(csvDatas.Length > 1)
        {
            //기존 데이터에 연결된 UI데이터들을 삭제함.
            foreach(var position in positionList)
            {
                position.GetUIData.Delete(false);
            }

            //기존 데이터 완전 삭제.
            positionList.Clear();

            //파일 기준으로 데이터를 재구성
            for (int i = 1; i < csvDatas.Length; ++i)
            {
                //','를 기준으로 문자열을 잘라내서 분류해야 함.
                string[] datas = csvDatas[i].Split(',');
                AddData(int.Parse(datas[1]), int.Parse(datas[2]), int.Parse(datas[3]));
            }
        }
        else
        {
            Debug.LogWarning("파일 안에 데이터가 들어있지 않아 불러오기를 취소합니다.");
        }
    }

    private void Start()
    {
        LoadData();
    }
}
