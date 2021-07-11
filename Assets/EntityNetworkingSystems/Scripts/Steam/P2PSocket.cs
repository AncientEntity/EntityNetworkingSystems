﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;
using System.Threading;
using Steamworks.Data;

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
        private bool active = false;

        private List<Connection> activeConnections = new List<Connection>();
        private List<Tuple<ulong, byte[]>> queuedReliable = new List<Tuple<ulong, byte[]>>(); //ulong = steamid who sent it
        private List<Tuple<ulong, byte[]>> queuedUnreliable = new List<Tuple<ulong, byte[]>>(); //ulong = steamid who sent it


        private bool anyConnectionAllowed = false;
        private bool connectToIncoming = false; //Will connect back to any connections that are incoming and accepted, mainly for servers.
        private List<ulong> connectionsAllowedFrom = new List<ulong>();
        private List<ulong> blacklistedSteamIDs = new List<ulong>();

        private Thread reliableIncomingHandler;
        private Thread unreliableIncomingHandler;
        private Thread heartbeatCheck;

        private float timeoutDelay = 15f;

        public P2PSocket()
        {
            SteamNetworking.OnP2PSessionRequest += P2PRequest;
            reliableIncomingHandler = new Thread(() => IncomingPacketManager(sendType.reliable));
            reliableIncomingHandler.Name = "P2PSocketReliable";
            reliableIncomingHandler.Start();
            unreliableIncomingHandler = new Thread(() => IncomingPacketManager(sendType.unreliable));
            unreliableIncomingHandler.Name = "P2PSocketUnreliable";
            unreliableIncomingHandler.Start();
            heartbeatCheck = new Thread(new ThreadStart(HeartbeatChecker));
            heartbeatCheck.Name = "P2PSocketHeartbeat";
            heartbeatCheck.Start();

            active = true;
        }


        private void HeartbeatChecker()
        {
            while(active)
            {

                foreach(Connection conn in activeConnections.ToArray())
                {
                    if(conn.timeSinceLastMessage >= timeoutDelay)
                    {
                        Disconnect(conn.steamid);
                        Debug.Log(conn.steamid + " has timed out.");
                    }
                }

                Thread.Sleep(5000);
            }
        }

        private void HandleIncomingPacket(P2Packet? packet, ref List<Tuple<ulong, byte[]>> queue)
        {
            GetConnectionBySteamID(packet.Value.SteamId).UpdateTimeAtLastMessage(GetTimeStamp());

            Payload payload = Payload.DeSerialize(packet.Value.Data);
            if (payload.type == messageType.data)
            {
                queue.Add(new Tuple<ulong, byte[]>(packet.Value.SteamId, packet.Value.Data));
                return;
            } else if (payload.type == messageType.protocol)
            {
                Debug.Log("PROT");
                if(payload.info[0] == (byte)procotolType.connectionEnd)
                {
                    SteamNetworking.CloseP2PSessionWithUser(packet.Value.SteamId);
                    Disconnect(packet.Value.SteamId,false);
                }
            }
        }

        private void IncomingPacketManager(sendType sType)
        {
            int channel = (int)sType;
            ref List<Tuple<ulong, byte[]>> queue = ref queuedReliable;
            if(sType == sendType.unreliable)
            {
                queue = ref queuedUnreliable;
            }
            while (active)
            {
                if(SteamNetworking.IsP2PPacketAvailable(channel))
                {
                    P2Packet? packet = SteamNetworking.ReadP2PPacket(channel);
                    if(packet.HasValue)
                    {
                        HandleIncomingPacket(packet, ref queue);
                    }
                }
            }
        }

        public void Dispose()
        {
            active = false;
        }
        
        public void Connect(ulong steamid)
        {
            SendTo(steamid, sendType.reliable, Payload.Serialize(messageType.protocol, new byte[1] { (byte)procotolType.connectionStart }));
        }

        public void Disconnect(ulong steamid, bool letOtherKnow=true)
        {
            Connection conn = GetConnectionBySteamID(steamid);
            if (conn != null)
            {
                if (letOtherKnow)
                {
                    SendTo(steamid, sendType.reliable, Payload.Serialize(messageType.protocol, new byte[1] { (byte)procotolType.connectionEnd }));
                }
                SteamNetworking.CloseP2PSessionWithUser(steamid);
                activeConnections.Remove(conn);
            }
        }

        public Tuple<ulong, byte[]> GetNextReliable()
        {
            while (active)
            {
                if (queuedReliable.Count > 0)
                {
                    Tuple<ulong, byte[]> next;
                    lock (queuedReliable)
                    {
                        next = queuedReliable[0];
                        queuedReliable.RemoveAt(0);
                    }
                    return next;
                }
            }
            return null;
        }

        public Tuple<ulong, byte[]> GetNextUnreliable()
        {
            while (active)
            {
                if (queuedUnreliable.Count > 0)
                {
                    Tuple<ulong, byte[]> next;
                    lock (queuedUnreliable)
                    {
                        next = queuedUnreliable[0];
                        queuedUnreliable.RemoveAt(0);
                    }
                    return next;
                }
            }
            return null;
        }


        public void SendAll(sendType send, byte[] info)
        {
            byte[] serializedPayload = Payload.Serialize(new Payload(info, messageType.data));
            foreach (Connection c in activeConnections.ToArray())
            {
                SendRaw(c.steamid, send, serializedPayload);
            }
        }

        private void SendAll(sendType send, byte[] info, messageType msgType = messageType.data)
        {
            byte[] serializedPayload = Payload.Serialize(new Payload(info, msgType));
            foreach (Connection c in activeConnections.ToArray())
            {
                SendRaw(c.steamid, send, serializedPayload);
            }
        }

        public void SendTo(ulong steamid,sendType send, byte[] info, messageType msgType = messageType.data)
        {
            byte[] serializedPayload = Payload.Serialize(new Payload(info,msgType));
            SteamNetworking.SendP2PPacket(steamid, serializedPayload, serializedPayload.Length, (int)send, (P2PSend)send);
        }

        private void SendRaw(ulong steamid, sendType send, byte[] info)
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
        
        public void ConnectToIncoming(bool should)
        {
            connectToIncoming = should;
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

        public static uint GetTimeStamp()
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
        public uint timeSinceLastMessage
        {
            get
            {
                return P2PSocket.GetTimeStamp() - timeAtLastMessage;
            }
        }
        private uint timeAtLastMessage;

        public Connection(ulong steamid, uint timeAtLastMessage)
        {
            this.steamid = steamid;
            this.timeAtLastMessage = timeAtLastMessage;
        }

        public void UpdateTimeAtLastMessage(uint time)
        {
            timeAtLastMessage = time;
        }

    }

}