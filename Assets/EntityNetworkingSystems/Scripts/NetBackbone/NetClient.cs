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
using System.Linq;
using EntityNetworkingSystems.Steam;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class NetClient
    {
        public static NetClient instanceClient;

        public bool connectedToServer = false;
        public P2PSocket socket;
        Thread connectionHandler = null;
        Thread udpRecieveHandler = null;
        Thread udpSendHandler = null;
        Thread tcpSendHandler = null;
        [Space]
        public int clientID = -1;

        public int steamAppID = -1; //If -1 it wont initialize and you'll need to do it somewhere else :)
        public ulong serversSteamID = 0; //The server's steam ID.
        public string password;
#if UNITY_EDITOR
        [Space]
        public bool trackOverhead = false;
        public int packetsSent = 0;
        public Packet.pType overheadFilter = Packet.pType.unassigned;
        public string packetByteLength = "";
#endif
        public string lastConnectionError = "";


        public void Initialize()
        {

            instanceClient = this;


            //if (client != null)
            //{
            //    //Debug.LogError("Trying to initial NetClient when it has already been initialized.");
            //    return;
            //}
            socket = new P2PSocket();
            NetTools.isClient = true;


            if (SteamInteraction.instance == null)
            {
                GameObject steamIntegration = new GameObject("Steam Integration Handler");
                steamIntegration.AddComponent<SteamInteraction>();
                steamIntegration.GetComponent<SteamInteraction>().Initialize();
                //steamIntegration.GetComponent<SteamInteraction>().StartServer();
                GameObject.DontDestroyOnLoad(steamIntegration);
            }
            


            if (UnityPacketHandler.instance == null)
            {
                GameObject uPH = new GameObject("Unity Packet Handler");
                uPH.AddComponent<UnityPacketHandler>();
                GameObject.DontDestroyOnLoad(uPH);
            }


        }


        public void UnreliableHandler()
        {
            Thread.Sleep(1000);

            //udpPlayer.SendPacket(new Packet(Packet.pType.unassigned, Packet.sendType.nonbuffered, new byte[0]));

            while (connectedToServer)
            {
                try
                {
                    UnityPacketHandler.instance.QueuePacket(RecvUnreliable());
                    //Debug.Log("Packet Received UDP");
                }
                catch (System.Exception e)
                {
                    if (!e.ToString().Contains("ThreadAbortException")) //Game stopped in Editor play mode.
                    {
                        if (e.ToString().Contains("NullReferenceException"))
                        {
                            return; //Most likely Unity editor play mode ended.
                        }
                        Debug.LogError(e);
                    }
                }
            }
        }


        public void ConnectionHandler()
        {
            //Thread.Sleep(150);
            while (socket != null)
            {
                try
                {
                    Packet packet = RecvReliable();
                    UnityPacketHandler.instance.QueuePacket(packet);
                }
                catch
                {
                    //Debug.Log("Server closed");
                }

            }
            Debug.Log("NetClient.ConnectionHandler() thread has successfully finished.");
        }


        public void ConnectToSingleplayer()
        {
            NetTools.isClient = true;
            NetTools.isSingleplayer = true;

            connectedToServer = true;

            if (NetworkData.instance != null)
            {
                NetworkData.instance.GeneratePooledObjects();
            }
            else
            {
                Debug.LogWarning("There is no loaded NetworkData in the scene. This may break some features.");
            }

            NetworkPlayer player = new NetworkPlayer(null);
            player.clientID = 0;

            player.steamID = SteamInteraction.instance.ourSteamID;
            

            NetServer.serverInstance.connections.Add(player);

            //PlayerLoginData pLD = new PlayerLoginData();
            //pLD.playerNetworkID = 0;
            Packet loginPacket = new Packet(Packet.pType.loginInfo, Packet.sendType.nonbuffered, new PlayerLoginData((short)0, 0));
            loginPacket.packetOwnerID = -1;
            loginPacket.sendToAll = false;
            NetServer.serverInstance.SendPacket(player, loginPacket);

            //NetTools.onJoinServer.Invoke();
            UnityPacketHandler.instance.StartHandler();

            SteamFriends.SetRichPresence("steam_display", "Singleplayer");
            SteamFriends.SetRichPresence("steam_player_group", "Survival");

        }

        public bool ConnectToServer(ulong steamID)
        {
            //if(client != null)
            //{
            //    client.Dispose();
            //}

            socket = new P2PSocket();
            socket.AllowConnectionsAll(true);
            socket.ConnectToIncoming(true);
            socket.Start();
            NetTools.isClient = true;


            Debug.Log("Attempting Connection");
            try
            {
                socket.Connect(steamID);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                //Couldn't connect to server.
                Debug.LogError("Couldn't connect to server: " + steamID);
                NetTools.isClient = false;
                socket.Stop();
                socket = null;
                NetTools.onFailedServerConnection.Invoke();
                lastConnectionError = e.ToString();
                return false;
            }
            Debug.Log("Connection Accepted");



            //NetTools.isSingleplayer = false;
            ulong usedSteamID = 0;
            byte[] steamAuthData = new byte[0];
            int buildID = -1;
            if (!SteamInteraction.instance.clientStarted)
            {
                SteamInteraction.instance.StartClient();
            }

            steamAuthData = SteamInteraction.instance.clientAuth.Data;
            usedSteamID = SteamClient.SteamId.Value;
            buildID = SteamApps.BuildId; //ADDING +1 TO TEST VERSION MISMATCHES SHOULD BE REMOVED AFTER.
            
           
            Packet authPacket = new Packet(Packet.pType.networkAuth, Packet.sendType.nonbuffered, ENSSerialization.SerializeAuthPacket(new NetworkAuthPacket(steamAuthData, usedSteamID, 0, password,buildID)));
            authPacket.sendToAll = false;
            authPacket.reliable = true;
            SendReliable(authPacket);

            
            connectedToServer = true;
            
            connectionHandler = new Thread(new ThreadStart(ConnectionHandler));
            connectionHandler.Name = "ENSClientConnectionHandler";
            connectionHandler.Start();
            udpRecieveHandler = new Thread(new ThreadStart(UnreliableHandler));
            udpRecieveHandler.Name = "ENSClientUDPReciever";
            udpRecieveHandler.Start();
            packetTCPSendQueue = new List<Packet>();
            packetUDPSendQueue = new List<Packet>();
            tcpSendHandler = new Thread(new ParameterizedThreadStart(PacketSendThread))
            {
                Name = "ENSClientSendTCPHandler"
            };
            tcpSendHandler.Start(true);
            udpSendHandler = new Thread(new ParameterizedThreadStart(PacketSendThread))
            {
                Name = "ENSClientSendUDPHandler"
            };
            udpSendHandler.Start(false);

            SteamFriends.SetRichPresence("connect", ""+ steamID);
            
            SteamFriends.SetRichPresence("steam_display", "Multiplayer");
            //SteamFriends.SetRichPresence("steam_player_group", "Survival");
            //SteamFriends.SetRichPresence("steam_player_group_size", PlayerController.allPlayers.Count.ToString()); //Also gets updated in playercontroller.start

            return true;
        }

        //Must be ran on main thread.
        public void PostConnectStart()
        {
            if (NetworkData.instance != null)
            {
                NetworkData.instance.GeneratePooledObjects();
            }
            else
            {
                Debug.LogWarning("There is no loaded NetworkData in the scene. This may break some features.");
            }


            UnityPacketHandler.instance.StartHandler();


        }

        public void DisconnectFromServer()
        {
            if (socket != null && !NetTools.isSingleplayer)
            {
                Debug.Log("Disconnecting From Server");
                NetTools.onLeaveServer.Invoke("disconnect");
                //if (!NetTools.isServer)
                //{
                //    client.GetStream().Close();
                //}

                socket.Stop();
                socket = null;

                SteamInteraction.instance.StopClient();
                
                packetTCPSendQueue = new List<Packet>();
                packetUDPSendQueue = new List<Packet>();
            }
            connectedToServer = false;
            //NetClient.instanceClient = null;
            NetTools.clientID = -1;
            NetTools.isClient = false;
            NetTools.isSingleplayer = false;
            clientID = -1;

            SteamFriends.SetRichPresence("connect", "");
            SteamFriends.SetRichPresence("steam_display", "");
            SteamFriends.SetRichPresence("steam_player_group", "");
            SteamFriends.SetRichPresence("steam_player_group_size", "");

        }


        public void SendPacket(Packet packet)
        {
            if (NetTools.isSingleplayer)
            {
                if (packet.packetSendType == Packet.sendType.proximity)
                {
                    if (NetServer.serverInstance.connections.Count > 0)
                    {
                        if (Vector3.Distance(packet.packetPosition.ToVec3(), NetServer.serverInstance.connections[0].proximityPosition) > NetServer.serverInstance.connections[0].loadProximity)
                        {
                            return;
                        }
                    }
                }
                //if(packet.packetType == Packet.pType.netVarEdit && ((NetworkFieldPacket)packet.GetPacketData()).immediateOnSelf)
                //{
                //    return; //basically would be a double sync. No reason to.
                //}
                UnityPacketHandler.instance.QueuePacket(packet);
                return;
            }

            if (packet.reliable)
            {
                lock (packetTCPSendQueue)
                {
                    packetTCPSendQueue.Add(packet);
                }
            }
            else
            {
                lock (packetUDPSendQueue)
                {
                    packetUDPSendQueue.Add(packet);
                }
            }
            //if (packet.reliable)
            //{
            //    SendTCPPacket(packet);
            //} else
            //{
            //    SendUDPPacket(packet);
            //}
        }

        public List<Packet> packetUDPSendQueue = new List<Packet>();
        public List<Packet> packetTCPSendQueue = new List<Packet>();

        void PacketSendThread(object reliable)
        {
            ref List<Packet> queue = ref packetTCPSendQueue;
            //Debug.Log(((bool)reliable).ToString() + " Send Thread has Begun.");
            if (!(bool)reliable)
            {
                queue = ref packetUDPSendQueue;
            }
            while (true)
            {

                if (queue.Count <= 0)
                {
                    continue;
                }
                //DateTime sendTime = DateTime.Now;
                try
                {
                    Packet current;
                    lock (queue)
                    {
                        current = queue[0];
                    }

                    if (current == null)
                    {
                        queue.RemoveAt(0);
                        continue;
                    }

                    if (current.reliable)
                    {
                        SendReliable(current);
                    }
                    else
                    {
                        SendUnreliable(current);
                        //Debug.Log("UDP Took: " + (DateTime.Now.Subtract(sendTime)) + "Bytes: " + current.packetData.Length);
                    }
                    lock (queue)
                    {
                        queue.Remove(current);
                    }
                }
                catch (System.Exception e)
                {
                    //Debug.LogError(queue[0].packetType);
                    Debug.LogError(e);
                    queue.RemoveAt(0);
                    Debug.Log(((bool)reliable).ToString() + " Connection Lost has ended.");
                    return;
                }

            }
        }


        void SendUnreliable(Packet packet)
        {
            socket.SendAll(sendType.unreliable,ENSSerialization.SerializePacket(packet));
        }

        void SendReliable(Packet packet)//, bool queuedPacket = false)
        {

           
                
            byte[] array = ENSSerialization.SerializePacket(packet);//Packet.SerializeObject(packet);
            socket.SendAll(sendType.reliable, array);
                
            
        }

        public Packet RecvReliable()
        {
            return ENSSerialization.DeserializePacket(socket.GetNextReliable().Item2);
        }
        public Packet RecvUnreliable()
        {
            return ENSSerialization.DeserializePacket(socket.GetNextUnreliable().Item2);
        }


        //public List<Packet> queuedSendingPackets = new List<Packet>();

        //public void SendingPacketHandler()
        //{
        //    while (NetClient.instanceClient != null)
        //    {
        //        //Debug.Log("SendingPacketHandler running");
        //        if (queuedSendingPackets.Count <= 0)
        //        {
        //            continue;
        //        }

        //        try
        //        {
        //            foreach (Packet packet in queuedSendingPackets.ToArray())
        //            {
        //                SendPacket(packet, true);
        //            }
        //            queuedSendingPackets = new List<Packet>();
        //        } catch
        //        {

        //        }
        //    }
        //    Debug.Log("Sending packet handler stopped... Possible problem?");
        //}

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
}