using UnityEngine;

// Runtime-loadable, ordered list of every LevelDefinition in the game.
// AssetDatabase folder discovery (used by the editor batch tool) doesn't work
// in builds, so LevelLoader and the Garage level picker reference this one
// asset instead -- same reason CreatureRoster exists.
// Built / refreshed by Assets/Editor/GenerateLevelBatch.cs.
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "NuttyFluffies/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    public LevelDefinition[] levels;

    public int Count => levels != null ? levels.Length : 0;

    // Clamped so a catalog that shrank can never strand a stale saved index.
    public LevelDefinition Get(int index)
    {
        if (levels == null || levels.Length == 0) return null;
        return levels[Mathf.Clamp(index, 0, levels.Length - 1)];
    }
}
