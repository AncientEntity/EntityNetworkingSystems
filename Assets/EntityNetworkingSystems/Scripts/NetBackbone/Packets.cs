using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

[System.Serializable]
public class Packet
{
    //public static BinaryFormatter bF = null;

    [System.Serializable]
    public enum sendType
    {
        buffered,
        nonbuffered,
    }
    public sendType packetSendType = sendType.buffered;

    [System.Serializable]
    public enum pType
    {
        gOInstantiate,
        gODestroy,
        netVarEdit,
        rpc,
        message,
        unassigned,
        multiPacket,
        loginInfo,
        netObjNetStartInvoke,
        steamAuth,
    }
    public pType packetType = pType.unassigned;

    public string jsonData;
    public string jsonDataTypeName;

    public int packetOwnerID = NetTools.clientID; //If the client tries lying to server, the server verifies it in NetServer anyways...
    public bool serverAuthority = false; //Manually changed in NetServer. Client changing it wont effect other clients/server.
    public bool sendToAll = true;
    public List<int> usersToRecieve = new List<int>(); //if send to all is false.
    public int relatesToNetObjID = -1; 


    public Packet(pType packetType, sendType typeOfSend,object obj)
    {
        this.packetType = packetType;
        this.packetSendType = typeOfSend;
        SetPacketData(obj);
    }

    public Packet(object obj)
    {
        SetPacketData(obj);
    }

    public void SetPacketData(object data)
    {
        object formattedData = data;
        jsonDataTypeName = data.GetType().ToString();


        if (data.GetType().ToString() == "System.Int32" || data.GetType().ToString() == "System.Int16" || data.GetType().ToString() == "System.Int64")
        {
            //Automatically put it into an IntPacket so JsonUtility will jsonify it.
            formattedData = new IntPacket((int)data);
            jsonDataTypeName = formattedData.GetType().ToString();
        }

        jsonData = JsonUtility.ToJson(formattedData);
        
    }

    public object GetPacketData()
    {
        System.Type t = System.Type.GetType(jsonDataTypeName);
        //Debug.Log(t);
        if (t.ToString() == "IntPacket")
        {
            //If integer you must first convert it out of a IntPacket.
            return JsonUtility.FromJson<IntPacket>(jsonData).integer;
        }
        else
        {
            return JsonUtility.FromJson(jsonData, t);
        }
    }

    public static Packet DeJsonifyPacket(string jsonPacket)
    {
        //Debug.Log(jsonPacket);
        return JsonUtility.FromJson<Packet>(jsonPacket);
    }

    public static string JsonifyPacket(Packet packet)
    {
        return JsonUtility.ToJson(packet);
    }

    public static Packet DeserializePacket(byte[] serialized)
    {
        BinaryFormatter bF = new BinaryFormatter();

        using (MemoryStream ms = new MemoryStream(serialized))
        {
            //byte[] b = new byte[ms.Length];
            //ms.Seek(0, SeekOrigin.Begin);
            Debug.Log(serialized.Length);
            Packet decoded = (Packet)bF.Deserialize(ms);
            return decoded;
        }
    }


    public static byte[] SerializePacket(Packet packet)
    {
        BinaryFormatter bF = new BinaryFormatter();

        using (MemoryStream ms = new MemoryStream())
        {
            bF.Serialize(ms, packet);
            return ms.ToArray();
        }

    }

    public byte[] SelfSerialize()
    {
        return SerializePacket(this);
    }


}

[System.Serializable]
public class PlayerLoginData
{
    public int playerNetworkID = -1;
}

[System.Serializable]
public class GameObjectInstantiateData
{
    public int prefabDomainID = -1;
    public int prefabID = -1;
    public SerializableVector position;
    public SerializableQuaternion rotation;

    public int netObjID = -1;
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

[System.Serializable]
public class SerializableQuaternion
{
    public float x = 0f;
    public float y = 0f;
    public float z = 0f;
    public float w = 0f;

    public SerializableQuaternion(Quaternion q)
    {
        x = q.x;
        y = q.y;
        z = q.z;
        w = q.w;
    }

    public Quaternion ToQuaternion()
    {
        return new Quaternion(x, y, z, w);
    }
}

[System.Serializable]
public class JsonPacketObject
{
    public string jsonData;
    public string jsonDataTypeName;

    public JsonPacketObject(string data,string jsonTypeName)
    {
        jsonData = data;
        jsonDataTypeName = jsonTypeName;
    }

    public object ToObject()
    {
        return JsonUtility.FromJson(jsonData, System.Type.GetType(jsonDataTypeName));
    }

}


[System.Serializable]
public class PacketListPacket
{
    public List<Packet> packets = new List<Packet>();

    public PacketListPacket(List<Packet> packets)
    {
        this.packets = packets;
    }
}

[System.Serializable]
public class IntPacket {
    public int integer = 0;

    public IntPacket(int integer)
    {
        this.integer = integer;
    }
}

[System.Serializable]
public class FloatPacket
{
    public float value = 0f;
    public FloatPacket(float val)
    {
        value = val;
    }
}

[System.Serializable]
public class SteamAuthPacket
{
    public byte[] authData;
    public ulong steamID;

    public SteamAuthPacket(byte[] data, ulong sID)
    {
        authData = data;
        steamID = sID;
    }
}