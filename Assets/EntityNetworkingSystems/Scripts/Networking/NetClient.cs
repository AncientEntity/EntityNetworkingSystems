using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Steamworks;
using System.Runtime.InteropServices;
using System.Threading;
using Steamworks.Data;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class NetClient : IConnectionManager
    {
        public static NetClient instanceClient;

        public bool connectedToServer = false;
        [Space]
        public int clientID = -1;

        public int steamAppID = -1; //If -1 it wont initialize and you'll need to do it somewhere else :)
        public ulong serversSteamID = 0; //The server's steam ID.
        public string password;

        private ConnectionManager connectionManager;
        private Thread connectionRecieveThread;

#region IConnectionManager


        public void OnConnecting(ConnectionInfo info)
        {
            
        }

        public void OnConnected(ConnectionInfo info)
        {
            SetupNewConnection();
        }

        public void OnDisconnected(ConnectionInfo info)
        {
            DisconnectFromServer();
        }

        public void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            byte[] rawPacket = new byte[size];
            Marshal.Copy(data,rawPacket,0,size);
            Packet packet = ENSSerialization.DeserializePacket(rawPacket); //Packet.DeJsonifyPacket(Encoding.ASCII.GetString(byteMessage));//(Packet)Packet.DeserializeObject(byteMessage);
            
            UnityPacketHandler.instance.QueuePacket(packet);
        }


#endregion
        
        
        public void Initialize()
        {

            instanceClient = this;


            //if (client != null)
            //{
            //    //Debug.LogError("Trying to initial NetClient when it has already been initialized.");
            //    return;
            //}

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

            NetworkPlayer player = new NetworkPlayer();

            player.steamID = SteamInteraction.instance.ourSteamID;
            
            NetServer.serverInstance.networkPlayers.Add(player);

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

        public void ConnectToServer(ulong steamID64, ushort port = 432)
        {
            if (NetClient.instanceClient == null)
            {
                Debug.Log("[NetClient] Must Initialize() before connecting.");
                return;
            }

            connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(steamID64, port);
            connectionManager.Interface = this;

            connectionRecieveThread = new Thread(UpdateRecieve);
            connectionRecieveThread.Start();
            
            PostConnectStart();
        }
        
        private bool SetupNewConnection()//(string ip = "127.0.0.1", int port = 24424)
        {
            //if(client != null)
            //{
            //    client.Dispose();
            //}

            //client.NoDelay = true;
            NetTools.isClient = true;
            

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
        
           
            //todo will need to reimplement buildID/password authing (nothing handled in NetServer)
            Packet authPacket = new Packet(Packet.pType.networkAuth, Packet.sendType.nonbuffered, ENSSerialization.SerializeAuthPacket(new NetworkAuthPacket(steamAuthData, usedSteamID, 0, password,buildID)));
            authPacket.sendToAll = false;
            authPacket.reliable = true;
            SendPacket(authPacket);

            
            connectedToServer = true;

            SteamFriends.SetRichPresence("steam_display", "Multiplayer");
            //SteamFriends.SetRichPresence("steam_player_group", "Survival");
            //SteamFriends.SetRichPresence("steam_player_group_size", PlayerController.allPlayers.Count.ToString()); //Also gets updated in playercontroller.start

            
            //NetTools.onJoinServer.Invoke();
            
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
        
        
        private void UpdateRecieve()
        {
            while (connectionManager != null)
            {
                connectionManager.Receive();
            }
            Debug.Log("[NetServer] ConnectionManager Receive Thread has ended.");
        }

        public void DisconnectFromServer()
        {
            if (!NetTools.isSingleplayer)
            {
                connectionManager.Close();
                
                SteamInteraction.instance.StopClient();
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
            
            NetTools.onLeaveServer.Invoke("disconnected");
        }


        public void SendPacket(Packet packet)
        {
            if (NetTools.isSingleplayer || NetTools.isServer)
            {
                if (packet.packetSendType == Packet.sendType.proximity)
                {
                    if (NetServer.serverInstance.networkPlayers.Count > 0)
                    {
                        if (Vector3.Distance(packet.packetPosition.ToVec3(), NetServer.serverInstance.networkPlayers[0].proximityPosition) > NetServer.serverInstance.networkPlayers[0].loadProximity)
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

                        
            byte[] serializedPacket = ENSSerialization.SerializePacket(packet);
            connectionManager.Connection.SendMessage(serializedPacket,packet.reliable ? SendType.Reliable : SendType.Unreliable);
        }
        


    }
}