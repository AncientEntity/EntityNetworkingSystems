using System;
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
        public bool trackPlayerProxPos = false; //If true, and there is ENS_Position it'll be tracked as the players proximity position.
        public List<NetworkField> fields = new List<NetworkField>();
        public List<RPC> rpcs = new List<RPC>();
        [Space]
        public UnityEvent onNetworkStart;

        //[HideInInspector]
        public int prefabID = -1;
        //[HideInInspector]
        public int prefabDomainID = -1;

        //[HideInInspector]
        //public List<Packet> queuedNetworkPackets = new List<Packet>();


        void Awake()
        {
            //onNetworkStart.Invoke(); //Invokes inside of UnityPacketHandler

        }




        public void Initialize()
        {
            if (networkID == -1)
            {
                networkID = NetTools.GenerateNetworkObjectID();
            }


            NetworkData.AddUsedNetID(networkID);
            allNetObjs.Add(this);
            
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

            //StartCoroutine(NetworkFieldPacketHandler());
        }

        //public IEnumerator NetworkFieldPacketHandler ()
        //{
        //    yield return new WaitUntil(() => initialized);
        //    while(initialized)
        //    {
        //        yield return new WaitUntil(() => queuedNetworkPackets.Count > 0);

        //        Packet curPacket = queuedNetworkPackets[0];
        //        NetworkFieldPacket nFP = (NetworkFieldPacket)curPacket.GetPacketData();

        //        if ((ownerID != curPacket.packetOwnerID && !curPacket.serverAuthority && !sharedObject) || (curPacket.packetOwnerID == NetTools.clientID && nFP.immediateOnSelf))
        //        {
        //            queuedNetworkPackets.RemoveAt(0);
        //            continue;
        //        }

        //        SetFieldLocal(nFP.fieldName, nFP.data.ToObject());
        //        queuedNetworkPackets.RemoveAt(0);

        //    }
        //}

        public void DoRpcFieldInitialization()
        {
            foreach (NetworkField field in fields)
            {
                if (field.IsInitialized() == false)
                {
                    field.InitializeDefaultValue(this);
                }
            }
            int index = 0;
            foreach (RPC rpc in rpcs)
            {
                rpc.SetParentNetworkObject(this, index);
                index += 1;
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
            NetworkObject[] netObjArray = { };
            lock (allNetObjs)
            {
                netObjArray = allNetObjs.ToArray();
            }
            foreach (NetworkObject netObj in netObjArray)
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
                        newField.InitializeDefaultValue(this);
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
                        fields[i].InitializeDefaultValue(this);
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
                    new NetworkFieldPacket(networkID, netField.fieldName,jPO,false));
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

        public void FieldAddOnChangeMethod(string fieldName, UnityAction<FieldArgs> action)
        {
            foreach(NetworkField netField in fields)
            {
                if(netField.fieldName == fieldName)
                {
                    netField.onValueChange.AddListener(action);
                    return;
                }
            }
        }

        //A premade "on value change" method for positional network fields. Gets added to fields named ENS_Position
        public void ManagePositionField(FieldArgs args)
        {
            transform.position = args.GetValue<SerializableVector>().ToVec3();
        }

        //A premade "on value change" method for network fields representing scales. Gets added to fields named ENS_Scale
        public void ManageScaleField(FieldArgs args)
        {
            transform.localScale = args.GetValue<SerializableVector>().ToVec3();
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
        public OnFieldChange onValueChange;
        public bool shouldBeProximity = false;
        private string jsonData = "notinitialized";
        private string jsonDataTypeName = "notinitialized";
        private bool initialized = false;
        private int netID = -1;
        private NetworkObject netObj;

        public bool InitializeSpecialFields()
        {
            if(netID == -1)
            {
                return false;
            }

            if (fieldName == "ENS_Position")
            {
                IfServerLocalSetField(new SerializableVector(netObj.transform.position), false);
                //shouldBeProximity = true;
                onValueChange.AddListener(netObj.ManagePositionField);
                return true;
            }
            else if (fieldName == "ENS_Scale")
            {
                IfServerLocalSetField(new SerializableVector(netObj.transform.localScale), false);
                //shouldBeProximity = true;
                onValueChange.AddListener(netObj.ManageScaleField);
                return true;
            }
            return false;
        }

        public void InitializeDefaultValue(NetworkObject netObj)
        {
            if (initialized)
            {
                if (netID == -1)
                {
                    this.netID = netObj.networkID; //We do this here since NetworkData initializes the template fields but doesnt know this yet.
                    this.netObj = netObj;
                    InitializeSpecialFields();
                }
                return;
            }
            if (netObj != null)
            {
                int netID = netObj.networkID;
                this.netID = netID;
                this.netObj = netObj;
            }
            //LocalFieldSet(default(T));
            //UpdateField(default(T),netID,true);

            //initialized = true; //Gets set in LocalFieldSet


            if(!InitializeSpecialFields()) 
            {
                switch (defaultValue)
                {
                    case valueInitializer.INT:
                        IfServerLocalSetField(0, false);
                        break;
                    case valueInitializer.FLOAT:
                        IfServerLocalSetField(0.0f, false);
                        break;
                    case valueInitializer.DOUBLE:
                        IfServerLocalSetField(0.0, false);
                        break;
                    case valueInitializer.serializableVector:
                        IfServerLocalSetField(new SerializableVector(0, 0, 0), false);
                        break;
                    case valueInitializer.BYTE:
                        IfServerLocalSetField(new byte(), false);
                        break;
                    case valueInitializer.BYTE_ARRAY:
                        IfServerLocalSetField(new byte[0], false);
                        break;
                    case valueInitializer.String:
                        IfServerLocalSetField("", false);
                        break;
                }
            }
        }

        public void IfServerLocalSetField(object newValue, bool invokeOnChange=true)
        {
            if (NetTools.isServer)
            {
                LocalFieldSet(newValue, invokeOnChange);
            }
        }

        public void IfServerUpdateField(object newValue, int netObjID,bool immediateOnSelf=true)
        {
            if(NetTools.isServer)
            {
                UpdateField(newValue, netObjID, immediateOnSelf);
            }
        }

        public void UpdateField(object newValue, NetworkObject netObj)
        {
            UpdateField(newValue, netObj.networkID);
        }

        public void UpdateField<T>(T newValue, int netObjID, bool immediateOnSelf = true)
        {
            JsonPacketObject jPO = new JsonPacketObject("",newValue.GetType().ToString());
            jPO.jsonDataTypeName = newValue.GetType().ToString();
            jPO.actualObj = newValue;


            //if (!ENSUtils.IsSimple(newValue.GetType())) {
            //   jPO = new JsonPacketObject(JsonUtility.ToJson(newValue), newValue.GetType().ToString());
            //} else
            //{
            //    jPO = new JsonPacketObject(""+newValue, newValue.GetType().ToString());
            //}

            Packet pack = new Packet(Packet.pType.netVarEdit, Packet.sendType.nonbuffered,
                new NetworkFieldPacket(netObjID, fieldName, jPO,immediateOnSelf));
            pack.relatesToNetObjID = netObjID;
            if(shouldBeProximity)
            {
                pack.packetSendType = Packet.sendType.proximity;
                pack.packetPosition = new SerializableVector(netObj.transform.position);
            }
            NetClient.instanceClient.SendPacket(pack);
            if (immediateOnSelf)
            {
                LocalFieldSet(newValue);
            }
        }


        public void LocalFieldSet<T>(T newValue, bool invokeOnChange = true)
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

            FieldArgs constructedArgs = new FieldArgs();
            constructedArgs.fieldName = fieldName;
            constructedArgs.networkID = netID;
            constructedArgs.fieldValue = newValue;

            initialized = true;
            if (invokeOnChange)
            {
                onValueChange.Invoke(constructedArgs);
            }
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

        public void AddOnChangeMethod(UnityAction<FieldArgs> a)
        {
            onValueChange.AddListener(a);
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
            newField.shouldBeProximity = shouldBeProximity;
            return newField;
        }
        public static NetworkFieldPacket GenerateNFP<T>(string fieldName, T newValue, bool immediateOnSelf = false, int netObjID=-1)
        {
            JsonPacketObject jPO;

            if (!ENSUtils.IsSimple(newValue.GetType()))
            {
                jPO = new JsonPacketObject(JsonUtility.ToJson(newValue), newValue.GetType().ToString());
            }
            else
            {
                jPO = new JsonPacketObject("" + newValue, newValue.GetType().ToString());
            }

            return new NetworkFieldPacket(netObjID, fieldName, jPO, immediateOnSelf);
        }



    }

    [System.Serializable]
    public class OnFieldChange : UnityEvent<FieldArgs>
    {

    }

    [System.Serializable]
    public class FieldArgs
    {
        public string fieldName = "";
        public int networkID = -1;
        public object fieldValue = null;

        public T GetValue<T>()
        {
            return (T)System.Convert.ChangeType(fieldValue, typeof(T));
        }

    }

    [System.Serializable]
    public class NetworkFieldPacket
    {
        public int networkObjID = -1;
        public string fieldName = "";
        public JsonPacketObject data;
        public bool immediateOnSelf = false;
        public NetworkFieldPacket(int netID, string fieldName, JsonPacketObject val, bool immediateOnSelf)
        {
            networkObjID = netID;
            this.fieldName = fieldName;
            data = val;
            this.immediateOnSelf = immediateOnSelf;
        }

    }

}