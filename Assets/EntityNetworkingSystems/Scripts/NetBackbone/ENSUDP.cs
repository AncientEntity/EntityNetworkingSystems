using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

namespace EntityNetworkingSystems.UDP
{
    [System.Serializable]
    public class UDPListener
    {
        public int portToUse = 44595;
        
        //public UdpClient udpServer = null;

        

        public UDPListener(NetServer netServer)
        {
            portToUse = NetServer.serverInstance.hostPort + 1;
        }

        public void Start()
        {
            //if(udpServer != null)
            //{
            //    udpServer.Dispose();
            //    udpServer = null;
            //}

            //udpServer = new UdpClient(portToUse);

            ////This has something to do with UDP recieving a ICMP message and 'resetting' the connection somehow?
            ////So yeah SIO_UDP_CONNRESET = -1744830452
            //udpServer.Client.IOControl((IOControlCode)(-1744830452),    new byte[] { 0, 0, 0, 0 }, null);

            SteamNetworking.OnP2PSessionRequest = (steamID) =>
            {
                foreach (NetworkPlayer player in NetServer.serverInstance.connections)
                {
                    if (player.steamID == steamID)
                    {
                        Debug.Log("SERVER P2P Steam Connection Started: " + player.steamID);
                        SteamNetworking.AcceptP2PSessionWithUser(steamID);
                        break;
                    }
                }
                Debug.Log("SERVER P2P Steam Connection Failed: " + steamID);
            };

            SteamNetworking.OnP2PConnectionFailed = (steamID, failReason) =>
            {
                Debug.Log("SRV P2P Failed With: " + steamID + " for reason " + failReason);
                NetServer.serverInstance.KickPlayer(NetServer.serverInstance.GetPlayerBySteamID(steamID),"Server failed to make Steam P2P unreliable connection, reason: "+failReason);
            };
        }

        public void Stop()
        {
            //if(udpServer == null)
            //{
            //    return;
            //}
            //udpServer.Close();
            //udpServer.Dispose();
            foreach(NetworkPlayer player in NetServer.serverInstance.connections.ToArray())
            {
                SteamNetworking.CloseP2PSessionWithUser(player.steamID);
            }
        }

        public void SendPacket(Packet packet, bool ignoreSelf=false)
        {
            byte[] bytePacket = ENSSerialization.SerializePacket(packet);
            foreach (NetworkPlayer player in NetServer.serverInstance.connections.ToArray())
            {
                if(ignoreSelf && NetServer.serverInstance.myConnection == player)
                {
                    continue;
                }

                if(packet.sendToAll || packet.usersToRecieve.Contains(player.clientID))
                {
                    bool outcome = SteamNetworking.SendP2PPacket(player.steamID, bytePacket, bytePacket.Length,1, sendType: P2PSend.Unreliable);
                    //Debug.Log(outcome);
                    //udpServer.Send(bytePacket, bytePacket.Length, player.udpEndpoint);
                }
            }
        }



        public Packet Recieve()
        {
            while (true)
            {
                try
                {
                    P2Packet recieved = RecieveMessage();
                    Packet p = ENSSerialization.DeserializePacket(recieved.Data);
                    if (NetServer.serverInstance.VerifyPacketValidity(recieved.SteamId,p))//(recieved.Key != null)
                    {
                        return p;
                    }
                    Debug.Log("Couldn't verify packet's validity");
                } catch (System.Exception e)
                {
                    if(e.ToString().Contains("NullReferenceException") || e.ToString().Contains("ThreadAbortException"))
                    {
                        return null; //Most likely the editor closed abruptly or server closed.
                    }
                    Debug.LogError(e);
                }
            }
        }

        P2Packet RecieveMessage()
        {
            bool done = false;
            while (!done)
            {
                while (!SteamNetworking.IsP2PPacketAvailable(1))
                {
                    continue;
                }
                //Debug.Log(serverEndpoint.ToString());
                P2Packet? packet = SteamNetworking.ReadP2PPacket(1);
                if (packet.HasValue)
                {
                    done = true;
                    return packet.Value;
                }
            }
            return new P2Packet();
        }

        

    }

    //Would call it UDPClient but C# has a UDPClient ;-;
    [System.Serializable]
    public class UDPPlayer
    {
        public int portToUse = 44595;
        //public UdpClient client;


        //private IPEndPoint serverEndpoint;

        public UDPPlayer()
        {
            //portToUse = serverEndpoint.Port + 1;

            //this.serverEndpoint = new IPEndPoint(serverEndpoint.Address, portToUse);
            //bool validPort = false;
            //while (!validPort)
            //{
            //    try
            //    {
            //        client = new UdpClient(portToUse);
            //        validPort = true;
            //    }
            //    catch
            //    {
            //        validPort = false;
            //        portToUse += 1;
            //    }
            //}

            SteamNetworking.OnP2PSessionRequest = (steamID) =>
            {
                if (NetClient.instanceClient.serversSteamID == steamID) {
                    
                        Debug.Log("CLIENT P2P Steam Connection Started: " + steamID);
                        SteamNetworking.AcceptP2PSessionWithUser(steamID);
                }
                
                Debug.Log("CLIENT P2P Steam Connection Failed: " + steamID);
            };

            SteamNetworking.OnP2PConnectionFailed = (steamID, failReason) =>
            {
                Debug.Log("CLIENT P2P Failed With: " + steamID + " for reason " + failReason);
            };

        }


        public void Stop()
        {
            SteamNetworking.CloseP2PSessionWithUser(NetClient.instanceClient.serversSteamID);
            //if(client == null)
            //{
            //    return;
            //}
            //client.Dispose();
        }

        public void SendPacket(Packet packet)
        {
            byte[] bytePacket = ENSSerialization.SerializePacket(packet);
            bool outcome = SteamNetworking.SendP2PPacket(NetClient.instanceClient.serversSteamID, bytePacket, bytePacket.Length,1, sendType: P2PSend.Unreliable);
            //Debug.Log(outcome);
            //client.Send(bytePacket, bytePacket.Length, serverEndpoint);
#if UNITY_EDITOR
            if (NetClient.instanceClient.trackOverhead)
            {
                if (NetClient.instanceClient.overheadFilter == Packet.pType.unassigned || NetClient.instanceClient.overheadFilter == packet.packetType)
                {
                    NetClient.instanceClient.packetsSent++;
                    //Debug.Log("Sent Packet: " + packet.packetSendType + ". To: "+serverEndpoint.ToString());
                }
            }
#endif
        }


        public Packet RecievePacket()
        {
            bool valid = false;
            while(!valid)
            {
                P2Packet packet = RecieveRaw();
                if(packet.SteamId == NetClient.instanceClient.serversSteamID)
                {
                    valid = true;
                    return ENSSerialization.DeserializePacket(packet.Data);
                }
            }
            return null;
        }

        P2Packet RecieveRaw()
        {
            bool done = false;
            while (!done)
            {
                while (!SteamNetworking.IsP2PPacketAvailable(1))
                {
                    continue;
                }
                //Debug.Log(serverEndpoint.ToString());
                P2Packet? packet = SteamNetworking.ReadP2PPacket(1);
                if (packet.HasValue)
                {
                    done = true;
                    return packet.Value;
                }
            }
            return new P2Packet();
        }



    }
}
