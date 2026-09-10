using UnityEngine;

// Which level the next coaster run will build. Mirrors GarageSave: a plain
// static PlayerPrefs holder, no MonoBehaviour/singleton -- callable from the
// Garage picker (write) and LevelLoader (read). Stores an index into
// LevelCatalog.levels; consumers clamp it, so a catalog that shrank can't
// strand a bad index.
public static class LevelSelection
{
    private const string IndexKey = "NF_LevelIndex";

    public static int Index
    {
        get => Mathf.Max(0, PlayerPrefs.GetInt(IndexKey, 0));
        set
        {
            PlayerPrefs.SetInt(IndexKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
        }
    }
}
