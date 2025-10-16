using System;

namespace DotGameAvalonia.Models
{
    public enum CombatAction { Attack, Defend, None }

    public class CombatManager
    {
        public Character Player { get; private set; }
        public Monster Enemy { get; private set; }
        public bool IsPlayerTurn { get; private set; } = true;
        public bool CombatActive { get; private set; } = false;
        public string LastMessage { get; private set; } = "";

        private int turnDelay = 0;
        private const int TURN_DELAY_MAX = 30;

        public CombatManager(Character player, Monster enemy)
        {
            Player = player;
            Enemy = enemy;
        }

        public void StartCombat()
        {
            CombatActive = true;
            IsPlayerTurn = true;
            LastMessage = $"Battle started with {Enemy.Name}!";
        }

        public void EndCombat()
        {
            CombatActive = false;
            LastMessage = "";
        }

        public void PlayerAttack()
        {
            if (!IsPlayerTurn || !CombatActive) return;

            int damage = Player.AttackTarget(Enemy);
            LastMessage = $"{Player.Name} attacks for {damage} damage!";

            if (!Enemy.IsAlive)
            {
                LastMessage = $"{Enemy.Name} defeated!";
                EndCombat();
                return;
            }

            IsPlayerTurn = false;
            turnDelay = 0;
        }

        public void PlayerDefend()
        {
            if (!IsPlayerTurn || !CombatActive) return;

            LastMessage = $"{Player.Name} is defending!";
            IsPlayerTurn = false;
            turnDelay = 0;
        }

        public void Update()
        {
            if (!CombatActive) return;

            if (!IsPlayerTurn)
            {
                turnDelay++;
                if (turnDelay >= TURN_DELAY_MAX)
                {
                    EnemyTurn();
                    turnDelay = 0;
                }
            }
        }

        private void EnemyTurn()
        {
            if (!Enemy.IsAlive || !Player.IsAlive)
            {
                EndCombat();
                return;
            }

            int damage = Math.Max(1, Enemy.Attack - Player.Attributes.Defense);
            int actualDamage = Player.TakeDamage(damage);

            LastMessage = $"{Enemy.Name} attacks for {actualDamage} damage!";

            if (!Player.IsAlive)
            {
                LastMessage = $"{Player.Name} has been defeated!";
                EndCombat();
                return;
            }

            IsPlayerTurn = true;
        }

        public bool CheckCombatTrigger(Character player, Monster monster)
        {
            return player.TileX == monster.TileX && player.TileY == monster.TileY;
        }
    }
}
