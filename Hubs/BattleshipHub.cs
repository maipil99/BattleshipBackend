using System.Diagnostics;
using System.Numerics;
using gamelogic;
using Microsoft.AspNetCore.SignalR;

namespace BattleshipBackend.Hubs;

public class BattleshipHub : Hub
{
    private static readonly Dictionary<string, Player> _users = new();
    private static readonly Dictionary<string, GameRoom> _games = new();

    /// <summary> 
    /// Checks against the user dictionary to see if the username is already in use.
    /// </summary>
    /// <param name="displayName"> The username the user wants to use. </param>
    /// <returns></returns>
    public bool Register(string displayName)
    {
        Debug.WriteLine(Context.ConnectionId + ": Requested the username '" + displayName + "'");

        //Checks if the username fulfills all rules
        bool allowedUserName = !string.IsNullOrEmpty(displayName);
        bool usernameExists = _users.Any(user => user.Key.Equals(displayName));

        //if username doesnt exists generate a new one based on it
        if (!usernameExists && allowedUserName)
        {
            var player = new Player
            {
                DisplayName = displayName,
                ConnectionId = Context.ConnectionId
            };

            _users.Add(Context.ConnectionId, player);

            return true;
        }

        return false;
    }

    public void PlaceShips(List<Ship> ships)
    {
        var player = _users[Context.ConnectionId];
        var grid = new GameGrid {Ships = ships};
        player.GameGrid = grid;
    }

    public async Task<bool> Shoot(Vector2 shotCoordinate)
    {
        var player = _users[Context.ConnectionId];
        var playerClient = Clients.Client(player.ConnectionId);
        var game = player.CurrentGame;
        var opponent = game.PlayerOne.Equals(player) ? game.PlayerOne : game.PlayerTwo;

        var opponentConnectionId = opponent.ConnectionId;
        var opponentClient = Clients.Client(opponentConnectionId);

        var isHit = opponent.GameGrid.ReceiveHit(shotCoordinate);
        if (isHit)
        {
            //get ship that just got hit
            var shipHit =
                opponent.GameGrid.Ships.FirstOrDefault(ship => ship.Tiles.Any(tile => tile.Equals(shotCoordinate)));

            //check if ship is sunk
            var shipIsSunk = shipHit?.IsSunk() ?? false;

            Debug.WriteLine(player.DisplayName + $": Hit a ship at {shotCoordinate.X}, {shotCoordinate.Y}");
            
            //if player's ship is sunk send a message to the player with the opponent's ship that was sunk
            if (shipIsSunk)
            {
                Debug.WriteLine(player.DisplayName + $": Sunk a ship at {shotCoordinate.X}, {shotCoordinate.Y}");
                await SendSunkShip(shipHit, playerClient);
            }
        }
        //sends the shot to the opponent
        await opponentClient.SendAsync("ReceiveShot", shotCoordinate);

        return isHit;
    }

    private async Task SendSunkShip(Ship ship, IClientProxy client)
    {
        await client.SendAsync("OpponentShipSunk", ship);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="roomName">Identifier for a game session.</param>
    /// <returns>True if the room was successfully created.</returns>
    public bool CreateGameRoom(string roomName)
    {
        if (_games.Any(game => game.Key.Equals(roomName))) return false;
        var playerOne = _users[Context.ConnectionId];
        var gameRoom = new GameRoom
        {
            GameID = roomName,
            PlayerOne = playerOne,
        };
        playerOne.CurrentGame = gameRoom;
        _games.Add(roomName, gameRoom);

        Debug.WriteLine(Context.ConnectionId + ": Created a room with the name '" + roomName + "'");

        return true;
    }

    public bool JoinGameRoom(string roomName)
    {
        
        if (_games.TryGetValue(roomName, out var gameRoom) && NotFull())
        {
            Player playerTwo = _users[Context.ConnectionId];
            gameRoom.PlayerTwo = playerTwo;
            playerTwo.CurrentGame = gameRoom;
            Debug.WriteLine(Context.ConnectionId + ": Joined the room with the name '" + roomName + "'");
            return true;
        }

        return false;
        
        bool NotFull() => gameRoom.PlayerTwo == null;
    }

    public bool LeaveGameRoom()
    {
        if (!_users.TryGetValue(Context.ConnectionId, out var player)) return false;

        var gameId = player?.ConnectionId;
        if (gameId == null || !_games.TryGetValue(gameId, out var gameRoom)) return false;
        //If host leaves, delete the gameroom
        if (Context.ConnectionId.Equals(gameRoom.PlayerOne.ConnectionId))
        {
            if (gameRoom.GameID != null)
            {
                _games.Remove(gameRoom.GameID);
                Debug.WriteLine(Context.ConnectionId + ": Left the room with the name '" + gameRoom.GameID + "'");
                Debug.WriteLine("gameroom: " + gameRoom.GameID + " closed");
            }

            return true;
        }

        //If player leaves, just remove them from the room
        if (gameRoom.PlayerTwo != null && Context.ConnectionId.Equals(gameRoom.PlayerTwo?.ConnectionId))
        {
            gameRoom.PlayerTwo = null;
            Debug.WriteLine(Context.ConnectionId + ": Left the room with the name '" + gameRoom.GameID + "'");
            return true;
        }

        return false;
    }


    public GameRoom[] GetGameRooms()
    {
        return _games.Values.ToArray();
    }


    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Debug.WriteLine(Context.ConnectionId + ": Disconnected");

        _users.Remove(Context.ConnectionId);


        LeaveGameRoom();
        return base.OnDisconnectedAsync(exception);
    }


    public override Task OnConnectedAsync()
    {
        Debug.WriteLine("User has connected with ID: " + Context.ConnectionId);

        return base.OnConnectedAsync();
    }
}