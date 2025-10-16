using System;
using DotGameAvalonia.Models;

namespace DotGameAvalonia.Models
{
    public static class MapEditor
    {
        public static void AddUnitToMap(Character unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));

            // Pseudo-code for adding the unit to the map
            Console.WriteLine($"Unit '{unit.Name}' added to the map at position ({unit.TileX}, {unit.TileY}).");
        }
    }
}