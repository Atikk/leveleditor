using System;
using System.Collections.Generic;
using DotGameAvalonia.Models;

namespace DotGameAvalonia.Models
{
    public static class UnitRepository
    {
        private static readonly List<Character> Units = new();

        public static void Save(Character unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            Units.Add(unit);
            Console.WriteLine($"Unit '{unit.Name}' saved successfully.");
        }

        public static IEnumerable<Character> GetAllUnits()
        {
            return Units;
        }
    }
}