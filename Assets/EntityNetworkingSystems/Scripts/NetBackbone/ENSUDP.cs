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

        

        public UDPListener(NetServer netServer)
        {
            portToUse = NetServer.serverInstance.hostPort + 1;
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
            udpServer.Close();
            udpServer.Dispose();
        }

        public void SendPacket(Packet packet)
        {
            foreach(NetworkPlayer player in NetServer.serverInstance.connections)
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
                try
                {
                    KeyValuePair<NetworkPlayer, byte[]> recieved = RecieveMessage();
                    if (recieved.Key != null)
                    {
                        return recieved;
                    }
                } catch
                {
                    
                }
            }
        }

        KeyValuePair<NetworkPlayer, byte[]> RecieveMessage()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, portToUse);
            byte[] recieved = udpServer.Receive(ref remoteEP);

            return new KeyValuePair<NetworkPlayer, byte[]>(NetServer.serverInstance.GetPlayerByUDPEndpoint(remoteEP),recieved);
        }

    }

    //Would call it UDPClient but C# has a UDPClient ;-;
    [System.Serializable]
    public class UDPPlayer
    {
        public int portToUse = 44595;
        public UdpClient client;

        private IPEndPoint serverEndpoint;

        public UDPPlayer(IPEndPoint serverEndpoint)
        {
            portToUse = serverEndpoint.Port + 1;

            this.serverEndpoint = new IPEndPoint(serverEndpoint.Address, portToUse);
            bool validPort = false;
            while (!validPort)
            {
                try
                {
                    client = new UdpClient(portToUse);
                    validPort = true;
                } catch
                {
                    validPort = false;
                    portToUse += 1;
                }
            }
            //this.serverEndpoint.Port = portToUse;
            //portToUse = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
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
            //Debug.Log("Sent Packet: " + packet.packetSendType + ". To: "+serverEndpoint.ToString());
        }

        public Packet RecievePacket()
        {
            return ENSSerialization.DeserializePacket(RecieveMessage());
        }


        byte[] RecieveMessage()
        {
            //Debug.Log(serverEndpoint.ToString());
            return client.Receive(ref serverEndpoint);
        }


    }
}
