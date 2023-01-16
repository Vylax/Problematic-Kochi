using System.Collections.Generic;
using UnityEngine;

namespace Riptide.Demos.PlayerHosted
{
    public class Player : MonoBehaviour
    {
        internal static Dictionary<ushort, Player> List = new Dictionary<ushort, Player>();

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
            Player player;
            if (id == NetworkManager.Singleton.Client.Id)
                player = Instantiate(NetworkManager.Singleton.LocalPlayerPrefab, position, Quaternion.identity).GetComponent<Player>();
            else
                player = Instantiate(NetworkManager.Singleton.PlayerPrefab, position, Quaternion.identity).GetComponent<Player>();

            player.Id = id;
            player.username = username;
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
            message.AddVector3(transform.position);
            NetworkManager.Singleton.Client.Send(message);
        }

        [MessageHandler((ushort)MessageId.SpawnPlayer)]
        private static void SpawnPlayer(Message message)
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
            ushort playerId = message.GetUShort();
            if (List.TryGetValue(playerId, out Player player))
                player.Move(message.GetVector3(), message.GetVector3());
        }
        #endregion
    }
}
