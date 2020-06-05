using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkObject : MonoBehaviour
{
    //NETWORK OBJECT AUTOMATICALLY GETS ADDED TO PREFAB WHEN INSTANTIATED OVER THE NETWORK.

    static List<NetworkObject> allNetObjs = new List<NetworkObject>();

    public int networkID = -1;
    public int ownerID = -1;
    public List<NetworkField> fields = new List<NetworkField>();

    [HideInInspector]
    public int prefabID = -1;
    [HideInInspector]
    public int prefabDomainID = -1;

    void Start()
    {
        if(NetworkData.usedNetworkObjectInstances.Contains(networkID) == false)
        {
            NetworkData.usedNetworkObjectInstances.Add(networkID);
        }

        if (allNetObjs.Contains(this) == false)
        {
            allNetObjs.Add(this);
        }
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

}

[System.Serializable]
public class NetworkField
{
    public string fieldName;
    private object data;

    public void UpdateField(object newValue, NetworkObject netObj)
    {
        UpdateField(newValue, netObj.networkID);
    }

    public void UpdateField(object newValue,int netObjID, bool immediateOnSelf=false)
    {
        Packet packet = new Packet(Packet.pType.netVarEdit, Packet.sendType.nonbuffered, new NetworkFieldPacket(netObjID,fieldName,newValue));
        NetClient.instanceClient.SendPacket(packet);
        if(immediateOnSelf)
        {
            data = newValue;
        }
    }

    public void LocalFieldSet(object newValue)
    {
        data = newValue; //This is used in UnityPacketHandler when setting it after packet being recieved. Don't use.
    }

    public object GetField()
    {
        return data;
    }
}

[System.Serializable]
public class NetworkFieldPacket
{
    public int networkObjID = -1;
    public string fieldName = "";
    public object data;

    public NetworkFieldPacket(int netID, string fieldName, object val)
    {
        networkObjID = netID;
        this.fieldName = fieldName;
        data = val;
    }
}