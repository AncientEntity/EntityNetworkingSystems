using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace EntityNetworkingSystems.UDP
{
    [System.Serializable]
    public class UDPListener
    {
        public int portToUse = 44595;
        public UdpClient udpServer = null;

        private NetServer parent = null;

        public UDPListener(NetServer netServer)
        {
            parent = netServer;
            portToUse = parent.hostPort + 1;
        }

        public void Start()
        {
            udpServer = new UdpClient(portToUse);
        }

        public void Stop()
        {
            if(udpServer == null)
            {
                return;
            }
            udpServer.Dispose();
        }

        public void SendPacket(Packet packet)
        {
            foreach(NetworkPlayer player in parent.connections)
            {
                if(packet.sendToAll || packet.usersToRecieve.Contains(player.clientID))
                {
                    byte[] bytePacket = ENSSerialization.SerializePacket(packet);
                    udpServer.Send(bytePacket, bytePacket.Length, player.udpEndpoint);
                }
            }
        }

        public KeyValuePair<NetworkPlayer, Packet> RecievePacket()
        {
            KeyValuePair<NetworkPlayer, byte[]> bytes = Recieve();
            return new KeyValuePair<NetworkPlayer,Packet>(bytes.Key,ENSSerialization.DeserializePacket(bytes.Value));
        }

        public KeyValuePair<NetworkPlayer, byte[]> Recieve()
        {
            while (true)
            {
                KeyValuePair<NetworkPlayer, byte[]> recieved = RecieveMessage();
                if (recieved.Key != null)
                {
                    return recieved;
                }
            }
        }

        KeyValuePair<NetworkPlayer, byte[]> RecieveMessage()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, portToUse);
            byte[] recieved = udpServer.Receive(ref remoteEP);
            Debug.Log(remoteEP.ToString());

            return new KeyValuePair<NetworkPlayer, byte[]>(parent.GetPlayerByUDPEndpoint(remoteEP),recieved);
        }

    }

    //Would call it UDPClient but C# has a UDPClient ;-;
    [System.Serializable]
    public class UDPPlayer
    {
        public int portToUse = 44595;
        public UdpClient client;

        private NetClient parent;
        private IPEndPoint serverEndpoint;

        public UDPPlayer(NetClient parent, IPEndPoint serverEndpoint)
        {
            this.parent = parent;
            portToUse = serverEndpoint.Port + 1;

            this.serverEndpoint = serverEndpoint;
        }

        public void Start()
        {
            client = new UdpClient(portToUse);
        }

        public void Stop()
        {
            if(client == null)
            {
                return;
            }
            client.Dispose();
        }

        public void SendPacket(Packet packet)
        {
            byte[] bytePacket = ENSSerialization.SerializePacket(packet);
            client.Send(bytePacket, bytePacket.Length, serverEndpoint);
        }

        public Packet RecievePacket()
        {
            return ENSSerialization.DeserializePacket(RecieveMessage());
        }


        byte[] RecieveMessage()
        {
            return client.Receive(ref serverEndpoint);
        }


    }
}
