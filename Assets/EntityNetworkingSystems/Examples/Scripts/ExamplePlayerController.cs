using EntityNetworkingSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExamplePlayerController : MonoBehaviour
{
    NetworkObject net;
    SpriteRenderer sR;
    void Start()
    {
        sR = GetComponent<SpriteRenderer>();
        net = GetComponent<NetworkObject>();
        //net.onNetworkStart.AddListener(OnNetStart);
        OnNetStart();

        InvokeRepeating("SetRandomColor", 0f, 1f);
    }

    public void RandomColor(RPCArgs args)
    {
        sR = GetComponent<SpriteRenderer>();

        float r = args.GetNext<float>();
        float g = args.GetNext<float>();
        float b = args.GetNext<float>();
        sR.color = new Color(r,g, b);
    }
    
    public void OnValueChangeTestExample(FieldArgs args)
    {
        Debug.Log("Player Position Changed: " + args.GetValue<SerializableVector>().ToVec3());
    } 

    void SetRandomColor()
    {
        net.CallRPC("RandomColor", Packet.sendType.culledbuffered, Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
    }

    void OnNetStart()
    {
        net.UpdateField("ENS_Position", new SerializableVector(transform.position),immediateOnSelf:true);
    }

    void FixedUpdate()
    {
        if(!net.initialized)
        {
            return;
        }

        if(!net.IsOwner())
        {
            return;
        }
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(new Vector3(0f, 0.4f, 0f));
            net.UpdateField("ENS_Position", new SerializableVector(transform.position));
        }
        else if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(new Vector3(0f, -0.4f, 0f));
            net.UpdateField("ENS_Position", new SerializableVector(transform.position));
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(new Vector3(0.4f, 0.0f, 0f));
            net.UpdateField("ENS_Position", new SerializableVector(transform.position));
        }
        else if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(new Vector3(-0.4f, 0.0f, 0f));
            net.UpdateField("ENS_Position", new SerializableVector(transform.position));
        }
    }
}
