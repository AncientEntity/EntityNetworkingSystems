using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleClient : MonoBehaviour
{
    public NetClient netClient;
    public List<NetworkObject> owned = new List<NetworkObject>();

    public void ConnectToServer()
    {
        netClient = new NetClient();
        netClient.Initialize();
        netClient.ConnectToServer();
        NetTools.onJoinServer.AddListener(delegate { InitializePlayer(); });
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
            GameObject g = NetTools.NetInstantiate(0, 0, new Vector3(Random.Range(-4.0f, 4.0f), Random.Range(-4.0f, 4.0f), 0),Quaternion.identity);
            owned.Add(g.GetComponent<NetworkObject>());
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            int randIndex = Random.Range(0, NetworkData.usedNetworkObjectInstances.Count);
            //print(randIndex);
            NetTools.NetDestroy(NetworkData.usedNetworkObjectInstances[randIndex]);
        }

        if (Input.GetKey(KeyCode.W))
        {
            foreach (NetworkObject nO in owned)
            {
                nO.fields[0].UpdateField(new SerializableVector(nO.transform.position.x, nO.transform.position.y + 0.04f, 0f), nO);
            }
        }


        if (Input.GetKeyDown(KeyCode.F11))
        {
            Screen.fullScreen = !Screen.fullScreen;
        }
    }

}
