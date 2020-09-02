using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace EntityNetworkingSystems
{

    public class UnityPacketHandler : MonoBehaviour
    {
        public static UnityPacketHandler instance = null;
        public bool handlerRunning = false;
        public List<Packet> packetQueue = new List<Packet>();
        public int amountPerUpdate = 85;
        bool syncingBuffered = false;
#if UNITY_EDITOR
        [Space]
        public List<Packet> problemPackets = new List<Packet>();
#endif

        Coroutine runningHandler = null;

        void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(this);
            }
        }

        void CheckForHandlerCrash()
        {
            if(runningHandler == null)
            {
                runningHandler = StartCoroutine(HandleThreads());
                Debug.LogError("UnityPacketHandler crashed. It has been restarted.");
            }
        }

        public void StartHandler()
        {
            if (handlerRunning == true)
            {
                return; //Prevent multiple handlers.
            }
            runningHandler = StartCoroutine(HandleThreads());
            InvokeRepeating("CheckForHandlerCrash", 0f, 5f);
        }

        public IEnumerator HandleThreads()
        {
            int countTillUpdate = 0;
            handlerRunning = true;
            yield return new WaitForFixedUpdate();
            while (NetClient.instanceClient != null || NetServer.serverInstance != null)
            {
                if (packetQueue.Count > 0)
                {
                    Packet curPacket = packetQueue[0];
                    try
                    {
                        packetQueue.RemoveAt(0);
                    }
                    catch
                    {
                        //For some reason the packetQueue already had nothing.
                    }

                    if (curPacket == null)
                    {
                        continue;
                    }

                    try
                    {
                        ExecutePacket(curPacket);
                    } catch (System.Exception e)
                    {
                        Debug.LogError("Error handling packet." + e);
#if UNITY_EDITOR
                        problemPackets.Add(curPacket);
#endif
                    }

                    countTillUpdate++;
                    if (packetQueue.Count < 0 || countTillUpdate >= amountPerUpdate)
                    {
                        yield return new WaitForFixedUpdate();
                        countTillUpdate = 0;
                    }

                }
                else
                {
                    if (syncingBuffered)
                    {
                        //Means all packets have been synced
                        if (NetTools.onBufferedCompletion != null)
                        {
                            NetTools.onBufferedCompletion.Invoke();
                        }
                        syncingBuffered = false;
                    }

                    yield return new WaitForFixedUpdate();
                }
            }
            handlerRunning = false;
        }

        public void ExecutePacket(Packet curPacket)
        {
            if (curPacket.packetType == Packet.pType.gOInstantiate) //It gets instantiated NetTools.
            {
                NetworkObject nObj = null;
                GameObjectInstantiateData gOID = ENSSerialization.DeserializeGOID(curPacket.packetData);

                if (NetTools.clientID != curPacket.packetOwnerID || NetworkObject.NetObjFromNetID(gOID.netObjID) == null)
                {
                    GameObject g = null;
                    try
                    {
                        g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID], gOID.position.ToVec3(), gOID.rotation.ToQuaternion());
                    } catch
                    {
                        Debug.Log("Error NetInstantiating: domainID: " + gOID.prefabDomainID + ", prefabID: " + gOID.prefabID);
                        return;
                    }
                    nObj = g.GetComponent<NetworkObject>();
                    if (nObj == null)
                    {
                        nObj = g.AddComponent<NetworkObject>();

                    }

                    foreach (NetworkField defaultField in NetworkData.instance.networkPrefabList[gOID.prefabDomainID].defaultFields)
                    {
                        nObj.fields.Add(defaultField.Clone());
                    }
                    foreach (RPC defaultRPC in NetworkData.instance.networkPrefabList[gOID.prefabDomainID].defaultRpcs)
                    {
                        nObj.rpcs.Add(defaultRPC.Clone());
                    }


                    nObj.ownerID = curPacket.packetOwnerID;
                    nObj.prefabDomainID = gOID.prefabDomainID;
                    nObj.prefabID = gOID.prefabID;
                    nObj.networkID = gOID.netObjID;
                    nObj.sharedObject = gOID.isShared;

                    nObj.Initialize();
                    //nObj.DoRpcFieldInitialization();

                    if (nObj.onNetworkStart != null)
                    {
                        nObj.onNetworkStart.Invoke();
                    }

                    if (gOID.fieldDefaults != null)
                    {
                        foreach (NetworkFieldPacket nFP in gOID.fieldDefaults)
                        {
                            nFP.networkObjID = gOID.netObjID;
                            //Debug.Log(nFP.data);
                            nObj.UpdateField(nFP.fieldName, nFP.data.ToObject(), nFP.immediateOnSelf);
                        }
                    }

                }



            }
            else if (curPacket.packetType == Packet.pType.gODestroy)
            {
                //Debug.Log(curPacket.jsonData);
                //Debug.Log(curPacket.GetPacketData());
                //int netID = ENSSerialization.DeserializeInt(curPacket.packetData);
                NetworkObject found = NetworkObject.NetObjFromNetID(curPacket.relatesToNetObjID);
                if (found != null && (found.ownerID == curPacket.packetOwnerID || curPacket.serverAuthority || found.sharedObject))
                {
                    Destroy(found.gameObject);
                } else if (found == null)
                {
                    Debug.LogWarning("Couldn't find NetworkObject of ID: " + curPacket.relatesToNetObjID);
                }
            }
            else if (curPacket.packetType == Packet.pType.multiPacket)
            {
                //Debug.Log("Recieved buffered packets.");
                List<byte[]> packetByteInfo = (curPacket.GetPacketData<PacketListPacket>()).packets;
                lock (packetQueue)
                {
                    foreach(byte[] packetByte in packetByteInfo)
                    {
                        packetQueue.Add(ENSSerialization.DeserializePacket(packetByte));
                    }
                }

                syncingBuffered = true;
            }
            else if (curPacket.packetType == Packet.pType.loginInfo)
            {
                //Debug.Log("Login Info Packet Recieved.");
                NetTools.clientID = System.BitConverter.ToInt16(curPacket.packetData,0);//(curPacket.GetPacketData<PlayerLoginData>()).playerNetworkID;
                NetClient.instanceClient.clientID = NetTools.clientID;

                NetTools.onJoinServer.Invoke();

                //print("Test");
            }
            else if (curPacket.packetType == Packet.pType.netVarEdit)
            {
                //As of 2020-06-24 netVar's are handled in NetworkObject, but queued from here. This prevents preinitialized packets from getting through.

                NetworkFieldPacket nFP = curPacket.GetPacketData<NetworkFieldPacket>();
                NetworkObject netObj = NetworkObject.NetObjFromNetID(nFP.networkObjID);

                if(netObj == null)
                {
                    return;
                }

                if(netObj.initialized == false)
                {
                    //netObj.queuedNetworkPackets.Add(curPacket);
                    return;
                }

                if ((netObj.ownerID != curPacket.packetOwnerID && !curPacket.serverAuthority && !netObj.sharedObject) || (curPacket.packetOwnerID == NetTools.clientID && nFP.immediateOnSelf))
                {
                    return;
                }

                
                //Debug.Log("Seting NetVarEdit.");
                try
                {
                    netObj.SetFieldLocal(nFP.fieldName, nFP.data.ToObject());
                }
                catch (System.Exception e)
                {
                    Debug.LogError(e);
                }
            }
            else if (curPacket.packetType == Packet.pType.rpc)
            {
                //Debug.Log(curPacket.jsonData);
                RPCPacketData rPD = curPacket.GetPacketData<RPCPacketData>();
                NetworkObject nObj = NetworkObject.NetObjFromNetID(rPD.networkObjectID);
                if (nObj == null || (nObj.rpcs[rPD.rpcIndex].serverAuthorityRequired && !curPacket.serverAuthority) || (nObj.ownerID != curPacket.packetOwnerID && !nObj.sharedObject))
                {
                    return; //Means only server can run it.
                }
                nObj.rpcs[rPD.rpcIndex].InvokeRPC(rPD.ReturnArgs());
            }
        }


        //void Update()
        //{
        //    if(packetQueue.Count > 0)
        //    {
        //        Packet curPacket = packetQueue[0];
        //        packetQueue.RemoveAt(0);

        //        if(curPacket.packetType == Packet.pType.gOInstantiate)
        //        {
        //            GameObjectInstantiateData gOID = (GameObjectInstantiateData)curPacket.data;
        //            GameObject g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID], gOID.position.ToVec3(),Quaternion.identity);
        //            NetworkObject nObj = g.AddComponent<NetworkObject>();
        //            nObj.ownerID = curPacket.packetOwnerID;
        //            nObj.prefabDomainID = gOID.prefabDomainID;
        //            nObj.prefabID = gOID.prefabID;
        //            nObj.networkID = gOID.netObjID;

        //        } else if (curPacket.packetType == Packet.pType.gODestroy)
        //        {
        //            NetworkObject found = NetworkObject.NetObjFromNetID((int)curPacket.data);
        //            if(found != null && (found.ownerID == curPacket.packetOwnerID || curPacket.packetOwnerID == -1))
        //            {
        //                Destroy(found.gameObject);
        //            }
        //        }
        //        else if(curPacket.packetType == Packet.pType.allBuffered)
        //        {
        //            //Debug.Log("Recieved buffered packets.");
        //            List<Packet> packetInfo = (List<Packet>)curPacket.data;
        //            packetQueue.AddRange(packetInfo);
        //        } else if (curPacket.packetType == Packet.pType.loginInfo)
        //        {
        //            Debug.Log("Login Info Packet Recieved.");
        //            NetTools.clientID = ((PlayerLoginData)curPacket.data).playerNetworkID;
        //        }
        //    }
        //}


        public void QueuePacket(Packet packet)
        {
            packetQueue.Add(packet);
        }



    }

}