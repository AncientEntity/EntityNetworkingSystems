using System.Collections;
using System.Collections.Generic;
//using System.Threading;
using UnityEngine;

namespace EntityNetworkingSystems {
    public class AnimationNetworker : MonoBehaviour
    {
        public List<AnimatorControllerParameter> animationBools = new List<AnimatorControllerParameter>();
        public List<bool> animationBoolsLastValue = new List<bool>();
        public bool manageSpriteFlips = true;



        private Animator anim;
        private NetworkObject net;
        private SpriteRenderer sR;
        //private Thread handleAnimation;

        private bool lastSpriteFlipXValue = false;
        private bool lastSpriteFlipYValue = false;

        private bool initialized = false;

        private void Start()
        {
            if(!initialized)
            {
                Initialize(); //Gets ran in the NetworkObject's Initialize() if added.
            }
        }

        public void Initialize()
        {
            anim = GetComponent<Animator>();
            net = GetComponent<NetworkObject>();
            sR = GetComponent<SpriteRenderer>();
            if (anim == null)
            {
                Destroy(this);
            }

            if(sR == null)
            {
                manageSpriteFlips = false;
            }

            foreach (AnimatorControllerParameter parameter in anim.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Bool)
                {
                    animationBools.Add(parameter);
                    animationBoolsLastValue.Add(anim.GetBool(parameter.name));

                    net.CreateField(parameter.name, anim.GetBool(parameter.name), NetworkField.valueInitializer.Boolean,false,false);
                    net.FieldAddOnChangeMethod(parameter.name, OnNetworkFieldBoolUpdate);
                    //net.FieldAddStringChangeMethod(parameter.name, "OnNetworkFieldBoolUpdate", "EntityNetworkingSystemsAnimationNetworker");
                }
            }

            if(manageSpriteFlips)
            {
                net.CreateField("SRFlipX", sR.flipX, init: NetworkField.valueInitializer.Boolean, true,false);
                net.CreateField("SRFlipY", sR.flipX, init: NetworkField.valueInitializer.Boolean, true,false);

                net.FieldAddOnChangeMethod("SRFlipX", OnNetworkFieldFlipX);
                net.FieldAddOnChangeMethod("SRFlipY", OnNetworkFieldFlipY);
            }


            if(net.IsOwner())
            {
                StartCoroutine(HandleAnimationBoolPackets());
            }

            initialized = true;
        }

        //IEnumerator CheckToDoAnim()
        //{
        //    yield return new WaitUntil(() => net.initialized);
        //    yield return new WaitForFixedUpdate();

        //    if(net.IsOwner())
        //    {
        //        StartCoroutine(HandleAnimationBoolPackets());
        //    }

        //    yield return new WaitForFixedUpdate();
        //}

        IEnumerator HandleAnimationBoolPackets()
        {
            while(NetServer.serverInstance != null || NetClient.instanceClient != null)
            {

                //Animation checking
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

                //SpriteRenderer flip checking
                if(manageSpriteFlips)
                {
                    if (lastSpriteFlipXValue != sR.flipX)
                    {
                        net.UpdateField("SRFlipX", sR.flipX, true);
                        lastSpriteFlipXValue = sR.flipX;
                    }
                    if (lastSpriteFlipYValue != sR.flipY)
                    {
                        net.UpdateField("SRFlipY", sR.flipY, true);
                        lastSpriteFlipYValue = sR.flipY;
                    }
                }

                yield return new WaitForSeconds(1f / 25f);
            }
            Debug.Log("Handle Animation Bool Packets has ended.");
        }

        public void OnNetworkFieldBoolUpdate(FieldArgs args)
        {
            if (net.IsOwner())
            {
                return;
            }
            anim.SetBool(args.fieldName, args.GetValue<bool>());
            //Debug.Log("Field Updated... " + args.fieldName + " "+args.GetValue<bool>());
        }

        public void OnNetworkFieldFlipX(FieldArgs args)
        {
            if (net.IsOwner())
            {
                return;
            }
            sR.flipX = args.GetValue<bool>();
        }

        public void OnNetworkFieldFlipY(FieldArgs args)
        {
            if (net.IsOwner())
            {
                return;
            }
            sR.flipX = args.GetValue<bool>();
        }


    }
}