using System.Diagnostics;
using System.Numerics;
using BattleshipBackend.Models;
using Microsoft.AspNetCore.SignalR;

namespace BattleshipBackend.Hubs;

public class BattleshipHub : Hub
{
    private static readonly Dictionary<string, Player> Users = new();
    private static readonly Dictionary<string, GameRoom> Games = new();

    /// <summary> 
    /// Checks against the user dictionary to see if the username is already in use.
    /// </summary>
    /// <param name="displayName"> The username the user wants to use. </param>
    /// <returns></returns>
    public bool Register(string displayName)
    {
        Debug.WriteLine(Context.ConnectionId + ": Requested the username '" + displayName + "'");

        //Checks if the username fulfills all rules
        var allowedUserName = !string.IsNullOrEmpty(displayName);
        var usernameExists = Users.Any(user => user.Key.Equals(displayName));
        
        //if username doesnt exists generate a new one based on it
        if (usernameExists || !allowedUserName) return false;
        
        var player = new Player
        {
            DisplayName = displayName,
            ConnectionId = Context.ConnectionId
        };

        Users.Add(Context.ConnectionId, player);

        return true;
    }

    public IEnumerable<GameRoom> GetGameRooms()
    {
        return Games.Values.ToArray();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="roomName">Identifier for a game session.</param>
    /// <returns>True if the room was successfully created.</returns>
    public bool CreateGameRoom(string roomName)
    {
        if (Games.Any(game => game.Key.Equals(roomName))) return false;
        var playerOne = Users[Context.ConnectionId];
        var gameRoom = new GameRoom
        {
            GameID = roomName,
            PlayerOne = playerOne,
        };
        playerOne.CurrentGame = gameRoom;
        Games.Add(roomName, gameRoom);

        Debug.WriteLine(Context.ConnectionId + ": Created a room with the name '" + roomName + "'");

        return true;
    }

    public bool JoinGameRoom(string roomName)
    {
        if (!Games.TryGetValue(roomName, out var gameRoom) || !NotFull()) return false;
        
        var playerTwo = Users[Context.ConnectionId];
        gameRoom.PlayerTwo = playerTwo;
        playerTwo.CurrentGame = gameRoom;
        
        Debug.WriteLine(Context.ConnectionId + ": Joined the room with the name '" + roomName + "'");
        
        GameRoomFull(gameRoom);
        
        return true;

        bool NotFull()
        {
            return gameRoom.PlayerTwo == null;
        }
    }

    public bool LeaveGameRoom()
    {
        if (!Users.TryGetValue(Context.ConnectionId, out var user)) return false;

        var gameId = user?.CurrentGame.GameID;
        
        if (gameId == null || !Games.TryGetValue(gameId, out var gameRoom)) return false;
        
        //If host leaves, delete the GameRoom
        if (Context.ConnectionId.Equals(gameRoom.PlayerOne.ConnectionId))
        {
            if (gameRoom.GameID == null) return true;
            
            Games.Remove(gameRoom.GameID);
            
            Debug.WriteLine(Context.ConnectionId + ": Left the room with the name '" + gameRoom.GameID + "'");
            Debug.WriteLine("GameRoom: " + gameRoom.GameID + " closed");

            return true;
        }

        //If player leaves, just remove them from the room
        if (gameRoom.PlayerTwo == null || !Context.ConnectionId.Equals(gameRoom.PlayerTwo?.ConnectionId)) return false;
        
        gameRoom.PlayerTwo = null;
        
        Debug.WriteLine(Context.ConnectionId + ": Left the room with the name '" + gameRoom.GameID + "'");
        
        return true;
    }

    private void GameRoomFull(GameRoom gameRoom)
    {
        var playerOne = gameRoom.PlayerOne.ConnectionId;
        var playerTwo = gameRoom.PlayerTwo.ConnectionId;
        
        Clients.Client(playerOne).SendAsync("GameRoomFull");
        Clients.Client(playerTwo).SendAsync("GameRoomFull");
    }

    public string GetOpponent()
    {
        try
        {
            var player = Users[Context.ConnectionId];
            var room = player.CurrentGame;
            
            return (room.PlayerOne.Equals(player) ? room.PlayerTwo : room.PlayerOne).DisplayName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
    
    public void PlaceShips(List<Ship> ships)
    {
        var player = Users[Context.ConnectionId];
        var grid = new GameGrid() {Ships = ships};
        player.GameGrid = grid;
    }

    public async Task<bool> Shoot(Vector2 coordinate)
    {
        var player = Users[Context.ConnectionId];
        var game = player.CurrentGame;
        var opponent = game.PlayerOne.Equals(player) ? game.PlayerTwo : game.PlayerOne;

        var opponentConnectionId = opponent.ConnectionId;
        var opponentClient = Clients.Client(opponentConnectionId); 
        await opponentClient.SendAsync("ReceiveShot", coordinate.X, coordinate.Y);
        
        var isHit = opponent.GameGrid.RecieveHit(coordinate);
        
        if (!isHit) return true;
        
        var ship = opponent.GameGrid.Ships.FirstOrDefault(ship => ship.IsSunk());
        
        if (ship is not null)
        {
            await SendSunkShip(ship, opponentClient);
        }
        
        return true;
    }

    public async Task SendSunkShip(Ship ship, IClientProxy opponentClient)
    {
        await opponentClient.SendAsync("OpponentShipSunk",ship);

    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Debug.WriteLine(Context.ConnectionId + ": Disconnected");

        Users.Remove(Context.ConnectionId);
        
        LeaveGameRoom();
        
        return base.OnDisconnectedAsync(exception);
    }
    
    public override Task OnConnectedAsync()
    {
        Debug.WriteLine("User has connected with ID: " + Context.ConnectionId);

        return base.OnConnectedAsync();
    }
}