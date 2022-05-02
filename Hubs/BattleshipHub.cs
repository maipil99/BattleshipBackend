using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using gamelogic;
using Microsoft.AspNetCore.SignalR;

namespace BattleshipBackend.Hubs;

public class BattleshipHub : Hub
{
    static Dictionary<string, Player> _users = new();
    static Dictionary<string, GameRoom> _games = new();

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
        var grid = new GameGrid() {Ships = ships};
        player.GameGrid = grid;
    }

    public async Task<bool> Shoot(Vector2 coordinate)
    {
        var player = _users[Context.ConnectionId];
        var game = player.CurrentGame;
        var opponent = game.PlayerOne.Equals(player) ? game.PlayerOne : game.PlayerTwo;

        var opponentConnectionId = opponent.ConnectionId;
        var opponentClient = Clients.Client(opponentConnectionId);
        // await client.SendAsync("ReceiveShot", coordinate.X, coordinate.Y);

        var isHit = opponent.GameGrid.RecieveHit(coordinate);
        if (isHit)
        {
            var ship = opponent.GameGrid.Ships.FirstOrDefault(ship => ship.IsSunk());
            if (ship is not null)
            {
                await SendSunkShip(ship, opponentClient);
            }
        }
        return isHit;
    }

    public async Task SendSunkShip(Ship ship, IClientProxy opponentClient)
    {
        await opponentClient.SendAsync("OpponentShipSunk",ship);

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


        bool NotFull()
        {
            return gameRoom.PlayerTwo == null;
        }
    }

    public bool LeaveGameRoom()
    {
        if (!_users.TryGetValue(Context.ConnectionId, out var user)) return false;

        var gameId = user?.CurrentGame.GameID;
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