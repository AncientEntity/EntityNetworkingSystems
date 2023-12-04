using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.Serialization;

namespace EntityNetworkingSystems
{
    public class ENSLobbyList : MonoBehaviour
    {
        public ServerJoinEvent serverJoinPressed = new ServerJoinEvent();
        
        [FormerlySerializedAs("serverButtonPrefab")] [Space]
        public GameObject lobbyButtonPrefab;
        [FormerlySerializedAs("serverScrollContent")] public RectTransform lobbyScrollContent;


        private Dictionary<Lobby,GameObject> lobbyButtons = new Dictionary<Lobby,GameObject>();
        private string currentSearchValue = "";

        private int lobbyFoundCount = 0;

        private void Start()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            ClearPreviousSearch();
        }

        public async void Refresh()
        {
            ClearPreviousSearch();

            Lobby[] foundLobbies = await AttemptFindLobbies();

            if (foundLobbies == null || foundLobbies.Length == 0)
            {
                return;
            }
            
            foreach (Lobby lobby in foundLobbies)
            {
                GameObject newLobbyButton = Instantiate(lobbyButtonPrefab, lobbyScrollContent);
                lobbyButtons[lobby] = newLobbyButton;
                newLobbyButton.GetComponent<ENSLobbyListButton>().SetupButton(lobby,lobbyFoundCount);
                newLobbyButton.GetComponent<ENSLobbyListButton>().button.onClick.AddListener(() =>
                {
                    serverJoinPressed.Invoke(lobby,ulong.Parse(lobby.GetData(LobbyDefinitions.LOBBY_HOSTID)));
                });
                lobbyButtons[lobby].SetActive(lobby.Owner.Name.ToLower().Contains(currentSearchValue));
                lobbyFoundCount += 1;
            }
            
        }

        public void OnSearchUpdated(string newSearch)
        {
            currentSearchValue = newSearch.ToLower();
            foreach(KeyValuePair<Lobby,GameObject> lobby in lobbyButtons)
            {
                lobby.Value.SetActive(lobby.Key.Owner.Name.ToLower().Contains(currentSearchValue));
            }
        }
        
        private async Task<Lobby[]> AttemptFindLobbies()
        {
            LobbyQuery lQ;
            lQ = SteamMatchmaking.LobbyList.WithMaxResults(100).FilterDistanceWorldwide();

            Lobby[] foundLobbies = await lQ.RequestAsync();
            return foundLobbies;
        }
        
        private void ClearPreviousSearch()
        {
            foreach (GameObject button in lobbyButtons.Values)
            {
                if(button == null) {continue;}
                Destroy(button);
            }

            lobbyButtons = new Dictionary<Lobby, GameObject>();
        }
        
        
        [System.Serializable]
        public class ServerJoinEvent : UnityEvent<Lobby,ulong> {} //Lobby, Target Join SteamID
    }
}