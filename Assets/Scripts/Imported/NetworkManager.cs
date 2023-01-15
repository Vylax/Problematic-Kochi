using Riptide.Utils;
using System;
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

        public static bool isRemoteServer = false;

        private void Awake()
        {
            Singleton = this;

#if UNITY_STANDALONE_LINUX
            Debug.Log("Standalone Linux Detected");
            
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;
#endif
        }

        private void Start()
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

            Server = new Server();
            Server.ClientConnected += PlayerJoined;
            Server.ClientDisconnected += PlayerLeft;
            Server.RelayFilter = new MessageRelayFilter(typeof(MessageId), MessageId.SpawnPlayer, MessageId.PlayerMovement);

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                Debug.LogError($"i={i}, arg={args[i]}");
                if (args[i] == "-launch-as-server")
                {
                    Debug.LogError("Launched as server");
                    isRemoteServer = true;
                    UIManager.Singleton.HostClicked();
                }
            }

            if (isRemoteServer)
                return;

            Client = new Client();
            Client.Connected += DidConnect;
            Client.ConnectionFailed += FailedToConnect;
            Client.Disconnected += DidDisconnect;
        }

        private void FixedUpdate()
        {
            if (Server.IsRunning)
                Server.Update();
            
            if(!isRemoteServer)
                Client.Update();
        }

        private void OnApplicationQuit()
        {
            Server.Stop();
            if (!isRemoteServer)
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
            if (!isRemoteServer)
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
            foreach (Player player in Player.List.Values)
                if (player.Id != e.Client.Id)
                    player.SendSpawn(e.Client.Id);
        }

        private void PlayerLeft(object sender, ServerDisconnectedEventArgs e)
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
