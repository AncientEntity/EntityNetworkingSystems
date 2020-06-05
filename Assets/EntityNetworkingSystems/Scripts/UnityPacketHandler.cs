using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityPacketHandler : MonoBehaviour
{
    public static UnityPacketHandler instance = null;
    public List<Packet> packetQueue = new List<Packet>();


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

    void Update()
    {
        if(packetQueue.Count > 0)
        {
            Packet curPacket = packetQueue[0];
            packetQueue.RemoveAt(0);

            if(curPacket.packetType == Packet.pType.gOInstantiate)
            {
                GameObjectInstantiateData gOID = (GameObjectInstantiateData)curPacket.data;
                GameObject g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID], gOID.position.ToVec3(),Quaternion.identity);
                NetworkObject nObj = g.AddComponent<NetworkObject>();
                nObj.ownerID = curPacket.packetOwnerID;
                nObj.prefabDomainID = gOID.prefabDomainID;
                nObj.prefabID = gOID.prefabID;
                nObj.networkID = gOID.netObjID;

            } else if (curPacket.packetType == Packet.pType.gODestroy)
            {
                NetworkObject found = NetworkObject.NetObjFromNetID((int)curPacket.data);
                if(found != null && (found.ownerID == curPacket.packetOwnerID || curPacket.packetOwnerID == -1))
                {
                    Destroy(found.gameObject);
                }
            }
            else if(curPacket.packetType == Packet.pType.allBuffered)
            {
                //Debug.Log("Recieved buffered packets.");
                List<Packet> packetInfo = (List<Packet>)curPacket.data;
                packetQueue.AddRange(packetInfo);
            } else if (curPacket.packetType == Packet.pType.loginInfo)
            {
                Debug.Log("Login Info Packet Recieved.");
                NetTools.clientID = ((PlayerLoginData)curPacket.data).playerNetworkID;
            }
        }
    }

    public void QueuePacket(Packet packet)
    {
        packetQueue.Add(packet);
    }

}
