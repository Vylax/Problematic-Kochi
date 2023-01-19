using Riptide;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static StorageSystem;
using static Utils;

public class PlayerCharacter
{
    // TODO: adjust the protection level of these attributes
    public ushort Id;
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
        gameObject = NetworkManager.Singleton.Spawn(Id);
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
    private bool raiderSyncedWithServer = false;

    public bool canActuallyJoinRaid
    {
        // TODO: this must return true only if readyToRaid is true and map is ready to be loaded and all playerchar info were received
        get { return readyToRaid && raiderSyncedWithServer; }
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
    public bool SetStatus(Status newStatus)
    {
        Status oldStatus = status;

        // The conditioning should happen here

        // Change status
        status = newStatus;
        Debug.Log($"Player {Id} ({username}) changed status: {oldStatus} --> {newStatus}");

        // The message sending part applies only to the server
        if (NetworkManager.Singleton.isHosting)
        {
            if (status == Status.JoiningRaid)
            {
                // Prepare the message to send to other raiders
                Message raidersMessage = RaiderMessage;

                foreach (Player raider in ServerListRaiders)
                {
                    // Send to ALL the Raiders the RaiderMessage of the new raider (ALL includes the new raider)
                    NetworkManager.Singleton.Server.Send(raidersMessage, raider.Id);

                    // Send to the new raider the RaiderMessage of all raiders except for himself
                    if (raider.Id != Id)
                        NetworkManager.Singleton.Server.Send(raider.RaiderMessage, raider.Id);
                }
            }
            else
            {
                Message answer = StatusMessage;

                if (status == Status.InGame || status == Status.LeavingRaid)
                {
                    // Send status Update to all Raiders
                    foreach (Player raider in ServerListRaiders)
                    {
                        NetworkManager.Singleton.Server.Send(answer, raider.Id);
                    }
                }
                else
                {
                    // Otherwise tell only the Client that sent the message about the status update
                    NetworkManager.Singleton.Server.Send(answer, Id);
                }
            }
        }

        switch (newStatus)
        {
            case Status.Hideout:
                // TODO: update current scene index
                break;
        }

        if(character == null)
        {
            // This shouldn't happen if the procedure is working correctly
            if (IsRaider)
                Debug.LogError("The player is a Raider but its PlayerCharacter instance hasn't been initialized");

            // This is normal it just mean the status update doesn't affect the ongoing raid
            return true;
        }

        // Update the associated PlayerCharacter status
        character.SetStatus(newStatus);

        return true;
    }

    /// <summary>
    /// (CLIENT ONLY) Called by the Client when attempting to change his status
    /// </summary>
    public bool ClientAskSetStatus(Status newStatus)
    {
        Status oldStatus = status;

        // TODO: check if status change is allowed, there should be more checks than the ones in place
        if (status == newStatus || !isRegistered && (newStatus == Status.JoiningRaid || newStatus == Status.InGame || newStatus == Status.LeavingRaid))
        {
            Debug.LogError($"Coudln't change Player {Id} ({username}) status from {oldStatus} to {newStatus}");
            return false;
        }

        bool condition = true;

        switch (newStatus)
        {
            case Status.JoiningRaid:
                condition = isRegistered && status == Status.InLobby && readyToRaid;
                break;
            case Status.InGame:
                condition = canActuallyJoinRaid;
                break;
        }

        // Ask the server permission to change status
        AskServerForStatusChange(newStatus, condition);

        return true;
    }

    private Message RaiderMessage
    {
        get
        {
            Message raiderMessage = Message.Create(MessageSendMode.Reliable, MessageId.NewRaider);
            raiderMessage.AddUShort(Id);
            raiderMessage.AddString(username);
            raiderMessage.AddUShort((ushort)status);
            // TODO: when storage system is finished, also send equipment so it can be displayed

            return raiderMessage;
        }
    }

    private Message StatusMessage
    {
        get
        {
            Message message = Message.Create(MessageSendMode.Reliable, MessageId.PlayerStatus);
            message.AddUShort(Id);
            message.AddUShort((ushort)status);

            return message;
        }
    }

    /// <summary>
    /// (CLIENT ONLY) Sends a message to the server to request a status change
    /// </summary>
    private void AskServerForStatusChange(Status newStatus, bool condition=true)
    {
        if (!condition)
        {
            Debug.LogError($"Couldn't ask Server to update status because the condition wasn't met");
            return;
        }

        Message message = Message.Create(MessageSendMode.Reliable, MessageId.PlayerStatus);
        message.AddUShort((ushort)newStatus);

        Debug.LogWarning($"Player {Id} ({username}) asked server permission to change its status from {status} to {newStatus}");
        NetworkManager.Singleton.Client.Send(message);
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
    /// Called on the client-side when the Player is allowed to Join the Raid
    /// </summary>
    /// <param name="raidersCount">The number of raider there was before the Raid Join was granted</param>
    public void JoinRaidGranted()
    {
        // Initialize PlayerCharacter instance
        character = new PlayerCharacter(this);

        // Set Status
        if (!SetStatus(Status.JoiningRaid))
        {
            Debug.LogError("Server allowed player to join raid but player status cannot be set to JoiningRaid, this shouldn't happen");
            return;
        }

        // Start a Coroutine that collects all raiders info are collected
        NetworkManager.Singleton.StartCoroutine(SyncRaidersWithServer());
    }

    // Send message to server with known raiders Ids
    // Server responds with all raiders Ids List, if some are missing, server sends NewRaider message for them
    // If all are received within 10 sec, sends known raiders Ids to server
    // Server responds (with all raiders Ids list again) and if they are no new raiders, Player status is set to Synced on both client and server
    private IEnumerator SyncRaidersWithServer()
    {
        Debug.Log($"Started collecting raiders from Server");
        ServerListRaidersIds = new List<ushort>();

        SendClientListRaidersIdsToServer();

        yield return new WaitForSeconds(3);
        while (!ClientListRaidersIds.SequenceEqual(ServerListRaidersIds))
        {
            // Send message to server with the current state of ClientListRaidersIds
            SendClientListRaidersIdsToServer();

            yield return new WaitForSeconds(10);
        }

        raiderSyncedWithServer = true;

        Debug.Log($"Successfully synced raider with Server");
    }

    private void SendClientListRaidersIdsToServer()
    {
        Message message = Message.Create(MessageSendMode.Reliable, MessageId.SyncRaider);
        message.AddUShorts(ClientListRaidersIds.ToArray());
        NetworkManager.Singleton.Client.Send(message);
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
    internal static List<PlayerCharacter> ClientListRaiders = new List<PlayerCharacter>();
    internal static List<ushort> ClientListRaidersIds => ClientListRaiders.Select(player => player.Id).ToList();
    internal static List<ushort> ServerListRaidersIds = new List<ushort>();

    private void OnDestroy()
    {
        Debug.LogError("This shouldn't be called anymore !!");
        List.Remove(Id);
    }

    #region Messages

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
            // Couldn't update Player status, send a message back to the Client to tell him
            Message answer = Message.Create(MessageSendMode.Reliable, MessageId.PlayerStatus);
            answer.AddUShort(fromClientId);
            answer.AddUShort((ushort)oldStatus);

            NetworkManager.Singleton.Server.Send(answer, fromClientId);
            Debug.LogError($"Coudln't change Player {player.Id} ({player.username}) status from {oldStatus} to {status}");
            return;
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
    /// Called on the client-side when receiving a NewRaider message from the server
    /// <br/> This call is triggered when a player status was set to JoiningRaid and the messages contains the PlayerCharacter data of this player
    /// </summary>
    [MessageHandler((ushort)MessageId.NewRaider)]
    private static void ClientNewRaider(Message message)
    {
        // Read the PlayerCharacter data from the message
        ushort playerId = message.GetUShort();

        // Check if the status change affects the current Client
        if (playerId == NetworkManager.Singleton.Client.Id)
        {
            // The PlayerCharacter will be Instantiated in the JoinRaidGranted() call and it doesn't need to be added to ClientListRaiders
            localPlayer.JoinRaidGranted();
            return;
        }

        // The New Raider isn't the localPlayer, we need to instantiate it and add it to ClientListRaiders here
        string playerUsername = message.GetString();
        Status playerStatus = (Status)message.GetUShort(); // status is passed because if a client joins an ongoing raid, some players can have the InGame status but some can also have the JoiningRaid status
        PlayerCharacter playerCharacter = new PlayerCharacter(playerId, playerUsername, playerStatus);

        // Add the PlayerCharacter to Raiders list
        ClientListRaiders.Add(playerCharacter);
    }

    /// <summary>
    /// Called when a client sends his known raiders ids list to the server
    /// </summary>
    [MessageHandler((ushort)MessageId.SyncRaider)]
    private static void ServerSyncRaider(ushort fromClientId, Message message)
    {
        List<ushort> ClientRaidersIds = message.GetUShorts().ToList();
        List<ushort> ServerRaidersIds = ServerListRaiders.Select(player => player.Id).ToList();
        List<ushort> missingIds = ServerRaidersIds.Except(ClientRaidersIds).ToList();

        if (missingIds.Count > 0)
        {
            // Send NewRaider message back to the client foreach player Id in ServerRaidersIds that aren't in ClientRaidersIds
            foreach (ushort id in missingIds)
            {
                NetworkManager.Singleton.Server.Send(List[id].RaiderMessage, fromClientId);
            }
        }

        // Send ServerRaidersIds to Client
        Message answer = Message.Create(MessageSendMode.Reliable, MessageId.SyncRaider);
        answer.AddUShorts(ServerRaidersIds.ToArray());
        NetworkManager.Singleton.Server.Send(answer, fromClientId);
    }

    /// <summary>
    /// Called when the server sends the raiders Ids list to the client
    /// </summary>
    [MessageHandler((ushort)MessageId.SyncRaider)]
    private static void ClientSyncRaider(Message message)
    {
        ServerListRaidersIds = message.GetUShorts().ToList();
    }
    #endregion
}