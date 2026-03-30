using System.Collections.Generic;
using System.Text;
using BugSwarmTD.Editor.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BugSwarmTD.Editor.Diagnostics
{
    /// <summary>
    /// Editor-only, read-only scan for non-unit or invalid Transform rotations. Does not modify objects.
    /// </summary>
    public sealed class RotationDiagnosticsWindow : EditorWindow
    {
        const string MainScenePath = "Assets/Scenes/Main.unity";
        public const string MenuPathRoot = "Tools/BugSwarmTD/Rotation Diagnostics";

        bool _strictTolerance;

        Vector2 _scroll;
        readonly List<string> _lastTopLevelBad = new List<string>();
        string _lastSummary = "";

        /// <summary>Unity does not allow the same path to be both a leaf and a submenu; use Open Window here.</summary>
        [MenuItem(MenuPathRoot + "/Open Window", false, 0)]
        static void Open()
        {
            var w = GetWindow<RotationDiagnosticsWindow>(false, "Rotation Diagnostics", true);
            w.minSize = new Vector2(460f, 420f);
        }

        [MenuItem(MenuPathRoot + "/Scan named objects (Camera / Main / Hex…)", false, 11)]
        static void MenuScanNamedObjects()
        {
            Open();
            GetWindow<RotationDiagnosticsWindow>().RunNamedSceneObjectsScan();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Read-only diagnostic. Uses quaternion components + magSq only (no euler, no Transform.rotation). " +
                "World check uses localRotation chain multiply.",
                EditorStyles.wordWrappedLabel);

            _strictTolerance = EditorGUILayout.ToggleLeft(
                "Strict tolerance (flag tiny |magSq−1|; good for hunting float drift)",
                _strictTolerance);

            float tol = _strictTolerance
                ? RotationDiagnosticsMath.UnitMagSqToleranceStrict
                : RotationDiagnosticsMath.UnitMagSqToleranceDefault;

            EditorGUILayout.HelpBox(
                "Tolerance on |magSq−1|: " + tol +
                "  (default " + RotationDiagnosticsMath.UnitMagSqToleranceDefault +
                ", strict " + RotationDiagnosticsMath.UnitMagSqToleranceStrict + ")",
                MessageType.Info);

            EditorGUILayout.Space(4f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Scene hierarchy", EditorStyles.boldLabel);

                if (GUILayout.Button("Scan CURRENT ACTIVE (open) scene only"))
                    RunScanActiveSceneOnly(tol);

                if (GUILayout.Button("Scan all currently loaded scene(s)"))
                    RunScanAllLoadedScenes(tol);

                if (GUILayout.Button("Scan SELECTED root GameObject subtree only"))
                    RunScanSelectedRootSubtree(tol);

                if (GUILayout.Button("Scan Main.unity — all root objects' hierarchies (if Main is loaded)"))
                    RunScanMainSceneAllRoots(tol);
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Targeted names (loaded scenes)", EditorStyles.boldLabel);
                if (GUILayout.Button("Scan named objects: Main Camera, CameraRig, CameraBoundsSource, BuildManager, HexGrid, EnemyInfoHUD"))
                    RunNamedSceneObjectsScan();
            }

            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Prefabs / assets", EditorStyles.boldLabel);

                if (GUILayout.Button("Scan prefabs under Assets/Prefabs"))
                    RunScanPrefabFolder("Assets/Prefabs", tol);

                if (GUILayout.Button("Scan prefabs under Assets/Art (if folder exists)"))
                {
                    if (AssetDatabase.IsValidFolder("Assets/Art"))
                        RunScanPrefabFolder("Assets/Art", tol);
                    else
                        Debug.LogWarning("[RotationDiagnostics] Folder Assets/Art does not exist; skipped.");
                }

                if (GUILayout.Button("Scan all prefabs under Assets (may be slow)"))
                    RunScanPrefabFolder("Assets", tol);

                if (GUILayout.Button("Scan selected Project assets (prefabs)"))
                    RunScanSelection(tol);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Last run summary", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(string.IsNullOrEmpty(_lastSummary) ? "(none)" : _lastSummary, GUILayout.MinHeight(48f));

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_lastTopLevelBad.Count > 0)
            {
                EditorGUILayout.LabelField("Bad objects (top-level names)", EditorStyles.boldLabel);
                foreach (var line in _lastTopLevelBad)
                    EditorGUILayout.SelectableLabel(line, GUILayout.Height(18f));
            }
            EditorGUILayout.EndScrollView();
        }

        static float GetToleranceForWindow(bool strict) =>
            strict
                ? RotationDiagnosticsMath.UnitMagSqToleranceStrict
                : RotationDiagnosticsMath.UnitMagSqToleranceDefault;

        /// <summary>World rotation from parent chain using only localRotation (does not read Transform.rotation).</summary>
        static Quaternion ComputeWorldRotationFromLocals(Transform t)
        {
            var list = new List<Transform>(8);
            for (Transform c = t; c != null; c = c.parent)
                list.Add(c);
            if (list.Count == 0)
                return Quaternion.identity;
            var w = list[list.Count - 1].localRotation;
            for (int i = list.Count - 2; i >= 0; i--)
                w = w * list[i].localRotation;
            return w;
        }

        static void LogSuspicious(
            Quaternion q,
            Transform t,
            string context,
            bool isLocalRotation,
            float magSq,
            float absMagSqMinus1,
            bool nan,
            bool inf)
        {
            var go = t.gameObject;
            var sc = go.scene;
            var sceneName = sc.IsValid() ? sc.name : "(no loaded scene — e.g. prefab asset)";
            var path = GetHierarchyPath(t);
            var field = isLocalRotation ? "localRotation" : "rotation (from local chain)";

            Debug.LogWarning(
                "[RotationDiagnostics] BAD " + field + "\n" +
                "Scene: " + sceneName + "\n" +
                "Context: " + context + "\n" +
                "Path: " + path + "\n" +
                "isLocalRotation: " + isLocalRotation + "\n" +
                "quaternion (x, y, z, w): (" + q.x + ", " + q.y + ", " + q.z + ", " + q.w + ")\n" +
                "magSq: " + magSq + "\n" +
                "abs(magSq - 1): " + absMagSqMinus1 + "\n" +
                "NaN: " + nan + "\n" +
                "Infinity: " + inf,
                go);
        }

        static string GetHierarchyPath(Transform t)
        {
            if (t == null)
                return "";
            var sb = new StringBuilder(256);
            Transform cur = t;
            while (cur != null)
            {
                if (sb.Length > 0)
                    sb.Insert(0, '/');
                sb.Insert(0, string.IsNullOrEmpty(cur.name) ? "?" : cur.name);
                cur = cur.parent;
            }
            return sb.ToString();
        }

        static void ScanTransforms(
            IEnumerable<Transform> transforms,
            string contextLabel,
            float unitMagSqTolerance,
            ref int badLocal,
            ref int badWorld,
            HashSet<string> topLevel)
        {
            foreach (var t in transforms)
            {
                if (t == null)
                    continue;

                var local = t.localRotation;
                if (RotationDiagnosticsMath.IsSuspicious(local, unitMagSqTolerance, out float magSqL, out float absL, out bool nanL, out bool infL))
                {
                    badLocal++;
                    LogSuspicious(local, t, contextLabel, true, magSqL, absL, nanL, infL);
                    var root = t.root;
                    if (root != null)
                        topLevel.Add(root.name + "  (" + contextLabel + ")");
                }

                var world = ComputeWorldRotationFromLocals(t);
                if (RotationDiagnosticsMath.IsSuspicious(world, unitMagSqTolerance, out float magSqW, out float absW, out bool nanW, out bool infW))
                {
                    badWorld++;
                    LogSuspicious(world, t, contextLabel, false, magSqW, absW, nanW, infW);
                    var root = t.root;
                    if (root != null)
                        topLevel.Add(root.name + "  (" + contextLabel + ")");
                }
            }
        }

        static IEnumerable<Transform> EnumerateAllLoadedSceneTransforms()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == null)
                        continue;
                    var trs = root.GetComponentsInChildren<Transform>(true);
                    for (var t = 0; t < trs.Length; t++)
                        yield return trs[t];
                }
            }
        }

        static IEnumerable<Transform> EnumerateActiveSceneTransforms()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded)
                yield break;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;
                var trs = root.GetComponentsInChildren<Transform>(true);
                for (var i = 0; i < trs.Length; i++)
                    yield return trs[i];
            }
        }

        void RunScanActiveSceneOnly(float tol)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isLoaded)
            {
                Debug.LogWarning("[RotationDiagnostics] No active scene loaded.");
                return;
            }

            int badLocal = 0, badWorld = 0;
            var top = new HashSet<string>();
            ScanTransforms(EnumerateActiveSceneTransforms(), "Active scene: " + scene.name, tol, ref badLocal, ref badWorld, top);
            FinishRun(badLocal, badWorld, top, "Scan active scene only (" + scene.name + ")");
        }

        void RunScanAllLoadedScenes(float tol)
        {
            int badLocal = 0, badWorld = 0;
            var top = new HashSet<string>();
            ScanTransforms(EnumerateAllLoadedSceneTransforms(), "All loaded scene(s)", tol, ref badLocal, ref badWorld, top);
            FinishRun(badLocal, badWorld, top, "Scan all loaded scene(s)");
        }

        void RunScanSelectedRootSubtree(float tol)
        {
            var go = Selection.activeGameObject;
            if (go == null)
            {
                Debug.LogWarning("[RotationDiagnostics] No GameObject selected. Select one root in the Hierarchy.");
                return;
            }

            var list = go.GetComponentsInChildren<Transform>(true);
            int badLocal = 0, badWorld = 0;
            var top = new HashSet<string>();
            ScanTransforms(list, "Selected root: " + go.name, tol, ref badLocal, ref badWorld, top);
            FinishRun(badLocal, badWorld, top, "Scan selected root subtree: " + go.name);
        }

        void RunScanMainSceneAllRoots(float tol)
        {
            var scene = EditorSceneManager.GetSceneByPath(MainScenePath);
            if (!scene.isLoaded)
            {
                Debug.LogWarning("[RotationDiagnostics] Main scene not loaded: " + MainScenePath + " — open it, then run again.");
                _lastSummary = "Main scene not loaded.";
                _lastTopLevelBad.Clear();
                return;
            }

            var list = new List<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;
                list.AddRange(root.GetComponentsInChildren<Transform>(true));
            }

            int badLocal = 0, badWorld = 0;
            var top = new HashSet<string>();
            ScanTransforms(list, MainScenePath + " (all roots)", tol, ref badLocal, ref badWorld, top);
            FinishRun(badLocal, badWorld, top, "Scan Main.unity all root hierarchies");
        }

        /// <summary>Menu + window: scan subtrees of well-known object names if present in any loaded scene.</summary>
        public void RunNamedSceneObjectsScan()
        {
            var tol = GetToleranceForWindow(_strictTolerance);
            var names = new[]
            {
                "Main Camera",
                "CameraRig",
                "CameraBoundsSource",
                "BuildManager",
                "HexGrid",
                "EnemyInfoHUD"
            };

            var aggregated = new List<Transform>();
            foreach (var n in names)
            {
                var found = FindAllTransformsByNameInLoadedScenes(n);
                if (found.Count == 0)
                    Debug.Log("[RotationDiagnostics] Named object not found in loaded scenes: " + n);
                else
                {
                    foreach (var rootTr in found)
                    {
                        if (rootTr == null)
                            continue;
                        aggregated.AddRange(rootTr.GetComponentsInChildren<Transform>(true));
                    }
                }
            }

            if (aggregated.Count == 0)
            {
                Debug.LogWarning("[RotationDiagnostics] No named objects found; nothing to scan.");
                FinishRun(0, 0, new HashSet<string>(), "Named objects scan (none found)");
                return;
            }

            var unique = new List<Transform>(aggregated.Count);
            var seen = new HashSet<Transform>();
            for (var i = 0; i < aggregated.Count; i++)
            {
                var tr = aggregated[i];
                if (tr == null || !seen.Add(tr))
                    continue;
                unique.Add(tr);
            }

            int badLocal = 0, badWorld = 0;
            var top = new HashSet<string>();
            ScanTransforms(unique, "Named scene objects (Main Camera, CameraRig, …)", tol, ref badLocal, ref badWorld, top);
            FinishRun(badLocal, badWorld, top, "Scan named scene objects");
        }

        static List<Transform> FindAllTransformsByNameInLoadedScenes(string objectName)
        {
            var list = new List<Transform>();
            for (var si = 0; si < SceneManager.sceneCount; si++)
            {
                var scene = SceneManager.GetSceneAt(si);
                if (!scene.isLoaded)
                    continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root == null)
                        continue;
                    var trs = root.GetComponentsInChildren<Transform>(true);
                    for (var i = 0; i < trs.Length; i++)
                    {
                        if (trs[i] != null && trs[i].name == objectName)
                            list.Add(trs[i]);
                    }
                }
            }

            return list;
        }

        void RunScanPrefabFolder(string folder, float tol)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("[RotationDiagnostics] Not a valid folder: " + folder);
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            int badLocal = 0, badWorld = 0;
            var top = new HashSet<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null)
                    continue;
                var trs = go.GetComponentsInChildren<Transform>(true);
                ScanTransforms(trs, path, tol, ref badLocal, ref badWorld, top);
            }

            FinishRun(badLocal, badWorld, top, "Scan prefabs in " + folder + " (" + guids.Length + " prefabs)");
        }

        void RunScanSelection(float tol)
        {
            var objs = Selection.objects;
            if (objs == null || objs.Length == 0)
            {
                Debug.LogWarning("[RotationDiagnostics] No assets selected.");
                return;
            }

            int badLocal = 0, badWorld = 0;
            var top = new HashSet<string>();

            foreach (var obj in objs)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (obj is GameObject goAsset)
                {
                    var trs = goAsset.GetComponentsInChildren<Transform>(true);
                    ScanTransforms(trs, path, tol, ref badLocal, ref badWorld, top);
                }
            }

            FinishRun(badLocal, badWorld, top, "Scan selection");
        }

        void FinishRun(int badLocal, int badWorld, HashSet<string> topLevel, string label)
        {
            var total = badLocal + badWorld;
            _lastSummary = "[RotationDiagnostics] SUMMARY — " + label + "\n" +
                           "Strict mode: " + _strictTolerance + "\n" +
                           "Suspicious localRotation reports: " + badLocal + "\n" +
                           "Suspicious world-rotation reports: " + badWorld + "\n" +
                           "Total bad field reports: " + total;
            Debug.Log(_lastSummary);

            _lastTopLevelBad.Clear();
            _lastTopLevelBad.AddRange(topLevel);
            Repaint();
        }
    }
}
