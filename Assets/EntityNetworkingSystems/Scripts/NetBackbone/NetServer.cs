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
using System.Threading.Tasks;
using Mono.Nat;
using EntityNetworkingSystems.Nat;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class NetServer
    {
        public static NetServer serverInstance = null;

        public string hostAddress;
        public int hostPort = 24424;
        public int maxConnections = 8;
        [Space]
        public bool useSteamworks = false; //Requires Facepunch.Steamworks to be within the project.
        public int steamAppID = -1; //If -1 it won't initialize, meaning you must on your own.
        public string modDir = "spacewar";
        public string gameDesc = "spacewar";
        public string mapName = "world1";
        public string password = "";
        public ulong mySteamID = 0;
        [Space]
        public Dictionary<int, NetworkPlayer> connectionsByID = new Dictionary<int, NetworkPlayer>();
        public List<NetworkPlayer> connections = new List<NetworkPlayer>();
        public NetworkPlayer myConnection = null; //The client that represents the host.
#if UNITY_EDITOR
        public List<BufferedPacketDisplay> bufferedDisplay = new List<BufferedPacketDisplay>();
#endif
        public Dictionary<string, List<Packet>> bufferedPackets = new Dictionary<string, List<Packet>>(); //Packet String Tag, List Of Packets with that tag.

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

            serverInstance = this;
            

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
                hostPort = 24424;
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
                //steamIntegration.GetComponent<SteamInteraction>().StartServer();
                #if !UNITY_SERVER
                mySteamID = SteamClient.SteamId.Value;
                #endif
                GameObject.DontDestroyOnLoad(steamIntegration);
            }

            if (NetworkData.instance == null)
            {
                Debug.LogWarning("NetworkData object not found.");
            }

            UPnP.Init();

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
            try
            {
                while (udpListener != null)
                {
                    Packet recieved = udpListener.Recieve();
                    //Debug.Log("Recieved Packet: " + recieved.Key.udpEndpoint.ToString() + ", " + recieved.Value.packetType);
                    UnityPacketHandler.instance.QueuePacket(recieved);
                    udpListener.SendPacket(recieved, true);
                }
            } catch (System.Exception e)
            {
                if(e.ToString().Contains("ThreadAbort"))
                {
                    Debug.Log("NetServer.UDPHandler has stopped.");
                }
            }
        }



        public void StartServer(bool isSingleplayer=false)
        {
            if (!NetTools.isSingleplayer)
            {
                SteamInteraction.instance.StartServer();
            }

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
            connectionsByID = new Dictionary<int, NetworkPlayer>();

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
            connectionHandler.Name = "ENSServerConnectionHandler";
            connectionHandler.Start();
            udpHandler = new Thread(new ThreadStart(UDPHandler));
            udpHandler.Name = "ENSServerUDPHandler";
            udpHandler.Start();
            //packetSendHandler = new Thread(new ThreadStart(SendingPacketHandler));
            //packetSendHandler.Start();


        }

        public void StopServer()
        {
            if (server != null)
            {
                ConnectionPacket cP = new ConnectionPacket(true, "Server Closed.");
                Packet p = new Packet(Packet.pType.connectionPacket, Packet.sendType.nonbuffered, ENSSerialization.SerializeConnectionPacket(cP));

                foreach (NetworkPlayer client in connections)
                {
                    SendPacket(client, p); //Server Closed Packet.
                    client.tcpClient.Close();
                    client.tcpClient.Dispose();
                }
                server.Stop();
                server = null;

                if (useSteamworks && !NetTools.isSingleplayer)
                {
                    SteamInteraction.instance.ShutdownServer();
                }
                UPnP.Shutdown();
            }
            if (udpListener != null)
            {
                udpListener.Stop();
                udpListener = null;
            }

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
            connectionsByID = new Dictionary<int, NetworkPlayer>();
            bufferedPackets = new Dictionary<string, List<Packet>>();
            if (NetServer.serverInstance != null && NetServer.serverInstance.myConnection != null)
            {
                NetServer.serverInstance.myConnection = null;
            }

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
                //while (CurrentConnectionCount() >= maxConnections)
                //{
                //    Thread.Sleep(1000);
                //}
                Debug.Log("Awaiting Client Connection...");

                try
                {
                    TcpClient tcpClient = server.AcceptTcpClient();

                    NetworkPlayer netClient = new NetworkPlayer(tcpClient);
                    netClient.clientID = lastPlayerID + 1;
                    connections.Add(netClient);
                    connectionsByID[netClient.clientID] = netClient;
                    netClient.udpEndpoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
                    netClient.udpEndpoint.Port += 1;
                    SendPacket(netClient, new Packet(Packet.pType.unassigned, Packet.sendType.nonbuffered, 0));
                    lastPlayerID += 1;
                    if(CurrentConnectionCount() > maxConnections)
                    {
                        //Too many players, kick em with reason 'Server Full'.
                        KickPlayer(netClient, "Server Full",5f);
                        continue;
                    }


                    Debug.Log("New Client Connected Successfully. <"+tcpClient.Client.RemoteEndPoint+">");

                    Thread connThread = new Thread(() => ClientHandler(netClient));
                    netClient.threadHandlingClient = connThread;
                    connThread.Start();

                } catch (System.Exception e)
                {
                    //Server is stopping.
                    Debug.LogError(e);
                }

            }
            Debug.Log("NetServer.ConnectionHandler() thread has successfully finished.");
        }

        public void ClientHandler(NetworkPlayer client)
        {
            //Debug.Log(p.jsonData);
            Packet curPacket = RecvPacket(client);

            if (curPacket.packetType == Packet.pType.networkAuth)
            {
                NetworkAuthPacket clientAuthPacket = ENSSerialization.DeserializeAuthPacket(curPacket.packetData);

                if (!client.gotAuthPacket)
                {
                    //Check password.
                    if(password != "" && clientAuthPacket.password != password)
                    {
                        //Kick player wrong password.
                        KickPlayer(client, "password");
                        return;
                    } else if (useSteamworks && SteamApps.BuildId != clientAuthPacket.steamBuildID)
                    {
                        //Kick player wrong version.
                        KickPlayer(client, "version");
                        return;
                    }



                    client.steamID = clientAuthPacket.steamID;


                    //Debug.Log(clientAuthPacket.udpPort);
                    client.udpEndpoint.Port = clientAuthPacket.udpPort;
                    client.playerPort = clientAuthPacket.udpPort;
                    if (NetServer.serverInstance.useSteamworks)
                    {
                        //BeginAuthResult bAR = SteamUser.BeginAuthSession(clientAuthPacket.authData, clientAuthPacket.steamID);
                        bool worked = SteamServer.BeginAuthSession(clientAuthPacket.authData, clientAuthPacket.steamID);

                        //Debug.Log(clientSteamAuthTicket.steamID);
                        if (!worked && !client.playerIP.Contains("127.0.0.1"))//(bAR != BeginAuthResult.OK && bAR != BeginAuthResult.DuplicateRequest)
                        {
                            KickPlayer(client, "Couldn't validate with steam. ");// + bAR.ToString());
                            //client.tcpClient.Close();
                            //Debug.Log(bAR);
                            //Debug.Log("Invalid auth ticket from: " + clientSteamAuthTicket.steamID);
                            //SteamUser.EndAuthSession(clientAuthPacket.steamID);
                            //SteamServer.EndSession(clientAuthPacket.steamID);
                        }
                        else
                        {
                            //SteamServer.UpdatePlayer(clientAuthPacket.steamID, "Player 1", 0);
                            //Debug.Log("Recieved Valid Client Auth Ticket.");
                            SteamInteraction.instance.connectedSteamIDs.Add(clientAuthPacket.steamID);
                            SteamServer.UpdatePlayer(client.steamID, new Friend(client.steamID).Name, 0);
                            SteamServer.ForceHeartbeat();
                            //Debug.Log(bAR);
                            //SteamServer.ForceHeartbeat();
                        }
                    }
                    client.gotAuthPacket = true;
                }
            } else
            {
                //Expected auth packet, got something else.
            }
            //Thread.Sleep(100);
            //Send login info
            //PlayerLoginData pLD = new PlayerLoginData();
            //pLD.playerNetworkID = client.clientID;
            Packet loginPacket = new Packet(Packet.pType.loginInfo, Packet.sendType.nonbuffered, new PlayerLoginData((short)client.clientID,mySteamID));
            loginPacket.packetOwnerID = -1;
            loginPacket.sendToAll = false;
            SendPacket(client, loginPacket);


            //Thread.Sleep(50); //Prevents a memory error on the client side? bruh.
            //Send buffered packets
            if (bufferedPackets.Count > 0)
            {
                List<Packet> packetsToSend = new List<Packet>(); //Will contain buffered packets and all network fields to be updated.
                packetsToSend.AddRange(GetAllBufferedPackets());
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
                    HandlePacketFromClient(pack, client);
                }
                catch (System.Exception e)
                {
                    string error = e.ToString();
                    //These errors in the if statement, are already being handled. For most the usual occur when the server stops.
                    if (!error.Contains("WSACancelBlockingCall") && !error.Contains("transport connection") && !error.Contains("System.ObjectDisposedException")) //Server closed
                    {
                        Debug.LogError(e);
                    }
                    if (!error.Contains("Check destIndex and length"))
                    {
                        clientRunning = false; //Basically end the thread.
                    }

                }
            }
            client.netStream.Close();
            client.playerConnected = false;
            SteamServer.EndSession(client.steamID);
            if (connections.Contains(client))
            {
                connections.Remove(client);
            }
            NetTools.onPlayerDisconnect.Invoke(client);
            Debug.Log("NetServer.ClientHandler() thread has successfully finished.");
        }

        public void HandlePacketFromClient(Packet pack, NetworkPlayer client, bool fromSelf = false)
        {
            if (pack == null)
            {
                return;
            }
            //if (fromSelf)
            //{
            //    Debug.Log("SELF PACKET: " + pack.packetType);
            //}

            if (pack.packetOwnerID != client.clientID)// && client.tcpClient == NetClient.instanceClient.client) //if server dont change cause if it is -1 it has all authority.
            {
                pack.packetOwnerID = client.clientID;
            }
            if (NetClient.instanceClient != null && client.clientID == NetClient.instanceClient.clientID) //Setup server authority.
            {
                pack.serverAuthority = true;
            }
            else
            {
                pack.serverAuthority = false;
            }

            if (pack.packetType == Packet.pType.rpc)
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
                    List<Packet> related = new List<Packet>();
                    if (bufferedPackets.ContainsKey(pack.relatesToNetObjID.ToString()))
                    {
                        related.AddRange(bufferedPackets[pack.relatesToNetObjID.ToString()]);
                    }
                    if (bufferedPackets.ContainsKey(pack.tag) && bufferedPackets[pack.tag] != null)
                    {
                        related.AddRange(bufferedPackets[pack.tag]);
                    }

                    foreach (Packet buff in related)
                    {
                        if (buff.relatesToNetObjID == pack.relatesToNetObjID && buff.packetType == pack.packetType)
                        {
                            if (buff.packetType == Packet.pType.netVarEdit)
                            {

                                if (buff.GetPacketData<NetworkFieldPacket>().fieldName == pack.GetPacketData<NetworkField>().fieldName)
                                {
                                    RemoveBufferedPacket(buff);
                                }
                            }
                            else if (buff.packetType == Packet.pType.rpc)
                            {
                                if (buff.GetPacketData<RPCPacketData>().rpcIndex == pack.GetPacketData<RPCPacketData>().rpcIndex)
                                {
                                    RemoveBufferedPacket(buff);
                                }
                            }
                        }
                    }
                }

                //Debug.Log("Buffered Packet");
                AddBufferedPacket(pack);
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
                        if (player.tcpClient.Connected)
                        {
                            try
                            {
                                SendPacket(player, pack);
                            }
                            catch (System.Exception e)
                            {
                                if (player.tcpClient.Connected)
                                {
                                    Debug.LogError(e); //If we ain't connected anymore then it makes sense.
                                    connections.Remove(player);
                                }
                            }
                        }
                        else
                        {
                            connections.Remove(player);
                        }
                    }

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

        public void SendPacketToAll(Packet packet)
        {
            if (NetTools.isSingleplayer) //Shouldn't get ran on a singleplayer game anyways but just in case!
            {
                UnityPacketHandler.instance.QueuePacket(packet);
                return;
            }

            foreach (NetworkPlayer player in connections)
            {
                if (player != null)
                {
                    if (packet.packetSendType == Packet.sendType.proximity)
                    {
                        if (Vector3.Distance(player.proximityPosition, packet.packetPosition.ToVec3()) >= player.loadProximity)
                        {
                            continue;
                        }
                    }
                    
                    if (packet.reliable)
                    {
                        SendTCPPacket(player, packet);
                    } else
                    {
                        SendUDPPacket(packet);
                    }
                }
            }
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
                SendUDPPacket(packet);
            }
        }

        public void SendUDPPacket(Packet packet, bool ignoreSelf=false)
        {
            udpListener.SendPacket(packet,ignoreSelf);
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

                    //player.tcpClient.SendBufferSize = array.Length+arraySize.Length;

                    player.netStream.Write(arraySize, 0, arraySize.Length);

                    //Send packet
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
            //player.tcpClient.ReceiveBufferSize = pSize;
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

        public List<Packet> GetAllBufferedPackets()
        {
            List<Packet> packets = new List<Packet>();
            int count = 0;
            foreach(List<Packet> p in bufferedPackets.Values.ToArray())
            {
                foreach(Packet newPacket in p.ToArray())
                {
                    if(packets.Contains(newPacket))
                    {
                        continue;
                    } else
                    {
                        packets.Add(newPacket);
                    }
                }
                count += 1;
            }
            return packets;
        }

        public void AddBufferedPacket(Packet p)
        {
            string tag = p.relatesToNetObjID.ToString();
            if (bufferedPackets.ContainsKey(tag))
            {
                bufferedPackets[tag].Add(p);
            }
            else
            {
                bufferedPackets[tag] = new List<Packet>();
                bufferedPackets[tag].Add(p);
#if UNITY_EDITOR
                bufferedDisplay.Add(new BufferedPacketDisplay(tag));
#endif
            }
            tag = p.tag.ToString();
            if (bufferedPackets.ContainsKey(tag))
            {
                bufferedPackets[tag].Add(p);
            }
            else
            {
                bufferedPackets[tag] = new List<Packet>();
                bufferedPackets[tag].Add(p);
#if UNITY_EDITOR
                bufferedDisplay.Add(new BufferedPacketDisplay(tag));
#endif
            }

        }
        
        public void RemoveBufferedPacket(Packet p)
        {
            string tag = p.relatesToNetObjID.ToString();
            if (bufferedPackets.ContainsKey(tag))
            {
                bufferedPackets[tag].Remove(p);
            } else if (bufferedPackets.ContainsKey(p.tag.ToString()))
            {
                bufferedPackets[p.tag.ToString()].Remove(p);
            }
        }

        public bool VerifyPacketValidity(ulong steamID, Packet p)
        {
            NetworkPlayer player = null;
            if (p.packetOwnerID == -1)
            {
                player = GetPlayerBySteamID(steamID);
                if (player == null)
                {
                    return false;
                }
            }
            else
            {
                player = connectionsByID[p.packetOwnerID];
            }

            if (player.clientID == p.packetOwnerID)
            {
                if(player.steamID == steamID)
                {
                    return true;
                }
            }
            return false;
        }

        public NetworkPlayer GetPlayerByUDPEndpoint(IPEndPoint endPoint)
        {
            foreach(NetworkPlayer player in connections)
            {
                Debug.Log(player.udpEndpoint.ToString() + "|||" + endPoint.ToString());
                if (player.udpEndpoint.ToString() == endPoint.ToString())
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

        public NetworkPlayer GetPlayerBySteamID(ulong steamID)
        {
            foreach(NetworkPlayer player in connections)
            {
                if(player.steamID == steamID)
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

        public void KickPlayer(NetworkPlayer player, string reason, float delay = 0f)
        {
            Task kickTask = new Task(delegate {
                DirectKick(player, reason, delay);
            });
            kickTask.Start();
            //DirectKick(player, reason, delay);
        }

        private void DirectKick(NetworkPlayer player, string reason, float delay = 0f)
        {
            Thread.Sleep((int)(delay * 1000)); //Convert to milliseconds.
            ConnectionPacket cP = new ConnectionPacket(true, reason);

            Packet p = new Packet(Packet.pType.connectionPacket, Packet.sendType.nonbuffered, ENSSerialization.SerializeConnectionPacket(cP));
            p.packetOwnerID = -1;
            p.sendToAll = false;
            p.serverAuthority = true;
            SendPacket(player, p);
            player.netStream.Close();
            player.playerConnected = false;
            SteamServer.EndSession(player.steamID);
            SteamUser.EndAuthSession(player.steamID);
            if (connections.Contains(player))
            {
                connections.Remove(player);
            }
            Debug.Log("Client<" + player.steamID + "> has been kicked for: " + reason);
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
        public bool gotAuthPacket = false;

        public IPEndPoint udpEndpoint;
        public string playerIP;
        public int playerPort;

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
            this.playerIP = this.tcpClient.Client.RemoteEndPoint.ToString();
        }

    }

#if UNITY_EDITOR

    [System.Serializable]
    public class BufferedPacketDisplay
    {
        public string tag;

        public BufferedPacketDisplay(string tag)
        {
            this.tag = tag;
        }
    }


#endif

}