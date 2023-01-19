using Riptide;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static StorageSystem;
using static Utils;

public class PlayerCharacter
{
    // TODO: adjust the protection level of these attributes
    ushort Id;
    string username;

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


    //TODO: adjust all the following code to work in the current class

    internal static Dictionary<ushort, OldPlayer> List = new Dictionary<ushort, OldPlayer>();

    private void OnDestroy()
    {
        List.Remove(Id);
    }

    private void Move(Vector3 newPosition, Vector3 forward)
    {
        character.transform.position = newPosition;
        forward.y = 0;
        character.transform.forward = forward.normalized;
    }

    internal static void Spawn(ushort id, string username, Vector3 position, bool shouldSendSpawn = false)
    {
        // TODO: send a Player instance to the associated client
        PlayerCharacter character;

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
    #endregion
}