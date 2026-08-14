namespace Dungeon.PartyManager
{
    /// <summary>
    /// What one adventurer is doing right now, which is what the dungeon earns from.
    /// </summary>
    /// <remarks>
    /// The energy curve used to ask a single yes-or-no question of the whole party — is it in
    /// combat? — and multiply by the worst survivor's health. That made being <i>wounded</i> the only
    /// thing that paid, and everything else followed from it: a party that could not be hurt earned a
    /// ninth of one that could, and two separate attempts to let fragile parties escape (faster
    /// archers, weaker monsters) both inverted the design's one rule, because keeping the party
    /// healthier made killing it relatively more attractive.
    /// <para>
    /// Per member and per action instead. An archer that kites a monster all raid is doing something
    /// worth watching and now earns for it, without having to stand still and bleed first.
    /// </para>
    /// </remarks>
    public enum AdventurerAction
    {
        /// <summary>Standing about with nothing to do. The floor.</summary>
        Idle = 0,

        /// <summary>Walking toward the objective. Deliberately the lowest-paying action.</summary>
        Walking = 1,

        /// <summary>Scrambling away from something too close to fight.</summary>
        Fleeing = 2,

        /// <summary>Prising a chest open, or working on a shut door.</summary>
        Working = 3,

        /// <summary>Shooting or casting at something from range.</summary>
        Shooting = 4,

        /// <summary>Toe to toe with a monster.</summary>
        Fighting = 5
    }
}
