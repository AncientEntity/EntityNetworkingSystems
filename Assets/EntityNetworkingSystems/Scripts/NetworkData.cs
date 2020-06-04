﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkData : MonoBehaviour
{
    public static NetworkData instance = null;

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

}

[System.Serializable]
public class GameObjectList
{
    public GameObject[] prefabList;
}