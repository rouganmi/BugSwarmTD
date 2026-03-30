using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot editor utility: normalize non-unit localRotation on Transforms in the active scene (fixes QuaternionToEuler editor warnings).
/// </summary>
public static class SceneRotationSanitizer
{
    const string MenuPath = "Tools/Rotation Debug/Normalize Non-Unit Local Rotations In Open Scene";

    /// <summary>Fix if |magSq - 1| exceeds this (matches CameraController defensive tolerance).</summary>
    const float UnitMagSqFixThreshold = 1e-4f;

    [MenuItem(MenuPath)]
    static void NormalizeOpenSceneLocalRotations()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.isLoaded)
        {
            Debug.LogWarning("[SceneRotationSanitizer] No active loaded scene.");
            return;
        }

        var transforms = CollectTransformsInScene(scene);
        int fixedCount = 0;
        var special = new Dictionary<string, string>(3)
        {
            { "Main Camera", " (not fixed)" },
            { "CameraRig", " (not fixed)" },
            { "CameraBoundsSource", " (not fixed)" }
        };

        foreach (var t in transforms)
        {
            if (t == null)
                continue;

            Quaternion q = t.localRotation;
            float sx = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            float magSqBefore = sx;

            bool needFix;
            Quaternion qAfter;

            if (!RotationDebug.IsFinite(q))
            {
                needFix = true;
                qAfter = Quaternion.identity;
            }
            else if (sx <= 1e-20f || float.IsInfinity(sx))
            {
                needFix = true;
                qAfter = Quaternion.identity;
            }
            else if (Mathf.Abs(sx - 1f) > UnitMagSqFixThreshold)
            {
                needFix = true;
                qAfter = RotationDebug.NormalizeOrIdentity(q);
                float s2 = qAfter.x * qAfter.x + qAfter.y * qAfter.y + qAfter.z * qAfter.z + qAfter.w * qAfter.w;
                if (!RotationDebug.IsFinite(qAfter) || Mathf.Abs(s2 - 1f) > UnitMagSqFixThreshold)
                    qAfter = Quaternion.identity;
            }
            else
            {
                needFix = false;
                qAfter = q;
            }

            if (!needFix)
                continue;

            Undo.RecordObject(t, "Normalize localRotation (SceneRotationSanitizer)");
            t.localRotation = qAfter;
            EditorUtility.SetDirty(t);

            float magSqAfter = qAfter.x * qAfter.x + qAfter.y * qAfter.y + qAfter.z * qAfter.z + qAfter.w * qAfter.w;
            fixedCount++;

            string path = GetHierarchyPath(t);
            string line = "[SceneRotationSanitizer] Fixed: name=" + t.name + " | Path=" + path +
                          " | magSqBefore=" + magSqBefore + " | magSqAfter=" + magSqAfter;
            Debug.Log(line, t.gameObject);

            if (special.ContainsKey(t.name))
                special[t.name] = " | FIXED | Path=" + path + " | magSqBefore=" + magSqBefore + " | magSqAfter=" + magSqAfter;
        }

        if (fixedCount > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            "[SceneRotationSanitizer] === SUMMARY ===\n" +
            "Scene: " + scene.path + " (" + scene.name + ")\n" +
            "Total transforms scanned: " + transforms.Count + "\n" +
            "Transforms fixed: " + fixedCount + "\n" +
            "--- Named checks ---\n" +
            "Main Camera: " + special["Main Camera"] + "\n" +
            "CameraRig: " + special["CameraRig"] + "\n" +
            "CameraBoundsSource: " + special["CameraBoundsSource"] + "\n" +
            "=== End (copy Console above) ===");
    }

    static List<Transform> CollectTransformsInScene(Scene scene)
    {
        var list = new List<Transform>(512);
        var roots = scene.GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var root = roots[i];
            if (root == null)
                continue;
            var trs = root.GetComponentsInChildren<Transform>(true);
            for (var j = 0; j < trs.Length; j++)
            {
                if (trs[j] != null)
                    list.Add(trs[j]);
            }
        }

        return list;
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
}
