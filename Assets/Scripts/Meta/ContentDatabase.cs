using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// The authoritative id -> asset registry for everything save data needs to reference.
/// </summary>
/// <remarks>
/// Run state is JSON, and JSON can't hold Unity object references, so <c>RunState</c> stores
/// string ids and resolves them through here. Ids are stamped once by "Rebuild From Project" and
/// then never change — renaming an asset afterwards is safe, which is exactly why save data must
/// not key off <c>characterName</c> or the asset filename.
///
/// The asset must live in a <c>Resources</c> folder and be named <c>ContentDatabase</c> so it can
/// be found without any scene wiring, from any scene.
/// </remarks>
[CreateAssetMenu(fileName = "ContentDatabase", menuName = "Scriptable Objects/Content Database")]
public class ContentDatabase : ScriptableObject
{
    private const string ResourcePath = "ContentDatabase";

    [Tooltip("Populated by 'Rebuild From Project' — right-click the asset. Don't hand-edit.")]
    [SerializeField] private List<Character> characters = new List<Character>();
    [SerializeField] private List<Action> actions = new List<Action>();

    private Dictionary<string, Character> charactersById;
    private Dictionary<string, Action> actionsById;

    private static ContentDatabase _instance;

    public static ContentDatabase Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ContentDatabase>(ResourcePath);

                if (_instance == null)
                {
                    Debug.LogError($"[ContentDatabase] No ContentDatabase found at Resources/{ResourcePath}. " +
                                   "Create one via Assets > Create > Scriptable Objects > Content Database, " +
                                   "put it in a Resources folder, and run 'Rebuild From Project' on it.");
                }
                else
                {
                    _instance.BuildLookups();
                }
            }

            return _instance;
        }
    }

    public IReadOnlyList<Character> Characters => characters;
    public IReadOnlyList<Action> Actions => actions;

    /// <summary>
    /// Builds the id lookups. Called automatically the first time <see cref="Instance"/> resolves;
    /// call it again by hand only if the lists changed at runtime.
    /// </summary>
    public void BuildLookups()
    {
        charactersById = new Dictionary<string, Character>();
        actionsById = new Dictionary<string, Action>();

        AddAll(characters, charactersById, nameof(Character));
        AddAll(actions, actionsById, nameof(Action));
    }

    private static void AddAll<T>(List<T> source, Dictionary<string, T> target, string label) where T : ScriptableObject
    {
        foreach (T entry in source)
        {
            if (entry == null)
            {
                Debug.LogWarning($"[ContentDatabase] Null {label} entry — re-run 'Rebuild From Project'.");
                continue;
            }

            string id = GetId(entry);

            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError($"[ContentDatabase] {label} '{entry.name}' has no id. Run 'Rebuild From Project' to stamp it — " +
                               "until then it can't be referenced by save data.");
                continue;
            }

            if (target.ContainsKey(id))
            {
                Debug.LogError($"[ContentDatabase] Duplicate {label} id '{id}' on '{entry.name}'. " +
                               "Ids must be unique; give one of them a different id by hand.");
                continue;
            }

            target[id] = entry;
        }
    }

    // Character and Action don't share a base type beyond ScriptableObject, so read the id per type.
    private static string GetId<T>(T asset) where T : ScriptableObject
    {
        return asset switch
        {
            Character character => character.Id,
            Action action => action.Id,
            _ => null
        };
    }

    /// <summary>Resolves a character id, or null (with an error) if it isn't registered.</summary>
    public Character GetCharacter(string id)
    {
        if (charactersById == null) BuildLookups();

        if (string.IsNullOrEmpty(id)) return null;

        if (charactersById.TryGetValue(id, out Character character))
        {
            return character;
        }

        Debug.LogError($"[ContentDatabase] No Character registered with id '{id}'. " +
                       "Either the asset was deleted or the database needs rebuilding.");
        return null;
    }

    /// <summary>Resolves an action id, or null (with an error) if it isn't registered.</summary>
    public Action GetAction(string id)
    {
        if (actionsById == null) BuildLookups();

        if (string.IsNullOrEmpty(id)) return null;

        if (actionsById.TryGetValue(id, out Action action))
        {
            return action;
        }

        Debug.LogError($"[ContentDatabase] No Action registered with id '{id}'. " +
                       "Either the asset was deleted or the database needs rebuilding.");
        return null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only. Rebuilds the database from the top menu, so it's reachable without hunting
    /// for the asset. Note [ContextMenu] entries on a ScriptableObject only show in the
    /// Inspector's ⋮ menu, never on a right-click in the Project window.
    /// </summary>
    [MenuItem("Tools/Pirate Kingdom/Rebuild Content Database")]
    private static void RebuildFromMenu()
    {
        ContentDatabase database = Resources.Load<ContentDatabase>(ResourcePath);

        if (database == null)
        {
            Debug.LogError($"[ContentDatabase] No ContentDatabase found at Resources/{ResourcePath}. " +
                           "Create one via Assets > Create > Scriptable Objects > Content Database " +
                           "and place it in a Resources folder.");
            return;
        }

        database.RebuildFromProject();
        Selection.activeObject = database;
        EditorGUIUtility.PingObject(database);
    }

    /// <summary>
    /// Editor-only. Scans the project for every Character (including Enemy) and Action asset,
    /// stamps an id onto any that lack one, and refills the lists.
    /// </summary>
    /// <remarks>
    /// Existing ids are never overwritten — that would silently break saved runs. Run this after
    /// adding or deleting content assets.
    /// </remarks>
    [ContextMenu("Rebuild From Project")]
    public void RebuildFromProject()
    {
        characters = FindAllAssets<Character>();
        actions = FindAllAssets<Action>();

        int stampedCharacters = StampMissingIds(characters, c => c.Id, (c, id) => c.EditorAssignId(id));
        int stampedActions = StampMissingIds(actions, a => a.Id, (a, id) => a.EditorAssignId(id));

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();

        BuildLookups();

        Debug.Log($"[ContentDatabase] Rebuilt: {characters.Count} characters ({stampedCharacters} newly stamped), " +
                  $"{actions.Count} actions ({stampedActions} newly stamped).");
    }

    private static List<T> FindAllAssets<T>() where T : ScriptableObject
    {
        var results = new List<T>();

        // t:Character also matches Enemy, since Enemy derives from Character.
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                results.Add(asset);
            }
        }

        return results;
    }

    private static int StampMissingIds<T>(List<T> assets, System.Func<T, string> getId, System.Action<T, string> setId)
        where T : ScriptableObject
    {
        // Seed with the ids already in use so a newly stamped asset can't collide with them.
        var used = new HashSet<string>();
        foreach (T asset in assets)
        {
            string existing = getId(asset);
            if (!string.IsNullOrEmpty(existing)) used.Add(existing);
        }

        int stamped = 0;

        foreach (T asset in assets)
        {
            if (!string.IsNullOrEmpty(getId(asset))) continue;

            string id = MakeUniqueId(asset.name, used);
            setId(asset, id);
            used.Add(id);
            EditorUtility.SetDirty(asset);
            stamped++;

            Debug.Log($"[ContentDatabase] Stamped '{asset.name}' with id '{id}'.");
        }

        return stamped;
    }

    /// <summary>"Black Sam" -> "black_sam", with a numeric suffix if that's taken.</summary>
    private static string MakeUniqueId(string assetName, HashSet<string> used)
    {
        var builder = new StringBuilder(assetName.Length);

        foreach (char c in assetName.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
            {
                // Collapse runs of separators so "Skeleton  Boss" doesn't become "skeleton__boss".
                builder.Append('_');
            }
        }

        string baseId = builder.ToString().Trim('_');
        if (string.IsNullOrEmpty(baseId)) baseId = "asset";

        string candidate = baseId;
        int suffix = 2;
        while (used.Contains(candidate))
        {
            candidate = $"{baseId}_{suffix}";
            suffix++;
        }

        return candidate;
    }
#endif
}
