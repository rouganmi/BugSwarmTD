/*
 * WIP (not integrated): Node1 intel/completion UI controller.
 * Scene-based only, but currently not attached in any scene in the baseline.
 * Reserved for future, explicit Chapter1_Node1 integration (no gameplay authority).
 */
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NodeIntroPanelController : MonoBehaviour
{
    [Header("References (scene-based)")]
    [SerializeField] private ChapterNodeController node;
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("UI (scene-based)")]
    [SerializeField] private GameObject intelButton;
    [SerializeField] private Button intelButtonComponent;
    [SerializeField] private GameObject intelPanel;
    [SerializeField] private GameObject completionToast;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text completionText;

    [Header("Completion detection")]
    [SerializeField] private int expectedFinalWave = 8;

    private bool _completedShown;

    private void Start()
    {
        if (node == null)
        {
            Debug.LogError("[Node] ChapterNodeController reference missing.", this);
            enabled = false;
            return;
        }

        if (enemySpawner == null)
            enemySpawner = FindObjectOfType<EnemySpawner>();

        if (intelButtonComponent == null && intelButton != null)
            intelButtonComponent = intelButton.GetComponent<Button>();

        if (intelButtonComponent == null)
            Debug.LogError("[Node] Intel button Button component missing.", this);
        else
        {
            intelButtonComponent.onClick.RemoveAllListeners();
            intelButtonComponent.onClick.AddListener(ToggleIntel);
        }

        node.StartNode();

        if (intelButton != null)
            intelButton.SetActive(node.autoShowStartIntel);

        if (intelPanel != null)
            intelPanel.SetActive(false);

        if (completionToast != null)
            completionToast.SetActive(false);

        RefreshPanelText();
    }

    private void Update()
    {
        if (node == null) return;
        if (node.State != ChapterNodeController.NodeState.Running) return;

        if (!_completedShown && IsNodeComplete())
        {
            node.CompleteNode();
            _completedShown = true;
            if (completionToast != null)
                completionToast.SetActive(true);
        }
    }

    private bool IsNodeComplete()
    {
        if (enemySpawner == null)
            enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner == null)
            return false;

        int w = enemySpawner.GetCurrentWave();
        if (w < expectedFinalWave) return false;
        if (FindObjectsOfType<Enemy>().Length != 0) return false;
        if (Time.timeScale <= 0f) return true;
        if (enemySpawner.IsWaitingForNextWave()) return true;
        return false;
    }

    private void RefreshPanelText()
    {
        if (titleText != null)
            titleText.text = node.nodeDisplayName;
        if (bodyText != null)
            bodyText.text = node.shortDescription;
    }

    private void ToggleIntel()
    {
        if (intelPanel == null) return;
        bool next = !intelPanel.activeSelf;
        intelPanel.SetActive(next);
        Debug.Log("[Node] Intel toggled " + (next ? "On" : "Off"), this);
    }
}

