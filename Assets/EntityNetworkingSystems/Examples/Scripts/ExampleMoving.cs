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

        foreach(ExamplePlayerController e in ExamplePlayerController.controllers)
        {
            if(e.GetComponent<NetworkObject>().IsOwner())
            {
                e.owned.Add(net);
                break;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(!net.initialized)
        {
            return;
        }
        //if(net.IsOwner() && Input.GetKey(KeyCode.J))
        //{
        //    net.UpdateField("ENS_Position",)
        //}
    }
}
