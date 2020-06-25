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
        public int amountPerUpdate = 100;

        bool syncingBuffered = false;

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

                    if (curPacket.packetType == Packet.pType.gOInstantiate) //It gets instantiated NetTools.
                    {
                        NetworkObject nObj = null;
                        GameObjectInstantiateData gOID = (GameObjectInstantiateData)JsonUtility.FromJson<GameObjectInstantiateData>(curPacket.jsonData);

                        if (NetTools.clientID != curPacket.packetOwnerID || NetworkObject.NetObjFromNetID(gOID.netObjID) == null)
                        {
                            //GameObjectInstantiateData gOID = (GameObjectInstantiateData)curPacket.GetPacketData();
                            GameObject g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID], gOID.position.ToVec3(), gOID.rotation.ToQuaternion());
                            nObj = g.GetComponent<NetworkObject>();
                            if (nObj == null)
                            {
                                nObj = g.AddComponent<NetworkObject>();

                                foreach (NetworkField defaultField in NetworkData.instance.networkPrefabList[gOID.prefabDomainID].defaultFields)
                                {
                                    nObj.fields.Add(defaultField.Clone());
                                }
                                foreach (RPC defaultRPC in NetworkData.instance.networkPrefabList[gOID.prefabDomainID].defaultRpcs)
                                {
                                    nObj.rpcs.Add(defaultRPC.Clone());
                                }
                            }

                            nObj.ownerID = curPacket.packetOwnerID;
                            nObj.prefabDomainID = gOID.prefabDomainID;
                            nObj.prefabID = gOID.prefabID;
                            nObj.networkID = gOID.netObjID;
                            nObj.sharedObject = gOID.isShared;

                            nObj.Initialize();
                            if (nObj.onNetworkStart != null)
                            {
                                nObj.onNetworkStart.Invoke();
                            }
                        }



                    }
                    else if (curPacket.packetType == Packet.pType.gODestroy)
                    {
                        //Debug.Log(curPacket.jsonData);
                        //Debug.Log(curPacket.GetPacketData());
                        NetworkObject found = NetworkObject.NetObjFromNetID((int)curPacket.GetPacketData());
                        if (found != null && (found.ownerID == curPacket.packetOwnerID || curPacket.serverAuthority || found.sharedObject))
                        {
                            Destroy(found.gameObject);
                        }
                    }
                    else if (curPacket.packetType == Packet.pType.multiPacket)
                    {
                        //Debug.Log("Recieved buffered packets.");
                        List<Packet> packetInfo = ((PacketListPacket)curPacket.GetPacketData()).packets;
                        packetQueue.AddRange(packetInfo);

                        syncingBuffered = true;
                    }
                    else if (curPacket.packetType == Packet.pType.loginInfo)
                    {
                        Debug.Log("Login Info Packet Recieved.");
                        NetTools.clientID = ((PlayerLoginData)curPacket.GetPacketData()).playerNetworkID;
                        NetClient.instanceClient.clientID = NetTools.clientID;

                        NetTools.onJoinServer.Invoke();

                        //print("Test");
                    }
                    else if (curPacket.packetType == Packet.pType.netVarEdit)
                    {
                        //As of 2020-06-24 netVar's are handled in NetworkObject, but queued from here. This prevents preinitialized packets from getting through.

                        NetworkFieldPacket nFP = (NetworkFieldPacket)curPacket.GetPacketData();
                        NetworkObject netObj = NetworkObject.NetObjFromNetID(nFP.networkObjID);
                        if (netObj == null)
                        {
                            //Debug.LogError("Invalid Ownership on netvar",netObj);
                            continue; //Probably was instantiated on client but not server or vice versa.
                        }
                        //Debug.Log("Seting NetVarEdit.");

                        netObj.queuedNetworkPackets.Add(curPacket);

                        //NetworkObject's now manually handle network fields.
                        //netObj.SetFieldLocal(nFP.fieldName, nFP.data.ToObject());
                    }
                    else if (curPacket.packetType == Packet.pType.rpc)
                    {
                        //Debug.Log(curPacket.jsonData);
                        RPCPacketData rPD = (RPCPacketData)curPacket.GetPacketData();
                        NetworkObject nObj = NetworkObject.NetObjFromNetID(rPD.networkObjectID);
                        if (nObj == null || (nObj.rpcs[rPD.rpcIndex].serverAuthorityRequired && !curPacket.serverAuthority) || (nObj.ownerID != curPacket.packetOwnerID && !nObj.sharedObject))
                        {
                            continue; //Means only server can run it.
                        }
                        nObj.rpcs[rPD.rpcIndex].InvokeRPC(rPD.ReturnArgs());
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