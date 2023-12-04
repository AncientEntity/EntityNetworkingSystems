using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using Steamworks.Data;
using TMPro;
using Color = UnityEngine.Color;

namespace EntityNetworkingSystems
{
    public class ENSServerListButton : MonoBehaviour
    {

        public Button button;
        public RawImage backgroundImage;
        public TextMeshProUGUI serverNameText;

        private ServerInfo myServer;

        public void SetupButton(ServerInfo steamServerInfo, int buttonID)
        {
            myServer = steamServerInfo;
            serverNameText.text = myServer.Name;
            
            if (buttonID % 2 == 1)
            {
                backgroundImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            }
        }
    }
    
}