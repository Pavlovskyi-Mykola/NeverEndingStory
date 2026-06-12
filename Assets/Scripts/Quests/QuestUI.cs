using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class QuestUI : MonoBehaviour
{
    [Header("Journal")]
    [SerializeField] private GameObject journalPanel;
    [SerializeField] private Button openJournalButton;
    [SerializeField] private Button closeJournalButton;
    [SerializeField] private bool closeJournalAfterSelection = true;

    [Header("List")]
    [SerializeField] private Transform listParent;
    [SerializeField] private QuestListItemUI listItemPrefab;

    [Header("Tracked Quest Details")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text objectiveText;
    [SerializeField] private Text statusText;

    [Header("Options")]
    [SerializeField] private bool showCompletedInList = false;

    private readonly List<QuestListItemUI> _spawned = new();

    // Tracking state lives in QuestJournal (persisted in saves); the UI only reflects it.
    private string TrackedQuestId => QuestJournal.Instance != null ? QuestJournal.Instance.TrackedQuestId : null;

    // View-only selection: lets the player read details of quests that can't be
    // tracked (completed ones). Transient — cleared when the journal closes, so
    // the HUD tracker panel (which shares the details texts) reverts to the
    // tracked quest.
    private string _viewedQuestId;

    private void OnEnable()
    {
        QuestManager.InstanceReady += HandleQuestManagerReady;

        if (QuestManager.Instance != null)
            HandleQuestManagerReady(QuestManager.Instance);

        QuestJournal.InstanceReady += HandleQuestJournalReady;

        if (QuestJournal.Instance != null)
            HandleQuestJournalReady(QuestJournal.Instance);

        if (openJournalButton != null)
        {
            openJournalButton.onClick.RemoveListener(OpenJournal);
            openJournalButton.onClick.AddListener(OpenJournal);
        }

        if (closeJournalButton != null)
        {
            closeJournalButton.onClick.RemoveListener(CloseJournal);
            closeJournalButton.onClick.AddListener(CloseJournal);
        }

        if (journalPanel != null)
            journalPanel.SetActive(false);

        Refresh();
    }

    private void OnDisable()
    {
        QuestManager.InstanceReady -= HandleQuestManagerReady;

        if (QuestManager.Instance != null)
            QuestManager.Instance.OnQuestStateChanged -= HandleQuestStateChanged;

        QuestJournal.InstanceReady -= HandleQuestJournalReady;

        if (QuestJournal.Instance != null)
            QuestJournal.Instance.OnTrackedQuestChanged -= HandleQuestStateChanged;

        if (openJournalButton != null)
            openJournalButton.onClick.RemoveListener(OpenJournal);

        if (closeJournalButton != null)
            closeJournalButton.onClick.RemoveListener(CloseJournal);
    }

    private void HandleQuestManagerReady(QuestManager qm)
    {
        qm.OnQuestStateChanged -= HandleQuestStateChanged;
        qm.OnQuestStateChanged += HandleQuestStateChanged;
    }

    private void HandleQuestJournalReady(QuestJournal journal)
    {
        journal.OnTrackedQuestChanged -= HandleQuestStateChanged;
        journal.OnTrackedQuestChanged += HandleQuestStateChanged;
        Refresh();
    }

    private void HandleQuestStateChanged()
    {
        Refresh();
    }

    public void OpenJournal()
    {
        if (journalPanel != null)
            journalPanel.SetActive(true);
    }

    public void CloseJournal()
    {
        if (journalPanel != null)
            journalPanel.SetActive(false);

        // Drop any view-only selection so the details/tracker shows the tracked quest again.
        if (!string.IsNullOrEmpty(_viewedQuestId))
        {
            _viewedQuestId = null;
            Refresh();
        }
    }

    public void ToggleJournal()
    {
        if (journalPanel != null)
            journalPanel.SetActive(!journalPanel.activeSelf);
    }

    public void Refresh()
    {
        var qm = QuestManager.Instance;
        var journal = QuestJournal.Instance;

        ClearList();

        if (qm == null || journal == null)
        {
            SetDetails(null, "Quest system not available.");
            return;
        }

        var ids = new List<string>();
        foreach (var id in journal.Active) ids.Add(id);

        if (showCompletedInList)
            foreach (var id in journal.Completed) ids.Add(id);

        if (ids.Count == 0)
        {
            _viewedQuestId = null;
            SetDetails("", "");
            return;
        }

        // Drop a view-only selection that left the list (e.g. hidden by options).
        if (!string.IsNullOrEmpty(_viewedQuestId) && !ids.Contains(_viewedQuestId))
            _viewedQuestId = null;

        // View-only selection wins while it exists; otherwise show the tracked
        // quest. Tracking consistency lives in QuestJournal: it clears the
        // tracked id when the quest completes or is no longer active.
        string shownId = !string.IsNullOrEmpty(_viewedQuestId) ? _viewedQuestId : TrackedQuestId;

        if (listParent != null && listItemPrefab != null)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                string questId = ids[i];
                string title = GetQuestTitle(qm, questId);

                var item = Instantiate(listItemPrefab, listParent);
                bool isSelected = questId == shownId;
                item.Bind(questId, title, OnQuestClicked, isSelected);
                _spawned.Add(item);
            }
        }

        ShowDetails(shownId);
    }

    private void OnQuestClicked(string questId)
    {
        if (string.IsNullOrEmpty(questId))
            return;

        var journal = QuestJournal.Instance;

        if (journal != null && journal.IsActive(questId))
        {
            // Active quest: toggle tracking; OnTrackedQuestChanged triggers Refresh.
            _viewedQuestId = null;
            journal.ToggleTrackedQuest(questId);

            if (closeJournalAfterSelection)
                CloseJournal();
        }
        else
        {
            // Completed (or otherwise untrackable) quest: view-only details.
            // Keep the journal open — the player is reading, not selecting.
            _viewedQuestId = _viewedQuestId == questId ? null : questId;
            Refresh();
        }
    }

    private void ShowDetails(string questId)
    {
        var qm = QuestManager.Instance;
        var journal = QuestJournal.Instance;

        if (qm == null || journal == null || string.IsNullOrEmpty(questId))
        {
            SetDetails("", "");
            return;
        }

        if (!TryGetQuestDefinition(qm, questId, out var def))
        {
            SetDetails("", "");
            return;
        }

        var prog = journal.GetOrCreateProgress(questId);

        string objective = "";

        if (journal.IsCompleted(questId))
        {
            objective = "Completed";
        }
        else if (journal.IsActive(questId) && def.Steps != null && prog != null)
        {
            if (prog.CurrentStepIndex >= 0 && prog.CurrentStepIndex < def.Steps.Count)
            {
                var step = def.Steps[prog.CurrentStepIndex];
                objective = step != null ? StripAutoPrefix(step.Text) : "";
            }
            else
            {
                objective = "";
            }
        }

        string status =
            journal.IsCompleted(questId) ? "Completed" :
            journal.IsActive(questId) ? "Active" :
            "";

        if (titleText != null) titleText.text = def.Title;
        if (descriptionText != null) descriptionText.text = def.Description;
        if (objectiveText != null) objectiveText.text = string.IsNullOrEmpty(objective) ? "" : objective;
        if (statusText != null) statusText.text = status;
    }

    private void SetDetails(string title, string msg)
    {
        if (titleText != null) titleText.text = title ?? "";
        if (descriptionText != null) descriptionText.text = msg ?? "";
        if (objectiveText != null) objectiveText.text = "";
        if (statusText != null) statusText.text = "";
    }

    private void ClearList()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i].gameObject);
        }

        _spawned.Clear();
    }

    private static string StripAutoPrefix(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        const string prefix = "[AUTO] ";
        return s.StartsWith(prefix, StringComparison.Ordinal) ? s.Substring(prefix.Length) : s;
    }

    private bool TryGetQuestDefinition(QuestManager qm, string questId, out QuestDefinition def)
    {
        def = null;
        if (qm.TryGetDefinition(questId, out def))
            return def != null;

        return false;
    }

    private string GetQuestTitle(QuestManager qm, string questId)
    {
        if (TryGetQuestDefinition(qm, questId, out var def) && def != null)
            return string.IsNullOrEmpty(def.Title) ? questId : def.Title;

        return questId;
    }
}