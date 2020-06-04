using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;

[System.Serializable]
public class NetClient
{

    public bool connectedToServer = false;
    public TcpClient client = null;
    Thread connectionHandler = null;

    public void Initialize()
    {
        client = new TcpClient();
    }

    public void ConnectionHandler()
    {
        while (client != null)
        {
            byte[] message = RecvMessage();
            Debug.Log(Encoding.Default.GetString(message));
        }
    }


    public void ConnectToServer(string ip="127.0.0.1", int port=44594)
    {
        Debug.Log("Attempting Connection");
        client.Connect(IPAddress.Parse(ip), port);
        Debug.Log("Connection Accepted");
        connectionHandler = new Thread(new ThreadStart(ConnectionHandler));
        connectionHandler.Start();
    }

    public void SendMessage(byte[] message)
    {
        client.GetStream().Write(message, 0, message.Length);
    }
    public byte[] RecvMessage()
    {
        byte[] message = new byte[1024];
        client.GetStream().Read(message, 0, message.Length);
        return message;
    }

}
