using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleHost : MonoBehaviour
{
    public NetServer netServer;

    public void StartServer()
    {
        netServer = new NetServer();
        netServer.Initialize();
    }

}
