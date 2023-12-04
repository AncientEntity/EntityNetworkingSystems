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
    public class ENSLobbyListButton : MonoBehaviour
    {

        public Button button;
        public RawImage backgroundImage;
        public TextMeshProUGUI serverNameText;

        private Lobby myLobby;

        public void SetupButton(Lobby steamLobbyInfo, int buttonID)
        {
            myLobby = steamLobbyInfo;
            myLobby.Refresh();
            serverNameText.text = myLobby.GetData(LobbyDefinitions.LOBBY_NAME);
            
            if (buttonID % 2 == 1)
            {
                backgroundImage.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            }
        }
    }
    
}