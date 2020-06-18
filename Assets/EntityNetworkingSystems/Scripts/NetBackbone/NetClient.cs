using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using System;
using UnityEngine.Events;
using System.Runtime.Serialization.Formatters.Binary;
using Steamworks;

[System.Serializable]
public class NetClient
{
    public static NetClient instanceClient;

    public bool connectedToServer = false;
    public TcpClient client = null;
    public NetworkStream netStream;
    Thread connectionHandler = null;
    [Space]
    public int clientID = -1;

    public bool useSteamworks = false;
    public int steamAppID = -1; //If -1 it wont initialize and you'll need to do it somewhere else :)

    public void Initialize()
    {

        if (instanceClient == null)
        {
            instanceClient = this;
        }

            if (client != null)
        {
            Debug.LogError("Trying to initial NetClient when it has already been initialized.");
            return;
        }

        client = new TcpClient();
        NetTools.isClient = true;

        if (useSteamworks)
        {
            if(SteamInteraction.instance == null)
            {
                GameObject steamIntegration = new GameObject("Steam Integration Handler");
                steamIntegration.AddComponent<SteamInteraction>();
                steamIntegration.GetComponent<SteamInteraction>().Initialize();
                //steamIntegration.GetComponent<SteamInteraction>().StartServer();
                GameObject.DontDestroyOnLoad(steamIntegration);
            }
        }


        if (UnityPacketHandler.instance == null)
        {
            GameObject uPH = new GameObject("Unity Packet Handler");
            uPH.AddComponent<UnityPacketHandler>();
            GameObject.DontDestroyOnLoad(uPH);
        }
    }

    public void ConnectionHandler()
    {
        //Thread.Sleep(150);
        while (client != null)
        {
            Packet packet = RecvPacket();

            //if (packet.packetType == Packet.pType.loginInfo)
            //{
            UnityPacketHandler.instance.QueuePacket(packet);
            //} //Otherwise NetServer will run it. But since the server is sending the login info to the client, it'll only get it here.
            //Thread.Sleep(50);
        }
        Debug.Log("NetClient.ConnectionHandler() thread has successfully finished.");
    }


    public void ConnectToServer(string ip="127.0.0.1", int port=44594)
    {
        Debug.Log("Attempting Connection");
        client.Connect(IPAddress.Parse(ip), port);
        Debug.Log("Connection Accepted");
        netStream = client.GetStream();
        UnityPacketHandler.instance.StartHandler();

        if (useSteamworks)
        {
            SteamInteraction.instance.StartClient();

            SteamInteraction.instance.clientAuth = SteamUser.GetAuthSessionTicket();
            //SteamInteraction.instance.clientAuth = ticket;

            //Debug.Log(SteamClient.SteamId.Value + " "+ SteamClient.SteamId.AccountId);
            //SteamUser.BeginAuthSession(ticket.Data, SteamClient.SteamId.Value);

            Packet authPacket = new Packet(Packet.pType.steamAuth, Packet.sendType.nonbuffered, new SteamAuthPacket(SteamInteraction.instance.clientAuth.Data,SteamClient.SteamId.Value));
            authPacket.sendToAll = false;
            SendPacket(authPacket);
        }

        connectionHandler = new Thread(new ThreadStart(ConnectionHandler));
        connectionHandler.Start();
    }

    public void DisconnectFromServer()
    {
        if (client != null)
        {
            Debug.Log("Disconnecting From Server");
            client.GetStream().Close();
            client.Close();
            client = null;
            NetClient.instanceClient = null;
            connectionHandler.Abort();
            if(useSteamworks)
            {
                SteamInteraction.instance.StopClient();
            }
        }
    }

    public void SendPacket(Packet packet)
    {
        byte[] array = Encoding.Unicode.GetBytes(Packet.JsonifyPacket(packet));

        //First send packet size
        byte[] arraySize = new byte[4];
        arraySize = System.BitConverter.GetBytes(array.Length);
        netStream.Write(arraySize, 0, arraySize.Length);

        //Send packet
        netStream.Write(array, 0, array.Length);
    }

    public Packet RecvPacket()
    {
        //First get packet size
        byte[] packetSize = new byte[4];
        netStream.Read(packetSize, 0, packetSize.Length);
        //Debug.Log(Encoding.ASCII.GetString(packetSize));
        int pSize = System.BitConverter.ToInt32(packetSize,0);
        //Debug.Log(pSize);

        //Get packet
        byte[] byteMessage = new byte[pSize];
        netStream.Read(byteMessage, 0, byteMessage.Length);
        //Debug.Log(Encoding.ASCII.GetString(byteMessage));
        return Packet.DeJsonifyPacket(Encoding.Unicode.GetString(byteMessage));
    }

    //public void SendMessage(byte[] message)
    //{
    //    netStream.Write(message, 0, message.Length);
    //}
    //public byte[] RecvMessage()
    //{
    //    byte[] message = new byte[1024];
    //    netStream.Read(message, 0, message.Length);
    //    return message;
    //}

    //void OnDestroy()
    //{
    //    DisconnectFromServer();
    //}

}
