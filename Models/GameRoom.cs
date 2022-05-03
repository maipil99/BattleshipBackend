namespace BattleshipBackend.Models;

public class GameRoom
{
    public string GameID { get; set; } = "";
    public Player PlayerOne { get; set; }
    public Player PlayerTwo { get; set; }
}