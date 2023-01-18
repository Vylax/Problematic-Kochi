using Riptide.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riptide.Demos.PlayerHosted
{
    internal enum MessageId : ushort
    {
        SpawnPlayer = 1,
        PlayerMovement
    }

    public class NetworkManager : MonoBehaviour
    {
        private static NetworkManager _singleton;
        public static NetworkManager Singleton
        {
            get => _singleton;
            private set
            {
                if (_singleton == null)
                    _singleton = value;
                else if (_singleton != value)
                {
                    Debug.Log($"{nameof(NetworkManager)} instance already exists, destroying object!");
                    Destroy(value);
                }
            }
        }

        [SerializeField] private ushort port;
        [SerializeField] private ushort maxPlayers;
        [Header("Prefabs")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject localPlayerPrefab;

        public GameObject PlayerPrefab => playerPrefab;
        public GameObject LocalPlayerPrefab => localPlayerPrefab;

        internal Server Server { get; private set; }
        internal Client Client { get; private set; }

        public bool isHosting => Server.IsRunning;

        private void Awake()
        {
            Singleton = this;

#if UNITY_STANDALONE_LINUX
            Debug.Log("Distant server detected");
            
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
#endif
        }

        private void Start()
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

            InitializeServer();
            InitializeClient();

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                Debug.Log($"i={i}, arg={args[i]}");
                if (args[i] == "-launch-as-server")
                {
                    Debug.Log("Launched as server");
                    UIManager.Singleton.HostClicked();
                }
            }
        }

        private void InitializeServer()
        {
            Server = new Server();
            Server.ClientConnected += PlayerJoined;
            Server.ClientDisconnected += PlayerLeft;

            // TODO: Uncomment this to enable auto relaying
            //Server.RelayFilter = new MessageRelayFilter(typeof(MessageId), MessageId.PlayerMovement);
        }

        private void InitializeClient()
        {
            Client = new Client();
            Client.Connected += DidConnect; // Invoked when a connection to the server is established
            Client.ConnectionFailed += FailedToConnect;
            Client.ClientDisconnected += PlayerLeft; // Invoked when another non-local client disconnects
            Client.Disconnected += DidDisconnect;
        }

        //DEBUG:
        private void OnGUI()
        {
            Dictionary<ushort, Player> players = Player.List;
            foreach (Player player in players.Values)
            {
                GUILayout.Box($"{player.Id} {player.name}");
            }
        }

        private void FixedUpdate()
        {
            if (isHosting)
            {
                Server.Update();
            }
            else
            {
                Client.Update();
            }
        }

        private void OnApplicationQuit()
        {
            Server.Stop();
            Client.Disconnect();
        }

        internal void StartHost()
        {
            Server.Start(port, maxPlayers);
        }

        internal void JoinGame(string ipString)
        {
            Client.Connect($"{ipString}:{port}");
        }

        internal void LeaveGame()
        {
            Server.Stop();
            Client.Disconnect();
        }

        private void DidConnect(object sender, EventArgs e)
        {
            Player.Spawn(Client.Id, UIManager.Singleton.Username, Vector3.zero, true);
        }

        private void FailedToConnect(object sender, EventArgs e)
        {
            UIManager.Singleton.BackToMain();
        }

        private void PlayerJoined(object sender, ServerConnectedEventArgs e)
        {
            // Sends a spawn message to all the clients other than the one who triggered the event by connecting
            foreach (Player player in Player.List.Values)
                if (player.Id != e.Client.Id)
                    player.SendSpawn(e.Client.Id);
        }

        private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
        {
            Destroy(Player.List[e.Id].gameObject);
        }

        private void PlayerLeft(object sender, ServerDisconnectedEventArgs e) //Server Event override
        {
            Destroy(Player.List[e.Client.Id].gameObject);
        }

        private void DidDisconnect(object sender, DisconnectedEventArgs e)
        {
            foreach (Player player in Player.List.Values)
                Destroy(player.gameObject);

            UIManager.Singleton.BackToMain();
        }
    }
}
