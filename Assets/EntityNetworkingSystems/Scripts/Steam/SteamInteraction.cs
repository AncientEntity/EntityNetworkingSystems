using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;
using System.Threading.Tasks;

namespace EntityNetworkingSystems
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

        public bool launchSteamGameServer = false;

        public ulong ourSteamID = 0;
        public string steamName = "";

        bool doCallbacks = false;
        bool serverRunning = false;

        private Lobby currentLobby;
        
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

            if ((NetServer.serverInstance != null && NetServer.serverInstance.steamAppID != -1) || (NetClient.instanceClient != null && NetClient.instanceClient.steamAppID != -1))
            {
                try
                {
                    if (NetServer.serverInstance != null)
                    {
                        SteamClient.Init((uint)NetServer.serverInstance.steamAppID, false);
                    }
                    else
                    {
                        SteamClient.Init((uint)NetClient.instanceClient.steamAppID, false);
                    }
                    doCallbacks = true;
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
            if (ourSteamID == 0)
            {
                ourSteamID = SteamClient.SteamId.Value;
            }
            if(steamName == "")
            {
                steamName = SteamClient.Name;
            }

            initialized = true;
            Debug.Log("Steam Interaction Initialized");
        }

        public void StartClient()
        {
            GenerateNewSteamAuth();
            clientStarted = true;
        }

        public void GenerateNewSteamAuth()
        {
            if(clientAuth != null)
            {
                clientAuth.Cancel();
                clientAuth = null;
            }
            clientAuth = SteamUser.GetAuthSessionTicket();
        }

        public void StopClient()
        {
            Debug.Log("StopClient() ran");
            if (clientAuth != null)
            {
                clientAuth.Cancel();
                clientAuth = null;
            }
            clientStarted = false;
        }

        public void StartServer()
        {
            if (!initialized)
            {
                Debug.LogError("Error run Initialize() before running StartServer().");
                return;
            }


            if (launchSteamGameServer)
            {
                
                SteamServerInit serverInitData =
                    new SteamServerInit(NetServer.serverInstance.modDir, "" + SteamClient.SteamId.Value) { };
                serverInitData.DedicatedServer = false;
                serverInitData.GamePort = 24424;
                SteamServer.Init(NetServer.serverInstance.steamAppID, serverInitData);
                SteamServer.ServerName = SteamClient.Name + "'s Server.";
                SteamServer.MapName = NetServer.serverInstance.mapName;
                SteamServer.MaxPlayers = NetServer.serverInstance.maxConnections;

                SteamServer.AutomaticHeartbeats = true;

                SteamServer.LogOnAnonymous();
            }

            doCallbacks = true;

            serverRunning = true;

            CreateLobby();

            if (launchSteamGameServer)
            {
                currentLobby.SetGameServer(SteamClient.SteamId);
            }
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
            if (launchSteamGameServer)
            {
                foreach (ulong steamID in connectedSteamIDs)
                {
                    SteamUser.EndAuthSession(steamID);
                    SteamServer.EndSession(steamID);
                }
            }

            connectedSteamIDs = new List<ulong>();

            SteamServer.Shutdown();
            LeaveLobby();
            
            serverRunning = false;
        }

        public async void CreateLobby()
        {
            Lobby? newLobby = await SteamMatchmaking.CreateLobbyAsync(NetServer.serverInstance.maxConnections);
            if (newLobby.HasValue)
            {
                currentLobby = newLobby.Value;
                currentLobby.Join();
                currentLobby.SetData(LobbyDefinitions.LOBBY_HOSTID, ""+SteamClient.SteamId.Value);
                currentLobby.SetData(LobbyDefinitions.LOBBY_NAME, ""+SteamClient.Name+"'s Lobby");
                currentLobby.SetJoinable(true);
                currentLobby.SetPublic();
                Debug.Log("[SteamInteraction] Successfully created lobby("+currentLobby.Id.Value+").");
            }
            else
            {
                Debug.Log("[SteamInteraction] Failed to create lobby.");
            }
        }

        public void LeaveLobby()
        {
            if (currentLobby.Id.IsValid)
            {
                currentLobby.Leave();
            }
        }



        void OnDisable()
        {
            if (serverRunning)
            {
                ShutdownServer();
            }
            if (initialized)
            {
                if ((NetServer.serverInstance != null && NetServer.serverInstance.steamAppID != -1 || NetClient.instanceClient != null && NetClient.instanceClient.steamAppID != -1))
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