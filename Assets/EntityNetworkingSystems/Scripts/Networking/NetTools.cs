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

        public static PlayerLeaveEvent onLeaveServer = new PlayerLeaveEvent(); //When the client leaves the game. Either from disconnect/loss of connection. It gives a reason as a parameter.
        public static PlayerEvent onPlayerJoin = new PlayerEvent();
        public static PlayerEvent onPlayerDisconnect = new PlayerEvent();
        public static UnityEvent onJoinServer = new UnityEvent(); //Gets ran when the login packet finishes :D
        public static UnityEvent onBufferedCompletion = new UnityEvent(); //Gets ran when the buffered packets complete.
        public static UnityEvent onFailedServerConnection = new UnityEvent(); //When you cant connect to the server, most commonly when the server isn't up.

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
                return Instantiate(NetworkData.instance.networkPrefabList[prefabDomain].prefabList[prefabID].prefab, position, rotation);
            }

        }

        public static GameObject Instantiate(int prefabDomain, int prefabID, Vector3 position, Quaternion rotation, Packet.sendType sT = Packet.sendType.buffered, bool isSharedObject = false, List<NetworkFieldPacket> fieldDefaults = null)
        {
            return NetInstantiate(prefabDomain,prefabID,position,rotation,sT,isSharedObject,fieldDefaults);
        }

        public static GameObject NetInstantiate(int prefabDomain, int prefabID, Vector3 position, Quaternion rotation, Packet.sendType sT = Packet.sendType.buffered, bool isSharedObject = false, List<NetworkFieldPacket> fieldDefaults = null)

        {
            int netObjID = GenerateNetworkObjectID();

            if (NetTools.IsMultiplayerGame())
            {
                if(!NetTools.isServer && NetworkData.instance.networkPrefabList[prefabDomain].prefabList[prefabID].serverInstantiateOnly)
                {
#if UNITY_EDITOR
                    Debug.LogWarning("Tried to make server authority only object: domain: " + prefabDomain + ", id: " + prefabID);
#endif
                    return null; //If it is server only, and you aren't the server, don't do it.
                }

                SerializableVector finalVector = new SerializableVector(position);
                SerializableQuaternion finalQuat = new SerializableQuaternion(rotation);

                GameObjectInstantiateData gOID = new GameObjectInstantiateData();
                gOID.position = finalVector;
                gOID.rotation = finalQuat;
                gOID.prefabDomainID = prefabDomain;
                gOID.prefabID = prefabID;
                gOID.isShared = isSharedObject;
                gOID.netObjID = netObjID;
                gOID.fieldDefaults = fieldDefaults;

                Packet p = new Packet(Packet.pType.gOInstantiate,Packet.sendType.buffered,ENSSerialization.SerializeGOID(gOID));
                p.tag = NetworkData.instance.TagIDToTagName(NetworkData.instance.networkPrefabList[prefabDomain].defaultPacketTagID);


                //if(sT == Packet.sendType.buffered && isServer)
                //{
                //    NetServer.serverInstance.bufferedPackets.Add(p);
                //}
                NetClient.instanceClient.SendPacket(p);
                
            }

            //Try to get a pooled one first.
            GameObject g = NetworkData.instance.RequestPooledObject(prefabDomain,prefabID);

            if (g == null) //If there is no pooled remaining make a new one.
            {
                g = Instantiate(NetworkData.instance.networkPrefabList[prefabDomain].prefabList[prefabID].prefab, position, rotation);
            }
            else
            {
                //If pooled apply required position/rotation
                g.transform.position = position;
                g.transform.rotation = rotation;
            }

            bool addedNewNetworkObject = false; //Sets to true if the Prefab had no NetworkObject and one was added,
            
            NetworkObject nObj = g.GetComponent<NetworkObject>();
            if (nObj == null)
            {
                nObj = g.AddComponent<NetworkObject>();
                addedNewNetworkObject = true;
            }


            nObj.ownerID = NetTools.clientID;
            nObj.prefabDomainID = prefabDomain;
            nObj.prefabID = prefabID;
            nObj.networkID = netObjID;
            nObj.sharedObject = isSharedObject;
            if (addedNewNetworkObject)
            {
                nObj.detectNetworkStarts = NetworkData.instance.networkPrefabList[prefabDomain].detectNetworkStarts;
            }

            nObj.Initialize();
            if (nObj.onNetworkStart != null)
            {
                nObj.onNetworkStart.Invoke();
            }

            if (fieldDefaults != null)
            {
                foreach (NetworkFieldPacket nFP in fieldDefaults)
                {
                    nFP.networkObjID = netObjID;
                    nObj.SetFieldLocal(nFP.fieldName,nFP.data.ToObject());
                    //Instead just update it in UnityPacketHandler for everyone else, preventing extra packets and data from being send and lagging.
                    //nObj.UpdateField(nFP.fieldName, nFP.data.ToObject(), nFP.immediateOnSelf);
                }
            }

            return g;
        }






        public static void Destroy(NetworkObject netObj, Packet.sendType sT = Packet.sendType.buffered)
        {
            NetDestroy(netObj, sT);
        }

        public static void NetDestroy(NetworkObject netObj, Packet.sendType sT = Packet.sendType.buffered)
        {
            if(netObj == null)
            {
                Debug.LogError("Object reference not set.");
                return;
            }

            NetDestroy(netObj.networkID, sT);
        }

        public static void NetDestroy(int netID, Packet.sendType sT = Packet.sendType.buffered, bool destroyImmediate=false, bool cullRelatedPackets=true)
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

                Packet p = new Packet(Packet.pType.gODestroy, sT, System.BitConverter.GetBytes(cullRelatedPackets));
                p.relatesToNetObjID = netID;
                p.packetOwnerID = clientID;
                p.tag = NetworkData.instance.TagIDToTagName(NetworkData.instance.networkPrefabList[netObj.prefabDomainID].defaultPacketTagID);
                NetClient.instanceClient.SendPacket(p);

                if(destroyImmediate)
                {
                    if (!UnityPacketHandler.instance.disableBeforeDestroy)
                    {
                        //Pool the object, if it doesn't pool, destroy it.
                        if (NetworkData.instance.ResetPooledObject(netObj) == false)
                        {
                            Destroy(netObj);
                        }
                    } else
                    {
                        if (NetworkData.instance.ResetPooledObject(netObj) == false)
                        {
                            UnityPacketHandler.instance.destroyQueue.Insert(0, netObj.gameObject);
                            netObj.gameObject.SetActive(false);
                        }
                    }
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

        public static int GenerateNetworkObjectID(bool doSafetyCheck=false)
        {
            bool found = false;
            while (found == false)
            {
                int random = Random.Range(int.MinValue, int.MaxValue);
                if (!doSafetyCheck || NetworkData.usedNetworkObjectInstances.Contains(random) == false)
                {
                    found = true;
                    return random;
                }
            }
            return Random.Range(0, int.MaxValue);
        }



        public static bool IsMultiplayerGame()
        {
            return !isSingleplayer;
        }

        public static void UpdatePlayerProximityPosition(int clientID, Vector3 position)
        {
            if (NetTools.isServer)
            {
                NetworkPlayer netPlayer = NetServer.serverInstance.GetPlayerByID(clientID);
                if(netPlayer != null)
                {
                    netPlayer.proximityPosition = position;
                }
            }
        }

        public static void CullPacketsByNetworkID(int networkID)
        {
            if (NetServer.serverInstance.bufferedPackets.ContainsKey(networkID.ToString()))
            {
                NetServer.serverInstance.bufferedPackets.Remove(networkID.ToString());
            }
        }

        public static void CullPacketsByTag(string tag)
        {
            if(NetServer.serverInstance.bufferedPackets.ContainsKey(tag))
            {
                foreach (Packet packet in NetServer.serverInstance.bufferedPackets[tag])
                {
                    CullPacketsByNetworkID(packet.relatesToNetObjID);
                }
                NetServer.serverInstance.bufferedPackets.Remove(tag);
            }
        }

        public static bool ENSManagingSteam()
        {
            if((NetServer.serverInstance != null && NetServer.serverInstance.steamAppID == -1) || (NetClient.instanceClient != null && NetClient.instanceClient.steamAppID == -1))
            {
                return false;
            }
            return true;
        }

        public static void GetNetChildrenRecursive(Transform objTransform, List<int> childrenList)
        {
            if (objTransform == null)
            {
                return;
            }
            
            for (int i = 0; i < objTransform.childCount; i++)
            {
                Transform child = objTransform.GetChild(i);
                NetworkObject childNet = child.GetComponent<NetworkObject>();
                if (childNet != null && childNet.initialized && childNet.networkID != -1)
                {
                    childrenList.Add(childNet.networkID);
                }
                GetNetChildrenRecursive(child,childrenList);
            }
        }
        
    }



    public class PlayerEvent : UnityEvent<NetworkPlayer>
    {

    }

    public class PlayerLeaveEvent : UnityEvent<string>
    {
        //String should be the reason...
    }
}

