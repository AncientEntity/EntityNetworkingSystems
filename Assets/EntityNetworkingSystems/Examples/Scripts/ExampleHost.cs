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
        NatUtility.DeviceFound += DeviceFound;
        NatUtility.StartDiscovery();

        if(isServerBuild)
        {
            StartServer();
        }
    }

    private async void DeviceFound(object sender, DeviceEventArgs args)
    {
        try
        {
            //Get Device Info
            INatDevice device = args.Device;
            Debug.Log("Device Protocol: " + device.NatProtocol);
            Debug.Log("Device Name: " + device.GetType().Name);
            Debug.Log("Device External IP: " + (await device.GetExternalIPAsync()).ToString());

            //Create Mapping
            Mapping portMapping = new Mapping(Protocol.Tcp, netServer.hostPort, netServer.hostPort);
            await device.CreatePortMapAsync(portMapping);
            Debug.Log("Creating uPnP Mapping: protocol=" + portMapping.Protocol + "ports=" + portMapping.PublicPort);

            try
            {
                Mapping m = await device.GetSpecificMappingAsync(Protocol.Tcp, portMapping.PublicPort);
                Debug.Log("Retrieved uPnP Mapping: protocol=" + m.Protocol + "ports=" + m.PublicPort);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Couldn't get specific mapping... "+e);
            }

        } catch (System.Exception e)
        {
            Debug.LogError(e);
        }
    }


    public void StartServer()
    {
        netServer.Initialize();
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
