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
    public static NetServer serverInstance = null;

    public string hostAddress;
    public int hostPort=44594;
    public int maxConnections = 8;
    [Space]
    public string localObjectTag = "localOnly";

    TcpListener server = null;
    List<NetworkPlayer> connections = new List<NetworkPlayer>();
    List<Thread> connThreads = new List<Thread>();
    Thread connectionHandler = null;

    public List<Packet> bufferedPackets = new List<Packet>();

    int lastPlayerID = -1;



    public void Initialize()
    {
        if (serverInstance == null)
        {
            serverInstance = this;
        }

        if (server != null)
        {
            Debug.LogError("Trying to initial NetServer when it has already been initialized.");
            return;
        }

        if (hostAddress == "")
        {
            //If no ip given, use 0.0.0.0
            hostAddress = IPAddress.Any.ToString();
        }
        if(hostPort == 0)
        {
            hostPort = 44594;
        }

        if (UnityPacketHandler.instance == null)
        {
            GameObject uPH = new GameObject("Unity Packet Handler");
            uPH.AddComponent<UnityPacketHandler>();
            GameObject.DontDestroyOnLoad(uPH);
        }
        if(NetworkData.instance == null)
        {
            Debug.LogWarning("NetworkData object not found.");
        }

        //Create server
        server = new TcpListener(IPAddress.Any, hostPort);
        server.Start();
        Debug.Log("Server started successfully.");
        NetTools.isServer = true;

        UnityPacketHandler.instance.StartHandler();

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
            netClient.clientID = lastPlayerID + 1;
            lastPlayerID += 1;
            connections.Add(netClient);
            Debug.Log("New Client Connected Successfully.");

            Thread connThread = new Thread(() => ClientHandler(netClient));
            connThread.Start();

            //onPlayerConnect.Invoke(netClient);
        }
    }

    public void ClientHandler(NetworkPlayer client)
    {
        Thread.Sleep(150);
        //Send login info
        PlayerLoginData pLD = new PlayerLoginData();
        pLD.playerNetworkID = client.clientID;
        Packet loginPacket = new Packet(Packet.pType.loginInfo, Packet.sendType.nonbuffered, pLD);
        loginPacket.packetOwnerID = -1;
        loginPacket.sendToAll = false;
        SendPacket(client, loginPacket);


        Thread.Sleep(150); //Prevents a memory error on the client side? bruh.
        //Send buffered packets
        if (bufferedPackets.Count > 0)
        {
            List<Packet> packetsToSend = new List<Packet>(); //Will contain buffered packets and all network fields to be updated.
            packetsToSend.AddRange(bufferedPackets);
            //Debug.Log(packetsToSend.Count);
            foreach (NetworkObject netObj in NetworkObject.allNetObjs)
            {
                if (netObj.fields.Count > 0)
                {
                    List<Packet> temp = netObj.GeneratePacketListForFields();
                    packetsToSend.AddRange(temp);
                }
            }
            //Debug.Log(packetsToSend.Count);
            Packet bpacket = new Packet(Packet.pType.allBuffered, Packet.sendType.nonbuffered, packetsToSend);
            bpacket.sendToAll = false;
            SendPacket(client, bpacket);
        }

        while (client != null)
        {
            try
            {
                Thread.Sleep(25);
                Packet pack = RecvPacket(client);
                if (pack.packetOwnerID != client.clientID)// && client.tcpClient == NetClient.instanceClient.client) //if server dont change cause if it is -1 it has all authority.
                {
                    pack.packetOwnerID = client.clientID;
                }
                if(client.clientID == NetClient.instanceClient.clientID) //Setup server authority.
                {
                    pack.serverAuthority = true;
                } else
                {
                    pack.serverAuthority = false;
                }

                if (pack.packetSendType == Packet.sendType.buffered)
                {
                    Debug.Log("Buffered Packet");
                    bufferedPackets.Add(pack);
                }

                UnityPacketHandler.instance.QueuePacket(pack);
                if (pack.sendToAll)
                {
                    foreach (NetworkPlayer player in connections.ToArray())
                    {
                        //Debug.Log(player.clientID + " " + NetTools.clientID);
                        if (player == null || player.tcpClient == null || (player.clientID == NetTools.clientID))
                        {
                            continue;
                        }

                        SendPacket(player, pack);
                    }
                }
            }catch
            {
               //Something went wrong with packet deserialization.
            }
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
        //Debug.Log(Encoding.Default.GetString(packetSize));
        int pSize = int.Parse(Encoding.Default.GetString(packetSize));
        //Debug.Log(pSize);

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
    public int clientID = -1;
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