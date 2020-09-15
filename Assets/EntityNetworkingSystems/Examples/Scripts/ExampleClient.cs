using EntityNetworkingSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleClient : MonoBehaviour
{
    public NetClient netClient;
    public List<NetworkObject> owned = new List<NetworkObject>();

    public string ip;
    public int port;


    public void ConnectToServer()
    {
        netClient.Initialize();
        netClient.ConnectToServer(ip,port);
        NetTools.onJoinServer.AddListener(delegate { InitializePlayer(); });
    }

    public void Disconnect()
    {
        netClient.DisconnectFromServer();
    }


    //void Update()
    //{
    //    Debug.Log(NetTools.clientID);
    //}

    void InitializePlayer()
    {
        NetTools.NetInstantiate(0, 1, new Vector3(Random.Range(-4.0f, 4.0f), Random.Range(-4.0f, 4.0f)),Quaternion.identity);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            for (int i = 0; i < 50; i++)
            {
                GameObject g = NetTools.NetInstantiate(0, 0, new Vector3(Random.Range(-4.0f, 4.0f), Random.Range(-4.0f, 4.0f), 0), Quaternion.identity);
                owned.Add(g.GetComponent<NetworkObject>());
            }
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            int randIndex = Random.Range(0, NetworkObject.allNetObjs.Count);
            //print(randIndex);
            NetTools.NetDestroy(NetworkObject.allNetObjs[randIndex]);
            Debug.Log(NetworkData.usedNetworkObjectInstances[randIndex]);
        }



        if (Input.GetKeyDown(KeyCode.F11))
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }


    void OnDestroy()
    {
        netClient.DisconnectFromServer();
    }

}
