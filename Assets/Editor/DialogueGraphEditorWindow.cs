#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DialogueGraphEditorWindow : EditorWindow
{
    private const float LeftPanelWidth = 300f;
    private const float NodeWidth = 280f;
    private const float NodeHeaderHeight = 22f;
    private const float PortSize = 14f;

    private DialogueGraph _graph;
    private SerializedObject _graphSO;
    private SerializedProperty _nodesProp;
    private SerializedProperty _startNodeIdProp;

    private string[] _speakerOptionsCache = Array.Empty<string>();
    private double _speakerOptionsNextRefreshTime = 0;
    private const double SpeakerRefreshInterval = 1.0;

    [SerializeField] private bool showGrid = true;
    [SerializeField] private float gridSmall = 20f;
    [SerializeField] private float gridLarge = 100f;
    [SerializeField] private float gridOpacitySmall = 0.12f;
    [SerializeField] private float gridOpacityLarge = 0.22f;

    private string _mouseDownNodeId;
    private bool _mouseDownOnHeader;

    private Vector2 _canvasScroll;
    private Vector2 _inspectorScroll;

    private string _selectedNodeId = null;

    // Per-choice "conditions" foldout state in the inline Choice node view.
    // Keyed by nodeId + choice index; transient UI state (safe to reset on reorder).
    private readonly HashSet<string> _expandedChoices = new();
    private static string ChoiceCondKey(string nodeId, int choiceIndex) => nodeId + ":" + choiceIndex;

    [SerializeField] private bool snapToGrid = false;
    [SerializeField] private float snapSize = 10f;

    private static float Snap(float v, float step) => step <= 0f ? v : Mathf.Round(v / step) * step;
    private static Vector2 Snap(Vector2 p, float step) => new Vector2(Snap(p.x, step), Snap(p.y, step));


    private bool HasSelection => !string.IsNullOrEmpty(_selectedNodeId);

    private int GetSelectedIndex()
    {
        if (string.IsNullOrEmpty(_selectedNodeId)) return -1;
        return FindNodeIndexById(_selectedNodeId);
    }

    // Connection state: click output → click target input
    private PendingConnection _pending;

    // Cached rects each repaint
    private readonly Dictionary<string, Rect> _nodeRects = new();
    private readonly Dictionary<(string nodeId, string portKey), Vector2> _portCentersLocal = new();

    private Vector2 _lastCanvasMouse; // in canvas coords (scroll included)

    // -------------------- Node coloring / styles --------------------
    private readonly Dictionary<string, GUIStyle> _nodeStyleCache = new();
    private readonly List<Texture2D> _generatedTextures = new();

    private void OnDisable()
    {
        // Cleanup generated textures to avoid leaking editor resources
        for (int i = 0; i < _generatedTextures.Count; i++)
        {
            if (_generatedTextures[i] != null)
                DestroyImmediate(_generatedTextures[i]);
        }
        _generatedTextures.Clear();
        _nodeStyleCache.Clear();
    }

    private static Color GetTypeColor(DialogueNodeType type)
    {
        // Requested palette:
        // Line   : dark blue
        // Choice : dark green
        // Branch : purple
        // Start  : gold (handled separately)
        return type switch
        {
            DialogueNodeType.Line => new Color(0.12f, 0.18f, 0.38f),
            DialogueNodeType.Choice => new Color(0.10f, 0.30f, 0.16f),
            DialogueNodeType.Branch => new Color(0.34f, 0.16f, 0.42f),

            // optional extras (safe defaults)
            DialogueNodeType.Command => new Color(0.35f, 0.26f, 0.12f),
            DialogueNodeType.End => new Color(0.30f, 0.10f, 0.10f),

            _ => new Color(0.18f, 0.18f, 0.18f),
        };
    }

    // -------------------- All-graphs list --------------------
    private const float GraphListWidth = 230f;
    [SerializeField] private bool showGraphList = true;
    private readonly List<DialogueGraph> _allGraphs = new();
    private string _graphSearch = "";
    private Vector2 _graphListScroll;

    // ---------------------------------------------------------------
    [MenuItem("Game/Dialogue/Dialogue Editor")]
    public static void Open()
    {
        var w = GetWindow<DialogueGraphEditorWindow>();
        w.titleContent = new GUIContent("Dialogue Editor");
        w.Show();
    }

    /// <summary>Opens the window focused on a specific graph (used by the asset inspector button).</summary>
    public static void OpenGraph(DialogueGraph graph)
    {
        var w = GetWindow<DialogueGraphEditorWindow>();
        w.titleContent = new GUIContent("Dialogue Editor");
        w.SetGraph(graph);
        w.Show();
        w.Focus();
    }

    private void OnEnable()
    {
        wantsMouseMove = true;
        RefreshGraphList();
    }

    private void OnFocus()
    {
        RefreshGraphList();
    }

    private void RefreshGraphList()
    {
        _allGraphs.Clear();

        foreach (var guid in AssetDatabase.FindAssets("t:DialogueGraph"))
        {
            var g = AssetDatabase.LoadAssetAtPath<DialogueGraph>(AssetDatabase.GUIDToAssetPath(guid));
            if (g != null) _allGraphs.Add(g);
        }

        _allGraphs.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
    }

    private void OnGUI()
    {
        DrawTopToolbar();

        // Keyboard delete for selected node (Delete)
        if (Event.current.type == EventType.KeyDown && HasSelection)
        {
            if (Event.current.keyCode == KeyCode.Delete)
            {
                DeleteSelectedNode();
                Event.current.Use();
            }
        }

        EditorGUILayout.BeginHorizontal();
        if (showGraphList)
            DrawGraphListPanel();
        DrawLeftPanel();
        DrawCanvas();
        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.MouseMove && _pending.IsActive)
            Repaint();
    }

    // ── All-dialogues list (same pattern as the Quest Editor) ────────────
    private void DrawGraphListPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(GraphListWidth), GUILayout.ExpandHeight(true)))
        {
            EditorGUILayout.LabelField($"All Dialogues ({_allGraphs.Count})", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _graphSearch = EditorGUILayout.TextField(_graphSearch, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck()) Repaint();

            EditorGUILayout.Space(2);
            _graphListScroll = EditorGUILayout.BeginScrollView(_graphListScroll, GUILayout.ExpandHeight(true));

            string search = string.IsNullOrEmpty(_graphSearch) ? null : _graphSearch.ToLowerInvariant();

            foreach (var g in _allGraphs)
            {
                if (g == null) continue;

                if (search != null && !g.name.ToLowerInvariant().Contains(search))
                    continue;

                bool isSelected = g == _graph;
                var rowRect = GUILayoutUtility.GetRect(GraphListWidth - 8, 36f);

                if (isSelected)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.50f, 0.86f, 0.35f));
                else if (rowRect.Contains(Event.current.mousePosition))
                    EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));

                // Colour strip: red when no start node is set.
                bool hasStart = !string.IsNullOrEmpty(g.StartNodeId);
                EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y, 3, rowRect.height),
                    hasStart ? new Color(0.3f, 0.7f, 0.3f) : Color.red);

                int nodeCount = g.Nodes != null ? g.Nodes.Count : 0;
                string sub = hasStart ? $"{nodeCount} nodes" : $"{nodeCount} nodes · ⚠ no start";
                if (g.CountsAsRelationshipTalk) sub += " · rel";

                GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 4, rowRect.width - 10, 16), g.name, EditorStyles.boldLabel);
                GUI.Label(new Rect(rowRect.x + 8, rowRect.y + 20, rowRect.width - 10, 12), sub, EditorStyles.miniLabel);

                if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
                {
                    SetGraph(g);
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // Divider
        var line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.Width(1), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(line, new Color(0f, 0f, 0f, 0.4f));
    }

    private void DrawTopToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            showGraphList = GUILayout.Toggle(showGraphList, "Dialogues", EditorStyles.toolbarButton, GUILayout.Width(70));

            var newGraph = (DialogueGraph)EditorGUILayout.ObjectField(
                _graph, typeof(DialogueGraph), false, GUILayout.Width(360));

            if (newGraph != _graph)
                SetGraph(newGraph);

            GUILayout.FlexibleSpace();
            snapToGrid = GUILayout.Toggle(snapToGrid, "Snap", EditorStyles.toolbarButton);

            if (GUILayout.Button("New Graph", EditorStyles.toolbarButton))
                CreateNewGraph();

            using (new EditorGUI.DisabledScope(_graph == null))
            {
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton))
                    ValidateGraph();

                if (GUILayout.Button("Ping Asset", EditorStyles.toolbarButton))
                {
                    if (_graph != null)
                    {
                        EditorGUIUtility.PingObject(_graph);
                        Selection.activeObject = _graph;
                    }
                }
            }
        }
    }

    private void DrawLeftPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPanelWidth)))
        {
            EditorGUILayout.LabelField("Graph", EditorStyles.boldLabel);

            if (_graph == null)
            {
                EditorGUILayout.HelpBox("Assign a DialogueGraph asset to edit.", MessageType.Info);
                return;
            }

            EnsureSerialized();

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Create Nodes", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tip: If a node is selected, creating a node will auto-connect from the first available output.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Line")) CreateNode(typeof(LineNode));
                if (GUILayout.Button("Choice")) CreateNode(typeof(ChoiceNode));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Branch")) CreateNode(typeof(BranchNode));
                if (GUILayout.Button("Command")) CreateNode(typeof(CommandNode));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("End")) CreateNode(typeof(EndNode));
            }

            EditorGUILayout.Space(10);

            if (_pending.IsActive)
            {
                EditorGUILayout.HelpBox(
                    $"Connecting from: {_pending.SourceNodeId}\nPort: {_pending.PortKey}\nClick target node input to complete.\nRight-click canvas to cancel.",
                    MessageType.Info);

                if (GUILayout.Button("Cancel Connect"))
                    _pending = default;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);

            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);

            int sel = GetSelectedIndex();
            if (sel < 0)
            {
                EditorGUILayout.HelpBox("Select a node to edit its fields.", MessageType.None);
            }
            else
            {
                DrawSelectedNodeInspector(sel);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!HasSelection))
                {
                    if (GUILayout.Button("Delete Selected"))
                        DeleteSelectedNode();
                }
            }
        }
    }

    private void DrawCanvas()
    {
        if (_graph == null)
        {
            GUILayout.FlexibleSpace();
            return;
        }

        EnsureSerialized();

        var canvasRect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        GUI.Box(canvasRect, GUIContent.none);

        // Scroll-view used as a simple pan/zoomless canvas
        var innerRect = new Rect(0, 0, 5000, 5000);
        _canvasScroll = GUI.BeginScrollView(canvasRect, _canvasScroll, innerRect);

        if (showGrid)
            DrawGrid(innerRect, _canvasScroll, gridSmall, gridLarge, gridOpacitySmall, gridOpacityLarge);

        // Track mouse in canvas (content) coordinates.
        // NOTE: Inside BeginScrollView, IMGUI already reports Event.current.mousePosition
        // in the scrolled content space. Adding _canvasScroll again will double-apply
        // the offset, which breaks node hit-testing, dragging, and node creation after panning.
        _lastCanvasMouse = Event.current.mousePosition;


        // Left-click empty canvas to deselect the current node.
        // IMPORTANT: only if the click wasn't already used by a node window.
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Event.current.type != EventType.Used)
        {
            bool clickedOnNode = false;
            foreach (var kv in _nodeRects)
            {
                if (kv.Value.Contains(_lastCanvasMouse))
                {
                    clickedOnNode = true;
                    break;
                }
            }

            if (!clickedOnNode)
            {
                _selectedNodeId = null;
                GUI.FocusControl(null);
                Repaint();
            }
        }

        _nodeRects.Clear();
        _portCentersLocal.Clear();

        // Cache node rects from serialized positions (used for initial hit-tests and as a
        // fallback before the first window layout pass).
        CacheNodeRects();

        BeginWindows();
        for (int i = 0; i < _nodesProp.arraySize; i++)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(i);
            var idProp = nodeProp.FindPropertyRelative("id");
            var posProp = nodeProp.FindPropertyRelative("editorPosition");

            string id = idProp.stringValue;
            Vector2 pos = posProp.vector2Value;

            float width = GetNodeWidth(nodeProp);
            var rect = new Rect(pos.x, pos.y, width, GetNodeHeight(nodeProp));
            rect = GUI.Window(i, rect, _ => DrawNodeWindow(i, nodeProp), GetNodeTitle(nodeProp));

            if (rect.position != pos)
            {
                var newPos = rect.position;

                if (snapToGrid)
                    newPos = Snap(newPos, snapSize);

                // If we snapped, also move the rect so the window visually “sticks” to the grid immediately.
                rect.position = newPos;

                posProp.vector2Value = newPos;
                ApplyModified();
            }

            // Keep rect cache fresh after window drag
            _nodeRects[id] = rect;
        }
        EndWindows();

        // When a connection is pending, LMB on empty canvas opens "Create node" menu and auto-connects.
        if (_pending.IsActive &&
            Event.current.type == EventType.MouseDown &&
            Event.current.button == 0 &&
            !IsMouseOverAnyNode(_lastCanvasMouse))
        {
            ShowCreateAndConnectMenu(_lastCanvasMouse);
            Event.current.Use();
        }

        // Frame selected node with F
        if (Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.F &&
            !EditorGUIUtility.editingTextField)
        {
            FrameSelectedNode(canvasRect);
            Event.current.Use();
        }

        // Now that ports were laid out and cached, draw connections using the cached port centers.
        DrawAllConnections();

        // Right-click on empty canvas: cancel any pending connection, then open "Create Node" menu.
        if (Event.current.type == EventType.MouseDown &&
            Event.current.button == 1 &&
            !IsMouseOverAnyNode(_lastCanvasMouse))
        {
            _pending = default;
            ShowCreateAndConnectMenu(_lastCanvasMouse);
            Event.current.Use();
        }

        GUI.EndScrollView();
    }

    private void DrawGrid(Rect innerRect, Vector2 scroll, float small, float large, float opacitySmall, float opacityLarge)
    {
        // Draw in canvas content coordinates (inside BeginScrollView)
        Handles.BeginGUI();

        var oldColor = Handles.color;

        void DrawStep(float step, float opacity)
        {
            Handles.color = new Color(1f, 1f, 1f, opacity);

            // Offset so grid appears stable while panning
            float xOff = scroll.x % step;
            float yOff = scroll.y % step;

            // Vertical lines
            for (float x = -xOff; x < innerRect.width; x += step)
            {
                Handles.DrawLine(new Vector3(x, 0f), new Vector3(x, innerRect.height));
            }

            // Horizontal lines
            for (float y = -yOff; y < innerRect.height; y += step)
            {
                Handles.DrawLine(new Vector3(0f, y), new Vector3(innerRect.width, y));
            }
        }

        DrawStep(small, opacitySmall);
        DrawStep(large, opacityLarge);

        Handles.color = oldColor;
        Handles.EndGUI();
    }

    private void DrawNodeWindow(int index, SerializedProperty nodeProp)
    {
        string nodeId = nodeProp.FindPropertyRelative("id").stringValue;
        bool isStart = _startNodeIdProp != null && _startNodeIdProp.stringValue == nodeId;

        Color headerColor = isStart
            ? new Color(0.85f, 0.70f, 0.20f) // Gold
            : GetTypeColor(GetNodeType(nodeProp));

        Rect headerRect = new Rect(0, 0, NodeWidth, NodeHeaderHeight);
        EditorGUI.DrawRect(headerRect, headerColor);

        // Optional subtle bottom line for separation
        EditorGUI.DrawRect(
            new Rect(0, NodeHeaderHeight - 1, NodeWidth, 1),
            new Color(0, 0, 0, 0.25f)
        );
        HandleNodeSelection(nodeId, headerRect);

        GUILayout.Space(10);

        // ---- your controls ----
        using (new GUILayout.HorizontalScope())
        {
            DrawInputPort(nodeProp);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("★", "Set as Start Node"),
                GUILayout.Width(22), GUILayout.Height(18)))
            {
                _startNodeIdProp.stringValue = nodeId;
                ApplyModified();
            }
        }

        GUILayout.Space(5);

        switch (GetNodeType(nodeProp))
        {
            case DialogueNodeType.Line: DrawLineNodeInline(nodeProp); break;
            case DialogueNodeType.Choice: DrawChoiceNodeInline(nodeProp); break;
            case DialogueNodeType.Branch: DrawBranchNodeInline(nodeProp); break;
            case DialogueNodeType.Command: DrawCommandNodeInline(nodeProp); break;
            case DialogueNodeType.End: GUILayout.Label("(End)", EditorStyles.miniLabel); break;
        }

        // Optional: make sure there IS clickable empty space at the bottom
        GUILayout.FlexibleSpace();

        GUI.DragWindow(headerRect);
    }

    private void HandleNodeSelection(string nodeId, Rect headerRect)
    {
        var e = Event.current;

        // Track where the click started
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            _mouseDownNodeId = nodeId;
            _mouseDownOnHeader = headerRect.Contains(e.mousePosition);

            // Header click: select immediately (and clear focus)
            if (_mouseDownOnHeader)
            {
                SelectNode(nodeId, clearFocus: true);
            }

            return;
        }

        // Decide "empty-space select" on MouseUp (after controls had a chance to grab the mouse)
        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (_mouseDownNodeId != nodeId)
                return;

            // If user clicked a control (TextArea/Popup/Button), Unity usually sets hotControl.
            // So only treat it as "empty space" when no control is active.
            if (!_mouseDownOnHeader && GUIUtility.hotControl == 0)
            {
                SelectNode(nodeId, clearFocus: true);
            }

            _mouseDownNodeId = null;
            _mouseDownOnHeader = false;
        }
    }

    private void SelectNode(string nodeId, bool clearFocus)
    {
        _selectedNodeId = nodeId;
        if (clearFocus) GUI.FocusControl(null);
        Repaint();
    }

    // ---------------- Inline node views + ports ----------------

    private const string CustomSpeakerOption = "<Custom…>";

    private void DrawLineNodeInline(SerializedProperty nodeProp)
    {
        var speakerProp = nodeProp.FindPropertyRelative("speaker");
        var textProp = nodeProp.FindPropertyRelative("text");
        var nextProp = nodeProp.FindPropertyRelative("nextNodeId");

        var options = GetSpeakerOptions();
        int idx = Array.IndexOf(options, speakerProp.stringValue);
        if (idx < 0) idx = 0;

        // 1) Popup
        EditorGUI.BeginChangeCheck();
        int newIdx = EditorGUILayout.Popup("Speaker", idx, options);
        if (EditorGUI.EndChangeCheck())
        {
            if (options[newIdx] != CustomSpeakerOption)
            {
                speakerProp.stringValue = options[newIdx];
                ApplyModified();
            }
            else
            {
                // Switching to custom: keep current text (or clear it if you prefer)
                // speakerProp.stringValue = "";
                ApplyModified();
            }
        }

        // 2) Custom field (draw every frame if custom is selected)
        bool isCustom =
            (newIdx >= 0 && newIdx < options.Length && options[newIdx] == CustomSpeakerOption) ||
            (idx >= 0 && idx < options.Length && options[idx] == CustomSpeakerOption);

        if (isCustom)
        {
            EditorGUI.BeginChangeCheck();
            string custom = EditorGUILayout.TextField("Custom", speakerProp.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                speakerProp.stringValue = custom;
                ApplyModified();
            }
        }

        // Text
        EditorGUI.BeginChangeCheck();
        string txt = EditorGUILayout.TextArea(textProp.stringValue ?? "", GUILayout.Height(50));
        if (EditorGUI.EndChangeCheck())
        {
            textProp.stringValue = txt;
            ApplyModified();
        }

        GUILayout.Space(6);

        DrawOutputRow(nodeProp, "Next", "Next", nextProp.stringValue, () =>
        {
            CreateEndAndConnect(nodeProp, "Next");
        });
    }

    private void DrawChoiceNodeInline(SerializedProperty nodeProp)
    {
        string nodeId = nodeProp.FindPropertyRelative("id").stringValue;
        var choicesProp = nodeProp.FindPropertyRelative("choices");

        GUILayout.Label($"Choices: {choicesProp.arraySize}", EditorStyles.miniBoldLabel);

        // Show all choices (still compact with a scroll in window? we'll keep it small)
        int maxShow = Mathf.Min(choicesProp.arraySize, 6);

        for (int i = 0; i < maxShow; i++)
        {
            var ch = choicesProp.GetArrayElementAtIndex(i);
            var textProp = ch.FindPropertyRelative("text");
            var nextProp = ch.FindPropertyRelative("nextNodeId");

            using (new GUILayout.HorizontalScope())
            {
                // Reorder up/down
                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button("↑", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        choicesProp.MoveArrayElement(i, i - 1);
                        ApplyModified();
                        return;
                    }
                }

                using (new EditorGUI.DisabledScope(i == choicesProp.arraySize - 1))
                {
                    if (GUILayout.Button("↓", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        choicesProp.MoveArrayElement(i, i + 1);
                        ApplyModified();
                        return;
                    }
                }

                // Duplicate
                if (GUILayout.Button("⎘", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    DuplicateChoice(choicesProp, i);
                    ApplyModified();
                    return;
                }

                // Inline edit choice text
                EditorGUI.BeginChangeCheck();
                string cText = EditorGUILayout.TextField(textProp.stringValue);
                if (EditorGUI.EndChangeCheck())
                {
                    textProp.stringValue = cText;
                    ApplyModified();
                }

                // Output port
                DrawOutputPort(nodeProp, $"Choice:{i}", nextProp.stringValue);
            }

            // "Create End" on dangling choice link
            if (string.IsNullOrEmpty(nextProp.stringValue))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Space(72);
                    if (GUILayout.Button("Create End →", GUILayout.Height(18)))
                    {
                        CreateEndAndConnect(nodeProp, $"Choice:{i}");
                        return;
                    }
                }
            }

            // ---- Per-choice condition ("gated" option: visible/pickable only when it passes) ----
            if (DrawChoiceConditionRow(ch, nodeId, i))
                return; // toggled or structure changed → relayout next repaint
        }

        if (choicesProp.arraySize > maxShow)
            GUILayout.Label("… (edit full list in inspector)", EditorStyles.miniLabel);

        GUILayout.Space(4);
        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Choice", GUILayout.Height(20)))
            {
                AddChoice(nodeProp);
            }

            if (GUILayout.Button("Auto End Dangling", GUILayout.Height(20)))
            {
                AutoEndDanglingChoiceLinks(nodeProp);
            }
        }
    }

    /// <summary>
    /// Inline "gated option" editor for a single choice. Shows a one-line summary and an
    /// expandable condition list. A choice with conditions is only shown/pickable at runtime
    /// when they all pass (see DialogueRunner.BuildPresentedChoices) — no false branch needed.
    /// Returns true when the node layout changed this frame (caller should bail and let it repaint).
    /// </summary>
    private bool DrawChoiceConditionRow(SerializedProperty choiceProp, string nodeId, int choiceIndex)
    {
        var condProp = choiceProp.FindPropertyRelative("conditions");
        var allProp = condProp.FindPropertyRelative("all");
        int condCount = allProp != null ? allProp.arraySize : 0;

        string key = ChoiceCondKey(nodeId, choiceIndex);
        bool expanded = _expandedChoices.Contains(key);

        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Space(24);

            string summary;
            if (condCount == 0) summary = "always shown";
            else if (condCount == 1) summary = "🔒 " + SingleConditionLabel(allProp.GetArrayElementAtIndex(0));
            else summary = $"🔒 {condCount} conditions (all must pass)";

            var oldC = GUI.contentColor;
            GUI.contentColor = condCount == 0
                ? new Color(1f, 1f, 1f, 0.5f)
                : new Color(0.95f, 0.85f, 0.4f);
            GUILayout.Label(summary, EditorStyles.miniLabel);
            GUI.contentColor = oldC;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(expanded ? "▾ if" : "▸ if", GUILayout.Width(42), GUILayout.Height(16)))
            {
                if (expanded) _expandedChoices.Remove(key);
                else _expandedChoices.Add(key);
                return true;
            }
        }

        if (expanded)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (DrawConditionGroupInline(condProp))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Draws an editable DialogueConditionGroup (its 'all' list). Returns true if the
    /// list structure changed (add/remove), so the caller can bail and relayout.</summary>
    private bool DrawConditionGroupInline(SerializedProperty conditionGroupProp)
    {
        var allProp = conditionGroupProp.FindPropertyRelative("all");
        if (allProp == null) return false;

        for (int i = 0; i < allProp.arraySize; i++)
        {
            var c = allProp.GetArrayElementAtIndex(i);
            var typeProp = c.FindPropertyRelative("type");

            using (new GUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(typeProp, GUIContent.none);
                if (EditorGUI.EndChangeCheck()) ApplyModified();

                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                {
                    allProp.DeleteArrayElementAtIndex(i);
                    ApplyModified();
                    return true;
                }
            }

            var type = (DialogueConditionType)typeProp.intValue;

            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 100f;

            EditorGUI.BeginChangeCheck();
            switch (type)
            {
                case DialogueConditionType.TimeOfDayIs:
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("timeOfDayValue"), new GUIContent("Time ="));
                    break;

                case DialogueConditionType.FlagIsTrue:
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("flagId"), new GUIContent("Flag"));
                    break;

                default:
                    EditorGUILayout.PropertyField(c.FindPropertyRelative("intValue"), new GUIContent(ConditionValueLabel(type)));
                    if (type == DialogueConditionType.RelationshipAtLeast)
                        EditorGUILayout.PropertyField(c.FindPropertyRelative("npcId"), new GUIContent("NPC (blank=current)"));
                    break;
            }
            if (EditorGUI.EndChangeCheck()) ApplyModified();

            EditorGUIUtility.labelWidth = oldLabelWidth;

            GUILayout.Space(3);
        }

        if (GUILayout.Button("＋ Condition", GUILayout.Height(18)))
        {
            int idx = allProp.arraySize;
            allProp.InsertArrayElementAtIndex(idx);
            var nc = allProp.GetArrayElementAtIndex(idx);
            // Reset to a clean default (InsertArrayElementAtIndex clones the previous entry).
            nc.FindPropertyRelative("type").intValue = 0;
            nc.FindPropertyRelative("intValue").intValue = 0;
            nc.FindPropertyRelative("flagId").stringValue = "";
            nc.FindPropertyRelative("npcId").stringValue = "";
            ApplyModified();
            return true;
        }

        return false;
    }

    private static string ConditionValueLabel(DialogueConditionType type)
    {
        return type switch
        {
            DialogueConditionType.MoneyAtLeast        => "Money ≥",
            DialogueConditionType.InfluenceAtLeast    => "Influence ≥",
            DialogueConditionType.StrategyAtLeast     => "Strategy ≥",
            DialogueConditionType.NetworkingAtLeast   => "Networking ≥",
            DialogueConditionType.ReputationAtLeast   => "Reputation ≥",
            DialogueConditionType.RelationshipAtLeast => "Relationship ≥",
            _                                         => "Value",
        };
    }

    private void DrawBranchNodeInline(SerializedProperty nodeProp)
    {
        var tProp    = nodeProp.FindPropertyRelative("trueNextNodeId");
        var fProp    = nodeProp.FindPropertyRelative("falseNextNodeId");
        var condProp = nodeProp.FindPropertyRelative("condition");

        // Show a one-line condition summary so the node is readable without opening the inspector
        if (condProp != null)
        {
            string summary = BuildConditionSummary(condProp);
            var oldC = GUI.contentColor;
            GUI.contentColor = new Color(0.85f, 0.85f, 0.60f);
            GUILayout.Label(summary, EditorStyles.miniLabel);
            GUI.contentColor = oldC;
        }

        GUILayout.Space(4);

        DrawOutputRow(nodeProp, "True ✓", "True", tProp.stringValue, () =>
        {
            CreateEndAndConnect(nodeProp, "True");
        });

        DrawOutputRow(nodeProp, "False ✗", "False", fProp.stringValue, () =>
        {
            CreateEndAndConnect(nodeProp, "False");
        });
    }

    private static string BuildConditionSummary(SerializedProperty condProp)
    {
        // condProp is a DialogueConditionGroup (has 'all' list inside)
        var allProp = condProp.FindPropertyRelative("all");
        if (allProp == null || allProp.arraySize == 0)
            return "if: <no condition set>";

        if (allProp.arraySize == 1)
            return "if: " + SingleConditionLabel(allProp.GetArrayElementAtIndex(0));

        return $"if: {allProp.arraySize} conditions (all must pass)";
    }

    private static string SingleConditionLabel(SerializedProperty c)
    {
        var typeProp = c.FindPropertyRelative("type");
        if (typeProp == null) return "?";
        var type = (DialogueConditionType)typeProp.intValue;
        int intVal = c.FindPropertyRelative("intValue")?.intValue ?? 0;
        return type switch
        {
            DialogueConditionType.MoneyAtLeast       => $"Money ≥ {intVal}",
            DialogueConditionType.InfluenceAtLeast   => $"Influence ≥ {intVal}",
            DialogueConditionType.StrategyAtLeast    => $"Strategy ≥ {intVal}",
            DialogueConditionType.NetworkingAtLeast  => $"Networking ≥ {intVal}",
            DialogueConditionType.ReputationAtLeast  => $"Reputation ≥ {intVal}",
            DialogueConditionType.TimeOfDayIs        =>
                $"Time = {(TimeOfDay)(c.FindPropertyRelative("timeOfDayValue")?.intValue ?? 0)}",
            DialogueConditionType.FlagIsTrue         => $"Flag '{c.FindPropertyRelative("flagId")?.stringValue ?? "?"}' is true",
            DialogueConditionType.RelationshipAtLeast => $"Relationship ≥ {intVal}",
            _                                        => type.ToString()
        };
    }

    private void DrawCommandNodeInline(SerializedProperty nodeProp)
    {
        var cmdsProp = nodeProp.FindPropertyRelative("commands");
        var nextProp = nodeProp.FindPropertyRelative("nextNodeId");

        GUILayout.Label($"Commands: {cmdsProp.arraySize}", EditorStyles.miniBoldLabel);
        GUILayout.Space(6);

        DrawOutputRow(nodeProp, "Next", "Next", nextProp.stringValue, () =>
        {
            CreateEndAndConnect(nodeProp, "Next");
        });
    }

    private void DrawOutputRow(SerializedProperty nodeProp, string label, string portKey, string currentTarget, Action onCreateEndIfDangling)
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(45));
            GUILayout.FlexibleSpace();

            DrawOutputPort(nodeProp, portKey, currentTarget);

            // Little "+End" button only when dangling
            if (string.IsNullOrEmpty(currentTarget))
            {
                if (GUILayout.Button("+End", GUILayout.Width(44), GUILayout.Height(18)))
                    onCreateEndIfDangling?.Invoke();
            }
            else
            {
                GUILayout.Space(48);
            }
        }
    }

    private void DrawInputPort(SerializedProperty nodeProp)
    {
        var r = GUILayoutUtility.GetRect(PortSize, PortSize, GUILayout.Width(PortSize), GUILayout.Height(PortSize));

        if (GUI.Button(r, GUIContent.none))
        {
            if (_pending.IsActive)
            {
                string targetNodeId = nodeProp.FindPropertyRelative("id").stringValue;
                CompleteConnection(targetNodeId);
            }
        }

        CachePortLocalCenter(nodeProp, "IN", r);
        DrawPortSquare(r, Color.white);
    }

    private void DrawOutputPort(SerializedProperty nodeProp, string portKey, string currentTarget)
    {
        var r = GUILayoutUtility.GetRect(PortSize, PortSize, GUILayout.Width(PortSize), GUILayout.Height(PortSize));

        if (GUI.Button(r, GUIContent.none))
        {
            string srcId = nodeProp.FindPropertyRelative("id").stringValue;
            _pending = new PendingConnection(srcId, portKey);
            Repaint();
        }

        CachePortLocalCenter(nodeProp, portKey, r);

        var col = string.IsNullOrEmpty(currentTarget)
            ? new Color(1f, 0.6f, 0.2f)
            : new Color(0.35f, 0.95f, 0.35f);

        DrawPortSquare(r, col);
    }

    private void CachePortLocalCenter(SerializedProperty nodeProp, string portKey, Rect localRect)
    {
        string nodeId = nodeProp.FindPropertyRelative("id").stringValue;
        var center = new Vector2(localRect.x + localRect.width * 0.5f, localRect.y + localRect.height * 0.5f);
        _portCentersLocal[(nodeId, portKey)] = center;
    }

    private static void DrawPortSquare(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    // ---------------- Inspector ----------------
    private void DrawSelectedNodeInspector(int index)
    {
        var nodeProp = _nodesProp.GetArrayElementAtIndex(index);
        var type = GetNodeType(nodeProp);

        EditorGUILayout.LabelField(GetNodeTitle(nodeProp), EditorStyles.boldLabel);

        EditorGUILayout.Space(6);

        switch (type)
        {
            case DialogueNodeType.Line:
                {
                    var speakerProp = nodeProp.FindPropertyRelative("speaker");

                    var options = GetSpeakerOptions(); // your helper that returns string[]
                    int idx = Array.IndexOf(options, speakerProp.stringValue);
                    if (idx < 0) idx = 0;

                    int newIdx = EditorGUILayout.Popup("Speaker", idx, options);

                    if (options.Length > 0 && options[newIdx] == "<Custom…>")
                    {
                        // keep current value editable as custom
                        speakerProp.stringValue = EditorGUILayout.TextField("Custom", speakerProp.stringValue);
                    }
                    else if (options.Length > 0)
                    {
                        speakerProp.stringValue = options[newIdx];
                    }

                    EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("text"));
                    EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("onEnterCommands"), true);
                    break;
                }

            case DialogueNodeType.Choice:
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("choices"), true);
                break;

            case DialogueNodeType.Branch:
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("condition"), true);

                break;

            case DialogueNodeType.Command:
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("commands"), true);
                break;

            case DialogueNodeType.End:
                EditorGUILayout.HelpBox("End node has no editable fields.", MessageType.None);
                break;
        }

        if (GUI.changed)
            ApplyModified();

        EditorGUILayout.Space(10);
    }

    // ---------------- Connections drawing ----------------
    private void CacheNodeRects()
    {
        for (int i = 0; i < _nodesProp.arraySize; i++)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(i);
            string id = nodeProp.FindPropertyRelative("id").stringValue;
            Vector2 pos = nodeProp.FindPropertyRelative("editorPosition").vector2Value;

            var r = new Rect(pos.x, pos.y, NodeWidth, GetNodeHeight(nodeProp));
            _nodeRects[id] = r;
        }
    }

    private void DrawAllConnections()
    {
        Handles.BeginGUI();

        for (int i = 0; i < _nodesProp.arraySize; i++)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(i);
            var type = GetNodeType(nodeProp);
            string srcId = nodeProp.FindPropertyRelative("id").stringValue;

            void Draw(string portKey, string dstId, string label)
                => DrawConnectionIfValid(srcId, portKey, dstId, label);

            switch (type)
            {
                case DialogueNodeType.Line:
                    Draw("Next", nodeProp.FindPropertyRelative("nextNodeId").stringValue, "Next");
                    break;

                case DialogueNodeType.Command:
                    Draw("Next", nodeProp.FindPropertyRelative("nextNodeId").stringValue, "Next");
                    break;

                case DialogueNodeType.Branch:
                    Draw("True", nodeProp.FindPropertyRelative("trueNextNodeId").stringValue, "True");
                    Draw("False", nodeProp.FindPropertyRelative("falseNextNodeId").stringValue, "False");
                    break;

                case DialogueNodeType.Choice:
                    {
                        var choicesProp = nodeProp.FindPropertyRelative("choices");
                        for (int c = 0; c < choicesProp.arraySize; c++)
                        {
                            var ch = choicesProp.GetArrayElementAtIndex(c);
                            string dst = ch.FindPropertyRelative("nextNodeId").stringValue;
                            string txt = ch.FindPropertyRelative("text").stringValue;
                            if (string.IsNullOrEmpty(txt)) txt = $"Choice {c}";
                            Draw($"Choice:{c}", dst, txt);
                        }
                        break;
                    }
            }
        }

        // Pending preview
        if (_pending.IsActive)
        {
            if (TryGetPortWorld(_pending.SourceNodeId, _pending.PortKey, out var a))
            {
                var b = _lastCanvasMouse; // canvas coords
                DrawBezier(a, b, Color.yellow);
            }
        }

        Handles.EndGUI();
    }

    private void DrawConnectionIfValid(string srcId, string portKey, string dstId, string label)
    {
        if (string.IsNullOrEmpty(dstId)) return;
        if (!_nodeRects.ContainsKey(dstId)) return;

        if (!TryGetPortWorld(srcId, portKey, out var a)) return;
        if (!TryGetPortWorld(dstId, "IN", out var b)) return;

        DrawBezier(a, b, Color.white);
    }


    private bool TryGetPortWorld(string nodeId, string portKey, out Vector2 worldPos)
    {
        worldPos = default;
        if (!_nodeRects.TryGetValue(nodeId, out var nodeRect)) return false;
        if (!_portCentersLocal.TryGetValue((nodeId, portKey), out var localCenter)) return false;

        worldPos = new Vector2(nodeRect.x + localCenter.x, nodeRect.y + localCenter.y);
        return true;
    }

    private static void DrawBezier(Vector2 a, Vector2 b, Color col)
    {
        var ta = a + Vector2.right * 90f;  // output pushes right
        var tb = b + Vector2.left * 90f;   // input pulls from left
        Handles.DrawBezier(a, b, ta, tb, col, null, 3f);
    }

    // ---------------- Create / Delete / Connect ----------------

    private void CreateNode(Type nodeType)
    {
        CreateNodeInternal(nodeType, _lastCanvasMouse, autoConnectFromSelected: true);
    }

    private string CreateNodeInternal(Type nodeType, Vector2 canvasPos, bool autoConnectFromSelected)
    {
        if (_graph == null) return null;
        EnsureSerialized();

        // IMPORTANT: do NOT use InsertArrayElementAtIndex for SerializeReference nodes.
        // It clones the previous element and can resurrect deleted nodes.
        int idx = _nodesProp.arraySize;
        _nodesProp.arraySize++;
        var newElem = _nodesProp.GetArrayElementAtIndex(idx);

        // Hard reset slot
        newElem.managedReferenceValue = null;
        ApplyModified();

        // Re-fetch after apply
        EnsureSerialized();
        newElem = _nodesProp.GetArrayElementAtIndex(idx);

        string guid = Guid.NewGuid().ToString("N");
        object instance;

        if (nodeType == typeof(LineNode)) instance = new LineNode(guid);
        else if (nodeType == typeof(ChoiceNode)) instance = new ChoiceNode(guid);
        else if (nodeType == typeof(BranchNode)) instance = new BranchNode(guid);
        else if (nodeType == typeof(CommandNode)) instance = new CommandNode(guid);
        else if (nodeType == typeof(EndNode)) instance = new EndNode(guid);
        else throw new ArgumentOutOfRangeException(nameof(nodeType));

        newElem.managedReferenceValue = instance;
        ApplyModified();

        // Position at canvas cursor
        EnsureSerialized();
        var elem = _nodesProp.GetArrayElementAtIndex(idx);
        elem.FindPropertyRelative("editorPosition").vector2Value = canvasPos;
        ApplyModified();

        // Auto-start if empty
        if (string.IsNullOrEmpty(_startNodeIdProp.stringValue))
        {
            _startNodeIdProp.stringValue = guid;
            ApplyModified();
        }

        // Auto-connect if requested
        if (autoConnectFromSelected && HasSelection)
        {
            int fromIndex = GetSelectedIndex();
            if (fromIndex >= 0 && fromIndex < _nodesProp.arraySize)
            {
                var fromProp = _nodesProp.GetArrayElementAtIndex(fromIndex);
                string fromId = fromProp.FindPropertyRelative("id").stringValue;

                if (TryGetFirstDanglingOutputPort(fromProp, out string portKey))
                {
                    _pending = new PendingConnection(fromId, portKey);
                    CompleteConnection(guid);
                }
            }
        }

        _selectedNodeId = guid;
        _pending = default;
        Repaint();

        return guid;
    }

    private bool TryGetFirstDanglingOutputPort(SerializedProperty nodeProp, out string portKey)
    {
        portKey = null;
        var type = GetNodeType(nodeProp);

        switch (type)
        {
            case DialogueNodeType.Line:
                {
                    var next = nodeProp.FindPropertyRelative("nextNodeId");
                    if (string.IsNullOrEmpty(next.stringValue)) { portKey = "Next"; return true; }
                    break;
                }
            case DialogueNodeType.Command:
                {
                    var next = nodeProp.FindPropertyRelative("nextNodeId");
                    if (string.IsNullOrEmpty(next.stringValue)) { portKey = "Next"; return true; }
                    break;
                }
            case DialogueNodeType.Branch:
                {
                    var t = nodeProp.FindPropertyRelative("trueNextNodeId");
                    var f = nodeProp.FindPropertyRelative("falseNextNodeId");
                    if (string.IsNullOrEmpty(t.stringValue)) { portKey = "True"; return true; }
                    if (string.IsNullOrEmpty(f.stringValue)) { portKey = "False"; return true; }
                    break;
                }
            case DialogueNodeType.Choice:
                {
                    var choices = nodeProp.FindPropertyRelative("choices");
                    for (int i = 0; i < choices.arraySize; i++)
                    {
                        var ch = choices.GetArrayElementAtIndex(i);
                        var next = ch.FindPropertyRelative("nextNodeId");
                        if (string.IsNullOrEmpty(next.stringValue))
                        {
                            portKey = $"Choice:{i}";
                            return true;
                        }
                    }
                    // If no dangling choice outputs, create a new one and use it
                    AddChoice(nodeProp);
                    EnsureSerialized();
                    choices = nodeProp.FindPropertyRelative("choices");
                    portKey = $"Choice:{choices.arraySize - 1}";
                    return true;
                }
        }
        return false;
    }

private void DeleteSelectedNode()
{
    int sel = GetSelectedIndex();
    if (sel < 0 || sel >= _nodesProp.arraySize) return;

    EnsureSerialized();

    var element = _nodesProp.GetArrayElementAtIndex(sel);
    string id = element.FindPropertyRelative("id").stringValue;

    RemoveAllLinksTo(id);

    // ManagedReference safe delete: null it first, apply, then delete
    element.managedReferenceValue = null;
    ApplyModified();

    // Now remove the slot
    _nodesProp.DeleteArrayElementAtIndex(sel);
    ApplyModified();

    if (_startNodeIdProp.stringValue == id)
    {
        _startNodeIdProp.stringValue = "";
        ApplyModified();
    }

    _selectedNodeId = null;
    _pending = default;
    Repaint();
}

private void CompleteConnection(string targetNodeId)
    {
        if (!_pending.IsActive) return;
        if (string.IsNullOrEmpty(targetNodeId)) return;
        if (targetNodeId == _pending.SourceNodeId) return;

        EnsureSerialized();

        int srcIndex = FindNodeIndexById(_pending.SourceNodeId);
        if (srcIndex < 0) { _pending = default; return; }

        var srcProp = _nodesProp.GetArrayElementAtIndex(srcIndex);
        var type = GetNodeType(srcProp);

        if (type == DialogueNodeType.Line || type == DialogueNodeType.Command)
        {
            if (_pending.PortKey == "Next")
                srcProp.FindPropertyRelative("nextNodeId").stringValue = targetNodeId;
        }
        else if (type == DialogueNodeType.Branch)
        {
            if (_pending.PortKey == "True")
                srcProp.FindPropertyRelative("trueNextNodeId").stringValue = targetNodeId;
            else if (_pending.PortKey == "False")
                srcProp.FindPropertyRelative("falseNextNodeId").stringValue = targetNodeId;
        }
        else if (type == DialogueNodeType.Choice)
        {
            if (_pending.PortKey.StartsWith("Choice:", StringComparison.Ordinal))
            {
                if (int.TryParse(_pending.PortKey.Substring("Choice:".Length), out int cIndex))
                {
                    var choicesProp = srcProp.FindPropertyRelative("choices");
                    if (cIndex >= 0 && cIndex < choicesProp.arraySize)
                    {
                        var ch = choicesProp.GetArrayElementAtIndex(cIndex);
                        ch.FindPropertyRelative("nextNodeId").stringValue = targetNodeId;
                    }
                }
            }
        }
        ApplyModified();
        _pending = default;
        Repaint();
    }

    private void CreateEndAndConnect(SerializedProperty sourceNodeProp, string portKey)
    {
        string srcId = sourceNodeProp.FindPropertyRelative("id").stringValue;

        // place end node near source node
        Vector2 srcPos = sourceNodeProp.FindPropertyRelative("editorPosition").vector2Value;
        Vector2 endPos = srcPos + new Vector2(NodeWidth + 80f, 20f);

        // Create end — use safe SerializeReference pattern: grow, null, apply, assign, apply.
        EnsureSerialized();
        int idx = _nodesProp.arraySize;
        _nodesProp.arraySize++;
        _nodesProp.GetArrayElementAtIndex(idx).managedReferenceValue = null;
        ApplyModified();

        EnsureSerialized();
        string guid = Guid.NewGuid().ToString("N");
        var newElem = _nodesProp.GetArrayElementAtIndex(idx);
        newElem.managedReferenceValue = new EndNode(guid);
        ApplyModified();

        EnsureSerialized();
        newElem = _nodesProp.GetArrayElementAtIndex(idx);
        newElem.FindPropertyRelative("editorPosition").vector2Value = endPos;
        ApplyModified();

        // connect
        _pending = new PendingConnection(srcId, portKey);
        CompleteConnection(guid);

        _selectedNodeId = guid;
    }

    private void AddChoice(SerializedProperty choiceNodeProp)
    {
        var choicesProp = choiceNodeProp.FindPropertyRelative("choices");
        int idx = choicesProp.arraySize;
        choicesProp.InsertArrayElementAtIndex(idx);

        var elem = choicesProp.GetArrayElementAtIndex(idx);
        elem.FindPropertyRelative("text").stringValue = "";
        elem.FindPropertyRelative("nextNodeId").stringValue = "";

        ApplyModified();
    }

    private void DuplicateChoice(SerializedProperty choicesProp, int index)
    {
        choicesProp.InsertArrayElementAtIndex(index + 1);
        // InsertArrayElementAtIndex clones the element — clear the connection so the
        // duplicate doesn't silently share the same target as the original.
        var duplicated = choicesProp.GetArrayElementAtIndex(index + 1);
        duplicated.FindPropertyRelative("nextNodeId").stringValue = "";
    }

    private void AutoEndDanglingChoiceLinks(SerializedProperty choiceNodeProp)
    {
        var choicesProp = choiceNodeProp.FindPropertyRelative("choices");
        for (int i = 0; i < choicesProp.arraySize; i++)
        {
            var ch = choicesProp.GetArrayElementAtIndex(i);
            var next = ch.FindPropertyRelative("nextNodeId");
            if (string.IsNullOrEmpty(next.stringValue))
            {
                CreateEndAndConnect(choiceNodeProp, $"Choice:{i}");
                // refresh props (list changed)
                EnsureSerialized();
                choiceNodeProp = FindNodePropById(choiceNodeProp.FindPropertyRelative("id").stringValue);
                if (choiceNodeProp == null) break;
                choicesProp = choiceNodeProp.FindPropertyRelative("choices");
            }
        }
    }

    private SerializedProperty FindNodePropById(string id)
    {
        int idx = FindNodeIndexById(id);
        if (idx < 0) return null;
        return _nodesProp.GetArrayElementAtIndex(idx);
    }

    private void RemoveAllLinksTo(string targetId)
    {
        for (int i = 0; i < _nodesProp.arraySize; i++)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(i);
            var type = GetNodeType(nodeProp);

            switch (type)
            {
                case DialogueNodeType.Line:
                case DialogueNodeType.Command:
                    {
                        var next = nodeProp.FindPropertyRelative("nextNodeId");
                        if (next.stringValue == targetId) next.stringValue = "";
                        break;
                    }
                case DialogueNodeType.Branch:
                    {
                        var t = nodeProp.FindPropertyRelative("trueNextNodeId");
                        var f = nodeProp.FindPropertyRelative("falseNextNodeId");
                        if (t.stringValue == targetId) t.stringValue = "";
                        if (f.stringValue == targetId) f.stringValue = "";
                        break;
                    }
                case DialogueNodeType.Choice:
                    {
                        var choices = nodeProp.FindPropertyRelative("choices");
                        for (int c = 0; c < choices.arraySize; c++)
                        {
                            var ch = choices.GetArrayElementAtIndex(c);
                            var next = ch.FindPropertyRelative("nextNodeId");
                            if (next.stringValue == targetId) next.stringValue = "";
                        }
                        break;
                    }
            }
        }
        ApplyModified();
    }

    // ---------------- Validation ----------------
    private void ValidateGraph()
    {
        EnsureSerialized();

        var errors = new List<string>();

        if (string.IsNullOrEmpty(_startNodeIdProp.stringValue))
            errors.Add("StartNodeId is empty.");
        else if (FindNodeIndexById(_startNodeIdProp.stringValue) < 0)
            errors.Add($"StartNodeId points to missing node '{_startNodeIdProp.stringValue}'.");

        for (int i = 0; i < _nodesProp.arraySize; i++)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(i);
            string id = nodeProp.FindPropertyRelative("id").stringValue;
            var type = GetNodeType(nodeProp);

            void CheckLink(string label, string linkId)
            {
                if (string.IsNullOrEmpty(linkId))
                    errors.Add($"[{type}] Node '{ShortId(id)}' has empty link: {label}");
                else if (FindNodeIndexById(linkId) < 0)
                    errors.Add($"[{type}] Node '{ShortId(id)}' has broken link: {label} -> '{ShortId(linkId)}'");
            }

            switch (type)
            {
                case DialogueNodeType.Line:
                    CheckLink("Next", nodeProp.FindPropertyRelative("nextNodeId").stringValue);
                    break;

                case DialogueNodeType.Command:
                    CheckLink("Next", nodeProp.FindPropertyRelative("nextNodeId").stringValue);
                    break;

                case DialogueNodeType.Branch:
                    CheckLink("TrueNext", nodeProp.FindPropertyRelative("trueNextNodeId").stringValue);
                    CheckLink("FalseNext", nodeProp.FindPropertyRelative("falseNextNodeId").stringValue);
                    break;

                case DialogueNodeType.Choice:
                    {
                        var choices = nodeProp.FindPropertyRelative("choices");
                        if (choices.arraySize == 0)
                            errors.Add($"[Choice] Node '{ShortId(id)}' has no choices.");

                        for (int c = 0; c < choices.arraySize; c++)
                        {
                            var ch = choices.GetArrayElementAtIndex(c);
                            string txt = ch.FindPropertyRelative("text").stringValue;
                            string next = ch.FindPropertyRelative("nextNodeId").stringValue;

                            if (string.IsNullOrEmpty(txt))
                                errors.Add($"[Choice] Node '{ShortId(id)}' choice #{c} text is empty.");

                            CheckLink($"Choice#{c} Next", next);
                        }
                        break;
                    }
            }
        }

        if (errors.Count == 0)
        {
            EditorUtility.DisplayDialog("Dialogue Validation", "No issues found ✅", "OK");
        }
        else
        {
            string detail = string.Join("\n• ", errors);
            EditorUtility.DisplayDialog("Dialogue Validation",
                $"Found {errors.Count} issue(s):\n• {detail}", "OK");
        }
    }

    // ---------------- Helpers ----------------
    private void CreateNewGraph()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Dialogue Graph", "DialogueGraph_New", "asset",
            "Choose where to save the dialogue graph", "Assets/ScriptableObjects");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<DialogueGraph>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        SetGraph(asset);
        EditorGUIUtility.PingObject(asset);
    }

    private void SetGraph(DialogueGraph graph)
    {
        _graph = graph;
        _selectedNodeId = null;
        _pending = default;

        if (_graph != null)
        {
            _graphSO = new SerializedObject(_graph);
            _nodesProp = _graphSO.FindProperty("nodes");
            _startNodeIdProp = _graphSO.FindProperty("startNodeId");
        }
        else
        {
            _graphSO = null;
            _nodesProp = null;
            _startNodeIdProp = null;
        }

        Repaint();
    }

    private void EnsureSerialized()
    {
        if (_graph == null) return;

        if (_graphSO == null || _graphSO.targetObject != _graph)
        {
            _graphSO = new SerializedObject(_graph);
            _nodesProp = _graphSO.FindProperty("nodes");
            _startNodeIdProp = _graphSO.FindProperty("startNodeId");
        }

        _graphSO.Update();
    }

    private void ApplyModified()
    {
        if (_graphSO == null) return;
        _graphSO.ApplyModifiedProperties();
        _graphSO.Update(); // keep properties in sync immediately
        EditorUtility.SetDirty(_graph);
    }

    private int FindNodeIndexById(string id)
    {
        if (string.IsNullOrEmpty(id)) return -1;
        for (int i = 0; i < _nodesProp.arraySize; i++)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(i);
            if (nodeProp.FindPropertyRelative("id").stringValue == id)
                return i;
        }
        return -1;
    }

    private static DialogueNodeType GetNodeType(SerializedProperty nodeProp)
    {
        string t = nodeProp.managedReferenceFullTypename;

        if (t.Contains("LineNode")) return DialogueNodeType.Line;
        if (t.Contains("ChoiceNode")) return DialogueNodeType.Choice;
        if (t.Contains("BranchNode")) return DialogueNodeType.Branch;
        if (t.Contains("CommandNode")) return DialogueNodeType.Command;
        if (t.Contains("EndNode")) return DialogueNodeType.End;

        return DialogueNodeType.Line;
    }

    private static string GetNodeTitle(SerializedProperty nodeProp)
    {
        var type = GetNodeType(nodeProp);
        var id = nodeProp.FindPropertyRelative("id").stringValue;
        return $"{type}  ({ShortId(id)})";
    }

    private static string ShortId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "----";
        return id.Length <= 6 ? id : id.Substring(0, 6);
    }

    private float GetNodeHeight(SerializedProperty nodeProp)
    {
        var type = GetNodeType(nodeProp);

        // Base padding + header
        float h = 28f; // title bar-ish space

        switch (type)
        {
            case DialogueNodeType.Choice:
                {
                    string nodeId = nodeProp.FindPropertyRelative("id").stringValue;
                    var choicesProp = nodeProp.FindPropertyRelative("choices");
                    int count = choicesProp != null ? choicesProp.arraySize : 0;

                    int maxShow = Mathf.Min(count, 6);
                    float row = 28f;
                    float smallGap = 4f;

                    h += row + smallGap;               // "Choices: X" label

                    for (int i = 0; i < maxShow; i++)
                    {
                        h += row + 2f;                 // choice row

                        var ch = choicesProp.GetArrayElementAtIndex(i);

                        // If dangling link -> extra "Create End →" row
                        var nextProp = ch.FindPropertyRelative("nextNodeId");
                        if (nextProp != null && string.IsNullOrEmpty(nextProp.stringValue))
                            h += row + 2f;

                        // Per-choice condition summary row (+ expanded editor)
                        h += GetChoiceConditionsHeight(ch, nodeId, i);
                    }

                    if (count > maxShow)
                        h += row;                      // "... edit full list" label

                    h += smallGap;
                    h += row + 6f;                     // bottom buttons row

                    return Mathf.Max(140f, h);         // ensure minimum
                }
            case DialogueNodeType.Line:
                return 180f; // your existing values are fine
            case DialogueNodeType.Branch:
                return 150f;
            case DialogueNodeType.Command:
                return 150f;
            case DialogueNodeType.End:
                return 90f;
            default:
                return 140f;
        }
    }

    private float GetChoiceConditionsHeight(SerializedProperty choiceProp, string nodeId, int choiceIndex)
    {
        // Always-visible summary + toggle row.
        float h = 20f;

        if (_expandedChoices.Contains(ChoiceCondKey(nodeId, choiceIndex)))
        {
            h += 8f; // helpBox padding

            var allProp = choiceProp.FindPropertyRelative("conditions").FindPropertyRelative("all");
            int n = allProp != null ? allProp.arraySize : 0;

            for (int i = 0; i < n; i++)
            {
                h += 22f + 20f + 3f; // type/remove row + value field + spacing

                var type = (DialogueConditionType)allProp.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("type").intValue;
                if (type == DialogueConditionType.RelationshipAtLeast)
                    h += 20f; // extra npcId field
            }

            h += 22f; // "＋ Condition" button
        }

        return h;
    }

    private float GetNodeWidth(SerializedProperty nodeProp)
    {
        switch (GetNodeType(nodeProp))
        {
            case DialogueNodeType.End:
                return 90f; // narrow
            default:
                return NodeWidth;
        }
    }


    private struct PendingConnection
    {
        public readonly string SourceNodeId;
        public readonly string PortKey;

        public bool IsActive => !string.IsNullOrEmpty(SourceNodeId) && !string.IsNullOrEmpty(PortKey);

        public PendingConnection(string sourceNodeId, string portKey)
        {
            SourceNodeId = sourceNodeId;
            PortKey = portKey;
        }
    }

    private string[] GetSpeakerOptions()
    {
        // Refresh at most once per second
        if (EditorApplication.timeSinceStartup < _speakerOptionsNextRefreshTime &&
            _speakerOptionsCache != null &&
            _speakerOptionsCache.Length > 0)
        {
            return _speakerOptionsCache;
        }

        _speakerOptionsNextRefreshTime = EditorApplication.timeSinceStartup + SpeakerRefreshInterval;

        var list = new List<string> { "Player" };

        // Prefer live NpcManager (play mode); fall back to asset scan in edit mode.
        var mgr = UnityEngine.Object.FindFirstObjectByType<NpcManager>();
        if (mgr != null)
        {
            var ids = mgr.GetAllSpeakerIds();
            for (int i = 0; i < ids.Count; i++)
            {
                var s = ids[i];
                if (!string.IsNullOrWhiteSpace(s) && !list.Contains(s))
                    list.Add(s);
            }
        }

        if (list.Count == 1) // only "Player" — NpcManager not available, scan assets
        {
            foreach (var guid in AssetDatabase.FindAssets("t:NpcDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && !string.IsNullOrWhiteSpace(def.NpcId) && !list.Contains(def.NpcId))
                    list.Add(def.NpcId);
            }
        }

        list.Add("<Custom…>");
        _speakerOptionsCache = list.ToArray();
        return _speakerOptionsCache;
    }

    private void FrameSelectedNode(Rect canvasRect)
    {
        if (string.IsNullOrEmpty(_selectedNodeId))
            return;

        if (!_nodeRects.TryGetValue(_selectedNodeId, out var rect))
            return;

        float visibleW = canvasRect.width;
        float visibleH = canvasRect.height;

        var center = rect.center;

        _canvasScroll = new Vector2(
            Mathf.Max(0f, center.x - visibleW * 0.5f),
            Mathf.Max(0f, center.y - visibleH * 0.5f)
        );

        Repaint();
    }
    private bool IsMouseOverAnyNode(Vector2 canvasMouse)
    {
        foreach (var kv in _nodeRects)
        {
            if (kv.Value.Contains(canvasMouse))
                return true;
        }
        return false;
    }
    private void ShowCreateAndConnectMenu(Vector2 canvasMouse)
    {
        var pending = _pending;

        var menu = new GenericMenu();

        void Add(string label, Type nodeType)
        {
            menu.AddItem(new GUIContent($"Create/{label}"), false, () =>
            {
                // Create without auto-connect-from-selected
                string newId = CreateNodeInternal(nodeType, canvasMouse, autoConnectFromSelected: false);

                // Restore pending (CreateNodeInternal may clear it), then connect to the new node.
                _pending = pending;
                CompleteConnection(newId);

                _pending = default;
                Repaint();
            });
        }

        Add("Line", typeof(LineNode));
        Add("Choice", typeof(ChoiceNode));
        Add("Branch", typeof(BranchNode));
        Add("Command", typeof(CommandNode));
        Add("End", typeof(EndNode));

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Cancel"), false, () =>
        {
            _pending = default;
            Repaint();
        });

        menu.ShowAsContext();
    }
}
#endif
