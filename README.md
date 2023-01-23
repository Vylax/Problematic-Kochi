# Problematic Kochi

Problematic Kochi is a multiplayer Top-Down shooter game I'm working on.

## Todo
### Handle Player leaving raid !!!
#### Figure out an encoding format for the Storage structure so that it can be passed through SQL procdedures OR as file ?

### Messages
- implement Message.AddStatus()
- implement Message.AddStorage()

### Rewrite the player class
- The players have a status (enum): 
	- Connected (logged into the game but still in main menu)
	- Hideout (logged into the game but still in main hideout)
	- In lobby (in the process of joining the game : queueing, waiting for raid start, loading scene, whatever)
	- Joining raid (the raid is fully loaded in background, the client set their status to ready and the server allows raiding, they are being sent to the raid, syncing all clients infos (with PlayerCharacter))
	- In game (they are raiding, this state is reach as soon as the playercharacter is spawned)
	- Leaving raid (they successfully extracted or died, make sure to make them immortal at this point and remove them from other raiders view (and drop body if they died), send them to Connected status when everything is good and display raid debrief)

- Players characters should no longer be spawned on connection in the raid
- Add a currentScene attribute (to know when changing status if the player is in the correct scene)
- Players movement should be handled on the server-side: if a player moves forward, we send the input over to the server, the server computes the movement and sends the position back to all cleints
- Clients data must be store in a SQL database, retrieve and store the data on server when the client is connected
- Player alive attribute (relevant only if raiding: maybe make subclasses within the player class that are used according to the current player Status (RaidPlayer, DeadPlayer, HideoutPlayer, ...) ????)
- Handle disconnections, so that the player character stays in game but if player reconnects he can resume

### Storage System
- Fix the serialization endless loop with storages
- Implement drag and drop (check cimleh/patrick code for that)
- Implement storage serialization and writting/reading from/to file 
- Implement actions behing performed on the server side and then sent to client if succeeded
- Implement dropping item into the world
- Implement world storages (dead bodies, containers)
- Implement proximity world inventory (storage with all items on the ground within a certain radius)
- Change the Storage System to allow for special slots (the equipment ones) which have special properties (size insensitives, item restriction, ...) (maybe use child class by inheritance ???) (==> Make Equipment class)

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

