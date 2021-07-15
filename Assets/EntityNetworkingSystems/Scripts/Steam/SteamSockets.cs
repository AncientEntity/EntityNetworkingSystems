using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace EntityNetworkingSystems
{
	public class SteamSocketServer : SocketManager
	{
		public bool active = true;

		public Dictionary<uint, List<byte[]>> messageQueue = new Dictionary<uint, List<byte[]>>();

		public override void OnConnecting(Connection connection, ConnectionInfo data)
		{
			NetServer.serverInstance.OnNewConnection(connection,data);
			messageQueue[connection.Id] = new List<byte[]>();
			//connection.Accept();
			Debug.Log($"{data.Identity} is connecting");
		}

		public override void OnConnected(Connection connection, ConnectionInfo data)
		{
			Debug.Log($"{data.Identity.SteamId} has joined the game");
		}

		public override void OnDisconnected(Connection connection, ConnectionInfo data)
		{
			Debug.Log($"{data.Identity} is out of here");
			messageQueue.Remove(connection.Id);
		}

		public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
		{
			Debug.Log($"We got a message from {identity}!");

			byte[] managedArray = new byte[size];
			Marshal.Copy(data, managedArray, 0, size);
			messageQueue[connection.Id].Add(managedArray);
		
		}

		public new void Close()
		{
			base.Close();
			active = false;
		}

		public byte[] GetNext(uint connId)
		{
			while (active)
			{
				if (messageQueue[connId].Count <= 0)
				{
					base.Receive();
					continue;
				}
				lock (messageQueue)
				{
					byte[] b = messageQueue[connId][0];
					messageQueue[connId].RemoveAt(0);
					return b;
				}
			}
			return null;
		}
	}

    public class SteamSocketClient : ConnectionManager
	{
		public bool active = true;

		public List<byte[]> messageQueue = new List<byte[]>();

		public override void OnConnected(ConnectionInfo info)
        {
			Debug.Log("Connected to: " + info.Identity.SteamId);
        }

        public override void OnConnecting(ConnectionInfo info)
        {
			Debug.Log("Connecting to: " + info.Identity.SteamId);
		}

        public override void OnConnectionChanged(ConnectionInfo info)
        {
			Debug.Log("Connection Change :" + info.State);
        }

        public override void OnDisconnected(ConnectionInfo info)
        {
			Debug.Log("Connection Change :" + info.EndReason);
		}

        public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
        {
			Debug.Log($"We got a message from server!");

			byte[] managedArray = new byte[size];
			Marshal.Copy(data, managedArray, 0, size);
			messageQueue.Add(managedArray);
		}

		public new void Close()
		{
			base.Close();
			active = false;
		}

		public byte[] GetNext()
		{
			while (active)
			{
				if (messageQueue.Count <= 0)
				{
					base.Receive();
					continue;
				}
				lock (messageQueue)
				{
					byte[] b = messageQueue[0];
					messageQueue.RemoveAt(0);
					return b;
				}
			}
			return null;
		}
	}
}
