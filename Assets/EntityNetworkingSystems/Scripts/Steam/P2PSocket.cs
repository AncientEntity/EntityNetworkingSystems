using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;
using System.Threading;
using Steamworks.Data;
using UnityEngine.Events;

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
    
    [System.Serializable]
    public class P2PSocket
    {

        public OnConnectEvent<Connection> onConnectionStart = new OnConnectEvent<Connection>();
        public OnConnectEvent<Connection> onConnectionEnd = new OnConnectEvent<Connection>();
        
        public int channelRecieveOffset = 0;
        public int channelSendOffset = 0;

        private bool active = false;

        [SerializeField]
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

        // Every heartbeat packet is the same, so instead of constructing it each time we use this.
        private static byte[] heartbeatPacket = new byte[2] { (byte)messageType.protocol,(byte)procotolType.heartbeat}; 



        public void Start()
        {
            SteamNetworking.OnP2PSessionRequest += P2PRequest;

            SteamNetworking.OnP2PConnectionFailed = (steamID, failReason) =>
            {
                Debug.Log("CLIENT P2P Failed With: " + steamID + " for reason " + failReason);
            };

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
                        Disconnect(conn.steamID);
                        Debug.Log(conn.steamID + " has timed out. ("+conn.timeSinceLastMessage+"s)");
                    }
                    SendRaw(conn.steamID, sendType.reliable, heartbeatPacket);
                }

                Thread.Sleep(10000);
            }
        }

        private void HandleIncomingPacket(P2Packet? packet, ref List<Tuple<ulong, byte[]>> queue)
        {
            GetConnectionBySteamID(packet.Value.SteamId).UpdateTimeAtLastMessage();

            Payload payload = Payload.DeSerialize(packet.Value.Data);
            if (payload.type == messageType.data)
            {
                queue.Add(new Tuple<ulong, byte[]>(packet.Value.SteamId, payload.info));
                return;
            } else if (payload.type == messageType.protocol)
            {
                if(payload.info[0] == (byte)procotolType.connectionEnd)
                {
                    SteamNetworking.CloseP2PSessionWithUser(packet.Value.SteamId);
                    Disconnect(packet.Value.SteamId,false);
                }
            }
        }

        private void IncomingPacketManager(sendType sType)
        {
            int channel = (int)sType + channelRecieveOffset;
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
                        Debug.Log("Packet Gotten");
                        HandleIncomingPacket(packet, ref queue);
                    }
                }
            }
        }

        public void Stop()
        {
            active = false;
        }
        
        public void Connect(ulong steamid)
        {
            if(steamid == SteamClient.SteamId)
            {
                //Connecting to myself, this means P2PRequest gets skipped.
                SteamNetworking.AcceptP2PSessionWithUser(steamid);
                Connection newConn = new Connection(steamid, GetTimeStamp());
                activeConnections.Add(newConn);
                onConnectionStart.Invoke(newConn);
            }

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
                onConnectionEnd.Invoke(conn);
                Debug.Log("Connection with player has closed: "+steamid);
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
                if(c == null)
                {
                    continue;
                }
                SendRaw(c.steamID, send, serializedPayload);
            }
        }

        private void SendAll(sendType send, byte[] info, messageType msgType = messageType.data)
        {
            byte[] serializedPayload = Payload.Serialize(new Payload(info, msgType));
            foreach (Connection c in activeConnections.ToArray())
            {
                if(c == null)
                {
                    continue;
                }
                SendRaw(c.steamID, send, serializedPayload);
            }
        }

        public void SendTo(ulong steamid,sendType send, byte[] info, messageType msgType = messageType.data)
        {
            byte[] serializedPayload = Payload.Serialize(new Payload(info,msgType));
            SteamNetworking.SendP2PPacket(steamid, serializedPayload, serializedPayload.Length, (int)send + channelSendOffset, (P2PSend)send);
        }

        private void SendRaw(ulong steamid, sendType send, byte[] info)
        {
            SteamNetworking.SendP2PPacket(steamid, info, info.Length, (int)send + channelSendOffset, (P2PSend)send);
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
                Debug.Log("Blacklisted SteamID denied from joining: " + steamid);
                return;
            }

            if (anyConnectionAllowed || connectionsAllowedFrom.Contains(steamid))
            {
                SteamNetworking.AcceptP2PSessionWithUser(steamid);
                if(connectToIncoming)
                {
                    Connect(steamid);
                }
                Connection newConn = new Connection(steamid, GetTimeStamp());
                activeConnections.Add(newConn);
                onConnectionStart.Invoke(newConn);
                Debug.Log("Connection Accepted: " + steamid);
                return;
            }
            Debug.Log("Connection Failed: " + steamid);
            
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
                if(conn.steamID == steamid)
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
                if(conn.steamID == steamid)
                {
                    return conn;
                }
            }
            if(steamid == SteamClient.SteamId)
            {
                Connect(steamid);
                Connection newConn = new Connection(steamid, GetTimeStamp());
                activeConnections.Add(newConn);
                return newConn;
            }
            return null;
        }

        public class OnConnectEvent<Connection> : UnityEvent<Connection> { };

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

    [System.Serializable]
    public class Connection
    {
        public ulong steamID;
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
            this.steamID = steamid;
            this.timeAtLastMessage = timeAtLastMessage;
        }

        public void UpdateTimeAtLastMessage()
        {
            timeAtLastMessage = P2PSocket.GetTimeStamp();
        }
        
    }


}
