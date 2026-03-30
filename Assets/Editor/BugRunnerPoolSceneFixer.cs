using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only utility: ensures the ACTIVE Main scene PoolManager has the Runner pool entry.
/// This updates the live serialized scene object (Inspector-visible), marks dirty, and saves.
/// </summary>
[InitializeOnLoad]
public static class BugRunnerPoolSceneFixer
{
    private const string RunnerKey = "Enemy_Bug_Runner";
    private const string RunnerPrefabPath = "Assets/Prefabs/Enemy_Bug_Runner.prefab";
    private const int RunnerInitialSize = 12;
    private const string TargetScenePath = "Assets/Scenes/Main.unity";

    private static bool _autoFixQueued;

    static BugRunnerPoolSceneFixer()
    {
        // Auto-apply on script reload + when a scene is opened.
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;

        QueueAutoFixIfTargetSceneIsOpen();
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (!string.Equals(scene.path, TargetScenePath, System.StringComparison.OrdinalIgnoreCase))
            return;

        QueueAutoFixIfTargetSceneIsOpen();
    }

    private static void QueueAutoFixIfTargetSceneIsOpen()
    {
        if (_autoFixQueued)
            return;

        Scene active = SceneManager.GetActiveScene();
        if (!string.Equals(active.path, TargetScenePath, System.StringComparison.OrdinalIgnoreCase))
            return;

        _autoFixQueued = true;
        EditorApplication.delayCall += () =>
        {
            _autoFixQueued = false;
            EnsureRunnerPoolEntry(auto: true);
        };
    }

    [MenuItem("BugSwarmTD/Fix Scene/Ensure Runner Pool Entry")]
    public static void EnsureRunnerPoolEntry()
    {
        EnsureRunnerPoolEntry(auto: false);
    }

    private static void EnsureRunnerPoolEntry(bool auto)
    {
        // Never mutate scene objects while in Play Mode / entering play mode.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Only operate on the intended scene.
        Scene active = SceneManager.GetActiveScene();
        if (!string.Equals(active.path, TargetScenePath, System.StringComparison.OrdinalIgnoreCase))
            return;

        // Operate on the currently open active scene object state (Inspector), not raw YAML.
        PoolManager pm = FindScenePoolManager(active);
        if (pm == null)
        {
            if (!auto)
                Debug.LogError("[BugRunnerPoolSceneFixer] No PoolManager found in the active Main scene.");
            return;
        }

        GameObject runnerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunnerPrefabPath);
        if (runnerPrefab == null)
        {
            Debug.LogError($"[BugRunnerPoolSceneFixer] Runner prefab not found at '{RunnerPrefabPath}'.");
            return;
        }

        SerializedObject so = new SerializedObject(pm);
        SerializedProperty poolsProp = so.FindProperty("pools");
        if (poolsProp == null || !poolsProp.isArray)
        {
            Debug.LogError("[BugRunnerPoolSceneFixer] Could not find PoolManager.pools serialized array.");
            return;
        }

        // Check existing entries.
        for (int i = 0; i < poolsProp.arraySize; i++)
        {
            SerializedProperty elem = poolsProp.GetArrayElementAtIndex(i);
            SerializedProperty keyProp = elem.FindPropertyRelative("key");
            if (keyProp != null && keyProp.stringValue == RunnerKey)
            {
                if (!auto)
                    Debug.Log("[BugRunnerPoolSceneFixer] Runner pool entry already present on active PoolManager.");
                return;
            }
        }

        int newIndex = poolsProp.arraySize;
        poolsProp.InsertArrayElementAtIndex(newIndex);
        SerializedProperty newElem = poolsProp.GetArrayElementAtIndex(newIndex);
        newElem.FindPropertyRelative("key").stringValue = RunnerKey;
        newElem.FindPropertyRelative("prefab").objectReferenceValue = runnerPrefab;
        newElem.FindPropertyRelative("initialSize").intValue = RunnerInitialSize;

        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(pm);
        EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
        EditorSceneManager.SaveScene(pm.gameObject.scene);

        Debug.Log($"[BugRunnerPoolSceneFixer] Added '{RunnerKey}' pool entry (size {RunnerInitialSize}) and saved scene.");
    }

    private static PoolManager FindScenePoolManager(Scene scene)
    {
        // Prefer the specifically named object to avoid picking up an inactive/duplicate PoolManager.
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (!string.Equals(roots[i].name, "PoolManager", System.StringComparison.Ordinal))
                continue;

            PoolManager pm = roots[i].GetComponent<PoolManager>();
            if (pm != null)
                return pm;
        }

        // Fallback: any PoolManager in scene.
        return Object.FindFirstObjectByType<PoolManager>();
    }
}

