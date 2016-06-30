using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Test01 : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Test01.Start");
        GetComponent<Text>().text = "01";

        var temp = GetComponent<Text>().text;
        StartCoroutine(TestCoroutine(
            10, 
            n =>
            {
                return string.Format("<{0}:{1}>", temp, n);
            }));
    }

    IEnumerator TestCoroutine(int a, Func<int, string> b)
    {
        var v = a;
        yield return null;
        GetComponent<Text>().text = v.ToString();
        v += 1;
        yield return null;
        GetComponent<Text>().text = b(v);
    }

    public void Rerun()
    {
        Start();
    }
}

