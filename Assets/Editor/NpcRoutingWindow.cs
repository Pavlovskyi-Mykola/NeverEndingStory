#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dedicated editor for NPC dialogue routing + placement: NPC list on the left
/// (same pattern as QuestEditorWindow), the selected NPC's identity and rules on
/// the right. Rule rows render via DialogueSelectorRuleDrawer and validation
/// comes from NpcRoutingValidation — both shared with the NpcDefinition
/// inspector. Also hosts the one-time migration of legacy Schedule entries into
/// placement rules.
/// </summary>
public class NpcRoutingWindow : EditorWindow
{
    // ── Layout ────────────────────────────────────────────────────────────
    private const float LeftPanelWidth = 230f;

    // ── State ─────────────────────────────────────────────────────────────
    private NpcDefinition _npc;
    private SerializedObject _so;
    private SerializedProperty _npcIdProp, _displayNameProp, _prefabProp;
    private SerializedProperty _rulesProp, _fallbackProp;
    private SerializedProperty _relPointsProp, _relMaxProp;

    private Vector2 _leftScroll, _rightScroll;
    private string _npcSearch = "";
    private string _ruleSearch = "";

    // NPC list cache
    private readonly List<NpcDefinition> _allNpcs = new();
    private double _nextListRefresh;
    private const double ListRefreshInterval = 2.0;

    // ── Open ──────────────────────────────────────────────────────────────
    [MenuItem("Game/NPCs/NPC Routing")]
    public static void Open()
    {
        var w = GetWindow<NpcRoutingWindow>();
        w.titleContent = new GUIContent("NPC Routing");
        w.minSize = new Vector2(760, 500);
        w.Show();
    }

    public static void OpenNpc(NpcDefinition npc)
    {
        Open();
        GetWindow<NpcRoutingWindow>().SetNpc(npc);
    }

    // ── Unity callbacks ───────────────────────────────────────────────────
    private void OnEnable() => RefreshNpcList();
    private void OnFocus()  => RefreshNpcList();

    private void OnGUI()
    {
        if (EditorApplication.timeSinceStartup > _nextListRefresh)
            RefreshNpcList();

        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawLeftPanel();
            DrawDivider();
            DrawRightPanel();
        }
    }

    private void RefreshNpcList()
    {
        _nextListRefresh = EditorApplication.timeSinceStartup + ListRefreshInterval;

        _allNpcs.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:NpcDefinition"))
        {
            var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (npc != null) _allNpcs.Add(npc);
        }
        _allNpcs.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
    }

    private void SetNpc(NpcDefinition npc)
    {
        _npc = npc;
        _so = null;
        _ruleSearch = "";
        Repaint();
    }

    private void EnsureSerialized()
    {
        if (_so != null && _so.targetObject == _npc)
            return;

        _so = new SerializedObject(_npc);
        _npcIdProp       = _so.FindProperty("NpcId");
        _displayNameProp = _so.FindProperty("DisplayName");
        _prefabProp      = _so.FindProperty("Prefab");
        _rulesProp       = _so.FindProperty("DialogueRules");
        _fallbackProp    = _so.FindProperty("DialogueFallback");
        _relPointsProp   = _so.FindProperty("RelationshipPointsPerLevel");
        _relMaxProp      = _so.FindProperty("RelationshipMaxLevel");
    }

    // ── Toolbar ───────────────────────────────────────────────────────────
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var picked = (NpcDefinition)EditorGUILayout.ObjectField(
                _npc, typeof(NpcDefinition), false, GUILayout.Width(280));
            if (picked != _npc) SetNpc(picked);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("New NPC", EditorStyles.toolbarButton, GUILayout.Width(70)))
                CreateNewNpc();

            using (new EditorGUI.DisabledScope(_npc == null))
            {
                if (GUILayout.Button("Ping Asset", EditorStyles.toolbarButton, GUILayout.Width(72)))
                {
                    EditorGUIUtility.PingObject(_npc);
                    Selection.activeObject = _npc;
                }
            }
        }
    }

    // ── Left panel – NPC list ─────────────────────────────────────────────
    private void DrawLeftPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPanelWidth), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField($"All NPCs ({_allNpcs.Count})", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _npcSearch = EditorGUILayout.TextField(_npcSearch, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck()) Repaint();

            EditorGUILayout.Space(2);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll, GUILayout.ExpandHeight(true));

            string search = _npcSearch.ToLowerInvariant();

            foreach (var npc in _allNpcs)
            {
                if (npc == null) continue;
                if (!string.IsNullOrEmpty(search))
                {
                    bool match = (npc.NpcId?.ToLowerInvariant().Contains(search) ?? false)
                              || (npc.DisplayName?.ToLowerInvariant().Contains(search) ?? false)
                              || npc.name.ToLowerInvariant().Contains(search);
                    if (!match) continue;
                }

                bool isSelected = npc == _npc;
                var  rowRect    = GUILayoutUtility.GetRect(LeftPanelWidth - 8, 36f);

                if (isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.50f, 0.86f, 0.35f));
                else if (rowRect.Contains(Event.current.mousePosition))
                    EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));

                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3, rowRect.height), NpcStripColor(npc));

                string title = string.IsNullOrEmpty(npc.DisplayName) ? npc.name : npc.DisplayName;
                int ruleCount = npc.DialogueRules?.Count ?? 0;
                string sub = string.IsNullOrEmpty(npc.NpcId)
                    ? "⚠ No ID"
                    : $"{npc.NpcId}  ·  {ruleCount} rule{(ruleCount == 1 ? "" : "s")}";

                GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 4,  rowRect.width - 10, 16), title, EditorStyles.boldLabel);
                GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 20, rowRect.width - 10, 12), sub,   EditorStyles.miniLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    SetNpc(npc);
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ New NPC"))
                CreateNewNpc();
        }
    }

    private static Color NpcStripColor(NpcDefinition npc)
    {
        if (string.IsNullOrEmpty(npc.NpcId) || npc.Prefab == null)
            return Color.red;

        bool anyPlacement = npc.DialogueRules != null &&
            npc.DialogueRules.Any(r => r != null && r.placement != NpcPlacement.WhereverNpcIs);

        // Legacy schedule pending migration, or no placement at all → amber.
        if ((npc.Schedule != null && npc.Schedule.Count > 0) || !anyPlacement)
            return new Color(0.85f, 0.65f, 0.20f);

        return new Color(0.3f, 0.7f, 0.3f);
    }

    private void DrawDivider()
    {
        var r = GUILayoutUtility.GetRect(1, 1, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(r, new Color(0.1f, 0.1f, 0.1f, 0.6f));
    }

    // ── Right panel ───────────────────────────────────────────────────────
    private void DrawRightPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
        {
            if (_npc == null)
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.HelpBox("Select an NPC or create a new one.", MessageType.Info, true);
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
            DrawLegacyScheduleSection();
            DrawRoutingSection();
            SectionGap();
            DrawRelationshipSection();
            GUILayout.Space(24);

            EditorGUILayout.EndScrollView();

            _so.ApplyModifiedProperties();
        }
    }

    private static void SectionGap() => EditorGUILayout.Space(14);

    private static void SectionHeader(string title)
        => EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

    // ── Identity ──────────────────────────────────────────────────────────
    private void DrawIdentitySection()
    {
        SectionHeader("Identity");

        EditorGUILayout.PropertyField(_npcIdProp);
        EditorGUILayout.PropertyField(_displayNameProp);
        EditorGUILayout.PropertyField(_prefabProp);

        string id = _npcIdProp.stringValue;
        if (string.IsNullOrWhiteSpace(id))
            EditorGUILayout.HelpBox("NpcId is empty — the NPC can't be spawned or addressed in dialogue.", MessageType.Error);
        else if (_allNpcs.Any(n => n != null && n != _npc && string.Equals(n.NpcId, id, System.StringComparison.OrdinalIgnoreCase)))
            EditorGUILayout.HelpBox($"NpcId '{id}' is used by another NPC asset.", MessageType.Error);
    }

    // ── Legacy schedule migration ─────────────────────────────────────────
    private void DrawLegacyScheduleSection()
    {
        if (_npc.Schedule == null || _npc.Schedule.Count == 0)
            return;

        EditorGUILayout.HelpBox(
            $"This NPC has {_npc.Schedule.Count} legacy schedule entr(ies). The schedule list is no longer used at runtime — presence is now driven by placement on dialogue rules.",
            MessageType.Warning);

        if (GUILayout.Button("Convert legacy schedule to placement rules"))
            MigrateLegacySchedule();

        SectionGap();
    }

    private void MigrateLegacySchedule()
    {
        Undo.RecordObject(_npc, "Convert Legacy NPC Schedule");

        _npc.DialogueRules ??= new List<DialogueSelectorRule>();

        foreach (var entry in _npc.Schedule)
        {
            _npc.DialogueRules.Add(new DialogueSelectorRule
            {
                output        = DialogueRuleOutput.SingleGraph,
                tier          = DialogueRuleTier.Routine,
                priority      = 0,
                placement     = entry.Absent ? NpcPlacement.Hidden : NpcPlacement.AtLocation,
                locationScene = entry.LocationScene,
                spawnPointKey = entry.SpawnPointKey,
                allowedDays   = entry.Days,
                allowedPhases = entry.Phases,
            });
        }

        int migrated = _npc.Schedule.Count;
        _npc.Schedule.Clear();

        EditorUtility.SetDirty(_npc);
        _so.Update();

        Debug.Log($"[NpcRouting] Migrated {migrated} schedule entr(ies) on '{_npc.name}' to presence-only placement rules (tier Routine). Assign graphs to them to give the NPC dialogue at those spots.", _npc);
    }

    // ── Routing (rules + fallback + validation) ───────────────────────────
    private void DrawRoutingSection()
    {
        int count = _npc.DialogueRules?.Count ?? 0;

        using (new EditorGUILayout.HorizontalScope())
        {
            SectionHeader($"Dialogue Routing ({count} rules)");
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(count < 2))
            {
                if (GUILayout.Button(new GUIContent("Sort to runtime order",
                        "Reorders the list by tier, then priority — the order the selector evaluates in."),
                    EditorStyles.miniButton, GUILayout.Width(140)))
                {
                    NpcRoutingEditorUtility.SortRulesToRuntimeOrder(_npc);
                    _so.Update();
                }
            }
        }

        foreach (var msg in NpcRoutingValidation.Validate(_npc))
            EditorGUILayout.HelpBox(msg.Text, msg.Type);

        EditorGUILayout.PropertyField(_fallbackProp,
            new GUIContent("Dialogue Fallback", "Played when no rule resolves a graph."));

        EditorGUILayout.Space(4);

        _ruleSearch = EditorGUILayout.TextField(_ruleSearch, EditorStyles.toolbarSearchField);

        if (string.IsNullOrWhiteSpace(_ruleSearch))
        {
            EditorGUILayout.PropertyField(_rulesProp, new GUIContent("Rules"), true);
            return;
        }

        // Filtered view: matching rules only (editable in place; reorder via the
        // unfiltered list). Matches graph/pool names, quest ids, locations, tiers.
        string search = _ruleSearch.ToLowerInvariant();
        int shown = 0;

        for (int i = 0; i < _rulesProp.arraySize; i++)
        {
            if (!NpcRoutingEditorUtility.RuleMatches(_npc, i, search))
                continue;

            shown++;
            EditorGUILayout.PropertyField(_rulesProp.GetArrayElementAtIndex(i),
                new GUIContent($"#{i}"), true);
        }

        if (shown == 0)
            EditorGUILayout.HelpBox("No rules match the search.", MessageType.Info);
        else
            EditorGUILayout.LabelField($"{shown} of {_rulesProp.arraySize} rules shown (clear search to reorder/add).",
                EditorStyles.miniLabel);
    }

    // ── Relationship ──────────────────────────────────────────────────────
    private void DrawRelationshipSection()
    {
        SectionHeader("Relationship (0 = RelationshipManager defaults)");
        EditorGUILayout.PropertyField(_relPointsProp, new GUIContent("Points Per Level"));
        EditorGUILayout.PropertyField(_relMaxProp, new GUIContent("Max Level"));
    }

    // ── Create ────────────────────────────────────────────────────────────
    private void CreateNewNpc()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "New NPC Definition", "NPC_New", "asset",
            "Choose where to save the new NPC definition.",
            "Assets/ScriptableObjects");

        if (string.IsNullOrEmpty(path))
            return;

        var npc = CreateInstance<NpcDefinition>();
        AssetDatabase.CreateAsset(npc, path);
        AssetDatabase.SaveAssets();

        RefreshNpcList();
        SetNpc(npc);
    }
}
#endif
