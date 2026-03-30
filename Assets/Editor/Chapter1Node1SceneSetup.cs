using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

public static class Chapter1Node1SceneSetup
{
    // WIP utility: not part of the active pipeline. Use only when Node1 integration is explicitly scheduled.
    [MenuItem("Tools/BugSwarmTD/WIP/Setup Chapter1 Node1 UI (Main Scene)")]
    public static void Setup()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "Main")
        {
            EditorUtility.DisplayDialog("Setup Chapter1 Node1", "Open the Main scene first.", "OK");
            return;
        }

        var canvasGo = GameObject.Find("GameCanvas");
        if (canvasGo == null)
        {
            EditorUtility.DisplayDialog("Setup Chapter1 Node1", "Couldn't find GameCanvas in scene.", "OK");
            return;
        }

        var nodeRoot = GameObject.Find("Chapter1_Node1") ?? new GameObject("Chapter1_Node1");
        var node = nodeRoot.GetComponent<ChapterNodeController>() ?? nodeRoot.AddComponent<ChapterNodeController>();
        var ui = nodeRoot.GetComponent<NodeIntroPanelController>() ?? nodeRoot.AddComponent<NodeIntroPanelController>();

        var nodeUiRoot = canvasGo.transform.Find("NodeUI")?.gameObject ?? new GameObject("NodeUI", typeof(RectTransform));
        nodeUiRoot.transform.SetParent(canvasGo.transform, false);
        var nodeUiRt = nodeUiRoot.GetComponent<RectTransform>();
        nodeUiRt.anchorMin = Vector2.zero;
        nodeUiRt.anchorMax = Vector2.one;
        nodeUiRt.offsetMin = Vector2.zero;
        nodeUiRt.offsetMax = Vector2.zero;

        var intelButtonGo = nodeUiRoot.transform.Find("IntelButton")?.gameObject ?? CreateButton(nodeUiRoot.transform, "IntelButton", "Intel");
        var intelButtonRt = intelButtonGo.GetComponent<RectTransform>();
        intelButtonRt.anchorMin = new Vector2(0f, 0.65f);
        intelButtonRt.anchorMax = new Vector2(0f, 0.65f);
        intelButtonRt.pivot = new Vector2(0f, 0.5f);
        intelButtonRt.anchoredPosition = new Vector2(12f, 0f);
        intelButtonRt.sizeDelta = new Vector2(92f, 36f);

        var intelPanelGo = nodeUiRoot.transform.Find("IntelPanel")?.gameObject ?? CreatePanel(nodeUiRoot.transform, "IntelPanel");
        var intelPanelRt = intelPanelGo.GetComponent<RectTransform>();
        intelPanelRt.anchorMin = new Vector2(0f, 0.65f);
        intelPanelRt.anchorMax = new Vector2(0f, 0.65f);
        intelPanelRt.pivot = new Vector2(0f, 0.5f);
        intelPanelRt.anchoredPosition = new Vector2(112f, 0f);
        intelPanelRt.sizeDelta = new Vector2(360f, 160f);

        var title = intelPanelGo.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>() ??
                    CreateTMP(intelPanelGo.transform, "TitleText", 22, FontStyles.Bold);
        var titleRt = title.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(12f, -10f);
        titleRt.sizeDelta = new Vector2(-24f, 32f);

        var body = intelPanelGo.transform.Find("BodyText")?.GetComponent<TextMeshProUGUI>() ??
                   CreateTMP(intelPanelGo.transform, "BodyText", 16, FontStyles.Normal);
        body.enableWordWrapping = true;
        var bodyRt = body.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 1f);
        bodyRt.pivot = new Vector2(0f, 1f);
        bodyRt.anchoredPosition = new Vector2(12f, -44f);
        bodyRt.sizeDelta = new Vector2(-24f, -56f);

        var completionGo = nodeUiRoot.transform.Find("CompletionToast")?.gameObject ?? new GameObject("CompletionToast", typeof(RectTransform));
        completionGo.transform.SetParent(nodeUiRoot.transform, false);
        var completionRt = completionGo.GetComponent<RectTransform>();
        completionRt.anchorMin = new Vector2(0f, 0.78f);
        completionRt.anchorMax = new Vector2(0f, 0.78f);
        completionRt.pivot = new Vector2(0f, 0.5f);
        completionRt.anchoredPosition = new Vector2(12f, 0f);
        completionRt.sizeDelta = new Vector2(220f, 32f);

        var completionText = completionGo.transform.Find("Text")?.GetComponent<TextMeshProUGUI>() ??
                             CreateTMP(completionGo.transform, "Text", 18, FontStyles.Bold);
        completionText.color = new Color(0.85f, 1f, 0.75f, 1f);
        completionText.alignment = TextAlignmentOptions.Left;
        completionText.text = "Node Complete";
        var completionTextRt = completionText.GetComponent<RectTransform>();
        completionTextRt.anchorMin = Vector2.zero;
        completionTextRt.anchorMax = Vector2.one;
        completionTextRt.offsetMin = Vector2.zero;
        completionTextRt.offsetMax = Vector2.zero;

        // Wire references (serialized fields).
        SerializedObject so = new SerializedObject(ui);
        so.FindProperty("node").objectReferenceValue = node;
        so.FindProperty("intelButton").objectReferenceValue = intelButtonGo;
        so.FindProperty("intelButtonComponent").objectReferenceValue = intelButtonGo.GetComponent<Button>();
        so.FindProperty("intelPanel").objectReferenceValue = intelPanelGo;
        so.FindProperty("completionToast").objectReferenceValue = completionGo;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("completionText").objectReferenceValue = completionText;
        so.FindProperty("expectedFinalWave").intValue = 8;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(nodeRoot);
        EditorUtility.SetDirty(nodeUiRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = nodeRoot;
    }

    private static GameObject CreatePanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
        return go;
    }

    private static GameObject CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f, 0.92f);

        var text = CreateTMP(go.transform, "Label", 18, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        text.text = label;
        var rt = text.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return go;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, float fontSize, FontStyles styles)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = fontSize;
        t.fontStyle = styles;
        t.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        t.raycastTarget = false;
        return t;
    }
}

