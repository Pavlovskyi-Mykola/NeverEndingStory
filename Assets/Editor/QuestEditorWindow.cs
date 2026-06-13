#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public class QuestEditorWindow : EditorWindow
{
    // ── Layout ────────────────────────────────────────────────────────────
    private const float LeftPanelWidth = 230f;

    // ── State ─────────────────────────────────────────────────────────────
    private QuestDefinition _quest;
    private SerializedObject     _so;
    private SerializedProperty   _questIdProp, _titleProp, _descProp, _stepsProp, _rewardsProp;
    private SerializedProperty   _autoStartProp, _startConditionProp, _repeatableProp, _repeatCooldownProp;

    private Vector2 _leftScroll, _rightScroll;
    private string  _searchFilter = "";
    private readonly HashSet<int> _expandedSteps = new();

    // Quest list cache
    private readonly List<QuestDefinition> _allQuests = new();
    private double _nextListRefresh;
    private const double ListRefreshInterval = 2.0;

    // Duplicate-check cache
    private string _lastCheckedId;
    private bool   _isDuplicate;

    // ── Step type colours ─────────────────────────────────────────────────
    private static readonly Dictionary<QuestStepType, Color> StepColors = new()
    {
        { QuestStepType.Manual,         new Color(0.25f, 0.40f, 0.58f) },
        { QuestStepType.AutoComplete,   new Color(0.32f, 0.32f, 0.32f) },
        { QuestStepType.ReachLocation,  new Color(0.15f, 0.50f, 0.44f) },
        { QuestStepType.TalkToDialogue, new Color(0.42f, 0.24f, 0.56f) },
        { QuestStepType.HaveItem,       new Color(0.55f, 0.42f, 0.14f) },
        { QuestStepType.HaveMoney,      new Color(0.20f, 0.48f, 0.24f) },
        { QuestStepType.PayMoney,       new Color(0.58f, 0.27f, 0.14f) },
        { QuestStepType.MinStats,       new Color(0.20f, 0.30f, 0.60f) },
        { QuestStepType.TimeWindow,     new Color(0.18f, 0.48f, 0.54f) },
    };

    // ── Day/Phase toggle data ─────────────────────────────────────────────
    private static readonly string[] DayLabels   = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly int[]    DayBits     = { 1, 2, 4, 8, 16, 32, 64 };
    private static readonly string[] PhaseLabels = { "Morning", "Afternoon", "Evening", "Night" };
    private static readonly int[]    PhaseBits   = { 1, 2, 4, 8 };
    private static readonly Color    ColOn  = new Color(0.25f, 0.60f, 1.00f);
    private static readonly Color    ColOff = new Color(0.28f, 0.28f, 0.28f);

    // ── Open ──────────────────────────────────────────────────────────────
    [MenuItem("Game/Quests/Quest Editor")]
    public static void Open()
    {
        var w = GetWindow<QuestEditorWindow>();
        w.titleContent = new GUIContent("Quest Editor");
        w.minSize = new Vector2(720, 500);
        w.Show();
    }

    public static void OpenQuest(QuestDefinition quest)
    {
        var w = GetWindow<QuestEditorWindow>();
        w.titleContent = new GUIContent("Quest Editor");
        w.minSize = new Vector2(720, 500);
        w.SetQuest(quest);
        w.Show();
        w.Focus();
    }

    // ── Unity callbacks ───────────────────────────────────────────────────
    private void OnEnable()  => RefreshQuestList();
    private void OnFocus()   => RefreshQuestList();

    private void OnGUI()
    {
        RefreshQuestListIfNeeded();
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLeftPanel();
            DrawDivider();
            DrawRightPanel();
        }
    }

    // ── Toolbar ───────────────────────────────────────────────────────────
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var picked = (QuestDefinition)EditorGUILayout.ObjectField(
                _quest, typeof(QuestDefinition), false, GUILayout.Width(280));
            if (picked != _quest) SetQuest(picked);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("New Quest", EditorStyles.toolbarButton, GUILayout.Width(80)))
                CreateNewQuest();

            using (new EditorGUI.DisabledScope(_quest == null))
            {
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    RunFullValidation();

                if (GUILayout.Button("Ping Asset", EditorStyles.toolbarButton, GUILayout.Width(72)))
                {
                    EditorGUIUtility.PingObject(_quest);
                    Selection.activeObject = _quest;
                }
            }
        }
    }

    // ── Left panel – quest list ───────────────────────────────────────────
    private void DrawLeftPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPanelWidth), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField($"All Quests ({_allQuests.Count})", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck()) Repaint();

            EditorGUILayout.Space(2);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.ExpandHeight(true));

            string search = _searchFilter.ToLowerInvariant();

            foreach (var q in _allQuests)
            {
                if (q == null) continue;
                if (!string.IsNullOrEmpty(search))
                {
                    bool match = (q.QuestId?.ToLowerInvariant().Contains(search) ?? false)
                              || (q.Title?.ToLowerInvariant().Contains(search) ?? false);
                    if (!match) continue;
                }

                bool isSelected = q == _quest;
                var  rowRect    = GUILayoutUtility.GetRect(LeftPanelWidth - 8, 36f);

                if (isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.50f, 0.86f, 0.35f));
                else if (rowRect.Contains(Event.current.mousePosition))
                    EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));

                // Left colour strip by step count / validity
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3, rowRect.height),
                    string.IsNullOrEmpty(q.QuestId) ? Color.red : new Color(0.3f, 0.7f, 0.3f));

                string title = string.IsNullOrEmpty(q.Title) ? q.name : q.Title;
                string sub   = string.IsNullOrEmpty(q.QuestId) ? "⚠ No ID" : q.QuestId;

                GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 4,  rowRect.width - 10, 16), title, EditorStyles.boldLabel);
                GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 20, rowRect.width - 10, 12), sub,   EditorStyles.miniLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    SetQuest(q);
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ New Quest"))
                CreateNewQuest();
        }
    }

    private void DrawDivider()
    {
        var r = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f, 0.6f));
    }

    // ── Right panel – quest editor ────────────────────────────────────────
    private void DrawRightPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
        {
            if (_quest == null)
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.HelpBox("Select a quest or create a new one.", MessageType.Info, true);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.FlexibleSpace();
                return;
            }

            EnsureSerialized();
            _so.Update();

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            DrawIdentitySection();
            SectionGap();
            DrawAvailabilitySection();
            SectionGap();
            DrawStepsSection();
            SectionGap();
            DrawRewardsSection();
            GUILayout.Space(24);

            EditorGUILayout.EndScrollView();

            _so.ApplyModifiedProperties();
        }
    }

    // ── Identity section ──────────────────────────────────────────────────
    private void DrawIdentitySection()
    {
        SectionHeader("Identity");

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(_questIdProp);
            if (GUILayout.Button("Generate", GUILayout.Width(70)))
            {
                _questIdProp.stringValue = SlugifyTitle(_titleProp.stringValue);
                _lastCheckedId = null;
                _so.ApplyModifiedProperties();
            }
        }

        EditorGUILayout.PropertyField(_titleProp);
        EditorGUILayout.PropertyField(_descProp);

        EditorGUILayout.Space(4);

        string id = _questIdProp.stringValue;
        if (string.IsNullOrEmpty(id))
            EditorGUILayout.HelpBox("QuestId is empty. Set a unique id or press Generate.", MessageType.Error);
        else if (CheckDuplicate(id))
            EditorGUILayout.HelpBox($"QuestId '{id}' is used by another quest asset.", MessageType.Error);
    }

    // ── Availability section (auto-start + repeatable) ────────────────────
    private void DrawAvailabilitySection()
    {
        SectionHeader("Availability");

        EditorGUILayout.PropertyField(_autoStartProp,
            new GUIContent("Auto Start", "Start automatically when the condition below is met — no dialogue command needed."));

        if (_autoStartProp.boolValue && _startConditionProp != null)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_startConditionProp,
                new GUIContent("Start Condition"), true);
            EditorGUI.indentLevel--;

            string conditionWarning = ValidateStartCondition();
            if (!string.IsNullOrEmpty(conditionWarning))
                EditorGUILayout.HelpBox(conditionWarning, MessageType.Warning);
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(_repeatableProp,
            new GUIContent("Repeatable", "Can start again after completion (e.g. weekly rent)."));

        if (_repeatableProp.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_repeatCooldownProp,
                new GUIContent("Repeat Cooldown (phases)", "Phases to wait after completion before restarting. 4 phases = 1 day. 0 = as soon as the start condition holds again."));

            if (_repeatableProp.boolValue && !_autoStartProp.boolValue && _repeatCooldownProp.intValue <= 0)
                EditorGUILayout.HelpBox("A repeatable quest that isn't auto-started must be re-triggered manually (e.g. dialogue StartQuest).", MessageType.Info);

            EditorGUI.indentLevel--;
        }
    }

    // Returns a warning about the start condition's quest references, or null.
    private string ValidateStartCondition()
    {
        if (_startConditionProp == null)
            return null;

        string selfId = _questIdProp.stringValue;

        string completedMsg = CheckQuestIdList(_startConditionProp.FindPropertyRelative("RequireCompletedQuests"), selfId, "completed");
        if (completedMsg != null) return completedMsg;

        return CheckQuestIdList(_startConditionProp.FindPropertyRelative("RequireActiveQuests"), selfId, "active");
    }

    private static string CheckQuestIdList(SerializedProperty listProp, string selfId, string label)
    {
        if (listProp == null || !listProp.isArray)
            return null;

        for (int i = 0; i < listProp.arraySize; i++)
        {
            string id = listProp.GetArrayElementAtIndex(i).stringValue;
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!string.IsNullOrEmpty(selfId) && string.Equals(id, selfId, System.StringComparison.Ordinal))
                return $"Start condition requires this quest itself to be {label} — it would never auto-start.";

            if (!QuestIdEditorUtility.IsKnownQuest(id))
                return $"Start condition references unknown quest '{id}' (no QuestDefinition with that id).";
        }

        return null;
    }

    // ── Steps section ─────────────────────────────────────────────────────
    private void DrawStepsSection()
    {
        int count = _stepsProp.arraySize;

        using (new EditorGUILayout.HorizontalScope())
        {
            SectionHeader($"Steps ({count})");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Step", GUILayout.Width(82)))
                ShowAddStepMenu();
        }

        if (count == 0)
        {
            EditorGUILayout.HelpBox("No steps — quest will complete instantly when started.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < count; i++)
            DrawStepCard(i, count);
    }

    private void DrawStepCard(int index, int total)
    {
        var stepProp = _stepsProp.GetArrayElementAtIndex(index);
        var typeProp = stepProp.FindPropertyRelative("Type");
        var textProp = stepProp.FindPropertyRelative("Text");

        var  stepType  = (QuestStepType)typeProp.intValue;
        bool expanded  = _expandedSteps.Contains(index);
        Color typeCol  = StepColors.TryGetValue(stepType, out var sc) ? sc : Color.grey;

        EditorGUILayout.Space(3);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // ── Card header ───────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                // Colour strip
                var stripRect = GUILayoutUtility.GetRect(6, 20, GUILayout.Width(6), GUILayout.Height(20));
                EditorGUI.DrawRect(stripRect, typeCol);
                GUILayout.Space(4);

                // Step number
                EditorGUILayout.LabelField($"{index + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(18));

                // Type badge
                var oldC = GUI.contentColor;
                GUI.contentColor = typeCol * 1.9f;
                EditorGUILayout.LabelField(StepTypeName(stepType), EditorStyles.boldLabel, GUILayout.Width(118));
                GUI.contentColor = oldC;

                // Text preview
                string preview = StripAutoPrefix(textProp?.stringValue ?? "");
                if (preview.Length > 44) preview = preview.Substring(0, 41) + "…";
                EditorGUILayout.LabelField(preview, EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                // Reorder up
                using (new EditorGUI.DisabledScope(index == 0))
                    if (GUILayout.Button("▲", GUILayout.Width(22), GUILayout.Height(18)))
                    { MoveStep(index, index - 1); return; }

                // Reorder down
                using (new EditorGUI.DisabledScope(index == total - 1))
                    if (GUILayout.Button("▼", GUILayout.Width(22), GUILayout.Height(18)))
                    { MoveStep(index, index + 1); return; }

                // Expand toggle
                if (GUILayout.Button(expanded ? "▼" : "►", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    if (expanded) _expandedSteps.Remove(index);
                    else          _expandedSteps.Add(index);
                }

                // Delete
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.75f, 0.22f, 0.22f);
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    GUI.backgroundColor = prevBg;
                    if (EditorUtility.DisplayDialog("Remove Step",
                        $"Remove step {index + 1} ({StepTypeName(stepType)})?", "Remove", "Cancel"))
                    {
                        DeleteStep(index);
                        return;
                    }
                }
                GUI.backgroundColor = prevBg;
            }

            // ── Expanded body ─────────────────────────────────────────────
            if (expanded)
            {
                EditorGUILayout.Space(4);
                DrawStepBody(stepProp, index);
            }
        }
    }

    private void DrawStepBody(SerializedProperty stepProp, int index)
    {
        var stepIdProp         = stepProp.FindPropertyRelative("StepId");
        var textProp           = stepProp.FindPropertyRelative("Text");
        var typeProp           = stepProp.FindPropertyRelative("Type");
        var restrictByDayProp  = stepProp.FindPropertyRelative("RestrictByDay");
        var restrictByPhaseProp= stepProp.FindPropertyRelative("RestrictByPhase");
        var allowedDaysProp    = stepProp.FindPropertyRelative("AllowedDays");
        var allowedPhasesProp  = stepProp.FindPropertyRelative("AllowedPhases");

        EditorGUI.BeginChangeCheck();

        // StepId + Auto ID button
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(stepIdProp);
            if (GUILayout.Button("Auto ID", GUILayout.Width(58)))
            {
                string prefix = string.IsNullOrEmpty(_questIdProp.stringValue)
                    ? "step" : _questIdProp.stringValue;
                stepIdProp.stringValue = $"{prefix}_{index + 1:00}";
            }
        }

        EditorGUILayout.PropertyField(textProp);

        var stepType = (QuestStepType)typeProp.intValue;

        // Type-specific fields
        EditorGUILayout.Space(6);
        switch (stepType)
        {
            case QuestStepType.ReachLocation:
                DrawLocationDropdown(stepProp);
                break;

            case QuestStepType.TalkToDialogue:
                DrawTalkToDialoguePickers(stepProp);
                break;

            case QuestStepType.HaveItem:
                EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("RequiredItem"), true);
                break;

            case QuestStepType.HaveMoney:
            case QuestStepType.PayMoney:
                EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("RequiredMoney"));
                break;

            case QuestStepType.MinStats:
                EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("RequiredInfluence"));
                EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("RequiredStrategy"));
                EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("RequiredNetworking"));
                EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("RequiredReputation"));
                break;

            case QuestStepType.FlagTrue:
            {
                var rect = EditorGUILayout.GetControlRect();
                WorldFlagEditorUtility.DrawFlagField(rect, stepProp.FindPropertyRelative("RequiredFlagId"), new GUIContent("Required Flag"));
                break;
            }

            case QuestStepType.TimeWindow:
            case QuestStepType.Manual:
            case QuestStepType.AutoComplete:
                break;
        }

        // Time restrictions
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Time Restrictions", EditorStyles.miniBoldLabel);

        EditorGUILayout.PropertyField(restrictByDayProp);
        if (restrictByDayProp.boolValue)
            DrawMaskButtons("Days", allowedDaysProp, DayLabels, DayBits);

        EditorGUILayout.PropertyField(restrictByPhaseProp);
        if (restrictByPhaseProp.boolValue)
            DrawMaskButtons("Phases", allowedPhasesProp, PhaseLabels, PhaseBits);

        // Validation warning
        string warn = ValidateStep(stepProp);
        if (!string.IsNullOrEmpty(warn))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(warn, MessageType.Warning);
        }

        if (EditorGUI.EndChangeCheck())
            RefreshAutoText(stepProp);
    }

    // ── Rewards section ───────────────────────────────────────────────────
    private void DrawRewardsSection()
    {
        SectionHeader("Completion Rewards");
        EditorGUILayout.PropertyField(_rewardsProp, true);
    }

    // ── Toggle buttons (days / phases) ────────────────────────────────────
    private static void DrawMaskButtons(string rowLabel, SerializedProperty maskProp,
        string[] labels, int[] bits)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(rowLabel, GUILayout.Width(56));
            int current = maskProp.intValue;
            for (int i = 0; i < labels.Length; i++)
            {
                bool on  = (current & bits[i]) != 0;
                var  old = GUI.backgroundColor;
                GUI.backgroundColor = on ? ColOn : ColOff;
                if (GUILayout.Button(labels[i], GUILayout.Height(20)))
                    maskProp.intValue = on ? current & ~bits[i] : current | bits[i];
                GUI.backgroundColor = old;
            }
        }
    }

    // ── Location dropdown ─────────────────────────────────────────────────
    private static void DrawLocationDropdown(SerializedProperty stepProp)
    {
        var prop = stepProp.FindPropertyRelative("TargetLocationSceneName");
        var db   = SceneDatabase.Instance;

        if (db == null)
        {
            EditorGUILayout.HelpBox("SceneDatabase not found.", MessageType.Warning);
            return;
        }

        var labels = new List<string> { "<None>" };
        var values = new List<string> { "" };

        if (db.Locations != null)
        {
            for (int i = 0; i < db.Locations.Count; i++)
            {
                var entry = db.Locations[i];
                if (entry.Scene == null || !entry.Scene.IsValid) continue;
                string sn = entry.Scene.SceneName;
                if (string.IsNullOrEmpty(sn) || values.Contains(sn)) continue;
                labels.Add(string.IsNullOrEmpty(entry.Id) ? sn : $"{entry.Id} ({sn})");
                values.Add(sn);
            }
        }

        if (values.Count <= 1)
        {
            EditorGUILayout.HelpBox("No locations in SceneDatabase.", MessageType.Warning);
            return;
        }

        string cur    = prop.stringValue ?? "";
        int    curIdx = 0;
        for (int i = 0; i < values.Count; i++)
            if (string.Equals(values[i], cur, StringComparison.Ordinal)) { curIdx = i; break; }

        EditorGUI.BeginChangeCheck();
        int newIdx = EditorGUILayout.Popup("Target Location", curIdx, labels.ToArray());
        if (EditorGUI.EndChangeCheck())
            prop.stringValue = values[Mathf.Clamp(newIdx, 0, values.Count - 1)];
    }

    // ── NPC / Dialogue pickers ────────────────────────────────────────────
    private static void DrawTalkToDialoguePickers(SerializedProperty stepProp)
    {
        var npcProp = stepProp.FindPropertyRelative("TargetNpcId");
        var dlgProp = stepProp.FindPropertyRelative("TargetDialogueId");

        BuildNpcOptions(out var npcLabels, out var npcValues);

        string curNpc = npcProp.stringValue ?? "";
        int npcIdx = 0;
        for (int i = 0; i < npcValues.Count; i++)
            if (string.Equals(npcValues[i], curNpc, StringComparison.Ordinal)) { npcIdx = i; break; }

        EditorGUI.BeginChangeCheck();
        int newNpcIdx = EditorGUILayout.Popup("Target NPC", npcIdx, npcLabels.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            npcProp.stringValue = npcValues[Mathf.Clamp(newNpcIdx, 0, npcValues.Count - 1)];
            dlgProp.stringValue = "";
            return;
        }

        if (string.IsNullOrWhiteSpace(npcProp.stringValue))
        {
            EditorGUILayout.HelpBox("Select Target NPC first.", MessageType.Info);
            return;
        }

        BuildDialogueOptions(npcProp.stringValue, out var dlgLabels, out var dlgValues);
        if (dlgValues.Count <= 1)
        {
            EditorGUILayout.HelpBox($"No dialogues found for '{npcProp.stringValue}'.", MessageType.Warning);
            return;
        }

        string curDlg = dlgProp.stringValue ?? "";
        int dlgIdx = 0;
        for (int i = 0; i < dlgValues.Count; i++)
            if (string.Equals(dlgValues[i], curDlg, StringComparison.Ordinal)) { dlgIdx = i; break; }

        EditorGUI.BeginChangeCheck();
        int newDlgIdx = EditorGUILayout.Popup("Target Dialogue", dlgIdx, dlgLabels.ToArray());
        if (EditorGUI.EndChangeCheck())
            dlgProp.stringValue = dlgValues[Mathf.Clamp(newDlgIdx, 0, dlgValues.Count - 1)];
    }

    // ── Add / Delete / Move steps ─────────────────────────────────────────
    private void ShowAddStepMenu()
    {
        var menu = new GenericMenu();
        void Add(string label, QuestStepType t) =>
            menu.AddItem(new GUIContent(label), false, () => AddStep(t));

        Add("Manual",           QuestStepType.Manual);
        Add("Auto Complete",    QuestStepType.AutoComplete);
        Add("Reach Location",   QuestStepType.ReachLocation);
        Add("Talk To Dialogue", QuestStepType.TalkToDialogue);
        Add("Have Item",        QuestStepType.HaveItem);
        Add("Have Money",       QuestStepType.HaveMoney);
        Add("Pay Money",        QuestStepType.PayMoney);
        Add("Min Stats",        QuestStepType.MinStats);
        Add("Time Window",      QuestStepType.TimeWindow);
        menu.ShowAsContext();
    }

    private void AddStep(QuestStepType type)
    {
        _so.Update();
        int idx = _stepsProp.arraySize;
        _stepsProp.arraySize++;
        var el = _stepsProp.GetArrayElementAtIndex(idx);

        string prefix = string.IsNullOrEmpty(_questIdProp.stringValue)
            ? "step" : _questIdProp.stringValue;
        el.FindPropertyRelative("StepId").stringValue          = $"{prefix}_{idx + 1:00}";
        el.FindPropertyRelative("Text").stringValue            = "";
        el.FindPropertyRelative("Type").intValue               = (int)type;
        el.FindPropertyRelative("RestrictByDay").boolValue     = false;
        el.FindPropertyRelative("RestrictByPhase").boolValue   = false;

        RefreshAutoText(el);
        _so.ApplyModifiedProperties();
        _expandedSteps.Add(idx);
        _rightScroll.y = float.MaxValue;
        Repaint();
    }

    private void DeleteStep(int index)
    {
        _stepsProp.DeleteArrayElementAtIndex(index);
        _so.ApplyModifiedProperties();

        _expandedSteps.Remove(index);
        var shifted = new HashSet<int>();
        foreach (int e in _expandedSteps) shifted.Add(e > index ? e - 1 : e);
        _expandedSteps.Clear();
        foreach (int e in shifted) _expandedSteps.Add(e);
        Repaint();
    }

    private void MoveStep(int from, int to)
    {
        _stepsProp.MoveArrayElement(from, to);

        bool expFrom = _expandedSteps.Contains(from);
        bool expTo   = _expandedSteps.Contains(to);
        if (expFrom) _expandedSteps.Add(to);   else _expandedSteps.Remove(to);
        if (expTo)   _expandedSteps.Add(from); else _expandedSteps.Remove(from);

        _so.ApplyModifiedProperties();
        Repaint();
    }

    // ── Validation ────────────────────────────────────────────────────────
    private static string ValidateStep(SerializedProperty s)
    {
        var type = (QuestStepType)s.FindPropertyRelative("Type").intValue;

        if (type == QuestStepType.ReachLocation)
        {
            var p = s.FindPropertyRelative("TargetLocationSceneName");
            if (string.IsNullOrEmpty(p?.stringValue)) return "Target Location is not set.";
            var db = SceneDatabase.Instance;
            if (db?.Locations != null)
            {
                bool found = false;
                for (int i = 0; i < db.Locations.Count; i++)
                {
                    var e = db.Locations[i];
                    if (e.Scene != null && e.Scene.IsValid &&
                        string.Equals(e.Scene.SceneName, p.stringValue, StringComparison.Ordinal))
                    { found = true; break; }
                }
                if (!found) return $"'{p.stringValue}' not found in SceneDatabase. Renamed?";
            }
        }

        if (type == QuestStepType.TimeWindow)
        {
            bool rd = s.FindPropertyRelative("RestrictByDay")?.boolValue   ?? false;
            bool rp = s.FindPropertyRelative("RestrictByPhase")?.boolValue ?? false;
            if (!rd && !rp) return "Enable RestrictByDay or RestrictByPhase — otherwise step completes instantly.";
        }

        if (type == QuestStepType.MinStats)
        {
            int sum = (s.FindPropertyRelative("RequiredInfluence")?.intValue  ?? 0)
                    + (s.FindPropertyRelative("RequiredStrategy")?.intValue   ?? 0)
                    + (s.FindPropertyRelative("RequiredNetworking")?.intValue ?? 0)
                    + (s.FindPropertyRelative("RequiredReputation")?.intValue ?? 0);
            if (sum <= 0) return "Set at least one required stat.";
        }

        if (type == QuestStepType.HaveMoney || type == QuestStepType.PayMoney)
            if ((s.FindPropertyRelative("RequiredMoney")?.intValue ?? 0) <= 0)
                return "RequiredMoney should be > 0.";

        if ((s.FindPropertyRelative("RestrictByDay")?.boolValue ?? false) &&
            (s.FindPropertyRelative("AllowedDays")?.intValue ?? 0) == 0)
            return "RestrictByDay is on but AllowedDays is empty.";

        if ((s.FindPropertyRelative("RestrictByPhase")?.boolValue ?? false) &&
            (s.FindPropertyRelative("AllowedPhases")?.intValue ?? 0) == 0)
            return "RestrictByPhase is on but AllowedPhases is empty.";

        if (type == QuestStepType.TalkToDialogue)
        {
            if (string.IsNullOrEmpty(s.FindPropertyRelative("TargetNpcId")?.stringValue))
                return "Target NPC is not set.";
            if (string.IsNullOrEmpty(s.FindPropertyRelative("TargetDialogueId")?.stringValue))
                return "Target Dialogue is not set.";
        }

        if (type == QuestStepType.HaveItem)
        {
            var item = s.FindPropertyRelative("RequiredItem");
            if (string.IsNullOrWhiteSpace(item?.FindPropertyRelative("ItemId")?.stringValue))
                return "Required item is not set.";
            if ((item?.FindPropertyRelative("Count")?.intValue ?? 0) <= 0)
                return "Item count should be > 0.";
        }

        if (type == QuestStepType.FlagTrue)
        {
            string flagId = s.FindPropertyRelative("RequiredFlagId")?.stringValue;
            if (string.IsNullOrWhiteSpace(flagId))
                return "Required flag is not set.";
            if (!WorldFlagEditorUtility.IsKnownFlag(flagId))
                return $"Flag '{flagId}' is not declared in the FlagDatabase. Typo?";
        }

        return null;
    }

    private void RunFullValidation()
    {
        if (_quest == null) return;
        EnsureSerialized();
        _so.Update();

        var errors = new List<string>();

        string id = _questIdProp.stringValue;
        if (string.IsNullOrEmpty(id))        errors.Add("QuestId is empty.");
        else if (CheckDuplicate(id))         errors.Add($"QuestId '{id}' duplicated.");
        if (_stepsProp.arraySize == 0)       errors.Add("No steps defined.");

        for (int i = 0; i < _stepsProp.arraySize; i++)
        {
            string msg = ValidateStep(_stepsProp.GetArrayElementAtIndex(i));
            if (!string.IsNullOrEmpty(msg)) errors.Add($"Step {i + 1}: {msg}");
        }

        if (errors.Count == 0)
            EditorUtility.DisplayDialog("Quest Validation", "No issues found ✅", "OK");
        else
            EditorUtility.DisplayDialog("Quest Validation",
                $"{errors.Count} issue(s):\n• " + string.Join("\n• ", errors), "OK");
    }

    // ── Auto text ─────────────────────────────────────────────────────────
    private static void RefreshAutoText(SerializedProperty stepProp)
    {
        var textProp = stepProp.FindPropertyRelative("Text");
        if (textProp == null) return;
        string cur = textProp.stringValue;
        if (!string.IsNullOrEmpty(cur) && !cur.StartsWith("[AUTO] ", StringComparison.Ordinal)) return;

        var type = (QuestStepType)stepProp.FindPropertyRelative("Type").intValue;
        string generated = BuildAutoText(stepProp, type);
        if (!string.IsNullOrEmpty(generated))
            textProp.stringValue = "[AUTO] " + generated;
    }

    private static string BuildAutoText(SerializedProperty s, QuestStepType type)
    {
        switch (type)
        {
            case QuestStepType.ReachLocation:
            {
                string sn    = s.FindPropertyRelative("TargetLocationSceneName")?.stringValue ?? "";
                string label = string.IsNullOrEmpty(sn) ? "<choose location>" : sn;
                var db = SceneDatabase.Instance;
                if (db?.Locations != null && !string.IsNullOrEmpty(sn))
                    for (int i = 0; i < db.Locations.Count; i++)
                    {
                        var e = db.Locations[i];
                        if (e.Scene?.IsValid == true &&
                            string.Equals(e.Scene.SceneName, sn, StringComparison.Ordinal) &&
                            !string.IsNullOrEmpty(e.Id)) { label = e.Id; break; }
                    }
                return $"Go to {label}";
            }
            case QuestStepType.HaveMoney:
            {
                int m = s.FindPropertyRelative("RequiredMoney")?.intValue ?? 0;
                return m > 0 ? $"Have ${m}" : "Have enough money";
            }
            case QuestStepType.PayMoney:
            {
                int m = s.FindPropertyRelative("RequiredMoney")?.intValue ?? 0;
                return m > 0 ? $"Pay ${m}" : "Pay the required amount";
            }
            case QuestStepType.MinStats:
            {
                var parts = new List<string>();
                int inf = s.FindPropertyRelative("RequiredInfluence")?.intValue  ?? 0;
                int str = s.FindPropertyRelative("RequiredStrategy")?.intValue   ?? 0;
                int net = s.FindPropertyRelative("RequiredNetworking")?.intValue ?? 0;
                int rep = s.FindPropertyRelative("RequiredReputation")?.intValue ?? 0;
                if (inf > 0) parts.Add($"Influence {inf}");
                if (str > 0) parts.Add($"Strategy {str}");
                if (net > 0) parts.Add($"Networking {net}");
                if (rep > 0) parts.Add($"Reputation {rep}");
                return parts.Count > 0 ? $"Reach {string.Join(", ", parts)}" : "Increase your stats";
            }
            case QuestStepType.Manual:       return "Complete the objective";
            case QuestStepType.AutoComplete: return "Progress";
            case QuestStepType.FlagTrue:
            {
                string fid = s.FindPropertyRelative("RequiredFlagId")?.stringValue ?? "";
                return string.IsNullOrEmpty(fid) ? "Progress (choose flag below)" : $"Waiting for: {fid}";
            }
            case QuestStepType.TimeWindow:
            {
                bool rd = s.FindPropertyRelative("RestrictByDay")?.boolValue   ?? false;
                bool rp = s.FindPropertyRelative("RestrictByPhase")?.boolValue ?? false;
                return (rd || rp) ? "Wait for the right time" : "Progress (configure time window below)";
            }
            case QuestStepType.TalkToDialogue:
            {
                string npc = s.FindPropertyRelative("TargetNpcId")?.stringValue      ?? "";
                string dlg = s.FindPropertyRelative("TargetDialogueId")?.stringValue ?? "";
                if (string.IsNullOrEmpty(npc) && string.IsNullOrEmpty(dlg)) return "Talk to <choose dialogue>";
                return string.IsNullOrEmpty(dlg) ? $"Talk to {npc}" : $"Talk to {npc} ({dlg})";
            }
            case QuestStepType.HaveItem:
            {
                var  req   = s.FindPropertyRelative("RequiredItem");
                string iid = req?.FindPropertyRelative("ItemId")?.stringValue ?? "";
                int count  = req?.FindPropertyRelative("Count")?.intValue     ?? 0;
                if (count <= 0) count = 1;
                string label = string.IsNullOrWhiteSpace(iid) ? "<choose item>" : iid;
                var db = FindItemDatabase();
                if (db != null && !string.IsNullOrWhiteSpace(iid) &&
                    db.TryGet(iid, out var item) && !string.IsNullOrWhiteSpace(item?.DisplayName))
                    label = item.DisplayName;
                return count > 1 ? $"Have {count}x {label}" : $"Have {label}";
            }
        }
        return null;
    }

    // ── Asset management ──────────────────────────────────────────────────
    private void CreateNewQuest()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Quest Definition", "Quest_New", "asset",
            "Choose where to save the quest", "Assets/ScriptableObjects");

        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<QuestDefinition>();
        asset.Title = System.IO.Path.GetFileNameWithoutExtension(path);

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        RefreshQuestList();
        SetQuest(asset);
        EditorGUIUtility.PingObject(asset);
    }

    private void RefreshQuestList()
    {
        _allQuests.Clear();
        string[] guids = AssetDatabase.FindAssets("t:QuestDefinition");
        foreach (var guid in guids)
        {
            var q = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (q != null) _allQuests.Add(q);
        }
        _allQuests.Sort((a, b) =>
            string.Compare(a.QuestId ?? a.name, b.QuestId ?? b.name, StringComparison.OrdinalIgnoreCase));
        _nextListRefresh = EditorApplication.timeSinceStartup + ListRefreshInterval;
    }

    private void RefreshQuestListIfNeeded()
    {
        if (EditorApplication.timeSinceStartup >= _nextListRefresh) RefreshQuestList();
    }

    private void SetQuest(QuestDefinition quest)
    {
        _quest = quest;
        _so    = null;
        _expandedSteps.Clear();
        _lastCheckedId = null;
        if (_quest != null) EnsureSerialized();
        Repaint();
    }

    private void EnsureSerialized()
    {
        if (_quest == null) return;
        if (_so == null || _so.targetObject != _quest)
        {
            _so          = new SerializedObject(_quest);
            _questIdProp = _so.FindProperty("QuestId");
            _titleProp   = _so.FindProperty("Title");
            _descProp    = _so.FindProperty("Description");
            _stepsProp   = _so.FindProperty("Steps");
            _rewardsProp = _so.FindProperty("CompletionRewards");
            _autoStartProp      = _so.FindProperty("AutoStart");
            _startConditionProp = _so.FindProperty("StartCondition");
            _repeatableProp     = _so.FindProperty("Repeatable");
            _repeatCooldownProp = _so.FindProperty("RepeatCooldownPhases");
        }
        _so.Update();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private bool CheckDuplicate(string questId)
    {
        if (string.IsNullOrEmpty(questId) || _quest == null) return false;
        if (questId == _lastCheckedId) return _isDuplicate;

        _lastCheckedId = questId;
        _isDuplicate   = false;

        string selfPath = AssetDatabase.GetAssetPath(_quest);
        string[] guids  = AssetDatabase.FindAssets("t:QuestDefinition");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == selfPath) continue;
            var other = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
            if (other != null && string.Equals(other.QuestId, questId, StringComparison.Ordinal))
            { _isDuplicate = true; break; }
        }
        return _isDuplicate;
    }

    private static string SlugifyTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "q_quest";
        var sb = new StringBuilder("q_");
        bool lastUs = true;
        foreach (char c in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) { sb.Append(c); lastUs = false; }
            else if (!lastUs)            { sb.Append('_'); lastUs = true; }
        }
        if (sb.Length > 2 && sb[sb.Length - 1] == '_') sb.Length--;
        return sb.ToString();
    }

    private static string StripAutoPrefix(string s)
    {
        const string p = "[AUTO] ";
        return !string.IsNullOrEmpty(s) && s.StartsWith(p, StringComparison.Ordinal)
            ? s.Substring(p.Length) : (s ?? "");
    }

    private static string StepTypeName(QuestStepType t) => t switch
    {
        QuestStepType.Manual         => "Manual",
        QuestStepType.AutoComplete   => "Auto Complete",
        QuestStepType.ReachLocation  => "Reach Location",
        QuestStepType.TalkToDialogue => "Talk To Dialogue",
        QuestStepType.HaveItem       => "Have Item",
        QuestStepType.HaveMoney      => "Have Money",
        QuestStepType.PayMoney       => "Pay Money",
        QuestStepType.MinStats       => "Min Stats",
        QuestStepType.TimeWindow     => "Time Window",
        _ => t.ToString()
    };

    private static void SectionHeader(string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.Height(1));
        EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.6f));
        GUILayout.Space(4);
    }

    private static void SectionGap() => GUILayout.Space(16);

    private static void BuildNpcOptions(out List<string> labels, out List<string> values)
    {
        labels = new List<string> { "<None>" };
        values = new List<string> { "" };
        var mgr = FindFirstObjectByType<NpcManager>();
        if (mgr?.Npcs != null)
        {
            foreach (var def in mgr.Npcs)
            {
                var id = def?.NpcId;
                if (!string.IsNullOrWhiteSpace(id) && !values.Contains(id))
                { labels.Add(id); values.Add(id); }
            }
            if (values.Count > 1) return;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:NpcDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            var id  = def?.NpcId;
            if (!string.IsNullOrWhiteSpace(id) && !values.Contains(id))
            { labels.Add(id); values.Add(id); }
        }
    }

    private static void BuildDialogueOptions(string npcId, out List<string> labels, out List<string> values)
    {
        labels = new List<string> { "<None>" };
        values = new List<string> { "" };
        var npc = FindNpcById(npcId);
        if (npc?.Routes == null) return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AddGraph(npc.Routes.fallback, labels, values, seen);
        if (npc.Routes.rules == null) return;
        foreach (var rule in npc.Routes.rules)
        {
            if (rule == null) continue;
            AddGraph(rule.graph,              labels, values, seen);
            AddGraph(rule.requireNotSeenThis,  labels, values, seen);
            AddGraph(rule.requireSeenThis,     labels, values, seen);
            if (rule.pool != null)
                foreach (var g in rule.pool) AddGraph(g, labels, values, seen);
        }
    }

    private static NpcDefinition FindNpcById(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return null;
        foreach (var guid in AssetDatabase.FindAssets("t:NpcDefinition"))
        {
            var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (npc != null && string.Equals(npc.NpcId, npcId, StringComparison.Ordinal)) return npc;
        }
        return null;
    }

    private static void AddGraph(DialogueGraph g, List<string> labels, List<string> values, HashSet<string> seen)
    {
        if (g == null) return;
        string id = !string.IsNullOrWhiteSpace(g.DialogueId) ? g.DialogueId : g.name;
        if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) return;
        labels.Add($"{id} ({g.name})"); values.Add(id);
    }

    private static ItemDatabase FindItemDatabase()
    {
        var guids = AssetDatabase.FindAssets("t:ItemDatabase");
        return guids.Length == 0 ? null
            : AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
#endif
