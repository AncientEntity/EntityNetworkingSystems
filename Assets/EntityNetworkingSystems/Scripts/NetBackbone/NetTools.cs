using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class NetTools : MonoBehaviour
{
    public static int clientID = -1;
    public static bool isServer = false;
    public static bool isClient = false;

    public static UnityEvent onJoinServer = new UnityEvent(); //Gets ran when the login packet finishes :D

    public static GameObject NetInstantiate(int prefabDomain, int prefabID, Vector3 position,Quaternion rotation, Packet.sendType sT = Packet.sendType.buffered, bool isSharedObject = false)
    {
        SerializableVector finalVector = new SerializableVector(position);
        SerializableQuaternion finalQuat = new SerializableQuaternion(rotation);

        GameObjectInstantiateData gOID = new GameObjectInstantiateData();
        gOID.position = finalVector;
        gOID.rotation = finalQuat;
        gOID.prefabDomainID = prefabDomain;
        gOID.prefabID = prefabID;
        gOID.isShared = isSharedObject;
        gOID.netObjID = GenerateNetworkObjectID();


        Packet p = new Packet(gOID);
        p.packetType = Packet.pType.gOInstantiate;
        p.packetSendType = sT;


        //if(sT == Packet.sendType.buffered && isServer)
        //{
        //    NetServer.serverInstance.bufferedPackets.Add(p);
        //}
        if (NetTools.IsMultiplayerGame())
        {
            NetClient.instanceClient.SendPacket(p);
        }

        GameObject g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID], gOID.position.ToVec3(), rotation);
        NetworkObject nObj = g.GetComponent<NetworkObject>();
        if (nObj == null)
        {
            nObj = g.AddComponent<NetworkObject>();
        }
        nObj.ownerID = NetTools.clientID;
        nObj.prefabDomainID = gOID.prefabDomainID;
        nObj.prefabID = gOID.prefabID;
        nObj.networkID = gOID.netObjID;
        nObj.sharedObject = gOID.isShared;

        nObj.Initialize();
        nObj.onNetworkStart.Invoke();
        //nObj.initialized = true;

        return g;
    }



    public static void NetDestroy(NetworkObject netObj, Packet.sendType sT = Packet.sendType.buffered)
    {
        NetDestroy(netObj.networkID,sT);
    }

    public static void NetDestroy(int netID,Packet.sendType sT=Packet.sendType.buffered)
    {
        NetworkObject netObj = NetworkObject.NetObjFromNetID(netID);

        if(netObj == null)
        {
            Debug.LogError("Network Object doesn't exist. ID("+netID+")");
            return;
        }

        if(clientID == netObj.ownerID || isServer)
        {
            //Destroy(netObj.gameObject);

            Packet p = new Packet(Packet.pType.gODestroy, sT,netObj.networkID);
            p.relatesToNetObjID = netID;
            p.packetOwnerID = clientID;
            NetClient.instanceClient.SendPacket(p);
        } else
        {
            Debug.Log("You don't have authority over this object.");
        }
    }

    public static List<Packet> GenerateScenePackets()
    {
        //Will generate all the packets required to sync scenes for users. Useful for right when the server begins.
        //Only generates packets for NetworkObject's that are included inside of NetworkData's prefab domains.
        List<Packet> objPackets = new List<Packet>();
        foreach(NetworkObject netObj in NetworkObject.allNetObjs)
        {
            if(netObj.prefabID == -1 || netObj.prefabDomainID == -1)
            {
                continue; //It isn't registered.
            } 

            GameObjectInstantiateData gOID = new GameObjectInstantiateData();
            gOID.netObjID = netObj.networkID;
            gOID.position = new SerializableVector(netObj.transform.position);
            gOID.rotation = new SerializableQuaternion(netObj.transform.rotation);
            gOID.prefabID = netObj.prefabID;
            gOID.prefabDomainID = netObj.prefabDomainID;

            Packet p = new Packet(Packet.pType.gOInstantiate, Packet.sendType.nonbuffered, gOID);
            objPackets.Add(p);
        }
        return objPackets;
    }

    public static int GenerateNetworkObjectID()
    {
        bool found = false;
        while (found == false)
        {
            int random = Random.Range(0, int.MaxValue);
            if(NetworkData.usedNetworkObjectInstances.Contains(random) == false)
            {
                found = true;
                return random;
            }
        }
        return Random.Range(0, int.MaxValue);
    }

    public static bool IsMultiplayerGame()
    {
        return isServer || isClient;
    }

}
