using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;

[System.Serializable]
public class NetClient
{
    public static NetClient instanceClient;

    public bool connectedToServer = false;
    public TcpClient client = null;
    public NetworkStream netStream;
    Thread connectionHandler = null;
    [Space]
    public string localObjectTag = "localOnly";


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


        if (UnityPacketHandler.instance == null)
        {
            GameObject uPH = new GameObject("Unity Packet Handler");
            uPH.AddComponent<UnityPacketHandler>();
            GameObject.DontDestroyOnLoad(uPH);
        }
    }

    public void ConnectionHandler()
    {
        while (client != null)
        {

            Packet packet = RecvPacket();

            //if (packet.packetType == Packet.pType.loginInfo)
            //{
            UnityPacketHandler.instance.QueuePacket(packet);
            //} //Otherwise NetServer will run it. But since the server is sending the login info to the client, it'll only get it here.

        }
    }


    public void ConnectToServer(string ip="127.0.0.1", int port=44594)
    {
        Debug.Log("Attempting Connection");
        client.Connect(IPAddress.Parse(ip), port);
        Debug.Log("Connection Accepted");
        netStream = client.GetStream();
        UnityPacketHandler.instance.StartHandler();
        connectionHandler = new Thread(new ThreadStart(ConnectionHandler));
        connectionHandler.Start();
    }

    public void SendPacket(Packet packet)
    {
        byte[] array = Packet.SerializePacket(packet);

        //First send packet size
        byte[] arraySize = new byte[4];
        arraySize = Encoding.Default.GetBytes("" + array.Length);
        netStream.Write(arraySize, 0, arraySize.Length);

        //Send packet
        netStream.Write(array, 0, array.Length);
    }

    public Packet RecvPacket()
    {
        //Fisrt get packet size
        byte[] packetSize = new byte[4];
        netStream.Read(packetSize, 0, packetSize.Length);
        int pSize = int.Parse(Encoding.Default.GetString(packetSize));
        //Debug.Log(pSize);

        //Get packet
        byte[] byteMessage = new byte[pSize];
        netStream.Read(byteMessage, 0, byteMessage.Length);
        return Packet.DeserializePacket(byteMessage);
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

}
