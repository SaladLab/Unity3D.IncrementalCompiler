using UnityEngine;
using UnityEngine.UI;

public class Test02 : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Test02.Start");
        GetComponent<Text>().text = "02";
    }
}
