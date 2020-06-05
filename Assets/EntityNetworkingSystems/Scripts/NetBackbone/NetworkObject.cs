using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkObject : MonoBehaviour
{
    //NETWORK OBJECT AUTOMATICALLY GETS ADDED TO PREFAB WHEN INSTANTIATED OVER THE NETWORK.

    static List<NetworkObject> allNetObjs = new List<NetworkObject>();

    public int networkID = -1;
    public int ownerID = -1;

    [HideInInspector]
    public int prefabID = -1;
    [HideInInspector]
    public int prefabDomainID = -1;

    void Start()
    {
        //Moved into Packet and UnityPacketHandler.
        //if(networkID == -1 && NetTools.isServer)
        //{
        //    networkID = NetTools.GenerateNetworkObjectID();
        //}

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
}
