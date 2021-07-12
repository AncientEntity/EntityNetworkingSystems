using Steamworks;
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
        public bool dynamicUpdateRate = true;
        public int amountPerUpdate = 50;
        public int maxPerFrame = 200;
        bool syncingBuffered = false;
        [Space]
        public bool disableBeforeDestroy = false; //To prevent lag, disable the object, then queue it up to be destroyed.
        public int amountToDestroyPerFrame = 3;
        public List<GameObject> destroyQueue = new List<GameObject>();

#if UNITY_EDITOR
        [Space]
        public List<Packet> problemPackets = new List<Packet>();
        //public RPCPacketData lastRPCPacket;
#endif

        Coroutine runningHandler = null;
        private int baseAmounts;


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

            baseAmounts = amountPerUpdate / 2;
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

                    if (dynamicUpdateRate)
                    {
                        if (packetQueue.Count > amountPerUpdate)
                        {
                            amountPerUpdate = Mathf.Clamp(amountPerUpdate + 3, baseAmounts, maxPerFrame);
                        }
                        else
                        {
                            amountPerUpdate = Mathf.Clamp(amountPerUpdate - 3, baseAmounts, maxPerFrame);
                        }
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
                        //Destroy queued objects
                        if (disableBeforeDestroy && destroyQueue.Count > 0)
                        {
                            for (int i = 0; i < Mathf.Clamp(amountToDestroyPerFrame, 0, destroyQueue.Count); i++)
                            {
                                Destroy(destroyQueue[0]);
                                destroyQueue.RemoveAt(0);
                            }
                        }
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

                //Debug.Log(NetTools.clientID + ", " + curPacket.packetOwnerID);
                if (NetTools.clientID != curPacket.packetOwnerID || NetworkObject.NetObjFromNetID(gOID.netObjID) == null)
                {
                    if (!curPacket.serverAuthority && NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID].serverInstantiateOnly)
                    {
                        return; //If it is server only, and you aren't the server, don't do it.
                    }

                    GameObject g = NetworkData.instance.RequestPooledObject(gOID.prefabDomainID,gOID.prefabID);
                    if (g == null) //If a pooled object couldn't be found, make a new one.
                    {
                        try
                        {
                            g = Instantiate(NetworkData.instance.networkPrefabList[gOID.prefabDomainID].prefabList[gOID.prefabID].prefab, gOID.position.ToVec3(), gOID.rotation.ToQuaternion());
                        }
                        catch(System.Exception e)
                        {
                            Debug.Log("Error NetInstantiating: domainID: " + gOID.prefabDomainID + ", prefabID: " + gOID.prefabID + e);
                            return;
                        }
                    } else
                    {
                        //If pooled apply required position/rotation/etc.
                        g.transform.position = gOID.position.ToVec3();
                        g.transform.rotation = gOID.rotation.ToQuaternion();
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
                            nObj.SetFieldLocal(nFP.fieldName, nFP.data.ToObject());
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
                    if (disableBeforeDestroy)
                    {
                        if (NetworkData.instance.ResetPooledObject(found) == false)
                        {
                            destroyQueue.Add(found.gameObject);
                        }

                    }
                    else
                    {
                        if (NetworkData.instance.ResetPooledObject(found) == false) {
                            Destroy(found.gameObject);
                        }
                    }

                    if(NetTools.isServer && System.BitConverter.ToBoolean(curPacket.packetData,0)) //Check NetTools.NetDestroy but basically it is cullRelatedPackets.
                    {
                        NetTools.CullPacketsByNetworkID(curPacket.relatesToNetObjID);
                    }

                } else if (found == null)
                {
                    Debug.LogWarning("Couldn't find NetworkObject of ID: " + curPacket.relatesToNetObjID);
                }
            }
            else if (curPacket.packetType == Packet.pType.multiPacket)
            {
                //Debug.Log("Recieved buffered packets.");
                List<byte[]> packetByteInfo = curPacket.GetPacketData<PacketListPacket>().packets;
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
                PlayerLoginData pLD = curPacket.GetPacketData<PlayerLoginData>();
                NetTools.clientID = pLD.clientID;//System.BitConverter.ToInt16(curPacket.packetData,0);
                NetClient.instanceClient.clientID = NetTools.clientID;
                NetClient.instanceClient.serversSteamID = pLD.serverSteamID;

                if (NetTools.isServer)
                {
                    NetServer.serverInstance.myConnection = NetServer.serverInstance.GetPlayerByID(NetTools.clientID);
                }

                NetTools.onJoinServer.Invoke();

                //print("Test");
            }
            else if (curPacket.packetType == Packet.pType.netVarEdit)
            {
                
                NetworkFieldPacket nFP = ENSSerialization.DeserializeNetworkFieldPacket(curPacket.packetData);
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
                RPCPacketData rPD = ENSSerialization.DeserializeRPCPacketData(curPacket.packetData);
                NetworkObject nObj = NetworkObject.NetObjFromNetID(rPD.networkObjectID);

                if (nObj == null || (nObj.rpcs[rPD.rpcIndex].serverAuthorityRequired && !curPacket.serverAuthority) || (nObj.ownerID != curPacket.packetOwnerID && !nObj.sharedObject && !curPacket.serverAuthority))
                {
                    return; //Means only server can run it.
                }
                nObj.rpcs[rPD.rpcIndex].InvokeRPC(rPD.ReturnArgs());

            } else if (curPacket.packetType == Packet.pType.connectionPacket)
            {
                if(!curPacket.serverAuthority && curPacket.packetOwnerID != -1)
                {
                    return; //Prevents anyone from disconnecting everyone.
                }

                ConnectionPacket cP = ENSSerialization.DeserializeConnectionPacket(curPacket.packetData);
                if(cP.immediateDisconnect)
                {
                    NetClient.instanceClient.DisconnectFromServer();
                    NetTools.onLeaveServer.Invoke(cP.reason);
                }
            } else if (curPacket.packetType == Packet.pType.networkAuth && NetTools.isServer)
            {
                NetworkAuthPacket clientAuthPacket = ENSSerialization.DeserializeAuthPacket(curPacket.packetData);

                NetworkPlayer client = NetServer.serverInstance.GetPlayerByID(curPacket.packetOwnerID);

                if (!client.gotAuthPacket)
                {
                    //Check password.
                    if (NetServer.serverInstance.password != "" && clientAuthPacket.password != NetServer.serverInstance.password)
                    {
                        //Kick player wrong password.
                        NetServer.serverInstance.KickPlayer(client, "password");
                        return;
                    }
                    else if (SteamApps.BuildId != clientAuthPacket.steamBuildID)
                    {
                        //Kick player wrong version.
                        NetServer.serverInstance.KickPlayer(client, "version");
                        return;
                    }



                    client.steamID = clientAuthPacket.steamID;



                    //BeginAuthResult bAR = SteamUser.BeginAuthSession(clientAuthPacket.authData, clientAuthPacket.steamID);
                    SteamInteraction.instance.connectedSteamIDs.Add(clientAuthPacket.steamID);
                    SteamServer.UpdatePlayer(client.steamID, new Friend(client.steamID).Name, 0);
                    SteamServer.ForceHeartbeat();
                    //Debug.Log(bAR);
                    //SteamServer.ForceHeartbeat();


                    client.gotAuthPacket = true;
                }
            }
            //else if (curPacket.packetType == Packet.pType.networkAuth && NetTools.isServer)
            //{



            //}
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