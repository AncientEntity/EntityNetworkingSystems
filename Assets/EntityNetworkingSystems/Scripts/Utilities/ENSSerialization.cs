﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EntityNetworkingSystems
{
    public class ENSSerialization
    {
        //Packet Serialization Data - Total Minimum Bytes: 38 bytes.
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


    }
}
