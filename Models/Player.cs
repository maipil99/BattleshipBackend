namespace BattleshipBackend.Models;

public class Player
{
    public string ConnectionId { get; set; }
    public string DisplayName { get; set; }
    public GameRoom CurrentGame { get; set; }
    public GameGrid GameGrid { get; set; }
    
    
}