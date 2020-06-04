using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;

[System.Serializable]
public class NetServer
{
    public string hostAddress;
    public int hostPort=44594;
    public int maxConnections = 8;


    TcpListener server = null;
    List<TcpClient> connections = new List<TcpClient>();
    List<Thread> connThreads = new List<Thread>();
    Thread connectionHandler = null;

    NetworkStream networkStream = null;

    public void Initialize()
    {
        if (hostAddress == "")
        {
            //If no ip given, use 0.0.0.0
            hostAddress = IPAddress.Any.ToString();
        }
        if(hostPort == 0)
        {
            hostPort = 44594;
        }

        //Create server
        server = new TcpListener(IPAddress.Any, hostPort);
        server.Start();
        Debug.Log("Server started successfully.");

        connectionHandler = new Thread(new ThreadStart(ConnectionHandler));
        connectionHandler.Start();

    }

    public void StopServer()
    {
        if(server != null)
        {
            foreach(TcpClient client in connections)
            {
                client.Close();
            }
            server.Stop();
        }
    }

    public void ConnectionHandler()
    {
        if (!IsInitialized())
        {
            Debug.Log("Server not initialized. Please run Initialize() first.");
            return;
        }

        while (server != null)
        {
            while (CurrentConnectionCount() >= maxConnections)
            {
                Thread.Sleep(1000);
            }
            Debug.Log("Awaiting Client Connection...");


            TcpClient newClient = server.AcceptTcpClient();
            connections.Add(newClient);
            Debug.Log("New Client Connected Successfully.");

            Thread connThread = new Thread(() => ClientHandler(newClient));
            connThread.Start();

        }
    }

    public void ClientHandler(TcpClient client)
    {
        while (client != null)
        {
            SendMessage(client, Encoding.Default.GetBytes("Hello World."));
            Debug.Log("Sent Test Message");
            Thread.Sleep(500);
        }
    }

    public bool IsInitialized()
    {
        if (server == null)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public int CurrentConnectionCount()
    {
        return connections.Count;
    }

    public void SendMessage(TcpClient client, byte[] message)
    {
        client.GetStream().Write(message, 0, message.Length);
    }
    public byte[] RecvMessage(TcpClient client)
    {
        byte[] message = new byte[1024];
        client.GetStream().Read(message, 0, message.Length);
        return message;
    }

}
