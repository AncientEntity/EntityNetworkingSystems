using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using UnityEngine;

namespace EntityNetworkingSystems
{
    public class ENSSerialization
    {
        public static BinaryFormatter bF = new BinaryFormatter();


        //Packet Serialization Data - Total Minimum Bytes: 30 bytes.
        // - sendType int8, 1 byte
        // - packetType int8, 1 byte
        // - packetOwnerID int16, 2 bytes
        // - serverAuthority bool, 1 byte
        // - sendToAll bool, 1 byte
        // - relatesToNetObjID int32, 4 bytes
        // - packetPosition Vec3, 12 bytes
        // - usersToRecieve List of varing size
        //    - List Count int32, 4 bytes
        //    - List elements int32(s), 4 bytes each
        // - packet info byte array of varying bytes
        //    - packet info byte length, 4 bytes
        //    - packet info byte array, varying

        public static byte[] SerializePacket(Packet packet)
        {
            List<byte> objectAsBytes = new List<byte>();

            //Basic Fields
            byte sendType = (byte)packet.packetSendType;
            objectAsBytes.Add(sendType);
            byte packetType = (byte)packet.packetType;
            objectAsBytes.Add(packetType);
            byte[] packetOwnerID = System.BitConverter.GetBytes((short)packet.packetOwnerID);
            objectAsBytes.AddRange(packetOwnerID);
            byte[] serverAuthority = System.BitConverter.GetBytes(packet.serverAuthority);
            objectAsBytes.AddRange(serverAuthority);
            byte[] sendToAll = System.BitConverter.GetBytes(packet.sendToAll);
            objectAsBytes.AddRange(sendToAll);
            byte[] relatesToNetObjID = System.BitConverter.GetBytes(packet.relatesToNetObjID);
            objectAsBytes.AddRange(relatesToNetObjID);
            byte[] packetTag = System.BitConverter.GetBytes(NetworkData.instance.PacketTagToID(packet.tag));
            objectAsBytes.AddRange(packetTag);

            //Vector3 Packet Position
            if(packet.packetPosition == null)
            {
                packet.packetPosition = new SerializableVector(0, 0, 0);
            }
            List<byte> positionBytes = new List<byte>();
            positionBytes.AddRange(System.BitConverter.GetBytes(packet.packetPosition.x));
            positionBytes.AddRange(System.BitConverter.GetBytes(packet.packetPosition.y));
            positionBytes.AddRange(System.BitConverter.GetBytes(packet.packetPosition.z));
            objectAsBytes.AddRange(positionBytes);

            //Lists & Arrays
            byte[] userToReceiveCount = System.BitConverter.GetBytes(packet.usersToRecieve.Count);
            objectAsBytes.AddRange(userToReceiveCount);
            List<byte> usersToReceive = new List<byte>();
            foreach(int userID in packet.usersToRecieve)
            {
                usersToReceive.AddRange(System.BitConverter.GetBytes(userID));
            }
            objectAsBytes.AddRange(usersToReceive);

            //Packet Info
            byte[] packetInfoSize = System.BitConverter.GetBytes(packet.packetData.Length);
            objectAsBytes.AddRange(packetInfoSize);
            objectAsBytes.AddRange(packet.packetData);

            return objectAsBytes.ToArray();
        }

        public static Packet DeserializePacket(byte[] bytes)
        {
            int intIndex = 0;
            List<byte> packetBytes = new List<byte>();
            packetBytes.AddRange(bytes);
            Packet packet = new Packet(Packet.pType.unassigned, Packet.sendType.nonbuffered, 0);

            //Basic Fields
            packet.packetSendType = (Packet.sendType)packetBytes[intIndex]; intIndex++;// (Packet.sendType)System.BitConverter.ToInt32(packetBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            packet.packetType = (Packet.pType)packetBytes[intIndex]; intIndex++; // (Packet.pType)System.BitConverter.ToInt32(packetBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            packet.packetOwnerID = System.BitConverter.ToInt16(packetBytes.GetRange(intIndex, 2).ToArray(), 0); intIndex += 2;
            packet.serverAuthority = System.BitConverter.ToBoolean(packetBytes.GetRange(intIndex, 1).ToArray(), 0); intIndex += 1;
            packet.sendToAll = System.BitConverter.ToBoolean(packetBytes.GetRange(intIndex, 1).ToArray(), 0); intIndex += 1;
            packet.relatesToNetObjID = System.BitConverter.ToInt32(packetBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            packet.tag = NetworkData.instance.TagIDToTagName(System.BitConverter.ToInt32(packetBytes.GetRange(intIndex, 4).ToArray(), 0)); intIndex += 4;

            //Vector3 packet position
            SerializableVector vec = new SerializableVector(0,0,0);
            vec.x = System.BitConverter.ToSingle(packetBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            vec.y = System.BitConverter.ToSingle(packetBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            vec.z = System.BitConverter.ToSingle(packetBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            packet.packetPosition = vec;

            //Lists & Arrays
            List<int> usersToRecieve = new List<int>();
            int arraySize = System.BitConverter.ToInt32(packetBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            for (int i = 0; i < arraySize; i++)
            {
                usersToRecieve.Add(System.BitConverter.ToInt32(packetBytes.GetRange(intIndex, 4).ToArray(), 0));
                intIndex += 4;
            }
            packet.usersToRecieve = usersToRecieve;

            List<byte> packetInfo = new List<byte>();
            arraySize = System.BitConverter.ToInt32(packetBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            packetInfo = packetBytes.GetRange(intIndex, arraySize); intIndex += arraySize;

            packet.packetData = packetInfo.ToArray();

            return packet;
        }

        //GOID Serialization Data - Total Minimum Bytes: 42 bytes
        // - PrefabDomainID int32, 4 bytes
        // - PrefabID int32, 4 bytes
        // - Position sVec, 12 bytes
        // - Rotation sQuat, 16 bytes
        // - NetObjID int32, 4 bytes - consider just using relatedNetObjID instead
        // - isShared bool, 1 byte
        // - doImmediate bool, 1 byte
        // - NetFieldDefaults List
        //   - List Element Count int32, 4 bytes
        //      - Byte array length of element int32, 4 bytes
        //      - NetFieldPacket BinaryFormattered, ? bytes

        public static byte[] SerializeGOID(GameObjectInstantiateData gOID)
        {
            List<byte> objectAsBytes = new List<byte>();

            //Basic Fields

            byte[] prefabDomainID = System.BitConverter.GetBytes(gOID.prefabDomainID);
            objectAsBytes.AddRange(prefabDomainID);
            byte[] prefabID = System.BitConverter.GetBytes(gOID.prefabID);
            objectAsBytes.AddRange(prefabID);

            List<byte> positionBytes = new List<byte>();
            positionBytes.AddRange(System.BitConverter.GetBytes(gOID.position.x));
            positionBytes.AddRange(System.BitConverter.GetBytes(gOID.position.y));
            positionBytes.AddRange(System.BitConverter.GetBytes(gOID.position.z));
            objectAsBytes.AddRange(positionBytes);

            List<byte> rotationBytes = new List<byte>();
            rotationBytes.AddRange(System.BitConverter.GetBytes(gOID.rotation.x));
            rotationBytes.AddRange(System.BitConverter.GetBytes(gOID.rotation.y));
            rotationBytes.AddRange(System.BitConverter.GetBytes(gOID.rotation.z));
            rotationBytes.AddRange(System.BitConverter.GetBytes(gOID.rotation.w));
            objectAsBytes.AddRange(rotationBytes);

            byte[] netObjID = System.BitConverter.GetBytes(gOID.netObjID);
            objectAsBytes.AddRange(netObjID);

            objectAsBytes.AddRange(System.BitConverter.GetBytes(gOID.isShared));
            objectAsBytes.AddRange(System.BitConverter.GetBytes(gOID.doImmediate));

            //networkFieldPacket List
            //Amount of field defaults.
            if(gOID.fieldDefaults == null)
            {
                gOID.fieldDefaults = new List<NetworkFieldPacket>();
            }


            objectAsBytes.AddRange(System.BitConverter.GetBytes(gOID.fieldDefaults.Count));
            foreach(NetworkFieldPacket nFP in gOID.fieldDefaults)
            {
                byte[] serializedField = SerializeNetworkFieldPacket(nFP);
                objectAsBytes.AddRange(System.BitConverter.GetBytes(serializedField.Length)); //Length of serialized Network Field Packet
                objectAsBytes.AddRange(serializedField);

            }

            return objectAsBytes.ToArray();
        }


        public static GameObjectInstantiateData DeserializeGOID(byte[] givenBytes)
        {
            List<byte> gOIDBytes = new List<byte>();
            gOIDBytes.AddRange(givenBytes);

            GameObjectInstantiateData gOID = new GameObjectInstantiateData();
            int intIndex = 0;

            //Prefab info
            gOID.prefabDomainID = System.BitConverter.ToInt32(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            gOID.prefabID = System.BitConverter.ToInt32(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;

            //Vector3 position
            SerializableVector vec = new SerializableVector(0, 0, 0);
            vec.x = System.BitConverter.ToSingle(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            vec.y = System.BitConverter.ToSingle(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            vec.z = System.BitConverter.ToSingle(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            gOID.position = vec;

            //Quaternion rotation
            SerializableQuaternion rot = new SerializableQuaternion(0,0,0,0);
            rot.x = System.BitConverter.ToSingle(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            rot.y = System.BitConverter.ToSingle(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            rot.z = System.BitConverter.ToSingle(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            rot.w = System.BitConverter.ToSingle(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            gOID.rotation = rot;

            //NetObjID
            gOID.netObjID = System.BitConverter.ToInt32(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;

            //isShared/doImmediate booleans
            gOID.isShared = System.BitConverter.ToBoolean(gOIDBytes.GetRange(intIndex, 1).ToArray(), 0); intIndex += 1;
            gOID.doImmediate = System.BitConverter.ToBoolean(gOIDBytes.GetRange(intIndex, 1).ToArray(), 0); intIndex += 1;

            //NetworkFieldDefaults List
            List<NetworkFieldPacket> nFPs = new List<NetworkFieldPacket>();
            int nFPCount = System.BitConverter.ToInt32(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            for(int i = 0; i < nFPCount; i++)
            {
                int serializeLength = System.BitConverter.ToInt32(gOIDBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
                NetworkFieldPacket netFieldPacket = DeserializeNetworkFieldPacket(gOIDBytes.GetRange(intIndex, serializeLength).ToArray());
                nFPs.Add(netFieldPacket);
                intIndex += serializeLength;
            }
            gOID.fieldDefaults = nFPs;



            return gOID;
        }

        //NetworkFieldPacket Serializer - Total Minimum Calculatable Bytes: 9 bytes
        // - networkObjID int32, 4 bytes
        // - fieldName string byte length int16, 4 byte
        // - fieldName string, ? bytes
        // - jsonData string byte length int16, 4 byte
        // - jsonData string, ? bytes
        // - jsonDataTypeName string byte length int16, 4 byte
        // - jsonDataTypeName string, ? bytes
        // - immediateOnSelf boolean, 1 byte

        public static byte[] SerializeNetworkFieldPacket(NetworkFieldPacket nFP)
        {
            List<byte> objectAsBytes = new List<byte>();

            //NetObjID int32
            byte[] networkObjID = System.BitConverter.GetBytes(nFP.networkObjID);
            objectAsBytes.AddRange(networkObjID);

            //FieldName String
            byte[] fieldNameString = Encoding.ASCII.GetBytes(nFP.fieldName);
            byte[] fieldNameLength = System.BitConverter.GetBytes((short)fieldNameString.Length);
            objectAsBytes.AddRange(fieldNameLength);
            objectAsBytes.AddRange(fieldNameString);

            //JsonData string
            byte[] jsonData = Encoding.ASCII.GetBytes(nFP.data.jsonData);
            byte[] jsonDataLength = System.BitConverter.GetBytes((short)jsonData.Length);
            objectAsBytes.AddRange(jsonDataLength);
            objectAsBytes.AddRange(jsonData);

            //JsonDataTypeName string
            byte[] jsonDataTypeName = Encoding.ASCII.GetBytes(nFP.data.jsonDataTypeName);
            byte[] jsonDataTypeNameLength = System.BitConverter.GetBytes((short)jsonDataTypeName.Length);
            objectAsBytes.AddRange(jsonDataTypeNameLength);
            objectAsBytes.AddRange(jsonDataTypeName);

            //immediateOnSelf
            byte[] immediateOnSelf = System.BitConverter.GetBytes(nFP.immediateOnSelf);
            objectAsBytes.AddRange(immediateOnSelf);


            return objectAsBytes.ToArray();
        }

        public static NetworkFieldPacket DeserializeNetworkFieldPacket(byte[] givenBytes)
        {
            List<byte> nFPBytes = new List<byte>();
            nFPBytes.AddRange(givenBytes);
            NetworkFieldPacket nFP = new NetworkFieldPacket(-1,"",new JsonPacketObject("",""),false);
            int intIndex = 0;

            //Network Object ID
            nFP.networkObjID = System.BitConverter.ToInt32(nFPBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;

            //FieldName String
            short stringByteLength = System.BitConverter.ToInt16(nFPBytes.GetRange(intIndex, 2).ToArray(), 0); intIndex += 2;
            string fieldName = Encoding.ASCII.GetString(nFPBytes.GetRange(intIndex, stringByteLength).ToArray()); intIndex += stringByteLength;
            nFP.fieldName = fieldName;

            //JsonData String
            short jsonDataLength = System.BitConverter.ToInt16(nFPBytes.GetRange(intIndex, 2).ToArray(), 0); intIndex += 2;
            string jsonData = Encoding.ASCII.GetString(nFPBytes.GetRange(intIndex, jsonDataLength).ToArray()); intIndex += jsonDataLength;
            nFP.data.jsonData = jsonData;

            //JsonDataTypeName String
            short jsonDataTypeNameLength = System.BitConverter.ToInt16(nFPBytes.GetRange(intIndex, 2).ToArray(), 0); intIndex += 2;
            string jsonDataTypeName = Encoding.ASCII.GetString(nFPBytes.GetRange(intIndex, jsonDataTypeNameLength).ToArray()); intIndex += jsonDataTypeNameLength;
            nFP.data.jsonDataTypeName = jsonDataTypeName;


            return nFP;
        }

        //NetAuthPacket Serializer
        // - byte array authData
        //   - length int32, 4 bytes
        //   - the information in bytes, ? bytes
        // - steamID ulong, 8 bytes.
        // - udpPort int, 4 bytes
        // - buildID int, 4 bytes
        // - password string, 4+? bytes
        //   - length int
        //   - characters
        
        public static byte[] SerializeAuthPacket(NetworkAuthPacket sAP)
        {
            List<byte> objectAsBytes = new List<byte>();

            byte[] arrayLength = System.BitConverter.GetBytes(sAP.authData.Length);
            objectAsBytes.AddRange(arrayLength);
            objectAsBytes.AddRange(sAP.authData);

            byte[] steamID = System.BitConverter.GetBytes(sAP.steamID);
            objectAsBytes.AddRange(steamID);

            byte[] udpPort = System.BitConverter.GetBytes(sAP.udpPort);
            objectAsBytes.AddRange(udpPort);

            byte[] buildID = System.BitConverter.GetBytes(sAP.steamBuildID);
            objectAsBytes.AddRange(buildID);

            byte[] stringBytes = Encoding.ASCII.GetBytes(sAP.password);
            objectAsBytes.AddRange(System.BitConverter.GetBytes(stringBytes.Length));
            objectAsBytes.AddRange(stringBytes);


            return objectAsBytes.ToArray();
        }

        public static NetworkAuthPacket DeserializeAuthPacket(byte[] sAPBytes)
        {
            List<byte> byteObject = new List<byte>();
            byteObject.AddRange(sAPBytes);
            int intIndex = 0;

            int arrayLength = System.BitConverter.ToInt32(byteObject.GetRange(intIndex, 4).ToArray(),0); intIndex += 4;
            byte[] authData = byteObject.GetRange(intIndex, arrayLength).ToArray(); intIndex += arrayLength;

            ulong steamID = System.BitConverter.ToUInt64(byteObject.GetRange(intIndex,8).ToArray(),0); intIndex += 8;

            int udpPort = System.BitConverter.ToInt32(byteObject.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;

            int buildID = System.BitConverter.ToInt32(byteObject.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;


            int byteLength = System.BitConverter.ToInt32(byteObject.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            string password = Encoding.ASCII.GetString(byteObject.GetRange(intIndex, byteLength).ToArray()); intIndex += byteLength;

            return new NetworkAuthPacket(authData,steamID,password,buildID);
        }

        //RPCPacketData Serializer - Minimum Calculatable Bytes: 10 bytes
        // - packetOwnerID int16, 2 bytes
        // - networkObjectID int32, 4 bytes
        // - rpcIndex int16, 2 bytes
        // - parameters string list
        //   - element count int32, 4 bytes
        //      - element byte length int32, 4 bytes
        //      - element string, ? bytes
        // - paramTypes string list
        //   - element count int32, 4 bytes
        //      - element byte length int32, 4 bytes
        //      - element string,  ? bytes

        public static byte[] SerializeRPCPacketData(RPCPacketData rpc)
        {
            List<byte> objectAsBytes = new List<byte>();

            //Packet OwnerID as a SHORT
            byte[] packetOwnerID = System.BitConverter.GetBytes((short)rpc.packetOwnerID);
            objectAsBytes.AddRange(packetOwnerID);
            //NetworkObjectID int32
            byte[] networkObjectID = System.BitConverter.GetBytes(rpc.networkObjectID);
            objectAsBytes.AddRange(networkObjectID);
            //RPC Index as a SHORT
            byte[] rpcIndex = System.BitConverter.GetBytes((short)rpc.rpcIndex);
            objectAsBytes.AddRange(rpcIndex);


            //Parameters
            byte[] parameterElementCount = System.BitConverter.GetBytes(rpc.parameters.Count);
            objectAsBytes.AddRange(parameterElementCount);
            for (int i = 0; i < rpc.parameters.Count; i++)
            {
                byte[] arrayElement = Encoding.ASCII.GetBytes(rpc.parameters[i]);
                objectAsBytes.AddRange(System.BitConverter.GetBytes(arrayElement.Length)); //Element length
                objectAsBytes.AddRange(arrayElement);

            }
            //ParamTypes
            byte[] paramTypeElementCount = System.BitConverter.GetBytes(rpc.paramTypes.Count);
            objectAsBytes.AddRange(paramTypeElementCount);
            for (int i = 0; i < rpc.parameters.Count; i++)
            {
                byte[] arrayElement = Encoding.ASCII.GetBytes(rpc.paramTypes[i]);
                objectAsBytes.AddRange(System.BitConverter.GetBytes(arrayElement.Length)); //Element length
                objectAsBytes.AddRange(arrayElement);

            }
            return objectAsBytes.ToArray();
        }

        public static RPCPacketData DeserializeRPCPacketData(byte[] givenBytes)
        {
            List<byte> rpcBytes = new List<byte>();
            rpcBytes.AddRange(givenBytes);

            int intIndex = 0;
            
            short packetOwnerID = System.BitConverter.ToInt16(rpcBytes.GetRange(intIndex, 2).ToArray(), 0); intIndex += 2;
            int networkObjectID = System.BitConverter.ToInt32(rpcBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            short rpcIndex = System.BitConverter.ToInt16(rpcBytes.GetRange(intIndex, 2).ToArray(), 0); intIndex += 2;
            RPCPacketData rpc = new RPCPacketData(networkObjectID,rpcIndex,packetOwnerID);

            //Parameter List
            int parameterElementCount = System.BitConverter.ToInt32(rpcBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            for (int i = 0; i < parameterElementCount; i++)
            {
                int byteLength = System.BitConverter.ToInt32(rpcBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
                string element = Encoding.ASCII.GetString(rpcBytes.GetRange(intIndex, byteLength).ToArray()); intIndex += byteLength;
                rpc.parameters.Add(element);
            }
            //ParamType List
            int paraTypeElementCount = System.BitConverter.ToInt32(rpcBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            for (int i = 0; i < paraTypeElementCount; i++)
            {
                int byteLength = System.BitConverter.ToInt32(rpcBytes.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
                string element = Encoding.ASCII.GetString(rpcBytes.GetRange(intIndex, byteLength).ToArray()); intIndex += byteLength;
                rpc.paramTypes.Add(element);
            }


            return rpc;
        }

        //Serialize ConnectionPacket
        //bool immediateDisconnect - 1 byte
        //string reason
            //- 4 bytes length
            //- ? bytes string
        public static byte[] SerializeConnectionPacket(ConnectionPacket cPacket)
        {
            List<byte> connectionBytes = new List<byte>();
            
            byte[] disconnectByte = System.BitConverter.GetBytes(cPacket.immediateDisconnect);
            connectionBytes.AddRange(disconnectByte);

            byte[] stringBytes = Encoding.ASCII.GetBytes(cPacket.reason);
            connectionBytes.AddRange(System.BitConverter.GetBytes(stringBytes.Length));
            connectionBytes.AddRange(stringBytes);

            return connectionBytes.ToArray();
        }
        
        public static ConnectionPacket DeserializeConnectionPacket(byte[] cBytes)
        {
            List<byte> cByteList = new List<byte>(); cByteList.AddRange(cBytes);
            
            int intIndex = 0;

            bool disconnect = System.BitConverter.ToBoolean(cBytes,intIndex);
            intIndex += 1;
            int stringLength = System.BitConverter.ToInt32(cByteList.GetRange(intIndex, 4).ToArray(), 0); intIndex += 4;
            string reason = Encoding.ASCII.GetString(cByteList.GetRange(intIndex, stringLength).ToArray()); intIndex += stringLength;
            return new ConnectionPacket(disconnect,reason);
        }



        //Use not recommended as the BinaryFormatter has a bunch of overhead...
        //Try to make your own like above!
        //Some things like NetworkFieldPackets may be using this however in the future
        //I wanna transition them to using a custom serialization as the overhead is
        //intense. (28 byte overhead for an empty byte array :O)
        public static byte[] SerializeObject(object obj)
        {
            byte[] objectAsBytes;

            using (MemoryStream ms = new MemoryStream())
            {
                bF.Serialize(ms, obj);
                objectAsBytes = ms.ToArray();
            }

            return objectAsBytes;
        }

        //Same as the comments for SerializeObject, I don't recommend using this, read those
        //commments to know why...
        public static T DeserializeObject<T>(byte[] byteObject)
        {
            object o = null;
            using (MemoryStream ms = new MemoryStream())
            {
                ms.Write(byteObject, 0, byteObject.Length);
                ms.Seek(0, SeekOrigin.Begin);
                o = bF.Deserialize(ms);
            }
            return (T)o;
        }


        //Serialize int - Total Minimum Bytes: 4 bytes
        public static byte[] SerializeInt(int intValue)
        {
            byte[] bytes = new byte[4];

            bytes[0] = (byte)(intValue >> 24);
            bytes[1] = (byte)(intValue >> 16);
            bytes[2] = (byte)(intValue >> 8);
            bytes[3] = (byte)intValue;

            return bytes;
        }

        public static int DeserializeInt(byte[] intBytes)
        {
            int outInt = 0;

            outInt += (intBytes[0] << 24);
            outInt += (intBytes[1] << 16);
            outInt += (intBytes[2] << 8);
            outInt += intBytes[3];

            return outInt;
        }

        public static object JsonToObject(string jsonData, string jsonDataTypeName)
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
}
