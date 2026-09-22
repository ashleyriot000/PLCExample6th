using UnityEngine;
using UnityEngine.UI;

public class PositionUIDisplayer : MonoBehaviour
{
    public PositioningManager manager;
    public ServoAmp xAxis, yAxis, zAxis;
    public Text idText, positionXText, positionYText, positionZText;

    private int positionX, positionY, positionZ;


    //처음 생성되서 활성화 될 때(초기화)
    public void Initialize(int id, int x, int y, int z)
    {
        positionX = x;
        positionY = y;
        positionZ = z;
        //표현식 결과는 동일하지만 다양하게 표현식을 사용할 수 있다.
        //idText.text = "P" + id.ToString("D3");
        idText.text = $"P{id:D3}";
        positionXText.text = positionX.ToString();
        positionYText.text = positionY.ToString();
        positionZText.text = positionZ.ToString();

        gameObject.SetActive(true);
    }

    //순서가 변경될 때 처리하는 함수
    public void ChangeIndex(int index)
    {
        idText.text = $"P{index:D3}";
    }

    //삭제될 때 호출되는 함수
    public void Delete(bool removeData)
    {
        if (removeData)
            manager.RemoveData(this);

        Destroy(gameObject);
    }


    //서보앰프에 이동 명령내리는 함수
    public void Go()
    {
        xAxis.Positioning(positionX);
        yAxis.Positioning(positionY);
        zAxis.Positioning(positionZ);
    }

}
