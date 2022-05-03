using System.Numerics;

namespace BattleshipBackend.Models;

public class Ship
{
    public Vector2[] Tiles { get; }
    public List<Vector2> TilesHit { get; } = new();


    public Ship(Vector2[] tiles)
    {
        Tiles = tiles;
    }

    public void Hit(Vector2 tile)
    {
        TilesHit.Add(tile);
    }

    public bool IsSunk()
    {
        return Tiles.All(TilesHit.Contains);
    }
}