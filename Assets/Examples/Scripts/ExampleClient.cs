using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleClient : MonoBehaviour
{
    public NetClient netClient;

    public void ConnectToServer()
    {
        netClient = new NetClient();
        netClient.Initialize();
        netClient.ConnectToServer();
    }
}
