using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EntityNetworkingSystems
{
    public class InputWorker : MonoBehaviour
    {
        //Each client/player should have it's own InputWorker. Must be attached to a NetworkObject that the client owns authority over.
        //They only get updated over the network when the keys get checked someone in code.

        public static List<InputWorker> allWorkers = new List<InputWorker>();
        public static InputWorker instance = null;

        public List<TrackedKey> keys = new List<TrackedKey>();
        public bool doInputDetection = true;

        private Dictionary<KeyCode, int> lookup = new Dictionary<KeyCode, int>(); //For slightly faster lookup. KeyCode=The Key, int = the index in keys List.

        private NetworkObject net = null;

        private void Awake()
        {
            net = GetComponent<NetworkObject>();
            allWorkers.Add(this);


            int index = 0;
            foreach (TrackedKey key in keys)
            {
                if (key.isNetworked)
                {
                    net.CreateField("IW" + key.displayName, "000", init: NetworkField.valueInitializer.None, false).reliable = key.reliable;
                    net.FieldAddOnChangeMethod("IW" + key.displayName, key.OnNetworkUpdate, false);
                }
                lookup.Add(key.key, index);
                index++;
            }
        }

        private void Start()
        {
            if (net.IsOwner()) //Only do this if this player that I am attached to is mine. Otherwise destroy self after initializing the fields required!
            {
                instance = this;
            }
        }

        public bool KeyDown(KeyCode key)
        {
            return keys[lookup[key]].CheckKey(net)[0];
        }
        public bool KeyPressed(KeyCode key)
        {
            return keys[lookup[key]].CheckKey(net)[1];
        }
        public bool KeyUp(KeyCode key)
        {
            return keys[lookup[key]].CheckKey(net)[2];
        }

        public static InputWorker GetInputWorker(int clientID)
        {
            foreach(InputWorker worker in allWorkers)
            {
                if(worker.net.ownerID == clientID)
                {
                    return worker;
                }
            }
            return null; //No input worker for the client.
        }





        [System.Serializable]
        public class TrackedKey
        {
            public string displayName = "";
            public KeyCode key;
            public bool keyDown;
            public bool keyPressed;
            public bool keyUp;
            [Space]
            public bool isNetworked = false;
            public bool reliable = true;

            public bool[] CheckKey(NetworkObject myNet)
            {
                if (!isNetworked || myNet.IsOwner())
                {
                    bool newKeyDown = Input.GetKeyDown(key);
                    bool newKeyPressed = Input.GetKey(key);
                    bool newKeyUp = Input.GetKeyUp(key);
                    if (isNetworked && (newKeyDown != keyDown || newKeyPressed != keyPressed || newKeyUp != keyUp))
                    {
                        myNet.UpdateField("IW" + displayName, new KeyNetworkData(newKeyDown, newKeyPressed, newKeyUp), false);

                        keyDown = newKeyDown;
                        keyPressed = newKeyPressed;
                        keyUp = newKeyUp;
                    }
                }
                return new bool[] { keyDown, keyPressed, keyUp };

            }

            public void OnNetworkUpdate(FieldArgs args)
            {
                try
                {
                    char[] keyData = args.GetValue<KeyNetworkData>().keyData.ToCharArray();
                    keyDown = keyData[0] == '1';
                    keyPressed = keyData[1] == '1';
                    keyUp = keyData[2] == '1';
                }
                catch
                {
                    //Can error when a client is first joining the game.
                }
            }


            //Tracked Key Serialization Order - Always Three Bytes
            // - keyDown boolean, 1 byte
            // - keyPressed boolean, 1 byte
            // - keyUp boolean , 1 byte
            //IMPORTANT: This Serialization only serializes the required booleans which are then sent over ENS's NetworkFields. displayName,key,isNetworked,etc is not transfered over.
            //As of making this NetworkField's use JSON to transfer information so these aren't being used.

            public static byte[] SerializeTrackedKey(TrackedKey key)
            {
                List<byte> objectAsBytes = new List<byte>();
                objectAsBytes.AddRange(System.BitConverter.GetBytes(key.keyDown));
                objectAsBytes.AddRange(System.BitConverter.GetBytes(key.keyPressed));
                objectAsBytes.AddRange(System.BitConverter.GetBytes(key.keyUp));
                return objectAsBytes.ToArray();
            }

            public static TrackedKey DeserializeTrackedKey(byte[] trackedAsBytes)
            {
                TrackedKey key = new TrackedKey();
                key.keyDown = System.BitConverter.ToBoolean(trackedAsBytes, 0);
                key.keyPressed = System.BitConverter.ToBoolean(trackedAsBytes, 1);
                key.keyUp = System.BitConverter.ToBoolean(trackedAsBytes, 2);
                return key;
            }

            [System.Serializable]
            class KeyNetworkData
            {
                public string keyData = "";

                public KeyNetworkData(bool keyDown, bool keyPressed, bool keyUp)
                {
                    if (keyDown)
                    {
                        keyData = keyData + "1";
                    }
                    else
                    {
                        keyData = keyData + "0";
                    }

                    if (keyPressed)
                    {
                        keyData = keyData + "1";
                    }
                    else
                    {
                        keyData = keyData + "0";
                    }

                    if (keyUp)
                    {
                        keyData = keyData + "1";
                    }
                    else
                    {
                        keyData = keyData + "0";
                    }
                }
            }
        }

    }
}