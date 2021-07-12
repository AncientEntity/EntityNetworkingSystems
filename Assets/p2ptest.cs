using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EntityNetworkingSystems;
using EntityNetworkingSystems.Steam;
using Steamworks;
using System.Threading;

public class p2ptest : MonoBehaviour
{
    public ulong targetID = 76561198078399124;
    public bool msgToSend;
    public bool shouldConnect;
    public P2PSocket p;

    private Thread testThread;
    private void Awake()
    {
        try
        {
            SteamClient.Init(480);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(e);
        }
        p.AllowConnectionsAll(true);
        p.ConnectToIncoming(true);
    }

    private void Start()
    {
        p.Start();

        testThread = new Thread(new ThreadStart(TestThread));
        testThread.Start();
        if (shouldConnect)
        {
            p.Connect(targetID);
        }
    }


    void TestThread()
    {
        while(true)
        {
            if (msgToSend)
            {
                p.SendAll(sendType.reliable, new byte[5] { 5,4,43,2,2});
            }
            else
            {
                p.GetNextReliable();
                Debug.Log("MSG Recieved");
            }
        }
    }

    private void OnDestroy()
    {
        testThread.Abort();
        SteamClient.Shutdown();
    }


}
