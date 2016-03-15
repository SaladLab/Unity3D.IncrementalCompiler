using UnityEngine;
using UnityEngine.UI;

public class Test02 : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Test02.Start");
        GetComponent<Text>().text = "02:" + Test(-1, 3.5f);
    }

    string Test(int param_a, float param_b)
    {
        int local_c = (int)(param_a + param_b);
        if (param_a > 0)
        {
            string local_d = "A+" + param_b;
            return local_d;
        }
        else
        {
            string local_e = "A-" + param_b;
            return local_e;
        }
    }
}
