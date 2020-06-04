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
            }
        }
    }

    public void QueuePacket(Packet packet)
    {
        packetQueue.Add(packet);
    }

}
