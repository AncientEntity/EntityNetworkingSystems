using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace EntityNetworkingSystems
{
    public class NetworkObject : MonoBehaviour
    {
        //NETWORK OBJECT AUTOMATICALLY GETS ADDED TO PREFAB WHEN INSTANTIATED OVER THE NETWORK IF THE PREFAB DOESN'T ALREADY CONTAIN ONE.

        public static List<NetworkObject> allNetObjs = new List<NetworkObject>();

        public bool initialized = false; //Has it been initialized on the network?
        [Space]
        public int networkID = -1;
        public int ownerID = -1;
        public bool sharedObject = false; //All clients/server can effect the networkObject.
        public List<NetworkField> fields = new List<NetworkField>();
        public List<RPC> rpcs = new List<RPC>();
        [Space]
        public UnityEvent onNetworkStart;

        //[HideInInspector]
        public int prefabID = -1;
        //[HideInInspector]
        public int prefabDomainID = -1;



        void Awake()
        {
            //onNetworkStart.Invoke(); //Invokes inside of UnityPacketHandler

        }

        void Start()
        {
            DoRpcFieldInitialization();
        }

        void DoRpcFieldInitialization()
        {
            foreach (NetworkField field in fields)
            {
                if (field.IsInitialized() == false)
                {
                    field.InitializeDefaultValue(networkID);
                }
            }
            int index = 0;
            foreach (RPC rpc in rpcs)
            {
                rpc.SetParentNetworkObject(this, index);
                index += 1;
            }
        }

        public void Initialize()
        {
            if (networkID == -1)
            {
                networkID = NetTools.GenerateNetworkObjectID();
            }

            if (NetworkData.usedNetworkObjectInstances.Contains(networkID) == false && networkID != -1)
            {
                NetworkData.AddUsedNetID(networkID);
            }

            if (NetworkObject.allNetObjs.Contains(this) == false)
            {
                allNetObjs.Add(this);
            }
            initialized = true;

            DoRpcFieldInitialization();

            foreach (RPC r in rpcs)
            {
                //Debug.Log(r.queued.Count);
                foreach (KeyValuePair<Packet.sendType, object[]> call in r.queued)
                {
                    NetClient.instanceClient.SendPacket(r.GenerateRPCPacket(call.Key, call.Value)); //Send any RPCs requested before the netObj was initialized.
                }
                r.queued = new Dictionary<Packet.sendType, object[]>(); //Clear it afterwards.
            }
        }

        public bool IsOwner()
        {
            //print(ownerID + " " + NetTools.clientID);
            if (ownerID == NetTools.clientID)
            {
                return true;
            }
            return false;
        }

        public void OnDestroy()
        {
            //Remove from used lists
            allNetObjs.Remove(this);
            NetworkData.usedNetworkObjectInstances.Remove(networkID);

        }

        public static NetworkObject NetObjFromNetID(int netID)
        {
            foreach (NetworkObject netObj in allNetObjs)
            {
                if (netObj.networkID == netID)
                {
                    return netObj;
                }
            }
            return null;
        }

        public bool FieldExists(string fieldName)
        {
            foreach (NetworkField f in fields)
            {
                if (f.fieldName == fieldName)
                {
                    return true;
                }
            }
            return false;
        }

        public void CreateField(string fieldName, object value = null, NetworkField.valueInitializer init = NetworkField.valueInitializer.None)
        {
            if (FieldExists(fieldName) == false)
            {
                NetworkField newField = new NetworkField();
                newField.fieldName = fieldName;
                newField.defaultValue = init;
                if (value != null)
                {
                    newField.UpdateField(value, this);
                }
                else
                {
                    if (newField.IsInitialized() == false)
                    {
                        newField.InitializeDefaultValue(networkID);
                    }
                }
                fields.Add(newField);
            }
        }

        public void UpdateField<T>(string fieldName, T data, bool immediateOnSelf = false)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].fieldName == fieldName)
                {
                    fields[i].UpdateField(data, networkID, immediateOnSelf);
                    break;
                }
            }
        }

        public void SetFieldLocal(string fieldName, object data)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].fieldName == fieldName)
                {
                    fields[i].LocalFieldSet(data);
                    break;
                }
            }
        }

        public T GetField<T>(string fieldName)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].fieldName == fieldName)
                {
                    if (fields[i].IsInitialized() == false)
                    {
                        fields[i].InitializeDefaultValue(networkID);
                    }

                    return (T)System.Convert.ChangeType(fields[i].GetField(), typeof(T));
                }
            }
            return default;
        }

        public List<Packet> GeneratePacketListForFields()
        {
            List<Packet> fieldPackets = new List<Packet>();
            foreach (NetworkField netField in fields)
            {
                if (!netField.IsInitialized())
                {
                    continue;
                }

                JsonPacketObject jPO;

                System.Convert.ChangeType(netField.GetField(), netField.GetField().GetType());

                if (!ENSUtils.IsSimple(netField.GetField().GetType()))
                {
                    jPO = new JsonPacketObject(JsonUtility.ToJson(System.Convert.ChangeType(netField.GetField(), netField.GetField().GetType())), netField.GetField().GetType().ToString());
                }
                else
                {
                    jPO = new JsonPacketObject("" + netField.GetField(), netField.GetField().GetType().ToString());
                }

                Packet packet = new Packet(Packet.pType.netVarEdit, Packet.sendType.nonbuffered,
                    new NetworkFieldPacket(networkID, netField.fieldName,jPO));
                fieldPackets.Add(packet);
                //print("Adding netfield: " + netField.fieldName);
            }
            return fieldPackets;
        }

        public void CallRPC(string rpcName, Packet.sendType sendType = Packet.sendType.culledbuffered, params object[] list)
        {
            foreach (RPC rpc in rpcs)
            {
                if (rpc.rpcName == rpcName)
                {
                    rpc.CallRPC(sendType, list: list);
                }
            }
        }


    }

    [System.Serializable]
    public class NetworkField
    {
        public string fieldName;
        public enum valueInitializer
        {
            INT,
            FLOAT,
            DOUBLE,
            serializableVector,
            BYTE,
            BYTE_ARRAY,
            None,
            String,
        };
        public valueInitializer defaultValue = valueInitializer.None;
        public UnityEvent onValueChange;
        private string jsonData = "notinitialized";
        private string jsonDataTypeName = "notinitialized";
        private bool initialized = false;

        public void InitializeDefaultValue(int netID)
        {
            if (initialized)
            {
                return;
            }

            //LocalFieldSet(default(T));
            //UpdateField(default(T),netID,true);

            //initialized = true; //Gets set in LocalFieldSet

            switch (defaultValue)
            {
                case valueInitializer.INT:
                    UpdateField(0, netID, true);
                    break;
                case valueInitializer.FLOAT:
                    UpdateField(0.0f, netID, true);
                    break;
                case valueInitializer.DOUBLE:
                    UpdateField(0.0, netID, true);
                    break;
                case valueInitializer.serializableVector:
                    UpdateField(new SerializableVector(0, 0, 0), netID, true);
                    break;
                case valueInitializer.BYTE:
                    UpdateField(new byte(), netID, true);
                    break;
                case valueInitializer.BYTE_ARRAY:
                    UpdateField(new byte[0], netID, true);
                    break;
                case valueInitializer.String:
                    UpdateField("", netID, true);
                    break;
            }
        }
        public void UpdateField(object newValue, NetworkObject netObj)
        {
            UpdateField(newValue, netObj.networkID);
        }

        public void UpdateField<T>(T newValue, int netObjID, bool immediateOnSelf = false)
        {
            JsonPacketObject jPO;

            if (!ENSUtils.IsSimple(newValue.GetType())) {
               jPO = new JsonPacketObject(JsonUtility.ToJson(newValue), newValue.GetType().ToString());
            } else
            {
                jPO = new JsonPacketObject(""+newValue, newValue.GetType().ToString());
            }

            Packet pack = new Packet(Packet.pType.netVarEdit, Packet.sendType.nonbuffered,
                new NetworkFieldPacket(netObjID, fieldName, jPO));
            NetClient.instanceClient.SendPacket(pack);
            if (immediateOnSelf)
            {
                LocalFieldSet(newValue);
            }
        }

        public void LocalFieldSet<T>(T newValue)
        {
            if (ENSUtils.IsSimple(newValue.GetType()))
            {
                jsonData = "" + newValue;
            }
            else
            {
                jsonData = JsonUtility.ToJson(newValue); //This is used in UnityPacketHandler when setting it after packet being recieved. Don't use.
            }
            jsonDataTypeName = newValue.GetType().ToString();

            initialized = true;
            onValueChange.Invoke();
        }

        public object GetField()
        {
            if (!initialized)
            {
                return default;
            }
            if (ENSUtils.IsSimple(System.Type.GetType(jsonDataTypeName)))
            {
                return System.Convert.ChangeType(jsonData, System.Type.GetType(jsonDataTypeName));
            }
            else
            {
                return JsonUtility.FromJson(jsonData, System.Type.GetType(jsonDataTypeName));
            }
        }

        public bool IsInitialized()
        {
            return initialized;
        }

        public NetworkField Clone()
        {
            NetworkField newField = new NetworkField();
            newField.fieldName = fieldName;
            newField.defaultValue = defaultValue;
            newField.onValueChange = onValueChange;
            newField.jsonData = jsonData;
            newField.jsonDataTypeName = jsonDataTypeName;
            newField.initialized = initialized;
            return newField;
        }

    }

    [System.Serializable]
    public class NetworkFieldPacket
    {
        public int networkObjID = -1;
        public string fieldName = "";
        public JsonPacketObject data;

        public NetworkFieldPacket(int netID, string fieldName, JsonPacketObject val)
        {
            networkObjID = netID;
            this.fieldName = fieldName;
            data = val;
        }

    }

}