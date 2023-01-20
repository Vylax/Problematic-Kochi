using Riptide;
using Riptide.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal enum MessageId : ushort
{
    //SpawnPlayer = 1,
    NewRaider = 1,
    PlayerMovement,
    PlayerRegister,
    PlayerStatus,
    SyncRaider,
    //SpawnPlayer // TODO REMOVE IT!!!
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
        foreach(Player.Status status in Enum.GetValues(typeof(Player.Status)))
        {
            if (Player.localPlayer != null && Player.localPlayer.isRegistered && GUILayout.Button($"SetStatus({status})"))
            {
                Player.localPlayer.ClientAskSetStatus(status);
            }
        }

        if (Player.localPlayer != null && Player.localPlayer.isRegistered && GUILayout.Button($"Ready? {UIManager.Singleton.playerIsReadyToJoinRaid}"))
        {
            UIManager.Singleton.playerIsReadyToJoinRaid = !UIManager.Singleton.playerIsReadyToJoinRaid;
        }

        GUILayout.Box($"isHosting={isHosting}");
        if(Player.localPlayer != null)
            GUILayout.Box($"Local Player Status={Player.localPlayer.status}");

        Dictionary<ushort, Player> players = Player.List;
        Dictionary<ushort, PlayerCharacter> playersChar = Player.ClientListRaiders;
        if (isHosting)
        {
            foreach (Player player in players.Values)
            {
                GUILayout.Box($"Id:{player.Id}, Name:{player.username}, Status:{player.status}");
            }
        }
        else
        {
            foreach (PlayerCharacter player in playersChar.Values)
            {
                GUILayout.Box($"Id:{player.Id}, Name:{player.username}, Status:{player.status}");
            }
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
        // Create the local Player instance with status Connected
        Player.localPlayer = new Player(Client.Id, UIManager.Singleton.Username, Player.Status.Connected);

        // Ask server to register the Player
        Player.localPlayer.AskRegister();
    }

    private void FailedToConnect(object sender, EventArgs e)
    {
        UIManager.Singleton.BackToMain();
    }

    private void PlayerJoined(object sender, ServerConnectedEventArgs e)
    {
        // TODO
        // Sends a spawn message to all the clients other than the one who triggered the event by connecting
        /*foreach (OldPlayer player in OldPlayer.List.Values)
            if (player.Id != e.Client.Id)
                player.SendSpawn(e.Client.Id);*/
    }

    private void PlayerLeft(object sender, ClientDisconnectedEventArgs e)
    {
        // TODO: Remove from Raiders List and Destroy
        // TODO: also think about the disconnection handling and join back while in raid problem
        //Destroy(OldPlayer.List[e.Id].gameObject);
    }

    private void PlayerLeft(object sender, ServerDisconnectedEventArgs e) //Server Event override
    {
        // TODO: Remove from List and send messages to client and then Destroy
        // TODO: also think about the disconnection handling and join back while in raid problem
        //Destroy(OldPlayer.List[e.Client.Id].gameObject);
    }

    private void DidDisconnect(object sender, DisconnectedEventArgs e)
    {
        // TODO
        /*foreach (OldPlayer player in OldPlayer.List.Values)
            Destroy(player.gameObject);
        */
        UIManager.Singleton.BackToMain();
    }

    public GameObject Spawn(ushort Id)
    {
        GameObject prefab = !isHosting && Client.Id != Id ? PlayerPrefab : LocalPlayerPrefab;
        GameObject spawnedCharacter = Instantiate(prefab, Vector3.up, Quaternion.identity);

        // Set playerId if is server or local client
        if(spawnedCharacter.GetComponent<PlayerController>())
            spawnedCharacter.GetComponent<PlayerController>().playerId = Id;
        return spawnedCharacter;
    }
}