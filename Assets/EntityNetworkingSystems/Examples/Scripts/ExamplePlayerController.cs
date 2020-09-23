using EntityNetworkingSystems;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExamplePlayerController : MonoBehaviour
{
    public static List<ExamplePlayerController> controllers = new List<ExamplePlayerController>();

    public List<NetworkObject> owned = new List<NetworkObject>();

    NetworkObject net;
    SpriteRenderer sR;
    InputWorker inputWorker;

    private int forcePositionSync = 0;

    private void Awake()
    {
        controllers.Add(this);
    }

    void Start()
    {
        sR = GetComponent<SpriteRenderer>();
        net = GetComponent<NetworkObject>();
        inputWorker = GetComponent<InputWorker>();
        net.onNetworkStart.AddListener(OnNetStart);

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
        if (net.IsOwner())
        {
            net.UpdateField("ENS_Position", new SerializableVector(transform.position), immediateOnSelf: true);
        }

        //foreach (ExampleMoving eM in FindObjectsOfType<ExampleMoving>())
        //{
        //    if (eM.GetComponent<NetworkObject>().ownerID == net.ownerID)
        //    {
        //        owned.Add(eM.GetComponent<NetworkObject>());
        //        break;
        //    }
        //}
    }

    void FixedUpdate()
    {
        if(!net.initialized)
        {
            return;
        }


        if (inputWorker.KeyPressed(KeyCode.W))
        {
            foreach (NetworkObject nO in owned)
            {
                nO.transform.position = new Vector3(nO.transform.position.x, nO.transform.position.y + 0.04f, 0f);
            }

            forcePositionSync += 1;
            if (forcePositionSync >= 40 && net.IsOwner())
            {
                foreach (NetworkObject nO in owned)
                {
                    nO.fields[0].UpdateField(new SerializableVector(nO.transform.position.x, nO.transform.position.y + 0.04f, 0f), nO);
                }
                forcePositionSync = 0;
            }
        }

        if (!net.IsOwner())
        {
            return;
        }
        if (inputWorker.KeyPressed(KeyCode.W))
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
