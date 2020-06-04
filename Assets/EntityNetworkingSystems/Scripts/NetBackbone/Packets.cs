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


[System.Serializable]
public class GameObjectInstantiateData
{
    public int prefabDomainID = -1;
    public int prefabID = -1;
    public SerializableVector position;
}

[System.Serializable]
public class SerializableVector
{
    public float x;
    public float y;
    public float z;

    public SerializableVector(float X, float Y, float Z)
    {
        x = X;
        y = Y;
        z = Z;
    }

    public SerializableVector(Vector3 vec)
    {
        x = vec.x;
        y = vec.y;
        z = vec.z;
    }

    public Vector3 ToVec3()
    {
        return new Vector3(x,y,z);
    }
}