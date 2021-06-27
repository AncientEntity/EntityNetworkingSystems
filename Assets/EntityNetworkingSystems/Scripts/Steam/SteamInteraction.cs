using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;

namespace EntityNetworkingSystems.Steam
{

    public class SteamInteraction : MonoBehaviour
    {
        public static SteamInteraction instance = null;
        public SocketManager steamworksServerManager = null;
        public SteamSocketManager steamSocket = null;

        public bool clientStarted = false;

        public bool initialized = false;

        public AuthTicket clientAuth = null;

        public List<ulong> connectedSteamIDs = new List<ulong>();

        public ulong ourSteamID = 0;
        public string steamName = "";

        bool doCallbacks = false;
        bool serverRunning = false;


        void FixedUpdate()
        {
            if (doCallbacks)
            {
                Steamworks.SteamClient.RunCallbacks();
            }
        }

        public void Initialize()
        {
            if (instance == null)
            {
                instance = this;
            }

            if ((ServerHandler.serverInstance != null && ServerHandler.serverInstance.steamAppID != -1) || (NetClient.instanceClient != null && NetClient.instanceClient.steamAppID != -1))
            {
                try
                {
                    //Debug.Log("Steamworks Initialized");
                    if (ServerHandler.serverInstance != null)
                    {
                        //Debug.Log((uint)NetServer.serverInstance.steamAppID);
                        SteamClient.Init((uint)ServerHandler.serverInstance.steamAppID, false);
                    }
                    else
                    {
                        //Debug.Log((uint)NetClient.instanceClient.steamAppID);
                        SteamClient.Init((uint)NetClient.instanceClient.steamAppID, false);
                    }
                    doCallbacks = true;
                }
                catch (Exception e)
                {
                    //Debug.Log((uint)NetServer.serverInstance.steamAppID);
                    Debug.LogError(e);
                }
            }
            //SteamFriends.OnGameLobbyJoinRequested += SteamFriends_OnGameLobbyJoinRequested;
            if (ourSteamID == 0)
            {
                //Debug.Log(SteamClient.SteamId.Value);
                ourSteamID = SteamClient.SteamId.Value;
            }
            if(steamName == "")
            {
                steamName = SteamClient.Name;
            }

            SteamServer.OnValidateAuthTicketResponse += (steamid, ownerid, response) =>
            {

                if (response == AuthResponse.OK)
                {
                    Debug.Log(steamid + " ticket is still valid");
                }
                else
                {
                    Debug.Log(steamid + " ticket is no longer valid");
                    //Add kick user stuff.
                }
            };


            initialized = true;
            Debug.Log("Steam Interaction Initialized");
        }

        //private void SteamFriends_OnGameLobbyJoinRequested(Lobby arg1, SteamId arg2)
        //{
        //    arg1.GetGameServer();
        //}

        public void StartClient()
        {
            if(clientAuth != null)
            {
                clientAuth.Cancel();
                clientAuth = null;
            }
            clientAuth = SteamUser.GetAuthSessionTicket();
            clientStarted = true;
        }

        public void StopClient()
        {
            Debug.Log("StopClient() ran");
            if (clientAuth != null)
            {
                clientAuth.Cancel();
                //clientAuth.Dispose();
                clientAuth = null;
            }
            //if ((NetTools.isServer && NetServer.serverInstance.steamAppID != -1) || (NetTools.isClient && NetClient.instanceClient.steamAppID != -1))
            //{
            //    Steamworks.SteamClient.Shutdown();
            //}
            clientStarted = false;
        }

        public void StartServer()
        {
            if (!initialized)
            {
                Debug.LogError("Error run Initialize() before running StartServer().");
                return;
            }
            


            SteamServerInit serverInitData = new SteamServerInit(ServerHandler.serverInstance.modDir, ServerHandler.serverInstance.gameDesc) { };
            serverInitData.DedicatedServer = false;
            serverInitData.GamePort = (ushort)0;
            SteamServer.Init(ServerHandler.serverInstance.steamAppID, serverInitData);
            SteamServer.ServerName = SteamClient.Name + "'s Server.";
            SteamServer.MapName = ServerHandler.serverInstance.mapName;
            SteamServer.MaxPlayers = ServerHandler.serverInstance.maxConnections;

            SteamServer.AutomaticHeartbeats = true;

            SteamServer.LogOnAnonymous();
            doCallbacks = true;

            SteamNetworking.OnP2PSessionRequest += (steamID) =>
            {
                Debug.Log("TEST");
                if (NetTools.IsMultiplayerGame())
                {
                    Debug.Log("SERVER P2P Steam Connection Started: " + steamID);
                    ServerHandler.serverInstance.AcceptNewClient(steamID);
                    
                    SteamNetworking.AcceptP2PSessionWithUser(steamID);
                    return;
                }

                Debug.Log("SERVER P2P Steam Connection Failed: " + steamID);
            };

            SteamNetworking.OnP2PConnectionFailed += (steamID, failReason) =>
            {
                Debug.Log("SRV P2P Failed With: " + steamID + " for reason " + failReason);
                ServerHandler.serverInstance.KickPlayer(ServerHandler.serverInstance.GetPlayerBySteamID(steamID), "Server failed to make Steam P2P unreliable connection, reason: " + failReason);
            };


            serverRunning = true;



            //steamSocket = new SteamSocketManager();
            //steamworksServerManager = SteamNetworkingSockets.CreateNormalSocket(Steamworks.Data.NetAddress.AnyIp((ushort)NetServer.serverInstance.hostPort), steamSocket);
        }

        public void ShutdownServer()
        {
            //foreach(Connection conn in steamworksServerManager.Connected)
            //{
            //    conn.Close();
            //}
            //steamworksServerManager.Close();
            //steamworksServerManager = null;
            //steamSocket = null;
            doCallbacks = false;

            foreach (ulong steamID in connectedSteamIDs)
            {
                SteamUser.EndAuthSession(steamID);
                SteamServer.EndSession(steamID);
            }
            connectedSteamIDs = new List<ulong>();

            SteamServer.Shutdown();

            if (NetTools.ENSManagingSteam())
            {
                //SteamServer.LogOff();

                if ((NetTools.isServer && ServerHandler.serverInstance.steamAppID != -1) || (NetTools.isClient && NetClient.instanceClient.steamAppID != -1))
                {
                    //SteamServer.Shutdown();
                    //SteamClient.Shutdown();
                }
            }
            serverRunning = false;
        }


        public bool IsRunning() {
            return serverRunning;
        }


        void OnDisable()
        {
            if (serverRunning)
            {
                ShutdownServer();
            }
            if (initialized)
            {
                if ((ServerHandler.serverInstance != null && ServerHandler.serverInstance.steamAppID != -1 || NetClient.instanceClient != null && NetClient.instanceClient.steamAppID != -1))
                {
                    //SteamClient.Shutdown();
                    //SteamServer.Shutdown();
                }
            }
            if (clientAuth != null)
            {
                //StopClient();
            }
        }

    }

    public class SteamSocketManager : ISocketManager
    {
        public void OnConnecting(Connection connection, ConnectionInfo data)
        {
            connection.Accept();
            Debug.Log($"{data.Identity} is connecting");
        }

        public void OnConnected(Connection connection, ConnectionInfo data)
        {
            Debug.Log($"{data.Identity} has joined the game");
        }

        public void OnDisconnected(Connection connection, ConnectionInfo data)
        {
            Debug.Log($"{data.Identity} is out of here");
        }

        public void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
            Debug.Log($"We got a message from {identity}!");

            // Send it right back
            connection.SendMessage(data, size, SendType.Reliable);
        }




    }
}