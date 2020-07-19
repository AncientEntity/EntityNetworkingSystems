using EntityNetworkingSystems;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace EntityNetworkingSystems
{

    public class NetTools : MonoBehaviour
    {
        public static int clientID = -1;
        public static bool isServer = false;
        public static bool isClient = false;
        public static bool isSingleplayer = false;

        public static PlayerJoinEvent onPlayerJoin = new PlayerJoinEvent();
        public static UnityEvent onJoinServer = new UnityEvent(); //Gets ran when the login packet finishes :D
        public static UnityEvent onBufferedCompletion = new UnityEvent(); //Gets ran when the buffered packets complete.

        public static Thread mainUnityThread = Thread.CurrentThread;

        //Useless cause NetInstantiate should check, but still here!
        public static GameObject ManagedInstantiate(int prefabDomain, int prefabID, Vector3 position, Quaternion rotation, Packet.sendType sT = Packet.sendType.buffered, bool isSharedObject = false)
        {
            //For if you aren't sure if it is a multiplayer or singleplayer game.

            if (isServer || isClient)
            {
                return NetInstantiate(prefabDomain, prefabID, position, rotation, sT, isSharedObject);
            }
            else
            {
                return Instantiate(NetworkData.instance.networkPrefabList[prefabDomain].prefabList[prefabID], position, rotation);
            }

        }

        public static GameObject NetInstantiate(int prefabDomain, int prefabID, Vector3 position, Quaternion rotation, Packet.sendType sT = Packet.sendType.buffered, bool isSharedObject = false, List<NetworkFieldPacket> fieldDefaults = null)

        {
            SerializableVector finalVector = new SerializableVector(position);
            SerializableQuaternion finalQuat = new SerializableQuaternion(rotation);

            GameObjectInstantiateData gOID = new GameObjectInstantiateData();
            gOID.position = finalVector;
            gOID.rotation = finalQuat;
            gOID.prefabDomainID = prefabDomain;
            gOID.prefabID = prefabID;
            gOID.isShared = isSharedObject;
            gOID.netObjID = GenerateNetworkObjectID();
            gOID.fieldDefaults = fieldDefaults;

            Packet p = new Packet(gOID);
            p.packetType = Packet.pType.gOInstantiate;
            p.packetSendType = sT;


            //if(sT == Packet.sendType.buffered && isServer)
            //{
            //    NetServer.serverInstance.bufferedPackets.Add(p);
            //}
            if (NetTools.IsMultiplayerGame())
            {
                NetClient.instanceClient.SendPacket(p);
            }

            GameObject g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID], gOID.position.ToVec3(), rotation);
            NetworkObject nObj = g.GetComponent<NetworkObject>();
            if (nObj == null)
            {
                nObj = g.AddComponent<NetworkObject>();

            }

            foreach (NetworkField defaultField in NetworkData.instance.networkPrefabList[gOID.prefabDomainID].defaultFields)
            {
                nObj.fields.Add(defaultField.Clone());
                //nObj.CreateField(defaultField.fieldName, null, init: defaultField.defaultValue, defaultField.shouldBeProximity);
            }
            foreach (RPC defaultRPC in NetworkData.instance.networkPrefabList[gOID.prefabDomainID].defaultRpcs)
            {
                nObj.rpcs.Add(defaultRPC.Clone());
            }

            nObj.ownerID = NetTools.clientID;
            nObj.prefabDomainID = gOID.prefabDomainID;
            nObj.prefabID = gOID.prefabID;
            nObj.networkID = gOID.netObjID;
            nObj.sharedObject = gOID.isShared;
            nObj.detectNetworkStarts = NetworkData.instance.networkPrefabList[gOID.prefabDomainID].detectNetworkStarts;

            nObj.Initialize();
            //nObj.DoRpcFieldInitialization();
            if (nObj.onNetworkStart != null)
            {
                nObj.onNetworkStart.Invoke();
            }
            //nObj.initialized = true;

            if (fieldDefaults != null)
            {
                foreach (NetworkFieldPacket nFP in fieldDefaults)
                {
                    nFP.networkObjID = gOID.netObjID;
                    nObj.UpdateField(nFP.fieldName, nFP.data.ToObject(), nFP.immediateOnSelf);
                }
            }

            return g;
        }



        public static void NetDestroy(NetworkObject netObj, Packet.sendType sT = Packet.sendType.buffered)
        {
            NetDestroy(netObj.networkID, sT);
        }

        public static void NetDestroy(int netID, Packet.sendType sT = Packet.sendType.buffered, bool destroyImmediate=false)
        {
            NetworkObject netObj = NetworkObject.NetObjFromNetID(netID);

            if (netObj == null)
            {
                Debug.LogError("Network Object doesn't exist. ID(" + netID + ")");
                return;
            }

            if (clientID == netObj.ownerID || isServer || netObj.sharedObject)
            {
                //Destroy(netObj.gameObject);

                Packet p = new Packet(Packet.pType.gODestroy, sT, netObj.networkID);
                p.relatesToNetObjID = netID;
                p.packetOwnerID = clientID;
                NetClient.instanceClient.SendPacket(p);

                if(destroyImmediate)
                {
                    Destroy(netObj);
                }

            }
            else
            {
                Debug.Log("You don't have authority over this object.");
            }
        }


        public static List<Packet> GenerateScenePackets()
        {
            //Will generate all the packets required to sync scenes for users. Useful for right when the server begins.
            //Only generates packets for NetworkObject's that are included inside of NetworkData's prefab domains.
            List<Packet> objPackets = new List<Packet>();
            foreach (NetworkObject netObj in NetworkObject.allNetObjs)
            {
                if (netObj.prefabID == -1 || netObj.prefabDomainID == -1)
                {
                    continue; //It isn't registered.
                }

                GameObjectInstantiateData gOID = new GameObjectInstantiateData();
                gOID.netObjID = netObj.networkID;
                gOID.position = new SerializableVector(netObj.transform.position);
                gOID.rotation = new SerializableQuaternion(netObj.transform.rotation);
                gOID.prefabID = netObj.prefabID;
                gOID.prefabDomainID = netObj.prefabDomainID;

                Packet p = new Packet(Packet.pType.gOInstantiate, Packet.sendType.nonbuffered, gOID);
                objPackets.Add(p);
            }
            return objPackets;
        }

        public static int GenerateNetworkObjectID()
        {
            bool found = false;
            while (found == false)
            {
                int random = Random.Range(0, int.MaxValue);
                if (NetworkData.usedNetworkObjectInstances.Contains(random) == false)
                {
                    found = true;
                    return random;
                }
            }
            return Random.Range(0, int.MaxValue);
        }



        public static bool IsMultiplayerGame()
        {
            return isServer || isClient;
        }

        public static void UpdatePlayerProximityPosition(int clientID, Vector3 position)
        {
            if (NetTools.isServer)
            {
                NetServer.serverInstance.GetPlayerByID(clientID).proximityPosition = position;
            }
        }

    }

    public class PlayerJoinEvent : UnityEvent<NetworkPlayer>
    {

    }
}

