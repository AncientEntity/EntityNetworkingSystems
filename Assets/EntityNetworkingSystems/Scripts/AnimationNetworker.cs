using System.Collections;
using System.Collections.Generic;
//using System.Threading;
using UnityEngine;

namespace EntityNetworkingSystems {
    public class AnimationNetworker : MonoBehaviour
    {
        public List<AnimatorControllerParameter> animationBools = new List<AnimatorControllerParameter>();
        public List<bool> animationBoolsLastValue = new List<bool>();

        private Animator anim;
        private NetworkObject net;
        //private Thread handleAnimation;

        void Start()
        {
            anim = GetComponent<Animator>();
            net = GetComponent<NetworkObject>();
            if (anim == null)
            {
                Destroy(this);
            }
            foreach (AnimatorControllerParameter parameter in anim.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool)
                {
                    if (NetTools.isServer)
                    {
                        animationBools.Add(parameter);
                        animationBoolsLastValue.Add(anim.GetBool(parameter.name));
                    }

                    net.CreateField(parameter.name, anim.GetBool(parameter.name), NetworkField.valueInitializer.Boolean,true);
                    net.FieldAddOnChangeMethod(parameter.name, OnNetworkFieldBoolUpdate);
                }
            }
            if(net.IsOwner() && net.initialized)
            {
                StartCoroutine(HandleAnimationBoolPackets());
            } else if (net.initialized == false)
            {
                StartCoroutine(CheckToDoAnim());
            }
            
        }

        IEnumerator CheckToDoAnim()
        {
            yield return new WaitUntil(() => net.initialized);
            yield return new WaitForFixedUpdate();

            if(net.IsOwner())
            {
                StartCoroutine(HandleAnimationBoolPackets());
            }

            yield return new WaitForFixedUpdate();
        }

        IEnumerator HandleAnimationBoolPackets()
        {
            while(NetServer.serverInstance != null || NetClient.instanceClient != null)
            {

                int index = 0;
                foreach(AnimatorControllerParameter parameter in animationBools)
                {
                    if (anim.GetBool(parameter.name) != animationBoolsLastValue[index])
                    {
                        bool value = anim.GetBool(parameter.name);
                        animationBoolsLastValue[index] = value;

                        net.UpdateField(parameter.name, value,true);
                        //Debug.Log("Field Update Registered. " + parameter.name);

                    }
                    index += 1;
                }
                yield return new WaitForSeconds(1f / 30f);
            }
            Debug.Log("Handle Animation Bool Packets has ended.");
        }

        public void OnNetworkFieldBoolUpdate(FieldArgs args)
        {
            anim.SetBool(args.fieldName, args.GetValue<bool>());
            //Debug.Log("Field Updated... " + args.fieldName + " "+args.GetValue<bool>());
        }


    }
}