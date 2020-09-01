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
    }

    // Update is called once per frame
    void Update()
    {
        if(!net.initialized)
        {
            return;
        }
    }
}
