using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleMoving : MonoBehaviour
{

    NetworkObject net;

    void Start()
    {
        net = GetComponent<NetworkObject>();
        net.fields[0].UpdateField(new SerializableVector(transform.position.x, transform.position.y, transform.position.z), net.networkID,immediateOnSelf:true);
    }

    // Update is called once per frame
    void Update()
    {
        SerializableVector sV = (SerializableVector)net.GetField("position");
        transform.position = sV.ToVec3();
    }
}
