using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Linq;

namespace EntityNetworkingSystems
{
    public class ENSUtils : MonoBehaviour
    {
        public static bool IsSimple(System.Type type)
        {
            return type.IsPrimitive || type.Equals(typeof(string));
        }

        public static string BytesToString(byte[] bytes)
        {

            return Encoding.ASCII.GetString(bytes);

            //string newString = "";
            //foreach(byte b in bytes)
            //{
            //    newString = newString + System.Convert.ToChar(b);
            //}
            //return newString;
        }

        public static byte[] StringToBytes(string str)
        {

            return Encoding.ASCII.GetBytes(str);

            //List<byte> byteList = new List<byte>();

            //foreach(char c in str)
            //{
            //    byteList.Add(System.Convert.ToByte(c));
            //}
            //return byteList.ToArray();
        }

        //public static Dictionary<T0, T1> CloneDictionary<T0,T1>(Dictionary<T0,T1> toBeCloned) {
        //    Dictionary<T0, T1> newDictionary = new Dictionary<T0, T1>();

        //    List<T0> keys = newDictionary.Keys.ToList();
        //    List<T1> values = newDictionary.Values.ToList();

        //    for(int i = 0; i < keys.Count; i++)
        //    {
        //        newDictionary.Add(keys[i], values[i]);
        //    }
        //    return newDictionary;
        //}


    }

}