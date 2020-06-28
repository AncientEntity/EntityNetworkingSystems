using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EntityNetworkingSystems
{

    public class NetworkData : MonoBehaviour
    {
        public static NetworkData instance = null;
        public static List<int> usedNetworkObjectInstances = new List<int>();

        public List<GameObjectList> networkPrefabList = new List<GameObjectList>();

        public string errorJson;


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

            foreach(GameObjectList gOL in networkPrefabList)
            {
                foreach(NetworkField netField in gOL.defaultFields)
                {
                    netField.InitializeDefaultValue(null);
                }
            }
        }

        public static void AddUsedNetID(int id)
        {
            usedNetworkObjectInstances.Add(id);
        }

        public static GameObject GetPrefab(int prefabDomain, int prefabID)
        {
            try
            {
                return instance.networkPrefabList[prefabDomain].prefabList[prefabID];
            } catch (System.Exception e)
            {
                Debug.LogError("Error Instantiating: pDomain: " + prefabDomain + ", prefabID: " + prefabID);
                return null;
            }
        }

    }

    [System.Serializable]
    public class GameObjectList
    {
        public string domainName;
        public GameObject[] prefabList;
        [Space]
        //These will automatically get applied to objects in this certain prefab domain when created, and if it doesn't have a netobj on it already.
        public List<NetworkField> defaultFields;
        public RPC[] defaultRpcs;
    }
}