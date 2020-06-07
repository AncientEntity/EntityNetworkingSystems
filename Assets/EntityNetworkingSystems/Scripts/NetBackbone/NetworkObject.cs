using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NetworkObject : MonoBehaviour
{
    //NETWORK OBJECT AUTOMATICALLY GETS ADDED TO PREFAB WHEN INSTANTIATED OVER THE NETWORK.

    public static List<NetworkObject> allNetObjs = new List<NetworkObject>();

    public bool initialized = false; //Has it been initialized on the network?
    [Space]
    public int networkID = -1;
    public int ownerID = -1;
    public List<NetworkField> fields = new List<NetworkField>();
    public List<RPC> rpcs = new List<RPC>();
    [Space]
    public UnityEvent onNetworkStart;

    [HideInInspector]
    public int prefabID = -1;
    [HideInInspector]
    public int prefabDomainID = -1;
    


    void Awake()
    {
        //onNetworkStart.Invoke(); //Invokes inside of UnityPacketHandler

    }

    public void Initialize()
    {
        if(networkID == -1)
        {
            networkID = NetTools.GenerateNetworkObjectID();
        }

        if (NetworkData.usedNetworkObjectInstances.Contains(networkID) == false && networkID != -1)
        {
            NetworkData.AddUsedNetID(networkID);
        }

        if (NetworkObject.allNetObjs.Contains(this) == false)
        {
            allNetObjs.Add(this);
        }
        foreach (NetworkField field in fields)
        {
            field.InitializeDefaultValue(networkID);
        }
        int index = 0;
        foreach(RPC rpc in rpcs)
        {
            rpc.SetParentNetworkObject(this,index);
            index += 1;
        }
        initialized = true;
    }

    public bool IsOwner()
    {
        if(ownerID == NetTools.clientID)
        {
            return true;
        }
        return false;
    }

    public void OnDestroy()
    {
        //Remove from used lists
        allNetObjs.Remove(this);
        NetworkData.usedNetworkObjectInstances.Remove(networkID);

    }

    public static NetworkObject NetObjFromNetID(int netID)
    {
        foreach(NetworkObject netObj in allNetObjs)
        {
            if(netObj.networkID == netID)
            {
                return netObj;
            }
        }
        return null;
    }

    public void UpdateField(string fieldName, object data,bool immediateOnSelf=false)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].fieldName == fieldName)
            {
                fields[i].UpdateField(data,networkID,immediateOnSelf);
                break;
            }
        }
    }

    public void SetFieldLocal(string fieldName, object data)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].fieldName == fieldName)
            {
                fields[i].LocalFieldSet(data);
                break;
            }
        }
    }

    public object GetField(string fieldName)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (fields[i].fieldName == fieldName)
            {
                return fields[i].GetField();
            }
        }
        return null;
    }

    public List<Packet> GeneratePacketListForFields()
    {
        List<Packet> fieldPackets = new List<Packet>();
        foreach(NetworkField netField in fields)
        {
            if(!netField.IsInitialized())
            {
                continue; 
            }

            Packet packet = new Packet(Packet.pType.netVarEdit, Packet.sendType.nonbuffered,
                new NetworkFieldPacket(networkID, netField.fieldName, new JsonPacketObject(JsonUtility.ToJson(netField.GetField()), netField.GetField().GetType().ToString())));
            fieldPackets.Add(packet);
            //print("Adding netfield: " + netField.fieldName);
        }
        return fieldPackets;
    }

    public void CallRPC(string rpcName,Packet.sendType sendType = Packet.sendType.buffered, params object[] list)
    {
        foreach(RPC rpc in rpcs)
        {
            if(rpc.rpcName == rpcName)
            {
                rpc.CallRPC(sendType, list: list);
            }
        }
    }


}

[System.Serializable]
public class NetworkField
{
    public string fieldName;
    public enum valueInitializer {
        INT,
        FLOAT,
        DOUBLE,
        serializableVector,
        BYTE,
        BYTE_ARRAY,
        None,
    };
    public valueInitializer defaultValue = valueInitializer.None;
    public UnityEvent onValueChange;
    private string jsonData = "notinitialized";
    private string jsonDataTypeName = "notinitialized";
    private bool initialized = false;

    public void InitializeDefaultValue(int netID)
    {
        if(initialized)
        {
            return;
        }

        switch (defaultValue)
        {
            case valueInitializer.INT:
                UpdateField(0, netID, true);
                break;
            case valueInitializer.FLOAT:
                UpdateField(0.0f, netID, true);
                break;
            case valueInitializer.DOUBLE:
                UpdateField(0.0, netID, true);
                break;
            case valueInitializer.serializableVector:
                UpdateField(new SerializableVector(0,0,0), netID, true);
                break;
            case valueInitializer.BYTE:
                UpdateField(new byte(), netID, true);
                break;
            case valueInitializer.BYTE_ARRAY:
                UpdateField(new byte[0], netID, true);
                break;
        }
    }
    public void UpdateField(object newValue, NetworkObject netObj)
    {
        UpdateField(newValue, netObj.networkID);
    }

    public void UpdateField(object newValue,int netObjID, bool immediateOnSelf=false)
    {
        Packet packet = new Packet(Packet.pType.netVarEdit, Packet.sendType.nonbuffered,
            new NetworkFieldPacket(netObjID,fieldName,new JsonPacketObject(JsonUtility.ToJson(newValue), newValue.GetType().ToString())));
        NetClient.instanceClient.SendPacket(packet);
        if(immediateOnSelf)
        {
            LocalFieldSet(newValue);
        }
    }

    public void LocalFieldSet(object newValue)
    {
        jsonData = JsonUtility.ToJson(newValue); //This is used in UnityPacketHandler when setting it after packet being recieved. Don't use.
        jsonDataTypeName = newValue.GetType().ToString();
        initialized = true;
        onValueChange.Invoke();
    }

    public object GetField()
    {
        if(!initialized)
        {
            return null;
        }

        return JsonUtility.FromJson(jsonData,System.Type.GetType(jsonDataTypeName));
    }

    public bool IsInitialized()
    {
        return initialized;
    }
}

[System.Serializable]
public class NetworkFieldPacket
{
    public int networkObjID = -1;
    public string fieldName = "";
    public JsonPacketObject data;

    public NetworkFieldPacket(int netID, string fieldName, JsonPacketObject val)
    {
        networkObjID = netID;
        this.fieldName = fieldName;
        data = val;
    }

}