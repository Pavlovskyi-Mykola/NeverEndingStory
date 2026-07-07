#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class DialogueRouteSetEditorWindow : EditorWindow
{
    // ── Layout ────────────────────────────────────────────────────────────
    private const float LeftPanelWidth = 230f;

    // ── State ─────────────────────────────────────────────────────────────
    private DialogueRouteSet _routeSet;
    private SerializedObject   _so;
    private SerializedProperty _rulesProp, _fallbackProp;

    private Vector2 _leftScroll, _rightScroll;
    private string  _searchFilter = "";
    private readonly HashSet<int> _expandedRules = new();

    // RouteSet list cache
    private readonly List<DialogueRouteSet> _allSets = new();
    private double _nextRefresh;
    private const double RefreshInterval = 2.0;

    // ── Output type colours ───────────────────────────────────────────────
    private static readonly Color ColSingle   = new Color(0.22f, 0.48f, 0.80f);
    private static readonly Color ColPool     = new Color(0.45f, 0.22f, 0.72f);
    private static readonly Color ColFallback = new Color(0.30f, 0.30f, 0.30f);

    // ── Day/Phase toggle colours ──────────────────────────────────────────
    private static readonly string[] DayLabels   = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly int[]    DayBits     = { 1, 2, 4, 8, 16, 32, 64 };
    private static readonly string[] PhaseLabels = { "Morning", "Afternoon", "Evening", "Night" };
    private static readonly int[]    PhaseBits   = { 1, 2, 4, 8 };
    private static readonly Color    ColOn  = new Color(0.25f, 0.60f, 1.00f);
    private static readonly Color    ColOff = new Color(0.28f, 0.28f, 0.28f);

    // ── Open ──────────────────────────────────────────────────────────────
    [MenuItem("Game/Dialogue/Route Set Editor")]
    public static void Open()
    {
        var w = GetWindow<DialogueRouteSetEditorWindow>();
        w.titleContent = new GUIContent("Route Set Editor");
        w.minSize = new Vector2(720, 500);
        w.Show();
    }

    public static void OpenRouteSet(DialogueRouteSet rs)
    {
        var w = GetWindow<DialogueRouteSetEditorWindow>();
        w.titleContent = new GUIContent("Route Set Editor");
        w.minSize = new Vector2(720, 500);
        w.SetRouteSet(rs);
        w.Show();
        w.Focus();
    }

    // ── Unity callbacks ───────────────────────────────────────────────────
    private void OnEnable() => RefreshList();
    private void OnFocus()  => RefreshList();

    private void OnGUI()
    {
        RefreshIfNeeded();
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
            var picked = (DialogueRouteSet)EditorGUILayout.ObjectField(
                _routeSet, typeof(DialogueRouteSet), false, GUILayout.Width(280));
            if (picked != _routeSet) SetRouteSet(picked);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("New Route Set", EditorStyles.toolbarButton, GUILayout.Width(96)))
                CreateNew();

            using (new EditorGUI.DisabledScope(_routeSet == null))
            {
                if (GUILayout.Button("Ping Asset", EditorStyles.toolbarButton, GUILayout.Width(72)))
                {
                    EditorGUIUtility.PingObject(_routeSet);
                    Selection.activeObject = _routeSet;
                }
            }
        }
    }

    // ── Left panel – asset list ───────────────────────────────────────────
    private void DrawLeftPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPanelWidth), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField($"Route Sets ({_allSets.Count})", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck()) Repaint();

            EditorGUILayout.Space(2);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.ExpandHeight(true));

            string filter = _searchFilter.ToLowerInvariant();
            foreach (var rs in _allSets)
            {
                if (rs == null) continue;
                if (!string.IsNullOrEmpty(filter) &&
                    !rs.name.ToLowerInvariant().Contains(filter)) continue;

                bool isSelected = rs == _routeSet;
                var  rowRect    = GUILayoutUtility.GetRect(LeftPanelWidth - 8, 36f);

                if (isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.50f, 0.86f, 0.35f));
                else if (rowRect.Contains(Event.current.mousePosition))
                    EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));

                int ruleCount = rs.rules?.Count ?? 0;
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3, rowRect.height),
                    ruleCount == 0 ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.3f, 0.7f, 0.3f));

                GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 4,  rowRect.width - 10, 16),
                    rs.name, EditorStyles.boldLabel);
                GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 20, rowRect.width - 10, 12),
                    $"{ruleCount} rule{(ruleCount == 1 ? "" : "s")} + fallback", EditorStyles.miniLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                { SetRouteSet(rs); Event.current.Use(); }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ New Route Set")) CreateNew();
        }
    }

    private void DrawDivider()
    {
        var r = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f, 0.6f));
    }

    // ── Right panel – route set editor ────────────────────────────────────
    private void DrawRightPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
        {
            if (_routeSet == null)
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.HelpBox("Select a route set or create a new one.", MessageType.Info, true);
                    GUILayout.FlexibleSpace();
                }
                GUILayout.FlexibleSpace();
                return;
            }

            EnsureSerialized();
            _so.Update();

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            DrawFlowHeader();
            DrawRules();
            DrawFallbackCard();
            GUILayout.Space(24);

            EditorGUILayout.EndScrollView();

            _so.ApplyModifiedProperties();
        }
    }

    // ── Flow header ───────────────────────────────────────────────────────
    private void DrawFlowHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                $"Dialogue Flow  —  {_routeSet.name}",
                EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Rule", GUILayout.Width(80)))
                ShowAddRuleMenu();
        }

        EditorGUILayout.HelpBox(
            "Rules are evaluated top-to-bottom. The first rule whose conditions all pass is used. " +
            "If nothing matches, the Fallback graph plays.",
            MessageType.None);

        GUILayout.Space(6);
    }

    // ── Rules ─────────────────────────────────────────────────────────────
    private void DrawRules()
    {
        int count = _rulesProp.arraySize;

        if (count == 0)
        {
            EditorGUILayout.HelpBox("No rules — all talk triggers will use the Fallback graph.", MessageType.Info);
            GUILayout.Space(8);
            return;
        }

        for (int i = 0; i < count; i++)
            DrawRuleCard(i, count);

        GUILayout.Space(4);
    }

    private void DrawRuleCard(int index, int total)
    {
        var rule     = _rulesProp.GetArrayElementAtIndex(index);
        var outProp  = rule.FindPropertyRelative("output");
        var output   = (DialogueRuleOutput)outProp.intValue;

        bool expanded = _expandedRules.Contains(index);
        Color stripCol = output == DialogueRuleOutput.Pool ? ColPool : ColSingle;

        EditorGUILayout.Space(2);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            // ── Card header ───────────────────────────────────────────────
            using (new EditorGUILayout.HorizontalScope())
            {
                // Priority arrow + number
                EditorGUILayout.LabelField("▶", GUILayout.Width(14));
                EditorGUILayout.LabelField($"#{index + 1}", EditorStyles.miniBoldLabel, GUILayout.Width(24));

                // Output type badge
                var oldC = GUI.contentColor;
                GUI.contentColor = stripCol * 1.8f;
                EditorGUILayout.LabelField(
                    output == DialogueRuleOutput.Pool ? "Pool" : "Single",
                    EditorStyles.boldLabel, GUILayout.Width(46));
                GUI.contentColor = oldC;

                // Compact condition summary
                string summary = BuildSummary(rule, output);
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                // Reorder
                using (new EditorGUI.DisabledScope(index == 0))
                    if (GUILayout.Button("▲", GUILayout.Width(22), GUILayout.Height(18)))
                    { MoveRule(index, index - 1); return; }
                using (new EditorGUI.DisabledScope(index == total - 1))
                    if (GUILayout.Button("▼", GUILayout.Width(22), GUILayout.Height(18)))
                    { MoveRule(index, index + 1); return; }

                // Expand toggle
                if (GUILayout.Button(expanded ? "▼" : "►", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    if (expanded) _expandedRules.Remove(index);
                    else          _expandedRules.Add(index);
                }

                // Delete
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.75f, 0.22f, 0.22f);
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    GUI.backgroundColor = prevBg;
                    if (EditorUtility.DisplayDialog("Remove Rule",
                        $"Remove rule #{index + 1}?", "Remove", "Cancel"))
                    { DeleteRule(index); return; }
                }
                GUI.backgroundColor = prevBg;
            }

            // ── Colour strip under header ─────────────────────────────────
            var lineRect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.Height(2));
            EditorGUI.DrawRect(lineRect, stripCol * 0.7f);

            // ── Expanded body ─────────────────────────────────────────────
            if (expanded)
            {
                GUILayout.Space(6);
                DrawRuleBody(rule, index);
            }
        }

        // Flow arrow between cards
        if (index < total - 1)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20);
                var oldC = GUI.contentColor;
                GUI.contentColor = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Label("↓  no match, try next…", EditorStyles.miniLabel);
                GUI.contentColor = oldC;
            }
        }
    }

    // ── Rule body (expanded) ──────────────────────────────────────────────
    private void DrawRuleBody(SerializedProperty rule, int index)
    {
        var outProp = rule.FindPropertyRelative("output");

        // Output section
        SectionLabel("What to Play");
        EditorGUILayout.PropertyField(outProp, new GUIContent("Output Type"));

        var output = (DialogueRuleOutput)outProp.intValue;
        if (output == DialogueRuleOutput.Pool)
        {
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("pool"),
                new GUIContent("Pool (pick from these)"), true);
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("pickMode"));
        }
        else
        {
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("graph"),
                new GUIContent("Graph"));
        }

        GUILayout.Space(8);

        // Seen / one-time conditions
        SectionLabel("Seen Conditions");

        var notSeenDlg  = rule.FindPropertyRelative("requireNotSeenDialogue");
        var notSeenThis = rule.FindPropertyRelative("requireNotSeenThis");
        var seenThis    = rule.FindPropertyRelative("requireSeenThis");

        EditorGUILayout.PropertyField(notSeenDlg,
            new GUIContent("One-Time (not seen yet)",
                "Only fires if this graph/pool has never been played. Set this to make an intro dialogue play exactly once."));
        EditorGUILayout.PropertyField(notSeenThis,
            new GUIContent("Require NOT Seen (other graph)",
                "Only fires if the specified OTHER graph has not been seen yet."));
        EditorGUILayout.PropertyField(seenThis,
            new GUIContent("Require SEEN (other graph)",
                "Only fires after the specified graph has already been played."));

        GUILayout.Space(8);

        // Time conditions
        SectionLabel("Time of Day");

        var phasesProp = rule.FindPropertyRelative("allowedPhases");
        var daysProp   = rule.FindPropertyRelative("allowedDays");

        EditorGUILayout.LabelField("Allowed Phases", EditorStyles.miniLabel);
        DrawMaskButtons(phasesProp, PhaseLabels, PhaseBits);
        GUILayout.Space(2);
        EditorGUILayout.LabelField("Allowed Days", EditorStyles.miniLabel);
        DrawMaskButtons(daysProp, DayLabels, DayBits);

        GUILayout.Space(8);

        // Location
        SectionLabel("Location Gate  (empty = everywhere)");
        DrawLocationMultiSelect(rule.FindPropertyRelative("allowedLocationIds"));

        GUILayout.Space(8);

        // Quest gate
        SectionLabel("Quest Gate  (optional)");
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredQuestId"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredQuestState"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredQuestStepId"),
            new GUIContent("Required Step Id", "Only checked when quest is Active."));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredQuestStepIndex"),
            new GUIContent("Required Step Index", "Use -1 to ignore."));

        GUILayout.Space(8);

        // Flag gate
        SectionLabel("World Flag Gate  (optional)");
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredFlagId"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredFlagValue"));

        GUILayout.Space(8);

        // Relationship gate
        SectionLabel("Relationship Gate  (0 = not checked)");
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredRelationshipLevel"),
            new GUIContent("Required Level", "Minimum relationship level with this route set's NPC."));

        GUILayout.Space(8);

        // Stats gate
        SectionLabel("Stat Minimums  (0 = not checked)");
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredMoney"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredInfluence"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredStrategy"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredNetworking"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("requiredReputation"));

        GUILayout.Space(8);

        // Extra conditions
        SectionLabel("Extra Conditions  (optional)");
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("extraConditions"), true);

        GUILayout.Space(4);
    }

    // ── Fallback card ─────────────────────────────────────────────────────
    private void DrawFallbackCard()
    {
        int count = _rulesProp.arraySize;
        if (count > 0)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20);
                var oldC = GUI.contentColor;
                GUI.contentColor = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Label("↓  nothing matched…", EditorStyles.miniLabel);
                GUI.contentColor = oldC;
            }
        }

        EditorGUILayout.Space(2);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldC = GUI.contentColor;
                GUI.contentColor = ColFallback * 1.5f;
                EditorGUILayout.LabelField("FALLBACK", EditorStyles.boldLabel, GUILayout.Width(72));
                GUI.contentColor = oldC;

                string fallbackName = _routeSet.fallback != null ? _routeSet.fallback.name : "<none>";
                EditorGUILayout.LabelField(
                    _routeSet.fallback == null
                        ? "⚠ No fallback set — NPC will have nothing to say when no rules match"
                        : $"→ {fallbackName}",
                    _routeSet.fallback == null ? EditorStyles.miniLabel : EditorStyles.miniLabel);
            }

            var lineRect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.Height(2));
            EditorGUI.DrawRect(lineRect, ColFallback * 0.6f);

            GUILayout.Space(4);
            EditorGUILayout.PropertyField(_fallbackProp,
                new GUIContent("Fallback Graph",
                    "Plays when no rule conditions match. Should always be set."));

            if (_routeSet.fallback == null)
                EditorGUILayout.HelpBox(
                    "Set a fallback graph — otherwise the NPC will say nothing when no rules match.",
                    MessageType.Warning);
        }
    }

    // ── Toggle buttons (days / phases) ────────────────────────────────────
    private static void DrawMaskButtons(SerializedProperty maskProp, string[] labels, int[] bits)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
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

    // ── Location multi-select ─────────────────────────────────────────────
    private static void DrawLocationMultiSelect(SerializedProperty locProp)
    {
        var db  = SceneDatabase.Instance;
        var ids = GetLocationIds(db);

        if (db == null)
        {
            EditorGUILayout.HelpBox(
                "No SceneDatabase found (t:SceneDatabase). Create one to use location filtering.",
                MessageType.Warning);
            return;
        }

        if (ids.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "SceneDatabase has no locations with an Id. Fill in LocationEntry.Id fields.",
                MessageType.Info);
            return;
        }

        // Current selection
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < locProp.arraySize; i++)
            selected.Add(locProp.GetArrayElementAtIndex(i).stringValue);

        // Clear / Select All
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear (anywhere)", GUILayout.Height(18))) selected.Clear();
            if (GUILayout.Button("Select All",       GUILayout.Height(18)))
            {
                selected.Clear();
                foreach (var id in ids) selected.Add(id);
            }
        }

        // Toggle rows
        int shown = Mathf.Min(ids.Length, 20);
        for (int i = 0; i < shown; i++)
        {
            bool was  = selected.Contains(ids[i]);
            bool next = EditorGUILayout.ToggleLeft(ids[i], was);
            if (next != was) { if (next) selected.Add(ids[i]); else selected.Remove(ids[i]); }
        }
        if (ids.Length > shown)
            EditorGUILayout.LabelField($"… {ids.Length - shown} more", EditorStyles.miniLabel);

        // Write back (sorted for stable diffs)
        var final = selected.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        locProp.arraySize = final.Count;
        for (int i = 0; i < final.Count; i++)
            locProp.GetArrayElementAtIndex(i).stringValue = final[i];
    }

    // ── Add / Delete / Move rules ─────────────────────────────────────────
    private void ShowAddRuleMenu()
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Single Graph"), false, () => AddRule(DialogueRuleOutput.SingleGraph));
        menu.AddItem(new GUIContent("Pool"),         false, () => AddRule(DialogueRuleOutput.Pool));
        menu.ShowAsContext();
    }

    private void AddRule(DialogueRuleOutput output)
    {
        _so.Update();
        int idx = _rulesProp.arraySize;
        _rulesProp.arraySize++;
        var el = _rulesProp.GetArrayElementAtIndex(idx);

        el.FindPropertyRelative("output").intValue           = (int)output;
        el.FindPropertyRelative("allowedPhases").intValue    = 15; // All
        el.FindPropertyRelative("allowedDays").intValue      = 127; // All
        el.FindPropertyRelative("requiredQuestStepIndex").intValue = -1;
        el.FindPropertyRelative("pickMode").intValue         = (int)DialoguePickMode.Random;
        el.FindPropertyRelative("requiredQuestState").intValue = (int)QuestRouteState.Any;

        _so.ApplyModifiedProperties();
        _expandedRules.Add(idx);
        _rightScroll.y = float.MaxValue;
        Repaint();
    }

    private void DeleteRule(int index)
    {
        _rulesProp.DeleteArrayElementAtIndex(index);
        _so.ApplyModifiedProperties();
        _expandedRules.Remove(index);
        var shifted = new HashSet<int>();
        foreach (int e in _expandedRules) shifted.Add(e > index ? e - 1 : e);
        _expandedRules.Clear();
        foreach (int e in shifted) _expandedRules.Add(e);
        Repaint();
    }

    private void MoveRule(int from, int to)
    {
        _rulesProp.MoveArrayElement(from, to);
        bool expFrom = _expandedRules.Contains(from);
        bool expTo   = _expandedRules.Contains(to);
        if (expFrom) _expandedRules.Add(to);   else _expandedRules.Remove(to);
        if (expTo)   _expandedRules.Add(from); else _expandedRules.Remove(from);
        _so.ApplyModifiedProperties();
        Repaint();
    }

    // ── Asset management ──────────────────────────────────────────────────
    private void CreateNew()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Dialogue Route Set", "DialogueRouteSet_New", "asset",
            "Choose where to save the route set", "Assets/ScriptableObjects");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<DialogueRouteSet>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        RefreshList();
        SetRouteSet(asset);
        EditorGUIUtility.PingObject(asset);
    }

    private void RefreshList()
    {
        _allSets.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:DialogueRouteSet"))
        {
            var rs = AssetDatabase.LoadAssetAtPath<DialogueRouteSet>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (rs != null) _allSets.Add(rs);
        }
        _allSets.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        _nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
    }

    private void RefreshIfNeeded()
    {
        if (EditorApplication.timeSinceStartup >= _nextRefresh) RefreshList();
    }

    private void SetRouteSet(DialogueRouteSet rs)
    {
        _routeSet = rs;
        _so       = null;
        _expandedRules.Clear();
        if (_routeSet != null) EnsureSerialized();
        Repaint();
    }

    private void EnsureSerialized()
    {
        if (_routeSet == null) return;
        if (_so == null || _so.targetObject != _routeSet)
        {
            _so          = new SerializedObject(_routeSet);
            _rulesProp   = _so.FindProperty("rules");
            _fallbackProp= _so.FindProperty("fallback");
        }
        _so.Update();
    }

    // ── Compact summary for card header ───────────────────────────────────
    private static string BuildSummary(SerializedProperty rule, DialogueRuleOutput output)
    {
        var parts = new List<string>();

        // What plays
        if (output == DialogueRuleOutput.SingleGraph)
        {
            var g = rule.FindPropertyRelative("graph").objectReferenceValue;
            parts.Add(g != null ? $"→ {g.name}" : "→ <no graph>");
        }
        else
        {
            var pool = rule.FindPropertyRelative("pool");
            parts.Add($"→ pool ({pool.arraySize} graphs)");
        }

        // One-time
        if (rule.FindPropertyRelative("requireNotSeenDialogue").boolValue)
            parts.Add("one-time");

        // Seen prerequisites
        var notSeenThis = rule.FindPropertyRelative("requireNotSeenThis").objectReferenceValue;
        if (notSeenThis != null) parts.Add($"not seen: {notSeenThis.name}");
        var seenThis = rule.FindPropertyRelative("requireSeenThis").objectReferenceValue;
        if (seenThis != null) parts.Add($"after: {seenThis.name}");

        // Quest
        string qid = rule.FindPropertyRelative("requiredQuestId").stringValue;
        if (!string.IsNullOrWhiteSpace(qid))
        {
            var qstate = (QuestRouteState)rule.FindPropertyRelative("requiredQuestState").intValue;
            parts.Add(qstate == QuestRouteState.Any ? $"quest: {qid}" : $"quest: {qid} [{qstate}]");
        }

        // Flag
        string fid = rule.FindPropertyRelative("requiredFlagId").stringValue;
        if (!string.IsNullOrWhiteSpace(fid))
            parts.Add($"flag: {fid}={rule.FindPropertyRelative("requiredFlagValue").boolValue}");

        // Stats
        var statParts = new List<string>();
        int money = rule.FindPropertyRelative("requiredMoney").intValue;
        int inf   = rule.FindPropertyRelative("requiredInfluence").intValue;
        int str   = rule.FindPropertyRelative("requiredStrategy").intValue;
        int net   = rule.FindPropertyRelative("requiredNetworking").intValue;
        int rep   = rule.FindPropertyRelative("requiredReputation").intValue;
        if (money > 0) statParts.Add($"${money}");
        if (inf   > 0) statParts.Add($"Inf≥{inf}");
        if (str   > 0) statParts.Add($"Str≥{str}");
        if (net   > 0) statParts.Add($"Net≥{net}");
        if (rep   > 0) statParts.Add($"Rep≥{rep}");
        if (statParts.Count > 0) parts.Add(string.Join(" ", statParts));

        // Phases (if not All)
        int phases = rule.FindPropertyRelative("allowedPhases").intValue;
        if (phases != 15 && phases != 0)
        {
            var phNames = new List<string>();
            if ((phases & 1) != 0)  phNames.Add("Morn");
            if ((phases & 2) != 0)  phNames.Add("Aftn");
            if ((phases & 4) != 0)  phNames.Add("Eve");
            if ((phases & 8) != 0)  phNames.Add("Night");
            parts.Add(string.Join("/", phNames));
        }

        // Days (if not All)
        int days = rule.FindPropertyRelative("allowedDays").intValue;
        if (days != 127 && days != 0)
        {
            var dNames = new List<string>();
            string[] abbr = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
            for (int i = 0; i < 7; i++)
                if ((days & (1 << i)) != 0) dNames.Add(abbr[i]);
            parts.Add(string.Join("", dNames));
        }

        // Locations
        var locProp = rule.FindPropertyRelative("allowedLocationIds");
        if (locProp.arraySize > 0)
        {
            string loc0 = locProp.GetArrayElementAtIndex(0).stringValue;
            string locLabel = locProp.arraySize == 1 ? $"@{loc0}" : $"@{locProp.arraySize} locations";
            parts.Add(locLabel);
        }

        return string.Join("  |  ", parts);
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static void SectionLabel(string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        var r = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true), GUILayout.Height(1));
        EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 0.5f));
        GUILayout.Space(3);
    }

    private static string[] GetLocationIds(SceneDatabase db)
    {
        if (db?.Locations == null) return Array.Empty<string>();
        return db.Locations
            .Where(l => !string.IsNullOrEmpty(l.Id))
            .Select(l => l.Id)
            .Distinct()
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
#endif
