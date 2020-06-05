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

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            NetTools.NetInstantiate(0, 0, new Vector3(Random.Range(-4.0f, 4.0f), Random.Range(-4.0f, 4.0f), 0));
        }
        if(Input.GetKeyDown(KeyCode.F1))
        {
            int randIndex = Random.Range(0, NetworkData.usedNetworkObjectInstances.Count);
            //print(randIndex);
            NetTools.NetDestroy(NetworkData.usedNetworkObjectInstances[randIndex]);
        }

        if(Input.GetKeyDown(KeyCode.F11))
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }

}
