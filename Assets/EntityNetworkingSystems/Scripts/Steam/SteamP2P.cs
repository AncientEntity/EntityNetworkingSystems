using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Steamworks;
using Steamworks.Data;

namespace EntityNetworkingSystems.Steam
{
    [System.Serializable]
    public class SteamListener
    {

        public enum SteamSendTypes
        {
            reliable = 2,
            unreliable = 0,
        }

        
    
        public void Stop()
        {
            //if(udpServer == null)
            //{
            //    return;
            //}
            //udpServer.Close();
            //udpServer.Dispose();
            foreach(NetworkPlayer player in ServerHandler.serverInstance.connections.ToArray())
            {
                SteamNetworking.CloseP2PSessionWithUser(player.steamID);
            }
        }

        public void EndConnectionWithSteamID(ulong steamID)
        {
            SteamNetworking.CloseP2PSessionWithUser(steamID);
        }

        public void SendPacketDirect(Packet packet, SteamSendTypes sendType, ulong steamID)
        {
            byte[] bytePacket = ENSSerialization.SerializePacket(packet);
            Debug.Log("Server: "+((int)sendType + NetTools.serverChannelOffset));
            bool outcome = SteamNetworking.SendP2PPacket(steamID, ENSSerialization.SerializePacket(packet), bytePacket.Length, (int)sendType+5, (P2PSend)sendType);
        }

        public void SendPacket(Packet packet,SteamSendTypes sendType, bool ignoreSelf = false)
        {
            byte[] bytePacket = ENSSerialization.SerializePacket(packet);
            foreach (NetworkPlayer player in ServerHandler.serverInstance.connections.ToArray())
            {
                if (ignoreSelf && ServerHandler.serverInstance.myConnection == player)
                {
                    continue;
                }

                if (packet.sendToAll || packet.usersToRecieve.Contains(player.clientID))
                {
                    //SteamSendTypes gets converted to P2PSend because the enums have the same values as the P2PSend Values. ----------------
                    Debug.Log("Server: " +((int)sendType + NetTools.serverChannelOffset));
                    bool outcome = SteamNetworking.SendP2PPacket(player.steamID, bytePacket, bytePacket.Length, (int)sendType+5, (P2PSend)sendType);
                }
            }
        }



        public Packet Recieve(SteamSendTypes protocol)
        {
            while (true)
            {
                try
                {
                    P2Packet recieved = RecieveMessage(protocol);
                    Packet p = ENSSerialization.DeserializePacket(recieved.Data);
                    if(p.packetType == Packet.pType.unassigned)
                    {
                        continue;
                    }
                    if (ServerHandler.serverInstance.VerifyPacketValidity(recieved.SteamId,p))//(recieved.Key != null)
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

        P2Packet RecieveMessage(SteamSendTypes protocol)
        {
            bool done = false;
            while (!done)
            {
                while (!SteamNetworking.IsP2PPacketAvailable((int)protocol))
                {
                    continue;
                }
                //Debug.Log(serverEndpoint.ToString());
                P2Packet? packet = SteamNetworking.ReadP2PPacket((int)protocol);
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
    public class SteamPlayer
    {


        public SteamPlayer()
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

            SteamNetworking.OnP2PSessionRequest += (steamID) =>
            {
                if (NetClient.instanceClient.serversSteamID == steamID) {
                    
                        Debug.Log("CLIENT P2P Steam Connection Started: " + steamID);
                        SteamNetworking.AcceptP2PSessionWithUser(steamID);
                }
                
                Debug.Log("CLIENT P2P Steam Connection Failed: " + steamID);
            };

            SteamNetworking.OnP2PConnectionFailed += (steamID, failReason) =>
            {
                Debug.Log("CLIENT P2P Failed With: " + steamID + " for reason " + failReason);
                NetTools.isClient = false;
                NetTools.onFailedServerConnection.Invoke();
                NetClient.instanceClient.lastConnectionError = failReason.ToString();
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

        public void SendPacket(Packet packet ,SteamListener.SteamSendTypes sendType)
        {
            byte[] bytePacket = ENSSerialization.SerializePacket(packet);
            bool outcome = SteamNetworking.SendP2PPacket(NetClient.instanceClient.serversSteamID, bytePacket, bytePacket.Length, (int)sendType, (P2PSend)sendType);
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


        public Packet RecievePacket(SteamListener.SteamSendTypes protocol)
        {
            bool valid = false;
            while(!valid)
            {
                P2Packet packet = RecieveRaw(protocol);
                if(packet.SteamId == NetClient.instanceClient.serversSteamID)
                {
                    valid = true;
                    return ENSSerialization.DeserializePacket(packet.Data);
                }
            }
            return null;
        }

        P2Packet RecieveRaw(SteamListener.SteamSendTypes protocol)
        {
            bool done = false;
            while (!done)
            {
                Debug.Log("Client: " +((int)protocol + NetTools.serverChannelOffset));
                while (!SteamNetworking.IsP2PPacketAvailable((int)protocol+5))
                {
                    continue;
                }
                //Debug.Log(serverEndpoint.ToString());
                P2Packet? packet = SteamNetworking.ReadP2PPacket((int)protocol+5);
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
