using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Text;
using Steamworks;
using System.Linq;
using EntityNetworkingSystems.UDP;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class NetServer
    {
        public static NetServer serverInstance = null;

        public string hostAddress;
        public int hostPort = 44594;
        public int maxConnections = 8;
        [Space]
        public bool useSteamworks = false; //Requires Facepunch.Steamworks to be within the project.
        public int steamAppID = -1; //If -1 it won't initialize, meaning you must on your own.
        public string modDir = "spacewar";
        public string gameDesc = "spacewar";
        public string mapName = "world1";
        [Space]
        public List<NetworkPlayer> connections = new List<NetworkPlayer>();
        public List<Packet> bufferedPackets = new List<Packet>();

        UDPListener udpListener = null;
        TcpListener server = null;
        List<Thread> connThreads = new List<Thread>();
        Thread connectionHandler = null;
        Thread udpHandler = null;
        //Thread packetSendHandler = null;
        //Dictionary<Packet, NetworkPlayer> queuedSendPackets = new Dictionary<Packet, NetworkPlayer>();

        int lastPlayerID = -1;


        public void Initialize()
        {

            if (IsInitialized())
            {
                return;
            }

            if (serverInstance == null)
            {
                serverInstance = this;
            }

            if (server != null)
            {
                Debug.LogError("Trying to initial NetServer when it has already been initialized.");
                return;
            }

            if (hostAddress == "" || hostAddress == "Any")
            {
                //If no ip given, use 0.0.0.0
                hostAddress = IPAddress.Any.ToString();
            }
            if (hostPort == 0)
            {
                hostPort = 44594;
            }

            if (UnityPacketHandler.instance == null)
            {
                GameObject uPH = new GameObject("Unity Packet Handler");
                uPH.AddComponent<UnityPacketHandler>();
                GameObject.DontDestroyOnLoad(uPH);
            }
            if (useSteamworks && !NetTools.isSingleplayer && SteamInteraction.instance == null)
            {
                GameObject steamIntegration = new GameObject("Steam Integration Handler");
                steamIntegration.AddComponent<SteamInteraction>();
                steamIntegration.GetComponent<SteamInteraction>().Initialize();
                steamIntegration.GetComponent<SteamInteraction>().StartServer();
                GameObject.DontDestroyOnLoad(steamIntegration);
            }

            if (NetworkData.instance == null)
            {
                Debug.LogWarning("NetworkData object not found.");
            }

            //if (packetSendHandler == null)
            //{
            //    packetSendHandler = new Thread(new ThreadStart(PacketSendHandler));
            //    packetSendHandler.Start();
            //}
            if(maxConnections <= 1)
            {
                NetTools.isSingleplayer = true;
            }

        }

        //public void PacketSendHandler()
        //{
        //    while (serverInstance != null)
        //    {
        //        while(queuedSendPackets.Count <= 0)
        //        {
        //            Thread.Sleep(5);
        //        }

        //        foreach(KeyValuePair<Packet,NetworkPlayer> entry in queuedSendPackets)
        //        {
        //            SendPacket(entry.Value, entry.Key,true);
        //            queuedSendPackets.Remove(entry.Key);
        //        }
        //    }
        //}

        //public void QueueSendPacket(Packet pack, NetworkPlayer player)
        //{
        //    queuedSendPackets.Add(pack, player);
        //}


        public void UDPHandler()
        {
            while(udpListener != null)
            {
                KeyValuePair<NetworkPlayer, Packet> recieved = udpListener.RecievePacket();
                udpListener.SendPacket(recieved.Value);
            }
        }



        public void StartServer(bool isSingleplayer=false)
        {
            if(NetworkData.instance != null)
            {
                NetworkData.instance.GeneratePooledObjects();
            } else
            {
                Debug.LogWarning("There is no loaded NetworkData in the scene. This may break some features.");
            }

            if(NetTools.isSingleplayer)
            {
                NetTools.clientID = 0;
                NetTools.isServer = true;
                UnityPacketHandler.instance.StartHandler();
                Debug.Log("Server started in Singleplayer Mode.");
                return;
            }

            //Create server
            //Debug.Log(IPAddress.Parse(hostAddress));
            if (hostAddress == "Any")
            {
                server = new TcpListener(IPAddress.Any, hostPort);
            } else
            {
                server = new TcpListener(IPAddress.Parse(hostAddress), hostPort);
            }
            udpListener = new UDPListener(this);
            server.Start();
            udpListener.Start();
            Debug.Log("Server started successfully.");
            NetTools.isServer = true;

            UnityPacketHandler.instance.StartHandler();

            connectionHandler = new Thread(new ThreadStart(ConnectionHandler));
            connectionHandler.Start();
            udpHandler = new Thread(new ThreadStart(UDPHandler));
            udpHandler.Start();
            //packetSendHandler = new Thread(new ThreadStart(SendingPacketHandler));
            //packetSendHandler.Start();


        }

        public void StopServer()
        {
            if (server != null)
            {
                foreach (NetworkPlayer client in connections)
                {
                    client.tcpClient.Close();
                }
                server.Stop();
                server = null;

                //if (useSteamworks && !NetTools.isSingleplayer)
                //{
                //    SteamInteraction.instance.ShutdownServer();
                //}
            }
            udpListener.Stop();
            udpListener = null;

            if (connectionHandler != null)
            {
                connectionHandler.Abort();
                connectionHandler = null;
            }
            if(udpHandler != null)
            {
                udpHandler.Abort();
                udpHandler = null;
            }

            if (GameObject.FindObjectOfType<UnityPacketHandler>() != null)
            {
                GameObject.Destroy(GameObject.FindObjectOfType<UnityPacketHandler>().gameObject);
            }
            //NetServer.serverInstance = null;
            connections = new List<NetworkPlayer>();
            bufferedPackets = new List<Packet>();

            NetTools.isServer = false;
            NetTools.isSingleplayer = false;
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
                netClient.udpEndpoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
                netClient.udpEndpoint.Port += 1;
                SendPacket(netClient, new Packet(Packet.pType.unassigned, Packet.sendType.nonbuffered, 0));
                lastPlayerID += 1;
                connections.Add(netClient);
                Debug.Log("New Client Connected Successfully.");

                Thread connThread = new Thread(() => ClientHandler(netClient));
                connThread.Start();

            }
            Debug.Log("NetServer.ConnectionHandler() thread has successfully finished.");
        }

        public void ClientHandler(NetworkPlayer client)
        {
            if (useSteamworks)
            {
                //VERIFY AUTH TICKET FIRST.
                SteamAuthPacket clientSteamAuthTicket;
                Packet p = RecvPacket(client);
                if (p.packetType == Packet.pType.steamAuth)
                {
                    //Debug.Log(p.jsonData);
                    clientSteamAuthTicket = p.GetPacketData<SteamAuthPacket>();
                    client.steamID = clientSteamAuthTicket.steamID;

                    Thread.Sleep(1500); //Wait for steam to authenticate it. Will take around this time. Probably should add x attempts over a few seconds.

                    BeginAuthResult bAR = SteamUser.BeginAuthSession(clientSteamAuthTicket.authData, clientSteamAuthTicket.steamID);

                    //Debug.Log(clientSteamAuthTicket.steamID);
                    if (bAR != BeginAuthResult.OK)
                    {
                        client.tcpClient.Close();
                        Debug.Log(bAR);
                        //Debug.Log("Invalid auth ticket from: " + clientSteamAuthTicket.steamID);
                        SteamUser.EndAuthSession(clientSteamAuthTicket.steamID);
                        SteamServer.EndSession(clientSteamAuthTicket.steamID);
                        return; //Invalid ticket cancel connection.
                    }
                    else
                    {
                        SteamServer.UpdatePlayer(clientSteamAuthTicket.steamID, "Player 1", 0);
                        //Debug.Log("Recieved Valid Client Auth Ticket.");
                        SteamInteraction.instance.connectedSteamIDs.Add(clientSteamAuthTicket.steamID);

                        SteamServer.ForceHeartbeat();
                    }
                }
            }


            //Thread.Sleep(100);
            //Send login info
            //PlayerLoginData pLD = new PlayerLoginData();
            //pLD.playerNetworkID = client.clientID;
            Packet loginPacket = new Packet(Packet.pType.loginInfo, Packet.sendType.nonbuffered, System.BitConverter.GetBytes((short)client.clientID));
            loginPacket.packetOwnerID = -1;
            loginPacket.sendToAll = false;
            SendPacket(client, loginPacket);


            //Thread.Sleep(50); //Prevents a memory error on the client side? bruh.
            //Send buffered packets
            if (bufferedPackets.Count > 0)
            {
                List<Packet> packetsToSend = new List<Packet>(); //Will contain buffered packets and all network fields to be updated.
                packetsToSend.AddRange(bufferedPackets.ToArray());
                //Debug.Log(packetsToSend.Count);
                foreach (NetworkObject netObj in NetworkObject.allNetObjs.ToArray())
                {
                    if (netObj.fields.Count > 0)
                    {
                        List<Packet> temp = netObj.GeneratePacketListForFields();
                        packetsToSend.AddRange(temp);
                    }
                }



                List<Packet> tempPackets = new List<Packet>();


                foreach (Packet p in packetsToSend)
                {
                    tempPackets.Add(p);
                    if (tempPackets.Count >= 100)
                    {
                        Packet multiPack = new Packet(Packet.pType.multiPacket, Packet.sendType.nonbuffered, new PacketListPacket(tempPackets));
                        multiPack.sendToAll = false;
                        SendPacket(client, multiPack);

                        tempPackets = new List<Packet>();
                    }
                }

                //Send whatever remains in it otherwise max-1 or less packets will be lost.
                //Debug.Log(tempPackets.Count);
                Packet lastMulti = new Packet(Packet.pType.multiPacket, Packet.sendType.nonbuffered, new PacketListPacket(tempPackets));
                lastMulti.sendToAll = false;
                SendPacket(client, lastMulti);
                //Debug.Log(packetsToSend.Count);
                //Packet bpacket = new Packet(Packet.pType.multiPacket, Packet.sendType.nonbuffered, new PacketListPacket(packetsToSend));
                //bpacket.sendToAll = false;
                //SendPacket(client, bpacket);
            }
            NetTools.onPlayerJoin.Invoke(client);

            bool clientRunning = true;

            while (client != null && server != null && clientRunning)
            {
                try
                {
                    //Thread.Sleep(50);
                    Packet pack = RecvPacket(client);

                    if(pack == null)
                    {
                        continue;
                    }

                    if (pack.packetOwnerID != client.clientID)// && client.tcpClient == NetClient.instanceClient.client) //if server dont change cause if it is -1 it has all authority.
                    {
                        pack.packetOwnerID = client.clientID;
                    }
                    if (client.clientID == NetClient.instanceClient.clientID) //Setup server authority.
                    {
                        pack.serverAuthority = true;
                    }
                    else
                    {
                        pack.serverAuthority = false;
                    }

                    if(pack.packetType == Packet.pType.rpc)
                    {
                        RPCPacketData rPD = ENSSerialization.DeserializeRPCPacketData(pack.packetData);
                        rPD.packetOwnerID = client.clientID;
                        pack.packetData = ENSSerialization.SerializeRPCPacketData(rPD);
                    }

                    if (pack.packetSendType == Packet.sendType.buffered || pack.packetSendType == Packet.sendType.culledbuffered)
                    {

                        //If it is a culledbuffered packet, if it is a netVarEdit packet, and it relates to the same netObj and is the RPC.
                        //Then cull the previous of the same RPC to prevent RPC spam
                        //This also happens with NetworkFields, even though network fields are generally *not buffered* the logic is here.
                        //The reason NetworkFields aren't buffered is because the NetServer already syncs them when a client joins.
                        if (pack.packetSendType == Packet.sendType.culledbuffered && (pack.packetType == Packet.pType.netVarEdit || pack.packetType == Packet.pType.rpc))
                        {
                            foreach (Packet buff in bufferedPackets.ToArray())
                            {
                                if (buff.relatesToNetObjID == pack.relatesToNetObjID && buff.packetType == pack.packetType)
                                {
                                    if (buff.packetType == Packet.pType.netVarEdit)
                                    {

                                        if (buff.GetPacketData<NetworkFieldPacket>().fieldName == pack.GetPacketData<NetworkField>().fieldName)
                                        {
                                            bufferedPackets.Remove(buff);
                                        }
                                    }
                                    else if (buff.packetType == Packet.pType.rpc)
                                    {
                                        if (buff.GetPacketData<RPCPacketData>().rpcIndex == pack.GetPacketData<RPCPacketData>().rpcIndex)
                                        {
                                            bufferedPackets.Remove(buff);
                                        }
                                    }
                                }
                            }
                        }

                        //Debug.Log("Buffered Packet");
                        bufferedPackets.Add(pack);
                    }

                    UnityPacketHandler.instance.QueuePacket(pack);
                    if (pack.sendToAll || pack.usersToRecieve.Count > 0)
                    {
                        foreach (NetworkPlayer player in connections.ToArray())
                        {
                            //Debug.Log(player.clientID + " " + NetTools.clientID);
                            if (player == null || player.tcpClient == null || (player.clientID == NetTools.clientID))
                            {
                                continue;
                            }

                            if (pack.sendToAll == true || pack.usersToRecieve.Contains(player.clientID))
                            {
                                SendPacket(player, pack);
                            }

                        }
                    }
                }
                catch (System.Exception e)
                {
                    //Something went wrong with packet deserialization or connection closed.
                    Debug.LogError(e);
                    clientRunning = false; //Basically end the thread.

                }
            }
            Debug.Log("NetServer.ClientHandler() thread has successfully finished.");
            client.netStream.Close();
            client.playerConnected = false;
            NetTools.onPlayerDisconnect.Invoke(client);
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

            if (packet.packetSendType == Packet.sendType.proximity)
            {
                if (Vector3.Distance(player.proximityPosition, packet.packetPosition.ToVec3()) >= player.loadProximity)
                {
                    return;
                }
            }

            if (NetTools.isSingleplayer)
            {
                UnityPacketHandler.instance.QueuePacket(packet);
                return;
            }


            if (packet.reliable)
            {
                SendTCPPacket(player, packet);
            } else
            {
                udpListener.SendPacket(packet);
            }
        }


        public void SendTCPPacket(NetworkPlayer player, Packet packet)//, bool queuedPacket = false)
        {
            //if(!queuedPacket)
            //{
            //    queuedSendingPackets.Add(packet, player);
            //    return;
            //}

            //if(packet.packetType == Packet.pType.netVarEdit && player.clientID == NetTools.clientID)
            //{
            //    return; //No need to double sync it.
            //}
            lock (player.netStream)
            {
                lock (player.tcpClient)
                {
                    byte[] array = ENSSerialization.SerializePacket(packet);//Encoding.ASCII.GetBytes(Packet.JsonifyPacket(packet));//Packet.SerializeObject(packet);

                    //First send packet size
                    byte[] arraySize = new byte[4];
                    arraySize = System.BitConverter.GetBytes(array.Length);
                    //Debug.Log("Length: " + arraySize.Length);

                    player.netStream.Write(arraySize, 0, arraySize.Length);

                    //Send packet
                    player.tcpClient.SendBufferSize = array.Length;
                    player.netStream.Write(array, 0, array.Length);
                }
            }
        }

        public Packet RecvPacket(NetworkPlayer player)
        {
            //First get packet size
            //byte[] packetSize = new byte[4];
            byte[] packetSize = RecieveSizeSpecificData(4, player.netStream);
            //player.netStream.Read(packetSize, 0, packetSize.Length);
            int pSize = System.BitConverter.ToInt32(packetSize, 0);
            //Debug.Log(pSize);

            //Get packet
            byte[] byteMessage = new byte[pSize];
            player.tcpClient.ReceiveBufferSize = pSize;
            byteMessage = RecieveSizeSpecificData(pSize, player.netStream);
            //player.netStream.Read(byteMessage, 0, byteMessage.Length);
            return ENSSerialization.DeserializePacket(byteMessage); //Packet.DeJsonifyPacket(Encoding.ASCII.GetString(byteMessage));//(Packet)Packet.DeserializeObject(byteMessage);
        }

        byte[] RecieveSizeSpecificData(int byteCountToGet, NetworkStream netStream)
        {
            //byteCountToGet--;
            byte[] bytesRecieved = new byte[byteCountToGet];

            int messageRead = 0;
            while (messageRead < bytesRecieved.Length)
            {
                int bytesRead = netStream.Read(bytesRecieved, messageRead, bytesRecieved.Length - messageRead);
                messageRead += bytesRead;
            }
            return bytesRecieved;
        }

        //public Dictionary<Packet, NetworkPlayer> queuedSendingPackets = new Dictionary<Packet, NetworkPlayer>();

        //public void SendingPacketHandler()
        //{
        //    while(NetServer.serverInstance != null)
        //    {
        //        while(queuedSendingPackets.Count > 0)
        //        {
        //            lock (queuedSendingPackets)
        //            {
        //                lock (queuedSendingPackets.Keys)
        //                {
        //                    foreach (Packet packetKey in queuedSendingPackets.Keys.ToArray())
        //                    {
        //                        if (packetKey == null || queuedSendingPackets.ContainsKey(packetKey) == false)
        //                        {
        //                            continue;
        //                        }

        //                        SendPacket(queuedSendingPackets[packetKey], packetKey, true);
        //                        queuedSendingPackets.Remove(packetKey);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}

        //void OnDestroy()
        //{
        //    StopServer();
        //}

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
        public NetworkPlayer GetPlayerByUDPEndpoint(IPEndPoint endPoint)
        {
            foreach(NetworkPlayer player in connections)
            {
                if(player.udpEndpoint == endPoint)
                {
                    return player;
                }
            }
            return null;
        }


        public NetworkPlayer GetPlayerByID(int id)
        {
            foreach(NetworkPlayer player in connections)
            {
                if(player.clientID == id)
                {
                    return player;
                }
            }
            return null;
        }

        public Thread GetServerThread()
        {
            return connectionHandler;
        }

    }

    [System.Serializable]
    public class NetworkPlayer
    {
        public int clientID = -1;
        public TcpClient tcpClient;
        public NetworkStream netStream;
        public Vector3 proximityPosition = Vector3.zero;
        public float loadProximity = 15f;
        public Thread threadHandlingClient;
        public ulong steamID;

        public IPEndPoint udpEndpoint;

        public bool playerConnected = true;

        public NetworkPlayer(TcpClient client)
        {
            if(client == null)
            {
                return;
            }

            this.tcpClient = client;
            this.netStream = client.GetStream();
            this.udpEndpoint = (IPEndPoint)client.Client.RemoteEndPoint;
        }

    }


}