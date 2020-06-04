using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

[System.Serializable]
public class Packet
{
    static BinaryFormatter bF = null;

    public enum pType
    {
        gOInstantiate,
        gODestroy,
        netVarEdit,
        rpc,
        message,
        unassigned,
    }
    public pType packetType = pType.unassigned;
    public object data;

    public Packet(object obj)
    {
        data = obj;
    }


    public static Packet DeserializePacket(byte[] serialized)
    {
        if (bF == null)
        {
            bF = new BinaryFormatter();
        }
        MemoryStream ms = new MemoryStream(serialized);
        byte[] b = new byte[ms.Length];
        ms.Seek(0, SeekOrigin.Begin);
        return (Packet)bF.Deserialize(ms);
    }


    public static byte[] SerializePacket(Packet packet)
    {
        if(bF == null)
        {
            bF = new BinaryFormatter();
        }
        MemoryStream ms = new MemoryStream();
        bF.Serialize(ms, packet);
        return ms.ToArray();
    }

    public byte[] SelfSerialize()
    {
        return SerializePacket(this);
    }


}