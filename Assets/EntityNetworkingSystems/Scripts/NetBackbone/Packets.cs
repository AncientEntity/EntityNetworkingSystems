using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

namespace EntityNetworkingSystems
{

    [System.Serializable]
    public class Packet
    {

        [System.Serializable]
        public enum sendType
        {
            buffered,
            nonbuffered,
            culledbuffered,
            proximity,
        }
        public sendType packetSendType = sendType.buffered;

        [System.Serializable]
        public enum pType
        {
            gOInstantiate, // Custom serializer done, 76 minimum bytes.
            gODestroy, // 38 bytes per destroy.
            netVarEdit, //100 bytes or so
            rpc, //100 bytes or so to transmit a Color over the network
            message, //doesnt go anywhere
            unassigned, //doesnt go anywhere
            multiPacket, //Optimized for the new packet serializers
            loginInfo, //Custom serializer done.
            networkAuth, //only is managed when the client first connects with the server. UnityPacketManager has no logic for it.
        }
        public pType packetType = pType.unassigned;
        public bool reliable = true;

        //public string jsonData;
        //public string jsonDataTypeName;

        public byte[] packetData;
        
        public int packetOwnerID = NetTools.clientID; //If the client tries lying to server, the server verifies it in NetServer anyways...
        public bool serverAuthority = false; //Manually changed in NetServer. Client changing it wont effect other clients/server.
        public bool sendToAll = true;
        public List<int> usersToRecieve = new List<int>(); //if send to all is false.
        public int relatesToNetObjID = -1;

        public SerializableVector packetPosition;

        public Packet(pType packetType, sendType typeOfSend, byte b)
        {
            this.packetType = packetType;
            this.packetSendType = typeOfSend;
            SetPacketData(new byte[] {b});
        }

        public Packet(pType packetType, sendType typeOfSend, byte[] obj)
        {
            this.packetType = packetType;
            this.packetSendType = typeOfSend;
            packetData = obj;
            
        }

        public Packet(pType packetType, sendType typeOfSend, object obj = null)
        {
            this.packetType = packetType;
            this.packetSendType = typeOfSend;
            if (obj != null)
            {
                SetPacketData(obj);
            }
        }

        //Not a good way to do it, suggested is making a custom serializor and using SetPacketData(byte[] data)
        public void SetPacketData(object obj)
        {
            if(obj.GetType().ToString() == "EntityNetworkingSystems.NetworkFieldPacket")
            {
                SetPacketData(ENSSerialization.SerializeNetworkFieldPacket((NetworkFieldPacket)obj));
                return;
            }

            SetPacketData(ENSSerialization.SerializeObject(obj));
        }

        public void SetPacketData(byte[] data)
        {
            packetData = data;

            //object formattedData = data;
            //jsonDataTypeName = data.GetType().ToString();


            //if (data.GetType().ToString() == "System.Int32" || data.GetType().ToString() == "System.Int16" || data.GetType().ToString() == "System.Int64")
            //{
            //    //Automatically put it into an IntPacket so JsonUtility will jsonify it.
            //    formattedData = new IntPacket((int)data);
            //    jsonDataTypeName = formattedData.GetType().ToString();
            //}

            //packetData = Encoding.ASCII.GetBytes(JsonUtility.ToJson(formattedData));

        }

        [Obsolete("This uses the BinaryFormatter. It is recommended to make your own serializer and use that instead. Then just set packetData manually.")]
        public T GetPacketData<T>()
        {
            if(typeof(T).ToString() == "EntityNetworkingSystems.NetworkFieldPacket")
            {
                return (T)System.Convert.ChangeType(ENSSerialization.DeserializeNetworkFieldPacket(packetData),typeof(T));
            } else if (typeof(T).ToString() == "EntityNetworkingSystems.RPCPacketData")
            {
                return (T)System.Convert.ChangeType(ENSSerialization.DeserializeRPCPacketData(packetData), typeof(T));
            }


            try
            {
                return (T)System.Convert.ChangeType(packetData, typeof(T));
            } catch
            {
                return ENSSerialization.DeserializeObject<T>(packetData);
            }
            //System.Type t = System.Type.GetType(jsonDataTypeName);
            ////Debug.Log(t);
            //if (t.ToString() == "EntityNetworkingSystems.IntPacket")
            //{
            //    //If integer you must first convert it out of a IntPacket.
            //    return JsonUtility.FromJson<IntPacket>(jsonData).integer;
            //}
            //else
            //{
            //    return JsonUtility.FromJson(jsonData, t);
            //}
        }



//        public static Packet DeJsonifyPacket(string jsonPacket)
//        {
//            //int lastJsonIndex = jsonPacket.LastIndexOf("}");
//            //jsonPacket = jsonPacket.Substring(0, lastJsonIndex);


//            try
//            {
//                //Debug.Log(jsonPacket);
//                return JsonUtility.FromJson<Packet>(jsonPacket);
//            }
//            catch (Exception e)
//            {
//                Debug.LogError(e);
//                //Debug.LogError("Error Dejsonify. Length: "+jsonPacket.Length+": " + jsonPacket);
//#if UNITY_EDITOR
//                NetworkData.instance.errorJson = jsonPacket;
//#endif
//                return null;
//            }
//        }

//        public static string JsonifyPacket(Packet packet)
//        {
//            return JsonUtility.ToJson(packet);
//        }
        

    }

    [System.Serializable]
    public class PlayerLoginData
    {
        public int playerNetworkID = -1;
    }

    [System.Serializable]
    public class GameObjectInstantiateData
    {
        public int prefabDomainID = -1;
        public int prefabID = -1;
        public SerializableVector position;
        public SerializableQuaternion rotation;

        public int netObjID = -1;
        public bool isShared = false;
        public bool doImmediate = true;

        public List<NetworkFieldPacket> fieldDefaults = new List<NetworkFieldPacket>();



    }

    [System.Serializable]
    public class SerializableVector
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector(float X, float Y, float Z)
        {
            x = X;
            y = Y;
            z = Z;
        }

        public SerializableVector(Vector3 vec)
        {
            x = vec.x;
            y = vec.y;
            z = vec.z;
        }

        public Vector3 ToVec3()
        {
            return new Vector3(x, y, z);
        }
    }

    [System.Serializable]
    public class SerializableQuaternion
    {
        public float x = 0f;
        public float y = 0f;
        public float z = 0f;
        public float w = 0f;

        public SerializableQuaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public SerializableQuaternion(Quaternion q)
        {
            x = q.x;
            y = q.y;
            z = q.z;
            w = q.w;
        }

        public Quaternion ToQuaternion()
        {
            return new Quaternion(x, y, z, w);
        }
    }

    [System.Serializable]
    public class JsonPacketObject
    {
        public string jsonData;
        public string jsonDataTypeName;

        public JsonPacketObject(string data, string jsonTypeName)
        {
            jsonData = data;
            jsonDataTypeName = jsonTypeName;
        }

        public object ToObject()
        {

            if (ENSUtils.IsSimple(System.Type.GetType(jsonDataTypeName)))
            {
                return System.Convert.ChangeType(jsonData, System.Type.GetType(jsonDataTypeName));
            }
            else
            {
                return JsonUtility.FromJson(jsonData, System.Type.GetType(jsonDataTypeName));
            }
        }

    }


    [System.Serializable]
    public class PacketListPacket
    {
        public List<byte[]> packets = new List<byte[]>();

        public PacketListPacket(List<Packet> packets)
        {
            foreach(Packet p in packets)
            {
                this.packets.Add(ENSSerialization.SerializePacket(p));
            }
        }
    }


    [System.Serializable]
    public class IntPacket
    {
        public int integer = 0;

        public IntPacket(int integer)
        {
            this.integer = integer;
        }
    }

    [System.Serializable]
    public class FloatPacket
    {
        public float value = 0f;
        public FloatPacket(float val)
        {
            value = val;
        }


    }

    [System.Serializable]
    public class NetworkAuthPacket
    {
        public byte[] authData;
        public ulong steamID;
        public int udpPort;

        public NetworkAuthPacket(byte[] data, ulong sID, int udpPort)
        {
            authData = data;
            steamID = sID;
            this.udpPort = udpPort;
        }
    }

}