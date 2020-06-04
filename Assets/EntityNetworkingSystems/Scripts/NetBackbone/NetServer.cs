using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Text;

[System.Serializable]
public class NetServer
{
    public string hostAddress;
    public int hostPort=44594;
    public int maxConnections = 8;


    TcpListener server = null;
    List<NetworkPlayer> connections = new List<NetworkPlayer>();
    List<Thread> connThreads = new List<Thread>();
    Thread connectionHandler = null;


    public void Initialize()
    {
        if (hostAddress == "")
        {
            //If no ip given, use 0.0.0.0
            hostAddress = IPAddress.Any.ToString();
        }
        if(hostPort == 0)
        {
            hostPort = 44594;
        }

        //Create server
        server = new TcpListener(IPAddress.Any, hostPort);
        server.Start();
        Debug.Log("Server started successfully.");

        connectionHandler = new Thread(new ThreadStart(ConnectionHandler));
        connectionHandler.Start();

    }

    public void StopServer()
    {
        if(server != null)
        {
            foreach(NetworkPlayer client in connections)
            {
                client.tcpClient.Close();
            }
            server.Stop();
        }
    }

    public void ConnectionHandler()
    {
        if (!IsInitialized())
        {
            Debug.Log("Server not initialized. Please run Initialize() first.");
            return;
        }

        while (server != null)
        {
            while (CurrentConnectionCount() >= maxConnections)
            {
                Thread.Sleep(1000);
            }
            Debug.Log("Awaiting Client Connection...");


            TcpClient tcpClient = server.AcceptTcpClient();
            NetworkPlayer netClient = new NetworkPlayer(tcpClient);
            connections.Add(netClient);
            Debug.Log("New Client Connected Successfully.");

            Thread connThread = new Thread(() => ClientHandler(netClient));
            connThread.Start();

        }
    }

    public void ClientHandler(NetworkPlayer client)
    {
        while (client != null)
        {
            //SendPacket(client, new Packet("Test What is up drama alert nation"));
            //Debug.Log("Sent Test Message");
            //Thread.Sleep(500);
        }
    }

    public bool IsInitialized()
    {
        if (server == null)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public int CurrentConnectionCount()
    {
        return connections.Count;
    }


    public void SendPacket(NetworkPlayer player, Packet packet)
    {
        byte[] array = Packet.SerializePacket(packet);

        //First send packet size
        byte[] arraySize = new byte[4];
        arraySize = Encoding.Default.GetBytes(""+array.Length);
        player.netStream.Write(arraySize, 0, arraySize.Length);

        //Send packet
        player.netStream.Write(array, 0, array.Length);
    }

    public Packet RecvPacket(NetworkPlayer player)
    {
        //Fisrt get packet size
        byte[] packetSize = new byte[4];
        player.netStream.Read(packetSize, 0, packetSize.Length);
        int pSize = int.Parse(Encoding.Default.GetString(packetSize));
        Debug.Log(pSize);

        //Get packet
        byte[] byteMessage = new byte[pSize];
        player.netStream.Read(byteMessage, 0, byteMessage.Length);
        return Packet.DeserializePacket(byteMessage);
    }

    //public void SendMessage(NetworkPlayer client, byte[] message)
    //{
    //    client.netStream.Write(message, 0, message.Length);
    //}
    //public byte[] RecvMessage(NetworkPlayer client)
    //{
    //    byte[] message = new byte[1024];
    //    client.netStream.Read(message, 0, message.Length);
    //    return message;
    //}


}

public class NetworkPlayer
{
    public TcpClient tcpClient;
    public NetworkStream netStream;
    public Vector3 proximityPosition = Vector3.zero;
    public float loadProximity = 10f;

    public NetworkPlayer (TcpClient client)
    {
        this.tcpClient = client;
        this.netStream = client.GetStream();
    }

}