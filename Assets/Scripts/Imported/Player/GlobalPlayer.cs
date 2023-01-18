using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static StorageSystem;

public class PlayerCharacter 
{
    ushort Id;
    string username;

    bool alive;
    GameObject gameObject;

    // TODO: Constructor on spawn, called by server message to other clients (the ones not associacted to this player character)

    // TODO: Constructor on joining raid, called from Player on the server side for the server !
    public PlayerCharacter(MyPlayer player)
    {
        Id = player.Id;
        username = player.username;

        // TODO: find a way to set alive to true only once status is InGame
        // TODO: find a way to set gameObject only once status is InGame
        // Possible solution: used the equivalent of the former spawn method and sends message to server when spawned, then setting alive and gameObject when spawning on server
    }
}

public class MyPlayer
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
    internal ushort Id; // Client Id relative to the server (can change and is not associated to the player account when the current connection ends)
    internal ushort AccountId; // Will be used to retrieve the player data from the database on connection, it is unique to each player accounts
    internal string username;
    public Status status;
    public int currentSceneId;
    public Storage inventory;
    public Storage stash;
    public PlayerCharacter character;

    public MyPlayer()
    {
        status = Status.Connected;

        // TODO: fetch player storages data here
        inventory = new Storage(10, 10);
        stash = new Storage(10, 10);

        character = new PlayerCharacter(this);
    }

    // TODO: add methods that handle status changes
}