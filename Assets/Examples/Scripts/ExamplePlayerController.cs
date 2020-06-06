using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExamplePlayerController : MonoBehaviour
{
    NetworkObject net;

    void Start()
    {
        net = GetComponent<NetworkObject>();
        net.onNetworkStart.AddListener(OnNetStart);
    }

    void OnNetStart()
    {
        net.UpdateField("position", new SerializableVector(transform.position),immediateOnSelf:true);
    }

    void FixedUpdate()
    {
        if(!net.initialized)
        {
            return;
        }

        if(!net.IsOwner())
        {
            if (net.fields[0].IsInitialized())
            {
                transform.position = ((SerializableVector)net.GetField("position")).ToVec3();
            }
            return;
        }
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(new Vector3(0f, 0.4f, 0f));
            net.UpdateField("position", new SerializableVector(transform.position));
        }
        else if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(new Vector3(0f, -0.4f, 0f));
            net.UpdateField("position", new SerializableVector(transform.position));
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(new Vector3(0.4f, 0.0f, 0f));
            net.UpdateField("position", new SerializableVector(transform.position));
        }
        else if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(new Vector3(-0.4f, 0.0f, 0f));
            net.UpdateField("position", new SerializableVector(transform.position));
        }
    }
}
