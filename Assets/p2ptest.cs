using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EntityNetworkingSystems;
using EntityNetworkingSystems.Steam;
using Steamworks;
using System.Threading;

public class p2ptest : MonoBehaviour
{
    public bool msgToSend;

    private P2PSocket p;
    private Thread testThread;
    private void Awake()
    {
        try
        {
            SteamClient.Init(480);
        }
        catch
        {

        }
        p = new P2PSocket();
        p.AllowConnectionFrom(SteamClient.SteamId);
    }

    private void Start()
    {
        testThread = new Thread(new ThreadStart(TestThread));
        testThread.Start();

        p.Connect(SteamClient.SteamId);
    }


    void TestThread()
    {
        while(true)
        {
            if (msgToSend)
            {
                p.SendAll(sendType.reliable, new byte[0]);
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
    }


}
