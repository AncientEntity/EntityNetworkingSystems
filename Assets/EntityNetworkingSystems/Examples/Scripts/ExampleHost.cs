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
    }
    public void StartSingleplayer()
    {
        netServer.StartServer(true);
    }

    private void Update()
    {
        SteamServer.RunCallbacks();
    }

    void OnDestroy()
    {
        netServer.StopServer();
    }
}
