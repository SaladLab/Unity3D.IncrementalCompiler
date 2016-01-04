using UnityEngine;
using UnityEngine.UI;

public class Test01 : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Test01.Start");
        GetComponent<Text>().text = "01";
    }
}
