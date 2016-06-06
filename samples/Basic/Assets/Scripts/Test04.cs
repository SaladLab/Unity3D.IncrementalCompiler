using System.Collections;
using UnityEngine;

public class Test04 : MonoBehaviour
{
    // warning test

    private Hashtable table;  // CS0649

    private int x = 1;        // CS0414

    public void Func(object o, string p)
    {
        int j;                // CS0168
        table[p] = o;
    }
}
