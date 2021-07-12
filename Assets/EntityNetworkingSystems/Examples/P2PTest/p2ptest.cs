using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EntityNetworkingSystems;
using EntityNetworkingSystems.Steam;
using Steamworks;
using System.Threading;
using System.Text;

public class p2ptest : MonoBehaviour
{
    public uint gameID;
    public ulong targetID = 76561198078399124;
    public bool msgToSend;
    public string message;
    public bool shouldConnect;
    public P2PSocket p;

    public int count;

    private Thread testThread;
    private void Awake()
    {
        if (gameID != 0)
        {
            try
            {
                SteamClient.Init(gameID,false);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning(e);
            }
        }
        ///SteamNetworkingUtils.InitRelayNetworkAccess();
        ///SteamNetworking.AllowP2PPacketRelay(true);
        p.AllowConnectionsAll(true);
        p.ConnectToIncoming(true);
        p.Start();
    }

    private void Start()
    {
        if (shouldConnect)
        {
            p.Connect(targetID);
        }

        testThread = new Thread(new ThreadStart(TestThread));
        testThread.Start();
    }

    private void Update()
    {
        SteamClient.RunCallbacks();
    }

    void TestThread()
    {
        while(true)
        {
            if (msgToSend)
            {
                p.SendAll(sendType.reliable, Encoding.ASCII.GetBytes(message));
                count++;
            }
            else
            {
                System.Tuple<ulong,byte[]> pack = p.GetNextReliable();
                if(pack == null)
                {
                    continue;
                }
                byte[] stuff = pack.Item2;
                string t = Encoding.ASCII.GetString(stuff);
                Debug.Log(t);

                Debug.Log("MSG Recieved");
            }
            Thread.Sleep(500);
        }
    }

    private void OnDestroy()
    {
        testThread.Abort();
        SteamClient.Shutdown();
    }


}
