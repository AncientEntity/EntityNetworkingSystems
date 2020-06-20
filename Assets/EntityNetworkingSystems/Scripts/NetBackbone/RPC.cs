using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class RPC
    {
        public string rpcName;
        public bool serverAuthorityRequired = false;
        public RPCEvent onRpc = new RPCEvent();
        private NetworkObject net; //Gets auto set by NetworkObject in Initialize
        private int rpcIndex = -1; //Gets auto set by NetworkObject in Initialize

        //Gets queued if the networkObject has not been initialized yet. When initialized NetworKObject checks if anything needs to be run on the RPCs
        public Dictionary<Packet.sendType, object[]> queued = new Dictionary<Packet.sendType, object[]>();

        public void CallRPC(Packet.sendType sendType = Packet.sendType.buffered, params object[] list)
        {
            if (NetTools.IsMultiplayerGame() == false)
            {
                return;
            }


            if (net == null || !net.initialized)
            {
                queued.Add(sendType, list);
                //Debug.Log("Rpc called before initialization. Adding to queue");
                return;
            }

            Packet p = GenerateRPCPacket(sendType, list);

            NetClient.instanceClient.SendPacket(p);
        }

        public Packet GenerateRPCPacket(Packet.sendType sendType = Packet.sendType.buffered, params object[] list)
        {
            RPCPacketData rpcData = new RPCPacketData(net.networkID, rpcIndex, list);

            Packet rpcPacket = new Packet(Packet.pType.rpc, sendType, rpcData);
            rpcPacket.sendToAll = true;

            return rpcPacket;
        }

        public void SetParentNetworkObject(NetworkObject net, int index)
        {
            this.net = net;
            this.rpcIndex = index;
        }

        public void InvokeRPC(RPCArgs args)
        {
            onRpc.Invoke(args);
        }

    }

    [System.Serializable]
    public class RPCPacketData
    {
        public int networkObjectID = -1;
        public int rpcIndex = -1;
        public List<string> parameters = new List<string>();
        public List<string> paramTypes = new List<string>();

        public RPCPacketData(int netID, int rpcIndex, params object[] list)
        {
            this.networkObjectID = netID;
            this.rpcIndex = rpcIndex;

            foreach (object o in list)
            {
                if (!ENSUtils.IsSimple(o.GetType()))
                {
                    parameters.Add(JsonUtility.ToJson(o));
                }
                else
                {
                    parameters.Add("" + o);
                }
                paramTypes.Add(o.GetType().ToString());
            }

        }

        public RPCArgs ReturnArgs()
        {
            RPCArgs rpcArgs = new RPCArgs();

            for (int i = 0; i < parameters.Count; i++)
            {
                if (!ENSUtils.IsSimple(System.Type.GetType(paramTypes[i])))
                {
                    rpcArgs.parameters.Add(JsonUtility.FromJson(parameters[i], System.Type.GetType(paramTypes[i])));
                }
                else
                {
                    System.Type t = System.Type.GetType(paramTypes[i]);
                    rpcArgs.parameters.Add(System.Convert.ChangeType(parameters[i], t));
                }
            }

            return rpcArgs;
        }

    }

    [System.Serializable]
    public class RPCArgs
    {
        public List<object> parameters = new List<object>();
        int nextIndex = 0;

        public T GetNext<T>()
        {
            try
            {
                T t = (T)System.Convert.ChangeType(parameters[nextIndex], typeof(T));
                nextIndex++;
                return t;
            }
            catch
            {
                Debug.LogError("RPCArgs.GetNext() error, sending default(" + typeof(T).ToString() + ")");
                if (parameters[nextIndex].GetType() != typeof(T))
                {
                    Debug.LogError("RPCArgs.GetNext() expected " + typeof(T).ToString() + ", but got: " + parameters[nextIndex].GetType().ToString());
                }
                return default;
            }
        }
    }
    [System.Serializable]
    public class RPCEvent : UnityEvent<RPCArgs>
    {

    }

}