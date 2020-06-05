using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityPacketHandler : MonoBehaviour
{
    public static UnityPacketHandler instance = null;
    public bool handlerRunning = false;
    public List<Packet> packetQueue = new List<Packet>();
    public int amountPerUpdate = 100;

    void Awake()
    {
        if(instance == null)
        {
            instance = this;
        } else
        {
            Destroy(this);
        }
    }

    public void StartHandler()
    {
        if(handlerRunning == true)
        {
            return; //Prevent multiple handlers.
        }
        StartCoroutine(HandleThreads());
    }

    public IEnumerator HandleThreads()
    {
        int countTillUpdate = 0;
        handlerRunning = true;
        yield return new WaitForFixedUpdate();
        while (NetClient.instanceClient != null || NetServer.serverInstance != null)
        {
            if (packetQueue.Count > 0)
            {
                Packet curPacket = packetQueue[0];
                packetQueue.RemoveAt(0);

                if(curPacket == null)
                {
                    continue;
                }

                if (curPacket.packetType == Packet.pType.gOInstantiate) //It gets instantiated NetTools.
                {
                    if(NetTools.clientID == curPacket.packetOwnerID)
                    {
                        continue;
                    }


                    GameObjectInstantiateData gOID = (GameObjectInstantiateData)curPacket.data;
                    GameObject g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID], gOID.position.ToVec3(), Quaternion.identity);
                    NetworkObject nObj = g.GetComponent<NetworkObject>();
                    if (nObj == null)
                    {
                        nObj = g.AddComponent<NetworkObject>();
                    }
                    nObj.ownerID = curPacket.packetOwnerID;
                    nObj.prefabDomainID = gOID.prefabDomainID;
                    nObj.prefabID = gOID.prefabID;
                    nObj.networkID = gOID.netObjID;

                }
                else if (curPacket.packetType == Packet.pType.gODestroy)
                {
                    NetworkObject found = NetworkObject.NetObjFromNetID((int)curPacket.data);
                    if (found != null && (found.ownerID == curPacket.packetOwnerID || curPacket.serverAuthority))
                    {
                        Destroy(found.gameObject);
                    }
                }
                else if (curPacket.packetType == Packet.pType.allBuffered)
                {
                    //Debug.Log("Recieved buffered packets.");
                    List<Packet> packetInfo = (List<Packet>)curPacket.data;
                    packetQueue.AddRange(packetInfo);
                }
                else if (curPacket.packetType == Packet.pType.loginInfo)
                {
                    //Debug.Log("Login Info Packet Recieved.");
                    NetTools.clientID = ((PlayerLoginData)curPacket.data).playerNetworkID;
                    NetClient.instanceClient.clientID = NetTools.clientID;

                    NetTools.onJoinServer.Invoke();
                } else if (curPacket.packetType == Packet.pType.netVarEdit)
                {
                    NetworkFieldPacket nFP = (NetworkFieldPacket)curPacket.data;
                    NetworkObject netObj = NetworkObject.NetObjFromNetID(nFP.networkObjID);
                    if(netObj == null || (netObj.ownerID != curPacket.packetOwnerID && !curPacket.serverAuthority))
                    {
                        continue; //Probably was instantiated on client but not server or vice versa.
                    }
                    //Debug.Log("Seting NetVarEdit.");
                    netObj.SetFieldLocal(nFP.fieldName, nFP.data);
                }



                countTillUpdate++;
                if (packetQueue.Count < 0 || countTillUpdate >= amountPerUpdate)
                {
                    yield return new WaitForFixedUpdate();
                    countTillUpdate = 0;
                }
            }
            else
            {
                yield return new WaitForFixedUpdate();
            }
        }
        handlerRunning = false;
    }

    //void Update()
    //{
    //    if(packetQueue.Count > 0)
    //    {
    //        Packet curPacket = packetQueue[0];
    //        packetQueue.RemoveAt(0);

    //        if(curPacket.packetType == Packet.pType.gOInstantiate)
    //        {
    //            GameObjectInstantiateData gOID = (GameObjectInstantiateData)curPacket.data;
    //            GameObject g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID], gOID.position.ToVec3(),Quaternion.identity);
    //            NetworkObject nObj = g.AddComponent<NetworkObject>();
    //            nObj.ownerID = curPacket.packetOwnerID;
    //            nObj.prefabDomainID = gOID.prefabDomainID;
    //            nObj.prefabID = gOID.prefabID;
    //            nObj.networkID = gOID.netObjID;

    //        } else if (curPacket.packetType == Packet.pType.gODestroy)
    //        {
    //            NetworkObject found = NetworkObject.NetObjFromNetID((int)curPacket.data);
    //            if(found != null && (found.ownerID == curPacket.packetOwnerID || curPacket.packetOwnerID == -1))
    //            {
    //                Destroy(found.gameObject);
    //            }
    //        }
    //        else if(curPacket.packetType == Packet.pType.allBuffered)
    //        {
    //            //Debug.Log("Recieved buffered packets.");
    //            List<Packet> packetInfo = (List<Packet>)curPacket.data;
    //            packetQueue.AddRange(packetInfo);
    //        } else if (curPacket.packetType == Packet.pType.loginInfo)
    //        {
    //            Debug.Log("Login Info Packet Recieved.");
    //            NetTools.clientID = ((PlayerLoginData)curPacket.data).playerNetworkID;
    //        }
    //    }
    //}

    public void QueuePacket(Packet packet)
    {
        packetQueue.Add(packet);
    }

}
