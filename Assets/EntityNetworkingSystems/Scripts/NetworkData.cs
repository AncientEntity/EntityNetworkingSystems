using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkData : MonoBehaviour
{
    public static NetworkData instance = null;
    public static List<int> usedNetworkObjectInstances = new List<int>();

    public List<GameObjectList> networkPrefabList = new List<GameObjectList>();


    void Awake()
    {
        if(instance == null)
        {
            instance = this;
        } else
        {
            Destroy(this);
        }
    }

    public static void AddUsedNetID(int id)
    {
        usedNetworkObjectInstances.Add(id);
    }

}

[System.Serializable]
public class GameObjectList
{
    public string domainName;
    public GameObject[] prefabList;
}