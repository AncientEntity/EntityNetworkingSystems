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
using EntityNetworkingSystems.Steam;
using System.Threading.Tasks;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class ServerHandler
    {
        public static ServerHandler serverInstance = null;

        public int maxConnections = 8;
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

        SteamListener steamListener = null;
        List<Thread> connThreads = new List<Thread>();
        Thread udpHandler = null;
        Thread tcpHandler = null;
        //Thread packetSendHandler = null;
        //Dictionary<Packet, NetworkPlayer> queuedSendPackets = new Dictionary<Packet, NetworkPlayer>();

        private int lastPlayerID = -1;
        private bool initialized = false;
        private bool serverRunning = false;

        public void Initialize()
        {

            if (IsInitialized())
            {
                return;
            }

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

            //if (packetSendHandler == null)
            //{
            //    packetSendHandler = new Thread(new ThreadStart(PacketSendHandler));
            //    packetSendHandler.Start();
            //}
            if(maxConnections <= 1)
            {
                NetTools.isSingleplayer = true;
            }

            initialized = true;

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
                while (steamListener != null)
                {
                    Packet recieved = steamListener.Recieve(SteamListener.SteamSendTypes.unreliable);
                    //Debug.Log("Recieved Packet: " + recieved.Key.udpEndpoint.ToString() + ", " + recieved.Value.packetType);
                    UnityPacketHandler.instance.QueuePacket(recieved);
                    steamListener.SendPacket(recieved,SteamListener.SteamSendTypes.unreliable, true);
                }
            } catch (System.Exception e)
            {
                if(e.ToString().Contains("ThreadAbort"))
                {
                    Debug.Log("NetServer.UDPHandler has stopped.");
                }
            }
        }
        public void TCPHandler()
        {
            try
            {
                while (steamListener != null)
                {
                    Packet recieved = steamListener.Recieve(SteamListener.SteamSendTypes.reliable);
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

            steamListener = new SteamListener();
            Debug.Log("Server started successfully.");
            NetTools.isServer = true;

            UnityPacketHandler.instance.StartHandler();

            udpHandler = new Thread(new ThreadStart(UDPHandler));
            udpHandler.Name = "ENSServerUDPHandler";
            udpHandler.Start();
            tcpHandler = new Thread(new ThreadStart(TCPHandler));
            tcpHandler.Name = "ENSServerTCPHandler";
            tcpHandler.Start();
            //packetSendHandler = new Thread(new ThreadStart(SendingPacketHandler));
            //packetSendHandler.Start();
            serverRunning = true;

        }

        public void StopServer()
        {
            if (initialized)
            {
                ConnectionPacket cP = new ConnectionPacket(true, "Server Closed.");
                Packet p = new Packet(Packet.pType.connectionPacket, Packet.sendType.nonbuffered, ENSSerialization.SerializeConnectionPacket(cP));

                foreach (NetworkPlayer client in connections.ToArray())
                {
                    SendPacket(client, p); //Server Closed Packet.
                    steamListener.EndConnectionWithSteamID(client.steamID);
                    client.playerConnected = false;
                    SteamServer.EndSession(client.steamID);
                    if (connections.Contains(client))
                    {
                        connections.Remove(client);
                    }
                    NetTools.onPlayerDisconnect.Invoke(client);
                }

                if (!NetTools.isSingleplayer)
                {
                    SteamInteraction.instance.ShutdownServer();
                }
            }
            if (steamListener != null)
            {
                steamListener.Stop();
                steamListener = null;
            }

            if (udpHandler != null)
            {
                udpHandler.Abort();
                udpHandler = null;
            }
            if (tcpHandler != null)
            {
                tcpHandler.Abort();
                tcpHandler = null;
            }

            if (GameObject.FindObjectOfType<UnityPacketHandler>() != null)
            {
                GameObject.Destroy(GameObject.FindObjectOfType<UnityPacketHandler>().gameObject);
            }
            //NetServer.serverInstance = null;
            connections = new List<NetworkPlayer>();
            connectionsByID = new Dictionary<int, NetworkPlayer>();
            bufferedPackets = new Dictionary<string, List<Packet>>();
            if (ServerHandler.serverInstance != null && ServerHandler.serverInstance.myConnection != null)
            {
                ServerHandler.serverInstance.myConnection = null;
            }

            NetTools.isServer = false;
            NetTools.isSingleplayer = false;
            serverRunning = false;
        }

        public void AcceptNewClient(ulong userSteamID)
        {
            try
            {
                NetworkPlayer netClient = new NetworkPlayer(userSteamID);
                netClient.clientID = lastPlayerID + 1;
                connections.Add(netClient);
                connectionsByID[netClient.clientID] = netClient;
                //SendTCPPacket(netClient, new Packet(Packet.pType.unassigned, Packet.sendType.nonbuffered, 0));
                lastPlayerID += 1;
                if(CurrentConnectionCount() > maxConnections)
                {
                    //Too many players, kick em with reason 'Server Full'.
                    KickPlayer(netClient, "Server Full",5f);
                    return;
                }


                Debug.Log("New Client Connected Successfully. <"+userSteamID+">("+netClient.clientID+")");

                Packet loginPacket = new Packet(Packet.pType.loginInfo, Packet.sendType.nonbuffered, new PlayerLoginData((short)netClient.clientID, userSteamID));
                loginPacket.packetOwnerID = -1;
                loginPacket.sendToAll = false;
                SendTCPPacket(netClient, loginPacket);


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
                            SendPacket(netClient, multiPack);

                            tempPackets = new List<Packet>();
                        }
                    }

                    //Send whatever remains in it otherwise max-1 or less packets will be lost.
                    //Debug.Log(tempPackets.Count);
                    Packet lastMulti = new Packet(Packet.pType.multiPacket, Packet.sendType.nonbuffered, new PacketListPacket(tempPackets));
                    lastMulti.sendToAll = false;
                    SendPacket(netClient, lastMulti);
                    //Debug.Log(packetsToSend.Count);
                    //Packet bpacket = new Packet(Packet.pType.multiPacket, Packet.sendType.nonbuffered, new PacketListPacket(packetsToSend));
                    //bpacket.sendToAll = false;
                    //SendPacket(client, bpacket);
                }
                NetTools.onPlayerJoin.Invoke(netClient);




            }
            catch (System.Exception e)
            {
                //Server is stopping.
                Debug.LogError(e);
            }

            
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
                    if (player == null || (player.clientID == NetTools.clientID))
                    {
                        continue;
                    }

                    if (pack.sendToAll == true || pack.usersToRecieve.Contains(player.clientID))
                    {
                        if (player.playerConnected)
                        {
                            try
                            {
                                SendPacket(player, pack);
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError(e); //If we ain't connected anymore then it makes sense.
                                connections.Remove(player);
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
            return initialized;
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
                SendUDPPacket(packet);
            }
        }

        public void SendUDPPacket(Packet packet, bool ignoreSelf=false)
        {
            steamListener.SendPacket(packet,SteamListener.SteamSendTypes.unreliable,ignoreSelf);
        }

        public void SendTCPPacket(NetworkPlayer player, Packet packet)//, bool queuedPacket = false)
        {
            steamListener.SendPacketDirect(packet, SteamListener.SteamSendTypes.reliable, player.steamID);
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
            //if(!connectionsByID.ContainsKey(p.packetOwnerID))
            //{
            //    return false;
            //}
            if(p.packetOwnerID == -1)
            {
                return true;
            }
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
            player.playerConnected = false;
            SteamServer.EndSession(player.steamID);
            SteamUser.EndAuthSession(player.steamID);
            steamListener.EndConnectionWithSteamID(player.steamID);
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
        public ulong steamID;
        public Vector3 proximityPosition = Vector3.zero;
        public float loadProximity = 15f;

        public bool playerConnected = true;

        public NetworkPlayer(ulong userSteamID)
        {
            steamID = userSteamID;

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