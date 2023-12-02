using System;
using EntityNetworkingSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;

public class ExampleHost : MonoBehaviour
{
    
    public NetServer netServer;
    

    public void StartServer()
    {
        SteamNetworkingUtils.InitRelayNetworkAccess();
        SteamNetworkingUtils.DebugLevel = NetDebugOutput.Warning;
        SteamNetworkingUtils.OnDebugOutput += (type, text) => {Debug.LogWarning(text);};
        
        netServer.StartServer();

        //InvokeRepeating("RpcFunOrSomething", 0, 1f);
    }
    public void StartSingleplayer()
    {
        netServer.StartServer(true);
    }

    private void Update()
    {
        SteamServer.RunCallbacks();
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
