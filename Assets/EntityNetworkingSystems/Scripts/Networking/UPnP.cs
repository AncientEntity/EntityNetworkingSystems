using Mono.Nat;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace EntityNetworkingSystems.Nat
{
    public class UPnP
    {
        //https://github.com/lontivero/Open.NAT
        //https://github.com/alanmcgovern/Mono.Nat/blob/master/Mono.Nat.Console/Main.cs

        private static INatDevice device;
        private static List<Mapping> currentMappings = new List<Mapping>();

        public static void Init()
        {
            if(device != null)
            {
                return; //Already initialized.
            }
            NatUtility.DeviceFound += DeviceFound;
            NatUtility.StartDiscovery();
            Debug.Log("ENS Nat Initialized");
        }

        public static async void Shutdown()
        {
            if (currentMappings != null && device != null)
            {
                foreach(Mapping m in currentMappings)
                {
                    await device.DeletePortMapAsync(m);
                }
            }
            Debug.Log("Port Mappings Cleaned Up");
        }
        
        public static async Task<Mapping> CreateNewMapping(Mapping portMapping)
        {
            try
            {
                if (device != null)
                {
                    //Create Mapping
                    //Debug.Log("Creating uPnP Mapping: protocol=" + portMapping.Protocol + "ports=" + portMapping.PublicPort);
                    Mapping createdMap = await device.CreatePortMapAsync(portMapping);
                    Debug.Log("Created Map: protocol=" + createdMap.Protocol + "ports=" + createdMap.PublicPort);
                    currentMappings.Add(createdMap);
                    return createdMap;
                }
            } catch (System.Exception e)
            {
                Debug.LogWarning("Error Caught: "+e);
            }
            return null;
        }


        private static async void DeviceFound(object sender, DeviceEventArgs args)
        {
            try
            {
                //Get Device Info
                device = args.Device;
                //Debug.Log("Device Protocol: " + device.NatProtocol);
                //Debug.Log("Device Name: " + device.GetType().Name);
                //Debug.Log("Device External IP: " + (await device.GetExternalIPAsync()).ToString());


                //try
                //{
                //    foreach (Mapping m in device.GetAllMappings())
                //    {
                //        Debug.Log("Retrieved uPnP Mapping: protocol=" + m.Protocol + "ports=" + m.PublicPort);
                //    }
                //}
                //catch (System.Exception e)
                //{
                //    Debug.LogError("Couldn't get specific mapping... " + e);
                //}

            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
            NatUtility.StopDiscovery();
        }
    }
}