using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EntityNetworkingSystems
{
    public class ENSUtils : MonoBehaviour
    {
        public static bool IsSimple(System.Type type)
        {
            return type.IsPrimitive || type.Equals(typeof(string));
        }
    }

}