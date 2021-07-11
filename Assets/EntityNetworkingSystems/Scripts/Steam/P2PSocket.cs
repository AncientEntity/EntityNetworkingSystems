using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;

namespace EntityNetworkingSystems.Steam
{
    public enum messageType : byte
    {
        data = 0, //Messages that have custom content and have to be handled.
        protocol = 1, //Heartbeats, timeout checking, etc.
    }
    public enum sendType : byte
    { 
        reliable = P2PSend.Reliable,
        unreliable = P2PSend.Unreliable,
    }
    public enum procotolType : byte
    {
        connectionStart = 0,
        connectionEnd = 1,
        heartbeat = 2,
    }




    public class P2PSocket
    {
        private List<Connection> activeConnections = new List<Connection>();

        private bool anyConnectionAllowed = false;
        private bool connectToIncoming = false; //Will connect back to any connections that are incoming and accepted, mainly for servers.
        private List<ulong> connectionsAllowedFrom = new List<ulong>();
        private List<ulong> blacklistedSteamIDs = new List<ulong>();

        public P2PSocket()
        {
            SteamNetworking.OnP2PSessionRequest += P2PRequest;
        }
        
        public void Connect(ulong steamid)
        {
            SendTo(steamid, sendType.reliable, Payload.Serialize(messageType.protocol, new byte[1] { (byte)procotolType.connectionStart }));
        }

        public void Disconnect(ulong steamid)
        {
            Connection conn = GetConnectionBySteamID(steamid);
            if (conn != null)
            {
                SendTo(steamid, sendType.reliable, Payload.Serialize(messageType.protocol, new byte[1] { (byte)procotolType.connectionEnd }));
                SteamNetworking.CloseP2PSessionWithUser(steamid);
                activeConnections.Remove(conn);
            }
        }

        public void SendAll(sendType send, byte[] info, messageType msgType = messageType.data)
        {
            byte[] serializedPayload = Payload.Serialize(new Payload(info, msgType));
            foreach (Connection c in activeConnections.ToArray())
            {
                SendRaw(c.steamid, send, serializedPayload, msgType);
            }
        }

        public void SendTo(ulong steamid,sendType send, byte[] info, messageType msgType = messageType.data)
        {
            byte[] serializedPayload = Payload.Serialize(new Payload(info,msgType));
            SteamNetworking.SendP2PPacket(steamid, serializedPayload, serializedPayload.Length, (int)send, (P2PSend)send);
        }

        private void SendRaw(ulong steamid, sendType send, byte[] info, messageType msgType = messageType.data)
        {
            SteamNetworking.SendP2PPacket(steamid, info, info.Length, (int)send, (P2PSend)send);
        }

        public void AllowConnectionFrom(ulong steamid, bool allowed=true)
        {
            if(allowed && !connectionsAllowedFrom.Contains(steamid))
            {
                connectionsAllowedFrom.Add(steamid);
            } else if (!allowed && connectionsAllowedFrom.Contains(steamid))
            {
                connectionsAllowedFrom.Remove(steamid);
            }
        }

        public void BlacklistConnectionFrom (ulong steamid, bool blacklisted=true)
        {
            if(blacklisted && !blacklistedSteamIDs.Contains(steamid)) {
                blacklistedSteamIDs.Add(steamid);
            } else if (!blacklisted && blacklistedSteamIDs.Contains(steamid))
            {
                blacklistedSteamIDs.Remove(steamid);
            }
        }
        
        public void AllowConnectionsAll(bool allowed)
        {
            anyConnectionAllowed = allowed;
        }

        public void P2PRequest(SteamId steamid)
        {
            if(blacklistedSteamIDs.Contains(steamid))
            {
                return;
            }

            if (anyConnectionAllowed || connectionsAllowedFrom.Contains(steamid))
            {
                SteamNetworking.AcceptP2PSessionWithUser(steamid);
                if(connectToIncoming)
                {
                    Connect(steamid);
                }
                activeConnections.Add(new Connection(steamid, GetTimeStamp()));
                Debug.Log("Connection Accepted: " + steamid);
            }
        }

        private uint GetTimeStamp()
        {
            TimeSpan t = (DateTime.UtcNow - new DateTime(1970, 1, 1));
            return (uint)t.TotalSeconds;
        }

        public bool SteamIdConnected(ulong steamid)
        {
            foreach(Connection conn in activeConnections)
            {
                if(conn.steamid == steamid)
                {
                    return true;
                }
            }
            return false;
        }

        public Connection GetConnectionBySteamID(ulong steamid)
        {
            foreach(Connection conn in activeConnections)
            {
                if(conn.steamid == steamid)
                {
                    return conn;
                }
            }
            return null;
        }
    }

    public class Payload
    {
        public messageType type;
        public byte[] info;

        public Payload(byte[] info, messageType type)
        {
            this.info = info;
            this.type = type;
        }

        public static byte[] Serialize(Payload payload)
        {
            return Serialize(payload.type, payload.info);
        }
        
        public static byte[] Serialize(messageType msgType, byte[] info)
        {
            List<byte> infoSerialized = new List<byte>(); //+1 for sending the messageType as a byte.
            infoSerialized.Add((byte)msgType);
            infoSerialized.AddRange(info);
            return infoSerialized.ToArray();
        }

        public static Payload DeSerialize(byte[] payloadSerialized)
        {
            byte[] destinationArray = new byte[payloadSerialized.Length - 1];
            System.Array.Copy(payloadSerialized, 1, destinationArray, 0, payloadSerialized.Length - 1);
            return new Payload(destinationArray,(messageType)payloadSerialized[0]);
        }

    }

    public class Connection
    {
        public ulong steamid;
        public uint timeSinceLastMessage;

        public Connection(ulong steamid, uint timeSinceLastMessage)
        {
            this.steamid = steamid;
            this.timeSinceLastMessage = timeSinceLastMessage;
        }
    }

}
