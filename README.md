# Problematic Kochi

Problematic Kochi is a multiplayer Top-Down shooter game I'm working on.

## Todo
### Rewrite the player class
- The players have a status (enum): 
	- Connected (logged into the game but still in main menu)
	- Hideout (logged into the game but still in main hideout)
	- In lobby (in the process of joining the game : queueing, waiting for raid start, loading scene, whatever)
	- Joining raid (the raid is fully loaded in background, all clients infos are synced and the client set their status to ready and the server allows raiding, they are being sent to the raid)
	- In game (they are raiding)
	- Leaving raid (they successfully extracted, make sure to make them immortal at this point and remove them from other raiders view, send them to Connected status when everything is good)
- Players characters should no longer be spawned on connection in the raid
- Add a currentScene attribute (to know when changing status if the player is in the correct scene)
- Players movement should be handled on the server-side: if a player moves forward, we send the input over to the server, the server computes the movement and sends the position back to all cleints
- Clients data must be store in a SQL database, retrieve and store the data on server when the client is connected
- Player alive attribute (relevant only if raiding: maybe make subclasses within the player class that are used according to the current player Status (RaidPlayer, DeadPlayer, HideoutPlayer, ...) ????)

### Storage System
- Fix the serialization endless loop with storages
- Implement drag and drop (check cimleh/patrick code for that)
- Implement storage serialization and writting/reading from/to file 
- Implement actions behing performed on the server side and then sent to client if succeeded
- Implement dropping item into the world
- Implement world storages (dead bodies, containers)
- Implement proximity world inventory (storage with all items on the ground within a certain radius)

#### Data distribution (Server-Clients-DBMS)
##### Inventory
- When extracting, save inventory to backup server
- When inserting, save inventory to backup server
- When interacting with inventory in the fighting area, each performed action is sent to server with an id (to preserve order), they are then performed by the server, and if successfull, the server sends a message back to the client and the action is performed on the client side
- If something is wrong, the server sends and error to the client with the id of the last successful action so that the client can revert back to it
##### Stash
- When player starts interaction with his stash : save stash to server backup download stash from server and save stash to client local file
- Each action in stash (item moved, …) must be stored just like in versionning systems in action history object
- If any problem happens before the successful end of interaction, revert to saved local file and erase actions history
- If interaction is completed successfully, save stash to local file, send local file to server and action history
- Server performs actions from action history and if resulting inventory is the same as the one sent, everything is okay
- If the resulting inventory is different, revert to original stash on backup server, and send error to client, forcing him to revert stash by re downloading the inventory from server

