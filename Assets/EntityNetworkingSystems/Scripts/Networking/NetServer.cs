using System;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Threading;
using Steamworks;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Steamworks.Data;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class NetServer : ISocketManager
    {
        public static NetServer serverInstance = null;
        
        public int maxConnections = 8;
        [Space]
        public int steamAppID = -1; //If -1 it won't initialize, meaning you must on your own.
        public string modDir = "spacewar";
        public string gameDesc = "spacewar";
        public string mapName = "world1";
        public string password = "";
        public ulong mySteamID = 0;
        [Space]
        public Dictionary<int, NetworkPlayer> networkPlayersByID = new Dictionary<int, NetworkPlayer>();
        public Dictionary<NetworkPlayer, Connection> connectionsByNetworkPlayer = new Dictionary<NetworkPlayer, Connection>();
        public Dictionary<Connection, NetworkPlayer> networkPlayerbyConnection = new Dictionary<Connection, NetworkPlayer>();
        public List<NetworkPlayer> networkPlayers = new List<NetworkPlayer>();
        public NetworkPlayer myConnection = null; //The client that represents the host.
#if UNITY_EDITOR
        public List<BufferedPacketDisplay> bufferedDisplay = new List<BufferedPacketDisplay>();
#endif
        public Dictionary<string, List<Packet>> bufferedPackets = new Dictionary<string, List<Packet>>(); //Packet String Tag, List Of Packets with that tag.
        
        
        int lastPlayerID = -1;

        private bool initialized = false;
        private SocketManager socketManager;
        
        
        public void StartServer(bool isSingleplayer=false)
        {
            Initialize();
            
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
            
            socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>(432);
            socketManager.Interface = this;


            Debug.Log("Server started successfully.");
            NetTools.isServer = true;

            UnityPacketHandler.instance.StartHandler();
            
            NetTools.TriggerReceiveThread();
        }

        public void StopServer()
        {
            if (socketManager == null)
            {
                return; //Server not running.
            }

            ConnectionPacket cP = new ConnectionPacket(true, "Server Closed.");
            Packet p = new Packet(Packet.pType.connectionPacket, Packet.sendType.nonbuffered, ENSSerialization.SerializeConnectionPacket(cP));

            foreach (NetworkPlayer client in networkPlayers)
            {
                SendPacket(client, p); //Server Closed Packet.
            }

            if (!NetTools.isSingleplayer)
            {
                SteamInteraction.instance.ShutdownServer();
            }
            

            if (GameObject.FindObjectOfType<UnityPacketHandler>() != null)
            {
                GameObject.Destroy(GameObject.FindObjectOfType<UnityPacketHandler>().gameObject);
            }

            NetTools.isServer = false;
            NetTools.isSingleplayer = false;
        }
        
        private void Initialize()
        {

            if (IsInitialized())
            {
                return;
            }

            initialized = true;
            
            serverInstance = this;

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

            if(maxConnections <= 1)
            {
                NetTools.isSingleplayer = true;
            }

        }

#region ISocketManager
        //Start of ISocketManager
        
        public void OnConnecting(Connection connection, ConnectionInfo info)
        {
            //base.OnConnecting(connection,info);
            Debug.Log("[NetServer] Client connecting: "+info.Identity);
            connection.Accept();
        }

        public void OnConnected(Connection connection, ConnectionInfo info)
        {
            //base.OnConnected(connection,info);
            Debug.Log("[NetServer] Client connected: "+info.Identity);
            SetupNewConnection(connection,info);
        }

        public void OnDisconnected(Connection connection, ConnectionInfo info)
        {
            //base.OnDisconnected(connection,info);
            Debug.Log("[NetServer] Client disconnected: "+info.Identity);
            //todo handle disconnect
            networkPlayerbyConnection[connection].networkState = NetworkPlayer.NetState.disconnected; //Client handler will take it from here.
            ClientDisconnect(networkPlayerbyConnection[connection]);
        }

        public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            byte[] rawPacket = new byte[size];
            Marshal.Copy(data,rawPacket,0,size);
            Packet packet = ENSSerialization.DeserializePacket(rawPacket); //Packet.DeJsonifyPacket(Encoding.ASCII.GetString(byteMessage));//(Packet)Packet.DeserializeObject(byteMessage);
            
            HandlePacketFromClient(packet,networkPlayerbyConnection[connection], SteamClient.SteamId == identity.SteamId);
        }
        
        //End of ISocketManager
#endregion

        private void UpdateRecieve()
        {
            while (socketManager != null)
            {
                socketManager.Receive();
            }
            Debug.Log("[NetServer] SocketManager Receive Thread has ended.");
        }

        public void SetupNewConnection(Connection connect, ConnectionInfo info)
        {
            if (!IsInitialized())
            {
                Debug.Log("Server not initialized. Please run Initialize() first.");
                return;
            }

            NetworkPlayer netClient = new NetworkPlayer(connect,info);
            networkPlayers.Add(netClient);
            networkPlayersByID[netClient.clientID] = netClient;
            connectionsByNetworkPlayer[netClient] = connect;
            networkPlayerbyConnection[connect] = netClient;

            if (info.Identity.SteamId == SteamClient.SteamId)
            {
                //Mine
                myConnection = netClient;
            }
            
            SendPacket(netClient, new Packet(Packet.pType.unassigned, Packet.sendType.nonbuffered, 0));
            lastPlayerID += 1;
            if(CurrentConnectionCount() > maxConnections) //todo move this into OnConnecting
            {
                //Too many players, kick em with reason 'Server Full'.
                KickPlayer(netClient, "Server Full",5f);
                return;
            }
        
        
            Debug.Log("New Client Connected Successfully. <"+info.Identity.SteamId+">");

            ClientConnect(netClient);

        }

        public void ClientConnect(NetworkPlayer client)
        {
            //Debug.Log(p.jsonData);
            //Packet curPacket = RecvPacket(client);

            
            //todo WILL NEED TO REIMPLEMENT NETWORK AUTH
            

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
                
                Packet lastMulti = new Packet(Packet.pType.multiPacket, Packet.sendType.nonbuffered, new PacketListPacket(tempPackets));
                lastMulti.sendToAll = false;
                SendPacket(client, lastMulti);
            }
            NetTools.onPlayerJoin.Invoke(client);
        }

        public void ClientDisconnect(NetworkPlayer client)
        {
            if (networkPlayers.Contains(client))
            {
                networkPlayers.Remove(client);
                networkPlayersByID.Remove(client.clientID);
                connectionsByNetworkPlayer[client].Close();
                connectionsByNetworkPlayer.Remove(client);
                networkPlayerbyConnection.Remove(client.connection);
            }

            NetTools.onPlayerDisconnect.Invoke(client);
        }

        public void HandlePacketFromClient(Packet pack, NetworkPlayer client, bool fromSelf = false)
        {
            if (pack == null)
            {
                return;
            }

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
            
            //If the player hasn't sent us the auth packet yet and this one isn't an auth packet kick em.
            if (!pack.serverAuthority && !client.gotAuthPacket && pack.packetType != Packet.pType.networkAuth)
            {
                KickPlayer(client,"Never Received Auth Packet",0.0f);
                return;
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
                foreach (NetworkPlayer player in networkPlayers.ToArray())
                {

                    //Debug.Log(player.clientID + " " + NetTools.clientID);
                    if (player == null || player.connection == null || (player.clientID == NetTools.clientID))
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

        public void AuthenticateClient(Packet authPacket)
        {
            NetworkAuthPacket clientAuthData = ENSSerialization.DeserializeAuthPacket(authPacket.packetData);
            NetworkPlayer client = GetPlayerByID(authPacket.packetOwnerID);
            
            if (!client.gotAuthPacket)
            {
                if(password != "" && clientAuthData.password != password)
                {
                    //Kick player wrong password.
                    KickPlayer(client, "password");
                    return;
                } else if (SteamApps.BuildId != clientAuthData.steamBuildID)
                {
                    //Kick player wrong version.
                    KickPlayer(client, "version mismatch us:"+SteamApps.BuildId+", them:"+clientAuthData.steamBuildID);
                    return;
                }
                
                //If launchSteamGameServer is false then just assume it's true as then we don't authenticate.
                bool worked = !SteamInteraction.instance.launchSteamGameServer || SteamServer.BeginAuthSession(clientAuthData.authData, clientAuthData.steamID);

                if (worked)
                {

                    client.steamID = clientAuthData.steamID;

                    SteamInteraction.instance.connectedSteamIDs.Add(client.steamID);
                    if (SteamInteraction.instance.launchSteamGameServer)
                    {
                        SteamServer.UpdatePlayer(client.steamID, new Friend(client.steamID).Name, 0);
                        SteamServer.ForceHeartbeat();
                    }

                    client.gotAuthPacket = true;
                    
                    //Send an auth packet back saying it was authed successfully! (See UnityPacketHandler auth section)
                    SendPacket(client,new Packet(Packet.pType.networkAuth,Packet.sendType.nonbuffered,new byte[]{0}));
                    
                    Debug.Log("[NetServer] Client("+client.steamID+") has successfully authenticated.");
                }
                else
                {
                    KickPlayer(client,"Couldn't authenticate with Steam.");
                }
            }
        }

        public bool IsInitialized()
        {
            return initialized;
        }

        public int CurrentConnectionCount()
        {
            return networkPlayers.Count;
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
            
            byte[] serializedPacket = ENSSerialization.SerializePacket(packet);
            connectionsByNetworkPlayer[player].SendMessage(serializedPacket,packet.reliable ? SendType.Reliable : SendType.Unreliable);
        }

        public void Receive()
        {
            socketManager?.Receive();
        }

        byte[] RecieveSizeSpecificData(int byteCountToGet, NetworkStream netStream)
        {
            // //byteCountToGet--;
            // byte[] bytesRecieved = new byte[byteCountToGet];
            //
            // int messageRead = 0;
            // while (messageRead < bytesRecieved.Length)
            // {
            //     int bytesRead = netStream.Read(bytesRecieved, messageRead, bytesRecieved.Length - messageRead);
            //     messageRead += bytesRead;
            // }
            // return bytesRecieved;
            return null;
        }

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
            NetworkPlayer player = networkPlayersByID[p.packetOwnerID];
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
            if (networkPlayersByID.ContainsKey(id))
            {
                return networkPlayersByID[id];
            }
            return null;
        }

        public NetworkPlayer GetPlayerBySteamID(ulong steamID)
        {
            foreach(NetworkPlayer player in networkPlayers)
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
            player.networkState = NetworkPlayer.NetState.connected;
            SteamServer.EndSession(player.steamID);
            SteamUser.EndAuthSession(player.steamID);
            if (networkPlayers.Contains(player))
            {
                networkPlayers.Remove(player);
            }
            Debug.Log("Client<" + player.steamID + "> has been kicked for: " + reason);
        }
    }

    [System.Serializable]
    public class NetworkPlayer
    {
        public static int lastID = -1;
        
        public int clientID { get; private set; }
        public Vector3 proximityPosition = Vector3.zero;
        public float loadProximity = 15f;
        public NetIdentity identity;
        public bool gotAuthPacket = false;
        public NetState networkState = NetState.connected;
        
        public Connection connection;
        public ConnectionInfo connectionInfo;

        public NetworkPlayer(Connection connection, ConnectionInfo connectionInfo)
        {
            this.connection = connection;
            this.connectionInfo = connectionInfo;
            this.clientID = lastID + 1;
            this.identity = connectionInfo.Identity;
            this.steamID = connectionInfo.Identity.SteamId.Value;
            lastID += 1;
        }
        
        //This constructor is intended for singleplayer
        public NetworkPlayer()
        {
            
        }

        public ulong steamID
        {
            get
            {
                if (_steamID != 0)
                {
                    return _steamID;
                }
                else
                {
                    return this.identity.SteamId.Value;
                }
            }
            set => _steamID = value;
        }

        private ulong _steamID = 0;
        

        public enum NetState
        {
            connected,
            disconnected,
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