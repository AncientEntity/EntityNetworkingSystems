using EntityNetworkingSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mono.Nat;

public class ExampleHost : MonoBehaviour
{
    public bool isServerBuild = false;
    public NetServer netServer;

    void Start()
    {
        netServer.Initialize();
    }



    public void StartServer()
    {
        netServer.StartServer();

        //InvokeRepeating("RpcFunOrSomething", 0, 1f);
    }

    //void RpcFunOrSomething ()
    //{
    //    foreach(ExamplePlayerController ePC in FindObjectsOfType<ExamplePlayerController>())
    //    {
    //        ePC.GetComponent<NetworkObject>().CallRPC("RandomColor",Packet.sendType.buffered,Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
    //        //Debug.Log("Calling RPC");
    //    }
    //}

    void OnDestroy()
    {
        netServer.StopServer();
    }
}
