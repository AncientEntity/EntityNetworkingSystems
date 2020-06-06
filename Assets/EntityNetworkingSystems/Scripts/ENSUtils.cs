using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ENSUtils : MonoBehaviour
{
    public static bool IsSimple(System.Type type)
    {
        return type.IsPrimitive || type.Equals(typeof(string));
    }
}
