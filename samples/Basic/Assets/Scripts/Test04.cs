using System.Collections;
using UnityEngine;

public class Test04 : MonoBehaviour
{
    // warning test

    [SerializeField]
    private Hashtable tableSerialized;   // CS0649
    private Hashtable tableAssigned;     // CS0649

    [SerializeField]
    private int xSerialized = 1;         // CS0414
    private int xAssigned = 1;           // CS0414

    public void Func(object o, string p)
    {
        int j;                          // CS0168
        tableSerialized[p] = o;
        tableAssigned[p] = o;
    }
}
