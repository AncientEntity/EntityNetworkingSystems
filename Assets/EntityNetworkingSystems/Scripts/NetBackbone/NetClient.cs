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

namespace EntityNetworkingSystems
{

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
#if UNITY_EDITOR
        [Space]
        public bool trackOverhead = false;
        public Packet.pType overheadFilter = Packet.pType.unassigned;
        public string packetByteLength = "";
#endif

        public void Initialize()
        {

            if (instanceClient == null)
            {
                instanceClient = this;
            }

            if (client != null)
            {
                //Debug.LogError("Trying to initial NetClient when it has already been initialized.");
                return;
            }

            client = new TcpClient();
            NetTools.isClient = true;

            if (useSteamworks)
            {
                if (SteamInteraction.instance == null)
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

                //if(packetSendHandler == null || packetSendHandler.IsAlive == false)
                //{
                //    packetSendHandler = new Thread(new ThreadStart(SendingPacketHandler));
                //    packetSendHandler.Start();
                //}
            }
            Debug.Log("NetClient.ConnectionHandler() thread has successfully finished.");
        }


        public void ConnectToSingleplayer()
        {
            NetTools.isClient = true;
            NetTools.isSingleplayer = true;

            connectedToServer = true;

            NetworkPlayer player = new NetworkPlayer(null);
            player.clientID = 0;

            if (useSteamworks)
            {
                player.steamID = SteamInteraction.instance.ourSteamID;
            }

            NetServer.serverInstance.connections.Add(player);

            //PlayerLoginData pLD = new PlayerLoginData();
            //pLD.playerNetworkID = 0;
            Packet loginPacket = new Packet(Packet.pType.loginInfo, Packet.sendType.nonbuffered, System.BitConverter.GetBytes((short)0));
            loginPacket.packetOwnerID = -1;
            loginPacket.sendToAll = false;
            NetServer.serverInstance.SendPacket(player, loginPacket);

            //NetTools.onJoinServer.Invoke();
            UnityPacketHandler.instance.StartHandler();
        }

        public void ConnectToServer(string ip = "127.0.0.1", int port = 44594)
        {
            if(client == null)
            {
                client = new TcpClient();
                NetTools.isClient = true;
            }

            Debug.Log("Attempting Connection");
            try
            {
                client.Connect(ip, port);
            } catch (System.Exception e)
            {
                Debug.LogError(e);
                //Couldn't connect to server.
                Debug.LogError("Couldn't connect to server: " + ip + ":" + port);
                NetTools.isClient = false;
                client.Dispose();
                NetTools.onFailedServerConnection.Invoke();
                return;
            }
            Debug.Log("Connection Accepted");
            netStream = client.GetStream();
            UnityPacketHandler.instance.StartHandler();

            //NetTools.isSingleplayer = false;

            if (useSteamworks && !SteamInteraction.instance.initialized)
            {
                SteamInteraction.instance.StartClient();

                SteamInteraction.instance.clientAuth = SteamUser.GetAuthSessionTicket();
                //SteamInteraction.instance.clientAuth = ticket;

                //Debug.Log(SteamClient.SteamId.Value + " "+ SteamClient.SteamId.AccountId);
                //SteamUser.BeginAuthSession(ticket.Data, SteamClient.SteamId.Value);

                Packet authPacket = new Packet(Packet.pType.steamAuth, Packet.sendType.nonbuffered, new SteamAuthPacket(SteamInteraction.instance.clientAuth.Data, SteamClient.SteamId.Value));
                authPacket.sendToAll = false;
                SendPacket(authPacket);
            }
            //NetTools.onJoinServer.Invoke();
            connectedToServer = true;
            //packetSendHandler = new Thread(new ThreadStart(SendingPacketHandler));
            //packetSendHandler.Start();
            connectionHandler = new Thread(new ThreadStart(ConnectionHandler));
            connectionHandler.Start();
        }

        public void DisconnectFromServer()
        {
            if (client != null && !NetTools.isSingleplayer)
            {
                Debug.Log("Disconnecting From Server");
                NetTools.onLeaveServer.Invoke("disconnect");
                client.GetStream().Close();
                client.Close();
                client = null;
                connectionHandler.Abort();
                //if (useSteamworks)
                //{
                //    SteamInteraction.instance.StopClient();
                //}
            }
            connectedToServer = false;
            //NetClient.instanceClient = null;
            NetTools.clientID = -1;
            NetTools.isClient = false;
            NetTools.isSingleplayer = false;
            clientID = -1;
            
        }

        public void SendPacket(Packet packet)//, bool queuedPacket = false)
        {

            //Debug.Log(NetTools.isSingleplayer);
            //if(!queuedPacket)
            //{
            //    queuedSendingPackets.Add(packet);
            //    return;
            //}

            //if (netStream == null)
            //{
            //    NetTools.isSingleplayer = true;
            //}

            if (NetTools.isSingleplayer)
            {
                if(packet.packetSendType == Packet.sendType.proximity)
                {
                    if(NetServer.serverInstance.connections.Count > 0)
                    {
                        if(Vector3.Distance(packet.packetPosition.ToVec3(), NetServer.serverInstance.connections[0].proximityPosition) > NetServer.serverInstance.connections[0].loadProximity)
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

            

            lock (netStream){
                lock (client)
                {
                    byte[] array = ENSSerialization.SerializePacket(packet);//Packet.SerializeObject(packet);
#if UNITY_EDITOR
                    if (trackOverhead)
                    {
                        if (overheadFilter == Packet.pType.unassigned || overheadFilter == packet.packetType)
                        {
                            packetByteLength = packetByteLength + array.Length + ",";
                            Debug.Log("JustData: " + packet.packetData.Length + ", All: " + array.Length);
                        }
                    }
#endif
                    //First send packet size
                    byte[] arraySize = new byte[4];
                    arraySize = System.BitConverter.GetBytes(array.Length);
                    client.SendBufferSize = 4;
                    try
                    {
                        netStream.Write(arraySize, 0, arraySize.Length);
                    } catch (Exception e)
                    {
                        if(e.ToString().Contains("SocketException"))
                        {
                            NetTools.onLeaveServer.Invoke(e.ToString());
                        }
                    }


                    //Send packet
                    client.SendBufferSize = array.Length;
                    netStream.Write(array, 0, array.Length);
                }
            }
        }

        public Packet RecvPacket()
        {
            //First get packet size
            //byte[] packetSize = new byte[4];
            byte[] packetSize = RecieveSizeSpecificData(4, netStream);
            //netStream.Read(packetSize, 0, packetSize.Length);
            //Debug.Log(Encoding.ASCII.GetString(packetSize));
            int pSize = System.BitConverter.ToInt32(packetSize, 0);
            //Debug.Log(pSize);

            //Get packet
            byte[] byteMessage = new byte[pSize];
            byteMessage = RecieveSizeSpecificData(pSize, netStream);


#if UNITY_EDITOR
            Packet finalPacket = ENSSerialization.DeserializePacket(byteMessage);

            if (trackOverhead)
            {
                if(overheadFilter == Packet.pType.unassigned || overheadFilter == finalPacket.packetType)
                {
                    packetByteLength = packetByteLength + pSize + ",";
                    Debug.Log("JustData: " + finalPacket.packetData.Length + ", All: " + byteMessage.Length);
                }

            }
            return finalPacket;
#else
            return ENSSerialization.DeserializePacket(byteMessage);//Packet.DeJsonifyPacket(Encoding.ASCII.GetString(byteMessage));//(Packet)Packet.DeserializeObject(byteMessage);
#endif
        }


        byte[] RecieveSizeSpecificData(int byteCountToGet, NetworkStream netStream)
        {
            //byteCountToGet--;
            client.ReceiveBufferSize = byteCountToGet;

            byte[] bytesRecieved = new byte[byteCountToGet];

            int messageRead = 0;
            while (messageRead < bytesRecieved.Length)
            {
                int bytesRead = netStream.Read(bytesRecieved, messageRead, bytesRecieved.Length - messageRead);
                messageRead += bytesRead;
            }
            return bytesRecieved;
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