using UnityEngine;

// Runtime-loadable list of every CreatureDefinition in the game (the Squirrel
// starter plus the Owl..Dragon unlock ladder). AssetDatabase (used by editor
// scripts like SetupPremiumCreatures.cs to discover the CreatureDefinition
// assets by folder) doesn't work in builds, so the Garage scene needs a
// single asset it can reference directly instead. Built once by
// Assets/Editor/SetupCreatureRoster.cs.
[CreateAssetMenu(fileName = "CreatureRoster", menuName = "NuttyFluffies/Creature Roster")]
public class CreatureRoster : ScriptableObject
{
    public CreatureDefinition[] creatures;

    public CreatureDefinition FindByName(string displayName)
    {
        if (creatures == null) return null;
        foreach (var def in creatures)
        {
            if (def != null && def.displayName == displayName) return def;
        }
        return null;
    }
}
