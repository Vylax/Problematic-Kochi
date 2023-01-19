using Riptide;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Numerics;
using UnityEngine;
using static StorageSystem;
using static Utils;

public class PlayerCharacter : MonoBehaviour
{
    // TODO: adjust the protection level of these attributes
    ushort Id;
    string username;
    Player.Status status;
    bool alive;
    public GameObject gameObject;

    public Transform transform => gameObject.transform;


    /// <summary>
    /// Constructor on joining raid, called by server or local Client for localPlayer
    /// </summary>
    /// <param name="player"></param>
    public PlayerCharacter(Player player)
    {
        Id = player.Id;
        username = player.username;

        // NOTE: status MUST NOT BE set here, once this is initialized, status will be set from a Player.SetStatus() call !

        // TODO: find a way to set alive to true only once status is InGame
        // TODO: find a way to set gameObject only once status is InGame
        // Possible solution: used the equivalent of the former spawn method and sends message to server when spawned, then setting alive and gameObject when spawning on server
    }

    /// <summary>
    /// This constructor should only be called when a Player joins an ongoing raid that was already joined by other raiders, it creates PlayerCharacter instances that are not associated to localPlayer
    /// </summary>
    public PlayerCharacter(ushort Id, string username, Player.Status status)
    {
        this.Id = Id;
        this.username = username;
        this.status = status;
        alive = false; // alive will be set to true when the PlayerCharacter is Spawned
        gameObject = null;
    }



    /// <summary>
    /// Spawn the PlayerCharacter GameObject
    /// </summary>
    public void Spawn()
    {
        // TODO
        alive = true;
        // TODO: Instantiate prefab here
        gameObject = Instantiate<>
        // TODO: When the storage implementation is complete, Display equipment here (the equipment should be passed by the NewRaider message along with the PlayerCharacter data
    }

    /// <summary>
    /// Called on the Client side when receiving a message from the server to Update PlayerCharacter transform after processing the inputs sent by the client to the server<br/>
    /// Called on the Server side to Update PlayerCharacter transform after processing the inputs sent by the client to the server
    /// </summary>
    private void Move(Vector3 newPosition, Vector3 forward)
    {
        transform.position = newPosition;
        forward.y = 0;
        transform.forward = forward.normalized;
    }

    public void SetStatus(Player.Status newStatus)
    {
        status = newStatus;

        switch (newStatus)
        {
            case Player.Status.InGame:
                // Spawn the PlayerCharacter GameObject
                Spawn();
                break;
            default:
                break;
        }
    }
}

public class Player
{
    public enum Status
    {
        Connected,
        Hideout,
        InLobby,
        JoiningRaid,
        InGame,
        LeavingRaid
    }

    // TODO: revise the protections levels
    public ushort Id; // Client Id relative to the server (can change and is not associated to the player account when the current connection ends)
    public ushort AccountId; // Will be used to retrieve the player data from the database on connection, it is unique to each player accounts
    public string username;
    public Status status;
    public SceneId currentSceneId;
    public Storage inventory;
    public Storage stash;
    public PlayerCharacter character;

    public bool isRegistered;

    // TODO: this should be set to true/false by clicking a Ready button in the lobby UI later on
    /// <summary>
    /// returns true if the player wants to join the raid: that doesn't mean he can !
    /// </summary>
    public bool readyToRaid => UIManager.Singleton.playerIsReadyToJoinRaid;
    private bool allRaidersCollected = false;

    public bool canActuallyJoinRaid
    {
        // TODO: this must return true only if readyToRaid is true and map is ready to be loaded and all playerchar info were received
        get { return readyToRaid && allRaidersCollected; }
    }

    public bool IsRaider => status == Status.JoiningRaid || status == Status.InGame;

    public Player(ushort Id, string username, Status status)
    {
        this.Id = Id;
        this.username = username;
        this.status = status;
    }

    public Player(ushort Id, ushort AccountId, string username, Status status, SceneId currentSceneId, Storage inventory, Storage stash)
    {
        this.Id = Id;
        this.AccountId = AccountId;
        this.username = username;
        this.status = status;
        this.currentSceneId = currentSceneId;
        this.inventory = inventory;
        this.stash = stash;
        isRegistered = true;
    }

    public void SetScene(SceneId sceneId)
    {
        // TODO: if client called, ask server and return, if server call, do as follows
        // TODO: load scene in background, call coroutine, when scene is fully loaded, change scene and change currentSceneId
        currentSceneId = sceneId;
    }

    public void SetScene(int sceneId)
    {
        SetScene((SceneId)sceneId);
    }

    // TODO: rewrite this while avoiding code repetition between server side and client side
    public bool SetStatus(Status newStatus, bool clientInstruction=false)
    {
        Status oldStatus = status;

        // TODO: continue only if server allows status change, there should be more checks than just changing to the current status
        if (status == newStatus || !isRegistered && (newStatus == Status.JoiningRaid || newStatus == Status.InGame || newStatus == Status.LeavingRaid))
        {
            // TODO: maybe send error to client
            Debug.LogError($"Coudln't change Player {Id} ({username}) status from {oldStatus} to {newStatus}");
            return false;
        }

        // Ask the server permission to change status
        if (clientInstruction)
        {
            if(status == Status.JoiningRaid)
            {
                AskJoinRaid();
                return false;
            }else if(status == Status.InGame)
            {
                if (canActuallyJoinRaid)
                {
                    // TODO: ask server to go in game
                }
                else
                {
                    Debug.LogError($"Coudln't join raid because Player {Id} ({username}) isn't actually ready: canActuallyJoinRaid={canActuallyJoinRaid}");
                }
                return false;
            }
            Message message = Message.Create(MessageSendMode.Reliable, MessageId.PlayerStatus);
            message.AddUShort((ushort)status);

            Debug.LogWarning($"Player {Id} ({username}) asked server permission to change its status from {oldStatus} to {newStatus}");
            NetworkManager.Singleton.Client.Send(message);

            return false;
        }

        // Change status
        status = newStatus;
        Debug.Log($"Player {Id} ({username}) changed status: {oldStatus} --> {newStatus}");

        switch (newStatus)
        {
            case Status.JoiningRaid:
                //TODO: Send message to server to ask for other raiders PlayerCharacter infos and have a "syncReadyToJoin" attribute that is set to true only when all playerchar infos were received and map is ready to be loaded

                break;
            case Status.Hideout:
                // TODO: update current scene index
                break;
            case Status.InGame:
                // TODO: Spawn player
            default:
                break;
        }

        if(character == null)
        {
            if(IsRaider)
            {
                // This shouldn't happen if the procedure is working correctly
                Debug.LogError("The player is a Raider but its PlayerCharacter instance hasn't been initialized");
            }

            // This is normal it just mean the status update doesn't affect the ongoing raid
            return true;
        }

        // Update the associated PlayerCharacter status
        character.SetStatus(newStatus);

        return true;
    }

    /// <summary>
    /// Ask the Server to register this player, this should be done automatically when a connection is successfully established
    /// </summary>
    public void AskRegister()
    {
        Message message = Message.Create(MessageSendMode.Reliable, MessageId.PlayerRegister);
        message.AddUShort(Id);
        message.AddString(username);

        NetworkManager.Singleton.Client.Send(message);
    }

    public void RegisterGranted(ushort AccountId)
    {
        this.AccountId = AccountId;
        isRegistered = true;
    }

    /// <summary>
    /// Ask the Server to join the raid, should be called by a UI button press later on
    /// </summary>
    public void AskJoinRaid()
    {
        if(isRegistered && status == Status.InLobby && readyToRaid)
        {
            // TODO: figure out if there's anything relevant to send here
            Message message = Message.Create(MessageSendMode.Reliable, MessageId.NewRaider);
            message.AddUShort(AccountId);
            NetworkManager.Singleton.Client.Send(message);
            Debug.Log("Successfully asked server to Join Raid");
        }
        else
        {
            Debug.LogError($"Couldn't ask Server to Join Raid");
        }
    }

    /// <summary>
    /// Called on the client-side when the Player is allowed to Join the Raid
    /// </summary>
    /// <param name="raidersCount">The number of raider there was before the Raid Join was granted</param>
    public void JoinRaidGranted(int raidersCount)
    {
        // Initialize PlayerCharacter instance
        character = new PlayerCharacter(this);

        // Set Status
        if (!SetStatus(Status.JoiningRaid))
            return;

        // Start a Coroutine that collects all raiders info are collected
        NetworkManager.Singleton.StartCoroutine(CollectAllRaidersFromServer(raidersCount));
    }

    private IEnumerator CollectAllRaidersFromServer(int raidersCount)
    {
        Debug.Log($"Started collecting raiders from Server: {ClientListRaiders.Count}/{raidersCount}");

        yield return new WaitUntil(() => ClientListRaiders.Count == raidersCount);
        allRaidersCollected = true;

        Debug.Log($"Successfully collected {ClientListRaiders.Count}/{raidersCount} raiders from Server");
    }

    //TODO: adjust all the following code to work in the current class

    /// <summary>
    /// The player instance associated to the local Client
    /// </summary>
    internal static Player localPlayer;

    /// <summary>
    /// Should only be used by the Server
    /// </summary>
    internal static Dictionary<ushort, Player> List = new Dictionary<ushort, Player>();

    /// <summary>
    /// (SERVER ONLY) Returns all the Raiders Player instances from List (Player with status in {JoiningRaid, InGame})
    /// </summary>
    internal static List<Player> ServerListRaiders => List.Values.Where(player => player.IsRaider).ToList();
    /// <summary>
    /// (CLIENT ONLY) Returns all the Raiders PlayerCharacter instances
    /// <br/> Note: This List doesn't contain localPlayer.character
    /// </summary>
    internal static List<PlayerCharacter> ClientListRaiders => new List<PlayerCharacter>();

    private void OnDestroy()
    {
        Debug.LogError("This shouldn't be called anymore !!");
        List.Remove(Id);
    }

    internal static void Spawn(ushort id, string username, Vector3 position, bool shouldSendSpawn = false)
    {
        /*
        // TODO: send a Player instance to the associated client
        PlayerCharacter character;

        // create new player instance
        Player player = new Player(id, username, SceneManager.GetActiveScene().buildIndex);
        //player.AskRegister();

        if (!NetworkManager.Singleton.isHosting && id == NetworkManager.Singleton.Client.Id)
            character = new PlayerCharacter();
        else
            character = Instantiate(NetworkManager.Singleton.PlayerPrefab, position, Quaternion.identity).GetComponent<OldPlayer>();

        character.Id = id;
        character.username = username;
        // TODO: declare accountId here
        character.name = $"Player {id} ({username})";

        List.Add(id, player);
        if (shouldSendSpawn)
            player.SendSpawn();*/
    }

    #region Messages

    /// <summary>
    /// Called when the local Client established a connection to the server and was Spawned
    /// <br/> It tells the server that the Player was spawned
    /// </summary>0
    private void SendSpawn()
    {
        Message message = Message.Create(MessageSendMode.Reliable, MessageId.SpawnPlayer);
        message.AddUShort(Id);
        message.AddString(username);

        // TODO: add actual accountId here
        message.AddUShort((ushort)Random.Range(0, 69420));

        // TODO: remove this, rename method something like SendConnected and update all the associated methods
        //message.AddVector3(transform.position);
        NetworkManager.Singleton.Client.Send(message);
    }

    [MessageHandler((ushort)MessageId.SpawnPlayer)]
    private static void ServerSpawnPlayer(ushort fromClientId, Message message)
    {
        // Relay the message to all clients except the newly connected client
        NetworkManager.Singleton.Server.SendToAll(message, fromClientId);

        // Spawn the player on the server side
        Spawn(message);
    }

    [MessageHandler((ushort)MessageId.SpawnPlayer)]
    private static void ClientSpawnPlayer(Message message)
    {
        Spawn(message);
    }

    private static void Spawn(Message message)
    {
        Spawn(message.GetUShort(), message.GetString(), message.GetVector3());
    }

    /// <summary>
    /// Method called when the Server sends the spawn message associated to the current player instance to the client who just connected
    /// <br/> It informs the newly connected client about the existence of all the players already connected
    /// </summary>
    /// <param name="newPlayerId">the Id of the Client who will receive the message (the one who just connected)</param>
    internal void SendSpawn(ushort newPlayerId)
    {
        Message message = Message.Create(MessageSendMode.Reliable, MessageId.SpawnPlayer);
        message.AddUShort(Id);
        message.AddString(username);
        //message.AddVector3(transform.position);
        NetworkManager.Singleton.Server.Send(message, newPlayerId);
    }

    [MessageHandler((ushort)MessageId.PlayerMovement)]
    private static void PlayerMovement(Message message)
    {
        Move(message);
    }

    [MessageHandler((ushort)MessageId.PlayerMovement)]
    private static void ServerPlayerMovement(ushort fromClientId, Message message)
    {
        // Relay the message to all clients except the newly connected client
        NetworkManager.Singleton.Server.SendToAll(message, fromClientId);

        Move(message);
    }

    public static void Move(Message message)
    {
        /*ushort playerId = message.GetUShort();
        if (List.TryGetValue(playerId, out Player player))
            player.Move(message.GetVector3(), message.GetVector3());*/
    }

    /// <summary>
    /// Called on the server-side when receiving a PlayerRegister message from a client
    /// </summary>
    [MessageHandler((ushort)MessageId.PlayerRegister)]
    private static void ServerPlayerRegister(ushort fromClientId, Message message)
    {
        // Read Player data from the message
        if(message.GetUShort() != fromClientId)
        {
            // TODO: Send error to client
            Debug.LogError($"The Player Id in the message isn't the same as the Id of the Client sending the message, this should never happen!!!");
            return;
        }

        if (List.ContainsKey(fromClientId))
        {
            // TODO: Send error to client
            Debug.LogError($"The Players list already contains the Id: {fromClientId}, registration failed");
            return;
        }

        string playerUsername = message.GetString();

        // TODO: Retrieve player data from DB server here instead of using random values
        ushort playerAccountId = (ushort)Random.Range(0, 69420);
        Storage playerInventory = new Storage(10, 10);
        Storage playerStash = new Storage(10, 10);

        Player newPlayer = new Player(fromClientId, playerAccountId, playerUsername, Status.Connected, SceneId.MainMenu, playerInventory, playerStash);

        // Add player to the Players List
        List.Add(fromClientId, newPlayer);

        // Tell the client that the registration was successful and send him the required Player data
        Message successMessage = Message.Create(MessageSendMode.Reliable, MessageId.PlayerRegister);
        successMessage.AddUShort(playerAccountId);
        // TODO: implement storage serialization or whatever and then implement a way to pass Storage through messages in order to send playerInventory and playerStash

        NetworkManager.Singleton.Server.Send(message, fromClientId);
    }

    /// <summary>
    /// Called on the client-side when receiving a PlayerRegister message from the server
    /// </summary>
    [MessageHandler((ushort)MessageId.PlayerRegister)]
    private static void ClientPlayerRegister(Message message)
    {
        // TODO
        ushort playerAccountId = message.GetUShort();
        // TODO: implement storage serialization or whatever and then implement a way to pass Storage through messages in order to receive playerInventory and playerStash
        localPlayer.RegisterGranted(playerAccountId);
    }

    /// <summary>
    /// Called on the server-side when receiving a PlayerStatus message from a client
    /// </summary>
    [MessageHandler((ushort)MessageId.PlayerStatus)]
    private static void ServerPlayerStatus(ushort fromClientId, Message message)
    {
        Player player = List[fromClientId];
        Status oldStatus = player.status;
        Status status = (Status)message.GetUShort();

        if (!player.SetStatus(status))
        {
            Message answer = Message.Create(MessageSendMode.Reliable, MessageId.PlayerStatus);
            answer.AddUShort(fromClientId);
            answer.AddUShort((ushort)oldStatus);

            NetworkManager.Singleton.Server.Send(answer, fromClientId);
            Debug.LogError($"Coudln't change Player {player.Id} ({player.username}) status from {oldStatus} to {status}");
            return;
        }

        // TODO: depending on the status change, tell all clients about it, for instance if (status is InGame or JoiningRaid) ie if player is raider, or if player leaves Raid
        if(status == Status.JoiningRaid || status == Status.InGame || status == Status.LeavingRaid)
        {
            Message raidersMessage = null;
            if (status == Status.JoiningRaid)
            {
                raidersMessage = Message.Create(MessageSendMode.Reliable, MessageId.NewRaider);
                raidersMessage.AddUShort(fromClientId);
                raidersMessage.AddString(player.username);
                raidersMessage.AddUShort((ushort)status);
                // TODO: when storage system is finished, also send equipment so it can be displayed
            }
            else // InGame or LeavingRaid
            {
                // Only send status update, the rest will be automatically handled on the client side
                raidersMessage = Message.Create(MessageSendMode.Reliable, MessageId.PlayerStatus);
                raidersMessage.AddUShort(fromClientId);
                raidersMessage.AddUShort((ushort)status);
            }

            // Send message to all the Raiders, including the one having his status updated
            foreach (Player raider in ServerListRaiders)
            {
                NetworkManager.Singleton.Server.Send(raidersMessage, raider.Id);
            }
        }
        else
        {
            // Otherwise tell only the Client that sent the message about the status update
            Message answer = Message.Create(MessageSendMode.Reliable, MessageId.PlayerStatus);
            answer.AddUShort(fromClientId);
            answer.AddUShort((ushort)status);

            NetworkManager.Singleton.Server.Send(answer, fromClientId);
        }
    }

    /// <summary>
    /// Called on the client-side when receiving a PlayerStatus message from the server
    /// </summary>
    [MessageHandler((ushort)MessageId.PlayerStatus)]
    private static void ClientPlayerStatus(Message message)
    {
        // Read player status data from the message
        ushort playerId = message.GetUShort();
        Status status = (Status)message.GetUShort();

        // Check if the status change affects the current Client
        if(playerId == NetworkManager.Singleton.Client.Id)
        {
            localPlayer.SetStatus(status);
        }
        else
        {
            ClientListRaiders[playerId].SetStatus(status);
        }
    }

    /// <summary>
    /// Called on the server-side when receiving a NewRaider message from a client
    /// </summary>
    [MessageHandler((ushort)MessageId.NewRaider)]
    private static void ServerNewRaider(ushort fromClientId, Message message)
    {
        // Read the Client's AccountId data from the message
        ushort playerAccountId = message.GetUShort();

        Player player = List[fromClientId];

        // TODO: there must be some check to do here like is there a raid going on, has the raid server reach max player capacity, etc ...
        // TODO: because right now Raid Join are always accepted

        // TODO: Add Player to Raiders on the server side !!!
        if (!player.SetStatus(Status.JoiningRaid))
        {
            // TODO: Send error message to client
            Debug.LogError($"Couldn't allow player {player.Id} ({player.username}) to Join Raid");
            return;
        }

        // Tell clients about the new Raider
        Message answer = Message.Create(MessageSendMode.Reliable, MessageId.NewRaider);
        answer.AddUShort(fromClientId);
        answer.AddInt(ServerListRaiders.Count);
        answer.AddString(player.username);
        answer.AddUShort((ushort)player.status);

        NetworkManager.Singleton.Server.SendToAll(answer);
    }

    /// <summary>
    /// Called on the client-side when receiving a NewRaider message from the server
    /// </summary>
    [MessageHandler((ushort)MessageId.NewRaider)]
    private static void ClientNewRaider(Message message)
    {
        // Read the PlayerCharacter data from the message
        ushort playerId = message.GetUShort();
        int raidersCount = message.GetInt();

        // Check if the status change affects the current Client
        if (playerId == NetworkManager.Singleton.Client.Id)
        {
            // The PlayerCharacter will be Instantiated in the JoinRaidGranted() call and it doesn't need to be added to ClientListRaiders
            localPlayer.JoinRaidGranted(raidersCount);
            return;
        }

        // The New Raider isn't the localPlayer, we need to instantiate it and add it to ClientListRaiders here
        string playerUsername = message.GetString();
        Status playerStatus = (Status)message.GetUShort(); // status is passed because if a client joins an ongoing raid, some players can have the InGame status but some can also have the JoiningRaid status
        PlayerCharacter playerCharacter = new PlayerCharacter(playerId, playerUsername, playerStatus);

        // Add the PlayerCharacter to Raiders list
        ClientListRaiders.Add(playerCharacter);
    }
    #endregion
}