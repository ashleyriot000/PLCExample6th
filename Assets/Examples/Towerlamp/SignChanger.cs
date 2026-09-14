using System;
using System.Collections;
using UnityEngine;

public class SignChanger : MonoBehaviour
{
    public enum LampState
    {
        Red,
        Yellow,
        Green
    }

    public SignController red;
    public SignController yellow;
    public SignController green;


    private void OnEnable()
    {
        StartCoroutine(LoopChange());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    IEnumerator LoopChange()
    {
        while(true)
        {
            green.IsOn = true;
            yield return new WaitForSeconds(10f);

            green.IsOn = false;

            for (int i = 0; i < 5; i++)
            {
                yellow.IsOn = true;
                yield return new WaitForSeconds(1f);
                yellow.IsOn = false;
                yield return new WaitForSeconds(1f);
            }

            red.IsOn = true;
            yield return new WaitForSeconds(10f);
        }
    }





    //public LampState currentState = LampState.Green;
    //public float remainTime = 0f;
    //public int repeatCount = 0;

    //private void Start()
    //{
    //    switch (currentState)
    //    {
    //        case LampState.Red:
    //            red.IsOn = true;
    //            break;
    //        case LampState.Yellow:
    //            yellow.IsOn = true;
    //            break;
    //        case LampState.Green:
    //            green.IsOn = true;
    //            break;
    //    }

    //    remainTime = Time.time + 10f;
    //}


    //void Update()
    //{
    //    switch (currentState)
    //    {
    //        case LampState.Red:
    //            Red();
    //            break;
    //        case LampState.Yellow:
    //            Yellow();
    //            break;
    //        case LampState.Green:
    //            Green();
    //            break;
    //        default:
    //            break;
    //    }
    //}

    //private void Green()
    //{
    //    if(remainTime < Time.time)
    //    {
    //        currentState = LampState.Yellow;
    //        green.IsOn = false;
    //        yellow.IsOn = true;
    //        remainTime = Time.time + 1f;
    //        repeatCount++;
    //    }
    //}

    //private void Yellow()
    //{
    //    if(remainTime < Time.time)
    //    {
    //        yellow.IsOn = !yellow.IsOn;
    //        remainTime = Time.time + 1f;
    //        repeatCount++;

    //        if(repeatCount >= 10)
    //        {
    //            currentState = LampState.Red;
    //            repeatCount = 0;
    //            remainTime = Time.time + 10f;
    //            yellow.IsOn = false;
    //            red.IsOn = true;
    //        }
    //    }
    //}

    //private void Red()
    //{
    //    if (remainTime < Time.time)
    //    {
    //        currentState = LampState.Green;
    //        green.IsOn = true;
    //        red.IsOn = false;
    //        remainTime = Time.time + 10f;
    //    }
    //}
}
