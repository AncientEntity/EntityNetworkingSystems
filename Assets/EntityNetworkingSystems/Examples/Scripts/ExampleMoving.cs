using EntityNetworkingSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleMoving : MonoBehaviour
{

    NetworkObject net;

    void Start()
    {
        net = GetComponent<NetworkObject>();
        net.onNetworkStart.AddListener(OnNetStart);
    }

    void OnNetStart()
    {
        net.fields[0].UpdateField<SerializableVector>(new SerializableVector(transform.position.x, transform.position.y, transform.position.z), net.networkID,immediateOnSelf:true);
    }

    // Update is called once per frame
    void Update()
    {
        if(!net.initialized)
        {
            return;
        }
        if (net.fields[0].IsInitialized())
        {
            SerializableVector sV = (SerializableVector)net.GetField<SerializableVector>("position");
            transform.position = sV.ToVec3();
        }
    }
}
