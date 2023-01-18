﻿using Riptide;
using System.Collections.Generic;
using UnityEngine;

public class OldPlayer : MonoBehaviour
{
    internal static Dictionary<ushort, OldPlayer> List = new Dictionary<ushort, OldPlayer>();

    internal ushort Id;
    private string username;

    private void OnDestroy()
    {
        List.Remove(Id);
    }

    private void Move(Vector3 newPosition, Vector3 forward)
    {
        transform.position = newPosition;
        forward.y = 0;
        transform.forward = forward.normalized;
    }

    internal static void Spawn(ushort id, string username, Vector3 position, bool shouldSendSpawn = false)
    {
        OldPlayer player;

        if (!NetworkManager.Singleton.isHosting && id == NetworkManager.Singleton.Client.Id)
            player = Instantiate(NetworkManager.Singleton.LocalPlayerPrefab, position, Quaternion.identity).GetComponent<OldPlayer>();
        else
            player = Instantiate(NetworkManager.Singleton.PlayerPrefab, position, Quaternion.identity).GetComponent<OldPlayer>();

        player.Id = id;
        player.username = username;
        // TODO: declare accountId here
        player.name = $"Player {id} ({username})";

        List.Add(id, player);
        if (shouldSendSpawn)
            player.SendSpawn();
    }

    #region Messages

    /// <summary>
    /// Called when the local Client established a connection to the server and was Spawned
    /// <br/> It tells the server that the Player was spawned
    /// </summary>
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