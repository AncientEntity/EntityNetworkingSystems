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
using System.Threading.Tasks;
using EntityNetworkingSystems.Steam;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class NetServer
    {
        public static NetServer serverInstance = null;

        public int maxConnections = 8;

        public P2PSocket socket;

        [Space]
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
        //public List<Packet> bufferedPackets = new List<Packet>();

        Thread unreliableHandler = null;
        Thread reliableHandler = null;
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
            

            if (socket != null)
            {
                Debug.LogError("Trying to initial NetServer when it has already been initialized.");
                return;
            }

            if (UnityPacketHandler.instance == null)
            {
                GameObject uPH = new GameObject("Unity Packet Handler");
                uPH.AddComponent<UnityPacketHandler>();
                GameObject.DontDestroyOnLoad(uPH);
            }
            if (!NetTools.isSingleplayer && SteamInteraction.instance == null)
            {
                GameObject steamIntegration = new GameObject("Steam Integration Handler");
                steamIntegration.AddComponent<SteamInteraction>();
                steamIntegration.GetComponent<SteamInteraction>().Initialize();
                //steamIntegration.GetComponent<SteamInteraction>().StartServer();
                mySteamID = SteamClient.SteamId.Value;
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


        public void UnreliableHandler()
        {
            try
            {
                while (socket != null)
                {
                    System.Tuple<ulong, byte[]> rawPacket = socket.GetNextUnreliable();
                    Packet recieved = ENSSerialization.DeserializePacket(rawPacket.Item2);
                    //Debug.Log("Recieved Packet: " + recieved.Key.udpEndpoint.ToString() + ", " + recieved.Value.packetType);
                    UnityPacketHandler.instance.QueuePacket(recieved);
                    socket.SendAll(sendType.unreliable, rawPacket.Item2);
                }
            }
            catch (System.Exception e)
            {
                if (e.ToString().Contains("ThreadAbort"))
                {
                    Debug.Log("NetServer.UDPHandler has stopped.");
                }
            }
        }
        public void ReliableHandler()
        {
            try
            {
                while (socket != null)
                {
                    Packet recieved = ENSSerialization.DeserializePacket(socket.GetNextReliable().Item2);
                    HandlePacketFromClient(recieved, connectionsByID[recieved.packetOwnerID]);
                    //Debug.Log("Recieved Packet: " + recieved.Key.udpEndpoint.ToString() + ", " + recieved.Value.packetType);
                    //UnityPacketHandler.instance.QueuePacket(recieved);
                    //steamListener.SendPacket(recieved, SteamListener.SteamSendTypes.reliable, true);
                }
            }
            catch (System.Exception e)
            {
                if (e.ToString().Contains("ThreadAbort"))
                {
                    Debug.Log("NetServer.TCPHandler has stopped.");
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
            socket.onConnectionStart.AddListener(HandleNewConnection);
            socket.onConnectionEnd.AddListener(OnConnectionEnd);
            socket.Start();
            Debug.Log("Server started successfully.");
            NetTools.isServer = true;

            UnityPacketHandler.instance.StartHandler();

            reliableHandler = new Thread(new ThreadStart(ReliableHandler));
            unreliableHandler = new Thread(new ThreadStart(ReliableHandler));
            reliableHandler.Start();
            unreliableHandler.Start();


        }

        public void StopServer()
        {
            if (socket != null)
            {
                ConnectionPacket cP = new ConnectionPacket(true, "Server Closed.");
                Packet p = new Packet(Packet.pType.connectionPacket, Packet.sendType.nonbuffered, ENSSerialization.SerializeConnectionPacket(cP));

                foreach (NetworkPlayer client in connections)
                {
                    SendPacket(client, p); //Server Closed Packet.
                    socket.Disconnect(client.steamID);
                }
                socket.Stop();
                socket = null;

                if (!NetTools.isSingleplayer)
                {
                    SteamInteraction.instance.ShutdownServer();
                }
            }

            if (unreliableHandler != null)
            {
                unreliableHandler.Abort();
            }
            if (reliableHandler != null)
            {
                reliableHandler.Abort();
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

        public void HandleNewConnection(Connection c)
        {
            if (!IsInitialized())
            {
                Debug.Log("Server not initialized. Please run Initialize() first.");
                return;
            }


            //while (CurrentConnectionCount() >= maxConnections)
            //{
            //    Thread.Sleep(1000);
            //}
            Debug.Log("Awaiting Client Connection...");

            try
            {
                
                NetworkPlayer netClient = new NetworkPlayer(c);
                netClient.clientID = lastPlayerID + 1;
                connections.Add(netClient);
                connectionsByID[netClient.clientID] = netClient;
                
                lastPlayerID += 1;
                if(CurrentConnectionCount() > maxConnections)
                {
                    //Too many players, kick em with reason 'Server Full'.
                    KickPlayer(netClient, "Server Full",5f);
                    return;
                }


                Debug.Log("New Client Connected Successfully. <"+c.steamID+">");

                Thread connThread = new Thread(() => ClientHandler(netClient));
                netClient.threadHandlingClient = connThread;
                connThread.Start();

            } catch (System.Exception e)
            {
                //Server is stopping.
                Debug.LogError(e);
            }
        }

        public void ClientHandler(NetworkPlayer client)
        {
            //Debug.Log(p.jsonData);
            
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

        }

        public void OnConnectionEnd(Connection c)
        {
            socket.Disconnect(c.steamID);
            NetworkPlayer client = GetPlayerBySteamID(c.steamID);
            client.playerConnected = false;
            SteamServer.EndSession(client.steamID);
            if (connections.Contains(client))
            {
                connections.Remove(client);
            }
            NetTools.onPlayerDisconnect.Invoke(client);
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
            if (client.clientID == NetClient.instanceClient.clientID) //Setup server authority.
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
                    if (player == null || player.playerConnected == false || (player.clientID == NetTools.clientID))
                    {
                        continue;
                    }

                    if (pack.sendToAll == true || pack.usersToRecieve.Contains(player.clientID))
                    {
                        try
                        {
                            SendPacket(player, pack);
                        }
                        catch (System.Exception e)
                        {
                            if (player.playerConnected)
                            {
                                Debug.LogError(e); //If we ain't connected anymore then it makes sense.
                                connections.Remove(player);
                            }
                        }
                        
                    }

                }
            }
        
    }

        public bool IsInitialized()
        {
            if (socket == null)
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
                SendReliable(player, packet);
            } else
            {
                SendUnreliable(packet);
            }
        }

        public void SendUnreliable(Packet packet, bool ignoreSelf=false)
        {
            socket.SendAll(sendType.unreliable,ENSSerialization.SerializePacket(packet));
        }

        public void SendReliable(NetworkPlayer player, Packet packet)//, bool queuedPacket = false)
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
            byte[] array = ENSSerialization.SerializePacket(packet);//Encoding.ASCII.GetBytes(Packet.JsonifyPacket(packet));//Packet.SerializeObject(packet);

            socket.SendTo(player.steamID, sendType.reliable, array);

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
            NetworkPlayer player = connectionsByID[p.packetOwnerID];
            if (player.clientID == p.packetOwnerID)
            {
                if(player.steamID == steamID)
                {
                    return true;
                }
            }
            return false;
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
            socket.Disconnect(player.steamID);
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
        public Vector3 proximityPosition = Vector3.zero;
        public float loadProximity = 15f;
        public Thread threadHandlingClient;
        public ulong steamID;
        public bool gotAuthPacket = false;

        

        public bool playerConnected = true;

        public NetworkPlayer(Connection client)
        {
            if(client == null)
            {
                return;
            }

            this.steamID = client.steamID;
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