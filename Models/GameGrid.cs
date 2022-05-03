using System.Numerics;

namespace BattleshipBackend.Models;

public class GameGrid
{
    public const int GridSize = 10;
    public List<Ship> Ships { get; set; } = new();

    public bool AllSunk => Ships.All(ship => ship.IsSunk());

    public void PlaceShip(Ship ship)
    {
        if (ship.Tiles.Length <= GridSize && ship.Tiles.All(vector2 => vector2.X < 11 && vector2.Y < 11))
        {
            Ships.Add(ship);
        }
        else
        {
            throw new ArgumentException("Ship is out of bounds");
        }
    }

    public bool RecieveHit(Vector2 coordinate)
    {
        foreach (var ship in Ships.Where(ship => !ship.IsSunk()))
        {
            if (ship.Tiles.Contains(coordinate))
            {
                Console.Out.WriteLine("hit");
                ship.Hit(coordinate);
                return true;
            }
        }

        return false;
    }
    
    
    
    
    
}