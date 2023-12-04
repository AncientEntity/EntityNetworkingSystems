using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace EntityNetworkingSystems
{
    public class ENSServerList : MonoBehaviour
    {
        public ServerJoinEvent serverJoinPressed = new ServerJoinEvent();
        
        [Space]
        public GameObject serverButtonPrefab;
        public RectTransform serverScrollContent;


        private Dictionary<ServerInfo,GameObject> serverButtons = new Dictionary<ServerInfo,GameObject>();
        private Steamworks.ServerList.Base serverRequest;
        private int serversFound = 0;
        private string currentSearchValue = "";

        private void Start()
        {
            Refresh();
        }

        private void OnDestroy()
        {
            ClearPreviousSearch();
        }

        public void Refresh()
        {
            ClearPreviousSearch();

            serverRequest = new Steamworks.ServerList.Friends();
            serverRequest.OnChanges += OnServersUpdated;
            serverRequest.RunQueryAsync();
        }

        private void OnServersUpdated()
        {
            if(serverRequest.Responsive.Count == 0) {return;}

            foreach (ServerInfo server in serverRequest.Responsive)
            {
                GameObject newServerButton = Instantiate(serverButtonPrefab, serverScrollContent);
                serverButtons.Add(server,newServerButton);
                newServerButton.GetComponent<ENSServerListButton>().SetupButton(server,serversFound);
                newServerButton.GetComponent<ENSServerListButton>().button.onClick.AddListener(() =>
                {
                    serverJoinPressed.Invoke(server);
                });
                serverButtons[server].SetActive(server.Name.ToLower().Contains(currentSearchValue));
                serversFound += 1;
            }
            serverRequest.Responsive.Clear();
        }

        public void OnSearchUpdated(string newSearch)
        {
            currentSearchValue = newSearch.ToLower();
            foreach(KeyValuePair<ServerInfo,GameObject> server in serverButtons)
            {
                server.Value.SetActive(server.Key.Name.ToLower().Contains(currentSearchValue));
            }
        }
        
        private void ClearPreviousSearch()
        {
            if (serverRequest != null)
            {
                serverRequest.Cancel();
                serverRequest = null;
            }

            foreach (GameObject button in serverButtons.Values)
            {
                if(button == null) {continue;}
                Destroy(button);
            }

            serverButtons = new Dictionary<ServerInfo, GameObject>();
        }
        
        
        [System.Serializable]
        public class ServerJoinEvent : UnityEvent<ServerInfo> {}
    }
}