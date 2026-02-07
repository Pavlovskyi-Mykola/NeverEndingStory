#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Put this file under an "Editor" folder.
// Requires your existing DialogueGraph, DialogueNode + derived nodes.

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

    private Vector2 _canvasScroll;
    private Vector2 _inspectorScroll;

    private string _selectedNodeId = null;

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

    // Context menu (right-click)
    private bool _suppressRightClickCancelOnce;
    private Vector2 _lastCanvasMouse; // in canvas coords (scroll included)

    [MenuItem("Game/Dialogue/Dialogue Editor")]
    public static void Open()
    {
        var w = GetWindow<DialogueGraphEditorWindow>();
        w.titleContent = new GUIContent("Dialogue Editor");
        w.Show();
    }

    private void OnEnable()
    {
        wantsMouseMove = true;
    }

    private void OnGUI()
    {
        DrawTopToolbar();

        // Keyboard delete for selected node (Backspace/Delete)
        if (Event.current.type == EventType.KeyDown && HasSelection)
        {
            if (Event.current.keyCode == KeyCode.Backspace || Event.current.keyCode == KeyCode.Delete)
            {
                DeleteSelectedNode();
                Event.current.Use();
            }
        }

        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawCanvas();
        EditorGUILayout.EndHorizontal();

        if (Event.current.type == EventType.MouseMove)
            Repaint();
    }

    private void DrawTopToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var newGraph = (DialogueGraph)EditorGUILayout.ObjectField(
                _graph, typeof(DialogueGraph), false, GUILayout.Width(360));

            if (newGraph != _graph)
                SetGraph(newGraph);

            GUILayout.FlexibleSpace();

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

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Start Node", GUILayout.Width(80));
                EditorGUILayout.PropertyField(_startNodeIdProp, GUIContent.none);
            }

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

        // Track mouse in canvas coordinates
        _lastCanvasMouse = Event.current.mousePosition + _canvasScroll;

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

        HandleCanvasRightClickContextMenu(canvasRect);

        _nodeRects.Clear();
        _portCentersLocal.Clear();

        CacheNodeRects();
        DrawAllConnections();

        BeginWindows();
        for (int i = 0; i < _nodesProp.arraySize; i++)
        {
            var nodeProp = _nodesProp.GetArrayElementAtIndex(i);
            var idProp = nodeProp.FindPropertyRelative("id");
            var posProp = nodeProp.FindPropertyRelative("editorPosition");

            string id = idProp.stringValue;
            Vector2 pos = posProp.vector2Value;

            var rect = new Rect(pos.x, pos.y, NodeWidth, GetNodeHeight(nodeProp));
            rect = GUI.Window(i, rect, _ => DrawNodeWindow(i, nodeProp), GetNodeTitle(nodeProp));

            if (rect.position != pos)
            {
                posProp.vector2Value = rect.position;
                ApplyModified();
            }

            // Keep rect cache fresh after window drag
            _nodeRects[id] = rect;
        }
        EndWindows();

        // Cancel pending connect by right click on canvas (but not when context menu is shown)
        if (!_suppressRightClickCancelOnce &&
            Event.current.type == EventType.MouseDown &&
            Event.current.button == 1)
        {
            _pending = default;
            Repaint();
        }
        _suppressRightClickCancelOnce = false;

        GUI.EndScrollView();
    }

    private void HandleCanvasRightClickContextMenu(Rect canvasRect)
    {
        // If right-click occurs on empty canvas area (not inside any node rect),
        // show context menu with "Add Node" at cursor.
        if (Event.current.type == EventType.MouseDown && Event.current.button == 1)
        {
            // Determine if click is on any node
            bool onNode = false;
            foreach (var kv in _nodeRects)
            {
                if (kv.Value.Contains(_lastCanvasMouse))
                {
                    onNode = true;
                    break;
                }
            }

            if (!onNode)
            {
                _suppressRightClickCancelOnce = true;

                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add/Line"), false, () => CreateNodeAtCursor(typeof(LineNode)));
                menu.AddItem(new GUIContent("Add/Choice"), false, () => CreateNodeAtCursor(typeof(ChoiceNode)));
                menu.AddItem(new GUIContent("Add/Branch"), false, () => CreateNodeAtCursor(typeof(BranchNode)));
                menu.AddItem(new GUIContent("Add/Command"), false, () => CreateNodeAtCursor(typeof(CommandNode)));
                menu.AddItem(new GUIContent("Add/End"), false, () => CreateNodeAtCursor(typeof(EndNode)));

                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Cancel Connection"), false, () => _pending = default);

                menu.ShowAsContext();
                Event.current.Use();
            }
        }
    }

    private void DrawNodeWindow(int index, SerializedProperty nodeProp)
    {
        var e = Event.current;

        // Select on click
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            _selectedNodeId = nodeProp.FindPropertyRelative("id").stringValue;
            GUI.FocusControl(null);
            Event.current.Use();   // prevents canvas click from immediately deselecting
            Repaint();
        }

        // Top row: input port (right), start button (left)
        string nodeId = nodeProp.FindPropertyRelative("id").stringValue;
        bool isStart = _startNodeIdProp != null && _startNodeIdProp.stringValue == nodeId;

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("★", "Set this node as Start"), GUILayout.Width(22), GUILayout.Height(18)))
            {
                _startNodeIdProp.stringValue = nodeId;
                ApplyModified();
            }

            GUILayout.FlexibleSpace();
            DrawInputPort(nodeProp);
        }

        // Visual indicator for the Start node
        if (isStart)
        {
            var r = GUILayoutUtility.GetRect(NodeWidth - 10f, 18f);
            r.x += 5f;
            r.width = NodeWidth - 10f;
            EditorGUI.DrawRect(r, new Color(1f, 0.85f, 0.2f, 0.30f));
            GUI.Label(r, "START", EditorStyles.miniBoldLabel);
        }

        GUILayout.Space(2);

        var nodeType = GetNodeType(nodeProp);

        switch (nodeType)
        {
            case DialogueNodeType.Line:
                DrawLineNodeInline(nodeProp);
                break;

            case DialogueNodeType.Choice:
                DrawChoiceNodeInline(nodeProp);
                break;

            case DialogueNodeType.Branch:
                DrawBranchNodeInline(nodeProp);
                break;

            case DialogueNodeType.Command:
                DrawCommandNodeInline(nodeProp);
                break;

            case DialogueNodeType.End:
                GUILayout.Label("(End)", EditorStyles.miniLabel);
                break;
        }

        GUI.DragWindow(new Rect(0, 0, NodeWidth, NodeHeaderHeight));
    }

    // ---------------- Inline node views + ports ----------------

    private void DrawLineNodeInline(SerializedProperty nodeProp)
    {
        var speakerProp = nodeProp.FindPropertyRelative("speaker");
        var textProp = nodeProp.FindPropertyRelative("text");
        var nextProp = nodeProp.FindPropertyRelative("nextNodeId");

        // Inline edit speaker
        EditorGUI.BeginChangeCheck();
        string speaker = EditorGUILayout.TextField("Speaker", speakerProp.stringValue);
        if (EditorGUI.EndChangeCheck())
        {
            speakerProp.stringValue = speaker;
            ApplyModified();
        }

        // Inline edit first lines of text
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
            // Create End Node on dangling output
            CreateEndAndConnect(nodeProp, "Next");
        });
    }

    private void DrawChoiceNodeInline(SerializedProperty nodeProp)
    {
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

    private void DrawBranchNodeInline(SerializedProperty nodeProp)
    {
        var tProp = nodeProp.FindPropertyRelative("trueNextNodeId");
        var fProp = nodeProp.FindPropertyRelative("falseNextNodeId");

        GUILayout.Label("Branch", EditorStyles.miniBoldLabel);
        GUILayout.Label("(condition in inspector)", EditorStyles.miniLabel);

        GUILayout.Space(6);

        DrawOutputRow(nodeProp, "True", "True", tProp.stringValue, () =>
        {
            CreateEndAndConnect(nodeProp, "True");
        });

        DrawOutputRow(nodeProp, "False", "False", fProp.stringValue, () =>
        {
            CreateEndAndConnect(nodeProp, "False");
        });
    }

    private void DrawCommandNodeInline(SerializedProperty nodeProp)
    {
        var cmdsProp = nodeProp.FindPropertyRelative("commands");
        var nextProp = nodeProp.FindPropertyRelative("nextNodeId");

        GUILayout.Label($"Commands: {cmdsProp.arraySize}", EditorStyles.miniBoldLabel);
        GUILayout.Label("(edit commands in inspector)", EditorStyles.miniLabel);

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

        var idProp = nodeProp.FindPropertyRelative("id");
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(idProp, new GUIContent("Id"));
        }

        EditorGUILayout.Space(6);

        switch (type)
        {
            case DialogueNodeType.Line:
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("speaker"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("text"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("onEnterCommands"), true);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("nextNodeId"));
                break;

            case DialogueNodeType.Choice:
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("choices"), true);
                break;

            case DialogueNodeType.Branch:
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("condition"), true);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("trueNextNodeId"));
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("falseNextNodeId"));
                break;

            case DialogueNodeType.Command:
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("commands"), true);
                EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("nextNodeId"));
                break;

            case DialogueNodeType.End:
                EditorGUILayout.HelpBox("End node has no editable fields.", MessageType.None);
                break;
        }

        if (GUI.changed)
            ApplyModified();

        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Set As Start"))
            {
                _startNodeIdProp.stringValue = idProp.stringValue;
                ApplyModified();
            }
        }
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

            switch (type)
            {
                case DialogueNodeType.Line:
                    {
                        string dst = nodeProp.FindPropertyRelative("nextNodeId").stringValue;
                        DrawConnectionIfValid(srcId, "Next", dst, "Next");
                        break;
                    }
                case DialogueNodeType.Command:
                    {
                        string dst = nodeProp.FindPropertyRelative("nextNodeId").stringValue;
                        DrawConnectionIfValid(srcId, "Next", dst, "Next");
                        break;
                    }
                case DialogueNodeType.Branch:
                    {
                        string t = nodeProp.FindPropertyRelative("trueNextNodeId").stringValue;
                        string f = nodeProp.FindPropertyRelative("falseNextNodeId").stringValue;
                        DrawConnectionIfValid(srcId, "True", t, "True");
                        DrawConnectionIfValid(srcId, "False", f, "False");
                        break;
                    }
                case DialogueNodeType.Choice:
                    {
                        var choicesProp = nodeProp.FindPropertyRelative("choices");
                        for (int c = 0; c < choicesProp.arraySize; c++)
                        {
                            var ch = choicesProp.GetArrayElementAtIndex(c);
                            string dst = ch.FindPropertyRelative("nextNodeId").stringValue;
                            string choiceText = ch.FindPropertyRelative("text").stringValue;
                            if (string.IsNullOrWhiteSpace(choiceText))
                                choiceText = $"Choice {c}";
                            DrawConnectionIfValid(srcId, $"Choice:{c}", dst, choiceText);
                        }
                        break;
                    }
            }
        }

        // Pending connection preview
        if (_pending.IsActive)
        {
            if (TryGetPortWorld(_pending.SourceNodeId, _pending.PortKey, out var a))
            {
                var b = _lastCanvasMouse;
                DrawBezier(a, b, Color.yellow);
                Repaint();
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

        // Label near midpoint
        var mid = (a + b) * 0.5f;
        var size = EditorStyles.miniLabel.CalcSize(new GUIContent(label));
        var r = new Rect(mid.x - size.x * 0.5f, mid.y - size.y * 0.5f, size.x + 6, size.y + 2);

        // background
        EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.35f));
        GUI.Label(new Rect(r.x + 3, r.y + 1, r.width, r.height), label, EditorStyles.miniLabel);
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
        var ta = a + Vector2.right * 90f;
        var tb = b + Vector2.left * 90f;
        Handles.DrawBezier(a, b, ta, tb, col, null, 3f);
    }

    // ---------------- Create / Delete / Connect ----------------

    private void CreateNode(Type nodeType)
    {
        CreateNodeInternal(nodeType, _lastCanvasMouse, autoConnectFromSelected: true);
    }

    private void CreateNodeAtCursor(Type nodeType)
    {
        CreateNodeInternal(nodeType, _lastCanvasMouse, autoConnectFromSelected: false);
    }

    private void CreateNodeInternal(Type nodeType, Vector2 canvasPos, bool autoConnectFromSelected)
    {
        if (_graph == null) return;
        EnsureSerialized();

        int idx = _nodesProp.arraySize;
        _nodesProp.InsertArrayElementAtIndex(idx);

        var newElem = _nodesProp.GetArrayElementAtIndex(idx);

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

        // create end
        EnsureSerialized();
        int idx = _nodesProp.arraySize;
        _nodesProp.InsertArrayElementAtIndex(idx);

        var newElem = _nodesProp.GetArrayElementAtIndex(idx);
        string guid = Guid.NewGuid().ToString("N");
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
        var duplicated = choicesProp.GetArrayElementAtIndex(index + 1);
        // Unity duplicates previous element values automatically; nothing else needed.
        // You can optionally tweak text like " (copy)" but keeping it identical is often useful.
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
            Debug.LogWarning($"DialogueGraph '{_graph.name}' validation found {errors.Count} issue(s):\n- " + string.Join("\n- ", errors));
            EditorUtility.DisplayDialog("Dialogue Validation", $"Found {errors.Count} issue(s). Check Console for details.", "OK");
        }
    }

    // ---------------- Helpers ----------------

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
        EditorUtility.SetDirty(_graph);
        AssetDatabase.SaveAssets();
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

    private static float GetNodeHeight(SerializedProperty nodeProp)
    {
        var type = GetNodeType(nodeProp);
        switch (type)
        {
            case DialogueNodeType.Line: return 190f;
            case DialogueNodeType.Command: return 120f;
            case DialogueNodeType.Branch: return 145f;
            case DialogueNodeType.Choice:
                {
                    var choicesProp = nodeProp.FindPropertyRelative("choices");
                    int lines = Mathf.Clamp(choicesProp.arraySize, 1, 6);
                    return 125f + lines * 26f;
                }
            case DialogueNodeType.End: return 70f;
            default: return 140f;
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
}
#endif
