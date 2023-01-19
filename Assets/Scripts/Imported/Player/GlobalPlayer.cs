using Riptide;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using static StorageSystem;
using static UnityEditor.Experimental.GraphView.GraphView;
using static Utils;

public class PlayerCharacter
{
    // TODO: adjust the protection level of these attributes
    ushort Id;
    string username;
    Player.Status status;
    bool alive;
    public GameObject gameObject;

    public Transform transform => gameObject.transform;

    // TODO: Constructor on spawn, called by server message to other clients (the ones not associacted to this player character)

    // TODO: Constructor on joining raid, called from Player on the server side for the server !
    public PlayerCharacter(Player player)
    {
        Id = player.Id;
        username = player.username;

        // TODO: find a way to set alive to true only once status is InGame
        // TODO: find a way to set gameObject only once status is InGame
        // Possible solution: used the equivalent of the former spawn method and sends message to server when spawned, then setting alive and gameObject when spawning on server
    }

    public PlayerCharacter(ushort id, string username, Player.Status status)
    {
        Id = id;
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

    public bool IsRaider => status == Status.JoiningRaid || status == Status.InGame;

    public Player(Message message)
    {
        Id = message.GetUShort();
        username = message.GetString();
        AccountId = message.GetUShort();

        status = Status.Connected;
        currentSceneId = SceneId.MainMenu;

        // TODO: fetch player storages data here
        inventory = new Storage(10, 10);
        stash = new Storage(10, 10);

        character = new PlayerCharacter(this);
    }

    public void SetScene(SceneId sceneId)
    {
        // Maybe do some stuff here ?
        currentSceneId = sceneId;
    }

    public void SetScene(int sceneId)
    {
        SetScene((SceneId)sceneId);
    }

    // TODO: add methods that handle status changes
    public void SetStatus(Status newStatus, bool clientInstruction=false)
    {
        // Unless server permission isn't required: Connected <--> Hideout <--> In lobby, ask the server to change status
        if (clientInstruction && !(newStatus == Status.Connected || newStatus == Status.Hideout || newStatus == Status.InLobby))
        {
            // TODO: send a message to server asking to change status and return, the method will be called again if the server allows the status change
            return;
        }

        status = newStatus;

        switch (newStatus)
        {
            case Status.JoiningRaid:
                // Send message to server to update status and get other raiders PlayerCharacter infos
                break;
            default:
                break;
        }
        character.SetStatus(newStatus);
    }

    /// <summary>
    /// Send message to the server asking to register the player
    /// </summary>
    public void AskRegister()
    {
        // TODO: Send message to server to ask it to register the player
        Message message = Message.Create(MessageSendMode.Reliable, MessageId.PlayerRegister);
        // TODO: Add the relevant infos

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
    /// </summary>
    internal static List<PlayerCharacter> ClientListRaiders => new List<PlayerCharacter>();

    private void OnDestroy()
    {
        List.Remove(Id);
    }

    internal static void Spawn(ushort id, string username, Vector3 position, bool shouldSendSpawn = false)
    {
        // TODO: send a Player instance to the associated client
        PlayerCharacter character;

        // create new player instance
        Player player = new Player(id, username, SceneManager.GetActiveScene().buildIndex);
        player.AskRegister();

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
            player.SendSpawn();
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
        message.AddVector3(transform.position);
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
        message.AddVector3(transform.position);
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
        ushort playerId = message.GetUShort();
        if (List.TryGetValue(playerId, out OldPlayer player))
            player.Move(message.GetVector3(), message.GetVector3());
    }

    /// <summary>
    /// Called on the server-side when receiving a PlayerRegister message from a client
    /// </summary>
    [MessageHandler((ushort)MessageId.PlayerRegister)]
    private static void ServerPlayerRegister(ushort fromClientId, Message message)
    {
        // TODO

    }

    /// <summary>
    /// Called on the client-side when receiving a PlayerRegister message from the server
    /// </summary>
    [MessageHandler((ushort)MessageId.PlayerRegister)]
    private static void ClientPlayerRegister(Message message)
    {
        // TODO
    }

    /// <summary>
    /// Called on the server-side when receiving a PlayerStatus message from a client
    /// </summary>
    [MessageHandler((ushort)MessageId.PlayerStatus)]
    private static void ServerPlayerStatus(ushort fromClientId, Message message)
    {
        Player player = List[fromClientId];
        Status status = (Status)message.GetUShort();

        // TODO: continue only if server allows status change, there should be more checks than just changing to the current status
        if (player.status == status)
        {
            // TODO: maybe send error to client
            return;
        }

        player.SetStatus(status);

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

        // If the status change affects the current Client
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
    /// </summary>
    [MessageHandler((ushort)MessageId.NewRaider)]
    private static void ClientNewRaider(Message message)
    {
        // Read the PlayerCharacter data from the message
        ushort playerId = message.GetUShort();
        string playerUsername = message.GetString();
        Status playerStatus = (Status)message.GetUShort(); // status is passed because if a client joins an ongoing raid, some players can have the InGame status but some can also have the JoiningRaid status
        PlayerCharacter playerCharacter = new PlayerCharacter(playerId, playerUsername, playerStatus);

        // Add the PlayerCharacter to Raiders list
        ClientListRaiders.Add(playerCharacter);
    }
    #endregion
}