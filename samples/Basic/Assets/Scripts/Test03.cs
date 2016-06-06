using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Test03 : MonoBehaviour
{
    void Start()
    {
        GetComponent<Text>().text = "03:" + GetStringLength("Compiler");
#if __COMPILER_OPTION_TEST__
        GetComponent<Text>().text += ":OK";
#endif
    }

    int GetStringLength(string str)
    {
        var len = 0;
        unsafe
        {
            fixed (char* value = str)
            {
                // calculate length with unsafe pointer.
                for (; value[len] != '\0'; len += 1) { }
            }
        }
        return len;
    }
}
