# Apple-Orchard
Multiplayer, "from scratch", efficient, high player count, social, apple picking game

All networking is implemented with a low level transport library, [ENet-CSharp](https://github.com/nxrighthere/ENet-CSharp)

## Movement
- Server Authoritative: Clients only send their inputs, the server moves the players for them
- Interpolation: Clients buffer a packet of data, and interpolate player's positions smoothly
- Client side prediction: A player doesn't need to wait until the server moves them, the client guesses their new position and the server corrects them if needed

## Serialization
- Delta compression: Server keeps track of what packets each client has succesfully received, and only sends each client new information
- Dirty Bits: Information that has not changed is excluded from a packet
- Bit Packing: Bit streams are used instead of byte streams to make sure that every bit of data being sent is important

## Architecture
- The server and client are both contained in the same Unity project
- An AWS EC2 instance has been setup to run a headless build of the game, which automatically causes it to run in server mode
- A SQL database has been created to store accounts and apples picked, it is deployed through AWS
- The same EC2 instance acts as a PHP server to handle database requests, the client never queries the SQL database directly

![player image](https://i.imgur.com/8MntvS9.png)
