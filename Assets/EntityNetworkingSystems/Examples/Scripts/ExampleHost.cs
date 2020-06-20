﻿using EntityNetworkingSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleHost : MonoBehaviour
{
    public bool isServerBuild = false;
    public NetServer netServer;

    void Start()
    {
        if(isServerBuild)
        {
            StartServer();
        }
    }

    public void StartServer()
    {
        netServer.Initialize();
        netServer.StartServer();

        InvokeRepeating("RpcFunOrSomething", 0, 1f);
    }

    void RpcFunOrSomething ()
    {
        foreach(ExamplePlayerController ePC in FindObjectsOfType<ExamplePlayerController>())
        {
            Debug.Log("RPC CALL RANDOM COLOR", ePC);
            ePC.GetComponent<NetworkObject>().rpcs[0].CallRPC(Packet.sendType.buffered,Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
            //Debug.Log("Calling RPC");
        }
    }

    void OnDestroy()
    {
        netServer.StopServer();
    }
}
