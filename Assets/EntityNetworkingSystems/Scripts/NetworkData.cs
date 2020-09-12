using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using static EntityNetworkingSystems.NetworkField;

namespace EntityNetworkingSystems
{

    public class NetworkData : MonoBehaviour
    {
        public static NetworkData instance = null;
        public static List<int> usedNetworkObjectInstances = new List<int>();

        public List<GameObjectList> networkPrefabList = new List<GameObjectList>();

#if UNITY_EDITOR
        public string errorJson;
#endif

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

            //Initializing moved to just happening when instantiating over the network.
            //foreach(GameObjectList gOL in networkPrefabList)
            //{
            //    foreach(NetworkField netField in gOL.defaultFields)
            //    {
            //        netField.InitializeDefaultValue(null);
            //    }
            //}
        }

        public static void AddUsedNetID(int id)
        {
            usedNetworkObjectInstances.Add(id);
        }

        public static GameObject GetPrefab(int prefabDomain, int prefabID)
        {
            try
            {
                return instance.networkPrefabList[prefabDomain].prefabList[prefabID].prefab;
            } catch (System.Exception e)
            {
                Debug.LogError("Error Instantiating: pDomain: " + prefabDomain + ", prefabID: " + prefabID);
                return null;
            }
        }

        public void GeneratePooledObjects()
        {
            foreach(GameObjectList gOL in networkPrefabList)
            {
                gOL.GeneratePooledObjectsAll();
            }
        }

        public GameObject RequestPooledObject(int prefabDomain, int prefabID) 
        {
            return networkPrefabList[prefabDomain].RequestPooledObject(prefabID);
        }

        public bool ResetPooledObject(NetworkObject net)
        {
            return networkPrefabList[net.prefabDomainID].ResetPooledObject(net);
        }

    }

    [System.Serializable]
    public class GameObjectList
    {
        public string domainName;
        public List<PrefabEntry> prefabList = new List<PrefabEntry>();
        [Space]
        //These will automatically get applied to objects in this certain prefab domain when created, and if it doesn't have a netobj on it already.
        public bool detectNetworkStarts = false;
        public List<NetworkField> defaultFields;
        public RPC[] defaultRpcs;

        public void GeneratePooledObjectsAll()
        {
            foreach(PrefabEntry entry in prefabList)
            {
                entry.GeneratePooledObjects();   
            }
        }

        public GameObject RequestPooledObject(int prefabID)
        {
            return prefabList[prefabID].RequestPooledObject();
        }

        public bool ResetPooledObject(GameObject obj)
        {
            NetworkObject net = obj.GetComponent<NetworkObject>();
            if(net == null)
            {
                return false;
            }
            return ResetPooledObject(net);
        }

        public bool ResetPooledObject(NetworkObject obj)
        {
            return prefabList[obj.prefabID].ResetPooledObject(obj.gameObject);
        }

        [System.Serializable]
        public class PrefabEntry
        {
#if UNITY_EDITOR
            public string prefabName = "";
#endif
            public GameObject prefab;
            public int poolAmount = 0;
            public OnValueMethodData[] resetPoolMethods;

            private NetworkObject prefabNetwork; //The prefab's network object.

            private List<GameObject> pooledObjects = new List<GameObject>();
            private List<GameObject> inUseObjects = new List<GameObject>();
            private List<GameObject> beenUsedObjects = new List<GameObject>();


            public PrefabEntry(GameObject prefab, int poolAmount = 0)
            {
                this.prefab = prefab;
                this.poolAmount = poolAmount;

                prefabNetwork = prefab.GetComponent<NetworkObject>();
            }

            public void GeneratePooledObjects()
            {
                if(pooledObjects == null)
                {
                    pooledObjects = new List<GameObject>();
                }
                if(inUseObjects == null)
                {
                    inUseObjects = new List<GameObject>();
                }
                if(beenUsedObjects == null)
                {
                    beenUsedObjects = new List<GameObject>();
                }
                if(prefabNetwork == null)
                {
                    prefabNetwork = prefab.GetComponent<NetworkObject>();
                }

                for(int i = 0; i < poolAmount-pooledObjects.Count-inUseObjects.Count;i++)
                {
                    GameObject newPool = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                    newPool.transform.SetParent(NetworkData.instance.transform);
                    newPool.SetActive(false);

                    if(newPool.GetComponent<NetworkObject>() != null)
                    {
                        GameObject.Destroy(newPool.GetComponent<NetworkObject>());
                    }

                    pooledObjects.Add(newPool);
                }
            }

            public GameObject RequestPooledObject()
            {
                if(pooledObjects.Count > 0)
                {
                    while(pooledObjects[0] == null)
                    {
                        pooledObjects.RemoveAt(0);
                    }

                    GameObject obj = pooledObjects[0];
                    NetworkObject net = obj.GetComponent<NetworkObject>();
                    if(net == null)
                    {
                        net = obj.AddComponent<NetworkObject>();
                    }
                    obj.transform.SetParent(null);
                    SceneManager.MoveGameObjectToScene(obj, SceneManager.GetActiveScene());
                    inUseObjects.Add(obj);
                    pooledObjects.Remove(obj);
                    obj.SetActive(true);

                    //Give any NetworkFields/RPCs the prefab has back
                    if (prefabNetwork != null)
                    {
                        foreach (NetworkField nF in prefabNetwork.fields)
                        {
                            net.fields.Add(nF.Clone());
                        }
                        foreach (RPC rpc in prefabNetwork.rpcs)
                        {
                            net.rpcs.Add(rpc.Clone());
                        }
                    }

                    if (!beenUsedObjects.Contains(obj))
                    {
                        beenUsedObjects.Add(obj);
                    } else
                    {
                        
                        foreach (OnValueMethodData entry in resetPoolMethods)
                        {
                            obj.GetComponent(entry.componentTypeName).SendMessage(entry.methodName);
                        }
                    }
                    obj.transform.localScale = prefab.transform.localScale;
                    return obj;
                } else
                {
                    return null;
                }
            }

            public bool ResetPooledObject(GameObject obj)
            {
                bool couldPool = false;
                inUseObjects.Remove(obj);
                if(pooledObjects.Count < poolAmount)
                {
                    pooledObjects.Add(obj);
                    couldPool = true;
                }
                if(!beenUsedObjects.Contains(obj))
                {
                    beenUsedObjects.Add(obj);
                }
                obj.transform.SetParent(NetworkData.instance.transform);
                obj.SetActive(false);
                GameObject.Destroy(obj.GetComponent<NetworkObject>());
                return couldPool;
            }
        }

    }
}