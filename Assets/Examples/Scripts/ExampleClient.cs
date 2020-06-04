using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleClient : MonoBehaviour
{
    public NetClient netClient;

    public void ConnectToServer()
    {
        netClient = new NetClient();
        netClient.Initialize();
        netClient.ConnectToServer();
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            GameObjectInstantiateData gOID = new GameObjectInstantiateData();
            gOID.prefabDomainID = 0;
            gOID.prefabID = 0;
            gOID.position = new SerializableVector(Random.Range(-4.0f,4.0f),Random.Range(-4.0f,4.0f),0);
            Packet p = new Packet(gOID);
            p.packetType = Packet.pType.gOInstantiate;
            netClient.SendPacket(p);
        }
        if(Input.GetKeyDown(KeyCode.F11))
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }

}
