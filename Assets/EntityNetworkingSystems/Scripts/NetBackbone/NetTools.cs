using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetTools : MonoBehaviour
{
    public static void NetInstantiate(int prefabDomain, int prefabID, Vector3 position, NetClient netClient)
    {
        SerializableVector finalVector = new SerializableVector(position);



        GameObjectInstantiateData gOID = new GameObjectInstantiateData();
        gOID.position = finalVector;
        gOID.prefabDomainID = prefabDomain;
        gOID.prefabID = prefabID;

        Packet p = new Packet(gOID);
        p.packetType = Packet.pType.gOInstantiate;

        netClient.SendPacket(p);    
    }
}
