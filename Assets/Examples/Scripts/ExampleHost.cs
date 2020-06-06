using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleHost : MonoBehaviour
{
    public NetServer netServer;

    public void StartServer()
    {
        netServer = new NetServer();
        netServer.Initialize();

        InvokeRepeating("RpcFunOrSomething", 0, 1f);
    }

    void RpcFunOrSomething ()
    {
        foreach(ExamplePlayerController ePC in FindObjectsOfType<ExamplePlayerController>())
        {
            ePC.GetComponent<NetworkObject>().rpcs[0].CallRPC(Packet.sendType.buffered,Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
            //Debug.Log("Calling RPC");
        }
    }

}
