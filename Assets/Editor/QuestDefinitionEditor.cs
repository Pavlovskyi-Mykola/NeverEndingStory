#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(QuestDefinition))]
public class QuestDefinitionEditor : Editor
{
    private SerializedProperty _questId;
    private SerializedProperty _title;
    private SerializedProperty _desc;
    private SerializedProperty _steps;
    private SerializedProperty _completionRewards;

    private ReorderableList _list;

    private void OnEnable()
    {
        _questId = serializedObject.FindProperty("QuestId");
        _title = serializedObject.FindProperty("Title");
        _desc = serializedObject.FindProperty("Description");
        _steps = serializedObject.FindProperty("Steps");
        _completionRewards = serializedObject.FindProperty("CompletionRewards");

        _list = new ReorderableList(serializedObject, _steps, true, true, true, true);
        _list.drawHeaderCallback = r => EditorGUI.LabelField(r, "Quest Steps");

        _list.elementHeightCallback = index =>
        {
            var el = _steps.GetArrayElementAtIndex(index);

            if (!el.isExpanded)
                return EditorGUIUtility.singleLineHeight + 6;

            // Dynamic height: sum only visible fields
            return CalcExpandedHeight(el) + 16;
        };

        _list.drawElementCallback = (rect, index, active, focused) =>
        {
            var el = _steps.GetArrayElementAtIndex(index);
            var typeProp = el.FindPropertyRelative("Type");
            var textProp = el.FindPropertyRelative("Text");

            rect.y += 2;

            // Foldout header
            var headerRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            string label = $"{index + 1}. {GetEnumDisplay(typeProp)}";
            string preview = (textProp?.stringValue ?? "").Replace("\n", " ").Replace("\r", " ");
            if (preview.Length > 42) preview = preview.Substring(0, 39) + "...";
            if (!string.IsNullOrEmpty(preview)) label += $" — {preview}";
            el.isExpanded = EditorGUI.Foldout(headerRect, el.isExpanded, label, true);

            if (!el.isExpanded) return;

            float y = headerRect.yMax + 4;

            // Draw only relevant fields
            y = DrawExpanded(el, new Rect(rect.x, y, rect.width, rect.height - (y - rect.y)));
        };

        _list.onAddDropdownCallback = (buttonRect, l) =>
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Manual"), false, () => AddStep(QuestStepType.Manual));
            menu.AddItem(new GUIContent("Reach Location"), false, () => AddStep(QuestStepType.ReachLocation));
            menu.AddItem(new GUIContent("Min Stats"), false, () => AddStep(QuestStepType.MinStats));
            menu.AddItem(new GUIContent("Require Money"), false, () => AddStep(QuestStepType.HaveMoney));
            menu.AddItem(new GUIContent("Pay Money"), false, () => AddStep(QuestStepType.PayMoney));
            menu.AddItem(new GUIContent("AutoComplete"), false, () => AddStep(QuestStepType.AutoComplete));
            menu.AddItem(new GUIContent("Talk To Dialogue"), false, () => AddStep(QuestStepType.TalkToDialogue));
            menu.AddItem(new GUIContent("Have Item"), false, () => AddStep(QuestStepType.HaveItem));
            menu.DropDown(buttonRect);
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_questId);
        EditorGUILayout.PropertyField(_title);
        EditorGUILayout.PropertyField(_desc);

        EditorGUILayout.Space(8);

        if (string.IsNullOrEmpty(_questId.stringValue))
            EditorGUILayout.HelpBox("QuestId is empty. Set a unique id (e.g., q_bob_intro).", MessageType.Error);

        _list.DoLayoutList();

        EditorGUILayout.Space(10);

        if (_completionRewards != null)
        {
            EditorGUILayout.PropertyField(_completionRewards, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ----------------------
    // Drawing helpers
    // ----------------------

    private static float CalcExpandedHeight(SerializedProperty stepEl)
    {
        float h = 0;

        var stepId = stepEl.FindPropertyRelative("StepId");
        h += PropHeight(stepId);
        h += Spacing();

        h += PropHeight(stepEl.FindPropertyRelative("Text"), includeChildren: true);
        h += Spacing();

        var type = GetStepType(stepEl);

        // Type-specific
        switch (type)
        {
            case QuestStepType.ReachLocation:
                // one popup line + spacing
                h += EditorGUIUtility.singleLineHeight;
                h += Spacing();
                break;

            case QuestStepType.MinStats:
                h += PropHeight(stepEl.FindPropertyRelative("RequiredInfluence"));
                h += Spacing();
                h += PropHeight(stepEl.FindPropertyRelative("RequiredStrategy"));
                h += Spacing();
                h += PropHeight(stepEl.FindPropertyRelative("RequiredNetworking"));
                h += Spacing();
                h += PropHeight(stepEl.FindPropertyRelative("RequiredReputation"));
                h += Spacing();
                break;

            case QuestStepType.HaveMoney:
            case QuestStepType.PayMoney:
                h += PropHeight(stepEl.FindPropertyRelative("RequiredMoney"));
                h += Spacing();
                break;
            case QuestStepType.TalkToDialogue:
                h += EditorGUIUtility.singleLineHeight + Spacing(); // NPC popup
                h += EditorGUIUtility.singleLineHeight + Spacing(); // Dialogue popup
                break;
            case QuestStepType.HaveItem:
                h += PropHeight(stepEl.FindPropertyRelative("RequiredItem"), includeChildren: true);
                h += Spacing();
                break;
        }

        // Time restriction block (always show toggles, show masks only if enabled)
        h += PropHeight(stepEl.FindPropertyRelative("RestrictByDay"));
        h += Spacing();
        if (stepEl.FindPropertyRelative("RestrictByDay").boolValue)
        {
            h += PropHeight(stepEl.FindPropertyRelative("AllowedDays"));
            h += Spacing();
        }

        h += PropHeight(stepEl.FindPropertyRelative("RestrictByPhase"));
        h += Spacing();
        if (stepEl.FindPropertyRelative("RestrictByPhase").boolValue)
        {
            h += PropHeight(stepEl.FindPropertyRelative("AllowedPhases"));
            h += Spacing();
        }

        // Validation helpbox (only if needed)
        var msg = Validate(stepEl);
        if (!string.IsNullOrEmpty(msg))
        {
            h += HelpBoxHeight();
            h += Spacing();
        }

        return h;
    }

    private static float DrawExpanded(SerializedProperty stepEl, Rect rect)
    {
        float innerPadding = 6f;
        rect.x += innerPadding;
        rect.width -= innerPadding * 2;
        float y = rect.y;
        EditorGUI.BeginChangeCheck();

        // StepId + Auto ID button row
        var stepId = stepEl.FindPropertyRelative("StepId");

        float idH = PropHeight(stepId); // dynamic height

        var idRect = new Rect(rect.x, y, rect.width - 70, idH);
        var btnRect = new Rect(rect.x + rect.width - 65, y, 65, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(idRect, stepId);

        // keep the button aligned to top of the row
        if (GUI.Button(btnRect, "Auto ID"))
            stepId.stringValue = $"step_{stepEl.propertyPath.GetHashCode():X8}";

        y += idH + Spacing();

        // Text
        y = DrawProp(stepEl.FindPropertyRelative("Text"), rect.x, y, rect.width, includeChildren: true);

        var type = GetStepType(stepEl);

        // Type-specific fields
        switch (type)
        {
            case QuestStepType.ReachLocation:
                y = DrawReachLocationDropdown(stepEl, rect.x, y, rect.width);
                break;

            case QuestStepType.MinStats:
                y = DrawProp(stepEl.FindPropertyRelative("RequiredInfluence"), rect.x, y, rect.width);
                y = DrawProp(stepEl.FindPropertyRelative("RequiredStrategy"), rect.x, y, rect.width);
                y = DrawProp(stepEl.FindPropertyRelative("RequiredNetworking"), rect.x, y, rect.width);
                y = DrawProp(stepEl.FindPropertyRelative("RequiredReputation"), rect.x, y, rect.width);
                break;

            case QuestStepType.HaveMoney:
            case QuestStepType.PayMoney:
                y = DrawProp(stepEl.FindPropertyRelative("RequiredMoney"), rect.x, y, rect.width);
                break;
            case QuestStepType.TalkToDialogue:
                y = DrawTalkToDialoguePickers(stepEl, rect.x, y, rect.width);
                break;
            case QuestStepType.HaveItem:
                y = DrawProp(stepEl.FindPropertyRelative("RequiredItem"), rect.x, y, rect.width, includeChildren: true);
                break;
        }

        // Time restrictions block
        y = DrawProp(stepEl.FindPropertyRelative("RestrictByDay"), rect.x, y, rect.width);
        if (stepEl.FindPropertyRelative("RestrictByDay").boolValue)
            y = DrawProp(stepEl.FindPropertyRelative("AllowedDays"), rect.x, y, rect.width);

        y = DrawProp(stepEl.FindPropertyRelative("RestrictByPhase"), rect.x, y, rect.width);
        if (stepEl.FindPropertyRelative("RestrictByPhase").boolValue)
            y = DrawProp(stepEl.FindPropertyRelative("AllowedPhases"), rect.x, y, rect.width);

        // Validation helpbox
        var msg = Validate(stepEl);
        if (!string.IsNullOrEmpty(msg))
        {
            var hb = new Rect(rect.x, y, rect.width, HelpBoxHeight());
            EditorGUI.HelpBox(hb, msg, MessageType.Warning);
            y += HelpBoxHeight() + Spacing();
        }

        if (EditorGUI.EndChangeCheck())
        {
            // Update auto text if needed
            RefreshAutoTextForStep(stepEl);
        }
        return y;
    }

    private static float DrawProp(SerializedProperty prop, float x, float y, float width, bool includeChildren = false)
    {
        if (prop == null) return y;

        float h = PropHeight(prop, includeChildren);
        var r = new Rect(x, y, width, h);
        EditorGUI.PropertyField(r, prop, includeChildren);
        return y + h + Spacing();
    }

    private static float PropHeight(SerializedProperty prop, bool includeChildren = false)
    {
        if (prop == null) return 0;
        return EditorGUI.GetPropertyHeight(prop, includeChildren);
    }

    private static float LineHeight() => EditorGUIUtility.singleLineHeight;
    private static float Spacing() => 8f;
    private static float HelpBoxHeight() => 42f;

    private static QuestStepType GetStepType(SerializedProperty stepEl)
    {
        var typeProp = stepEl.FindPropertyRelative("Type");
        if (typeProp == null) return QuestStepType.Manual;

        // IMPORTANT: our enum uses int values like 11,13,15 etc.
        return (QuestStepType)typeProp.intValue;
    }

    private static string GetEnumDisplay(SerializedProperty typeProp)
    {
        if (typeProp == null) return "Unknown";
        // enumValueIndex is safe for display names even if values are sparse
        int idx = Mathf.Clamp(typeProp.enumValueIndex, 0, typeProp.enumDisplayNames.Length - 1);
        return typeProp.enumDisplayNames[idx];
    }

    private static string Validate(SerializedProperty stepEl)
    {
        var type = GetStepType(stepEl);

        if (type == QuestStepType.ReachLocation)
        {
            var p = stepEl.FindPropertyRelative("TargetLocationSceneName");
            if (p == null || p.propertyType != SerializedPropertyType.String)
                return "ReachLocation: TargetLocationSceneName missing or wrong type.";

            if (string.IsNullOrEmpty(p.stringValue))
                return "ReachLocation: Target Location is not set.";
        }

        if (type == QuestStepType.MinStats)
        {
            int influence = stepEl.FindPropertyRelative("RequiredInfluence")?.intValue ?? 0;
            int strategy = stepEl.FindPropertyRelative("RequiredStrategy")?.intValue ?? 0;
            int networking = stepEl.FindPropertyRelative("RequiredNetworking")?.intValue ?? 0;
            int reputation = stepEl.FindPropertyRelative("RequiredReputation")?.intValue ?? 0;

            if (influence <= 0 && strategy <= 0 && networking <= 0 && reputation <= 0)
                return "MinStats: set at least one required corporate stat.";
        }

        if (type == QuestStepType.HaveMoney || type == QuestStepType.PayMoney)
        {
            int m = stepEl.FindPropertyRelative("RequiredMoney")?.intValue ?? 0;
            if (m <= 0)
                return "Money step: RequiredMoney should be > 0.";
        }

        bool rDay = stepEl.FindPropertyRelative("RestrictByDay")?.boolValue ?? false;
        if (rDay)
        {
            int days = stepEl.FindPropertyRelative("AllowedDays")?.intValue ?? 0;
            if (days == 0) return "RestrictByDay is enabled but AllowedDays is empty.";
        }

        bool rPhase = stepEl.FindPropertyRelative("RestrictByPhase")?.boolValue ?? false;
        if (rPhase)
        {
            int phases = stepEl.FindPropertyRelative("AllowedPhases")?.intValue ?? 0;
            if (phases == 0) return "RestrictByPhase is enabled but AllowedPhases is empty.";
        }

        if (type == QuestStepType.TalkToDialogue)
        {
            var npcProp = stepEl.FindPropertyRelative("TargetNpcId");
            var dialogueProp = stepEl.FindPropertyRelative("TargetDialogueId");

            if (npcProp == null || npcProp.propertyType != SerializedPropertyType.String)
                return "TalkToDialogue: TargetNpcId missing or wrong type.";

            if (dialogueProp == null || dialogueProp.propertyType != SerializedPropertyType.String)
                return "TalkToDialogue: TargetDialogueId missing or wrong type.";

            if (string.IsNullOrEmpty(npcProp.stringValue))
                return "TalkToDialogue: Target NPC is not set.";

            if (string.IsNullOrEmpty(dialogueProp.stringValue))
                return "TalkToDialogue: Target Dialogue is not set.";
        }

        if (type == QuestStepType.HaveItem)
        {
            var itemProp = stepEl.FindPropertyRelative("RequiredItem");
            if (itemProp == null)
                return "HaveItem: RequiredItem missing.";

            var itemIdProp = itemProp.FindPropertyRelative("ItemId");
            var countProp = itemProp.FindPropertyRelative("Count");

            if (itemIdProp == null || itemIdProp.propertyType != SerializedPropertyType.String)
                return "HaveItem: RequiredItem.ItemId missing or wrong type.";

            if (string.IsNullOrWhiteSpace(itemIdProp.stringValue))
                return "HaveItem: required item is not set.";

            int count = countProp != null ? countProp.intValue : 0;
            if (count <= 0)
                return "HaveItem: item count should be > 0.";
        }

        return null;
    }

    // ----------------------
    // Add step templates
    // ----------------------

    private void AddStep(QuestStepType type)
    {
        serializedObject.Update();

        int idx = _steps.arraySize;
        _steps.arraySize++;
        var el = _steps.GetArrayElementAtIndex(idx);

        // Clear/init
        el.FindPropertyRelative("StepId").stringValue = $"step_{idx + 1:00}";
        el.FindPropertyRelative("Text").stringValue = "";

        // IMPORTANT: set enum by VALUE (sparse enum safe)
        var typeProp = el.FindPropertyRelative("Type");
        typeProp.intValue = (int)type;

        // Defaults
        var rDay = el.FindPropertyRelative("RestrictByDay");
        if (rDay != null) rDay.boolValue = false;

        var rPhase = el.FindPropertyRelative("RestrictByPhase");
        if (rPhase != null) rPhase.boolValue = false;

        // Expand new step by default
        el.isExpanded = true;

        RefreshAutoTextForStep(el);

        serializedObject.ApplyModifiedProperties();
    }


    // ----------------------
    // Set auto text
    // ----------------------

    private static bool IsAutoText(string s)
    {
        if (string.IsNullOrEmpty(s)) return true;
        return s.StartsWith("[AUTO] ", StringComparison.Ordinal);
    }

    private static void SetAutoText(SerializedProperty stepEl, string value)
    {
        var textProp = stepEl.FindPropertyRelative("Text");
        if (textProp == null) return;

        // Only overwrite if empty or still auto-generated
        if (!IsAutoText(textProp.stringValue))
            return;

        textProp.stringValue = "[AUTO] " + value;
    }

    private static void RefreshAutoTextForStep(SerializedProperty stepEl)
    {
        if (stepEl == null) return;

        var typeProp = stepEl.FindPropertyRelative("Type");
        if (typeProp == null) return;

        var type = (QuestStepType)typeProp.intValue;

        switch (type)
        {
            case QuestStepType.ReachLocation:
                {
                    var sceneNameProp = stepEl.FindPropertyRelative("TargetLocationSceneName");
                    string sceneName = sceneNameProp != null ? sceneNameProp.stringValue : "";

                    string label = string.IsNullOrEmpty(sceneName) ? "<choose location>" : sceneName;

                    // Optional: make it prettier using SceneDatabase entry Id
                    var db = SceneDatabase.Instance;
                    if (db != null && !string.IsNullOrEmpty(sceneName) && db.Locations != null)
                    {
                        for (int i = 0; i < db.Locations.Count; i++)
                        {
                            var entry = db.Locations[i];
                            if (entry.Scene == null || !entry.Scene.IsValid) continue;
                            if (!string.Equals(entry.Scene.SceneName, sceneName, StringComparison.Ordinal)) continue;

                            if (!string.IsNullOrEmpty(entry.Id))
                                label = entry.Id; // show friendly id instead of raw scene name
                            break;
                        }
                    }

                    SetAutoText(stepEl, $"Go to {label}");
                    break;
                }
            case QuestStepType.HaveMoney:
                {
                    int money = stepEl.FindPropertyRelative("RequiredMoney")?.intValue ?? 0;
                    SetAutoText(stepEl, money > 0 ? $"Have ${money}" : "Have enough money");
                    break;
                }
            case QuestStepType.PayMoney:
                {
                    int money = stepEl.FindPropertyRelative("RequiredMoney")?.intValue ?? 0;
                    SetAutoText(stepEl, money > 0 ? $"Pay ${money}" : "Pay the required amount");
                    break;
                }
            case QuestStepType.MinStats:
                {
                    int influence = stepEl.FindPropertyRelative("RequiredInfluence")?.intValue ?? 0;
                    int strategy = stepEl.FindPropertyRelative("RequiredStrategy")?.intValue ?? 0;
                    int networking = stepEl.FindPropertyRelative("RequiredNetworking")?.intValue ?? 0;
                    int reputation = stepEl.FindPropertyRelative("RequiredReputation")?.intValue ?? 0;

                    var parts = new List<string>();

                    if (influence > 0) parts.Add($"Influence {influence}");
                    if (strategy > 0) parts.Add($"Strategy {strategy}");
                    if (networking > 0) parts.Add($"Networking {networking}");
                    if (reputation > 0) parts.Add($"Reputation {reputation}");

                    SetAutoText(stepEl, parts.Count > 0
                        ? $"Reach {string.Join(", ", parts)}"
                        : "Increase your corporate stats");
                    break;
                }
            case QuestStepType.Manual:
                SetAutoText(stepEl, "Complete the objective");
                break;

            case QuestStepType.AutoComplete:
                SetAutoText(stepEl, "Progress");
                break;
            case QuestStepType.TalkToDialogue:
                {
                    var npcProp = stepEl.FindPropertyRelative("TargetNpcId");
                    var dialogueProp = stepEl.FindPropertyRelative("TargetDialogueId");

                    string npc = npcProp != null ? npcProp.stringValue : "";
                    string dialogueId = dialogueProp != null ? dialogueProp.stringValue : "";

                    if (string.IsNullOrEmpty(npc) && string.IsNullOrEmpty(dialogueId))
                        SetAutoText(stepEl, "Talk to <choose dialogue>");
                    else if (string.IsNullOrEmpty(dialogueId))
                        SetAutoText(stepEl, $"Talk to {npc}");
                    else
                        SetAutoText(stepEl, $"Talk to {npc} ({dialogueId})");
                    break;
                }
            case QuestStepType.HaveItem:
                {
                    var requiredItemProp = stepEl.FindPropertyRelative("RequiredItem");
                    var itemIdProp = requiredItemProp != null ? requiredItemProp.FindPropertyRelative("ItemId") : null;
                    var countProp = requiredItemProp != null ? requiredItemProp.FindPropertyRelative("Count") : null;

                    string itemId = itemIdProp != null ? itemIdProp.stringValue : "";
                    int count = countProp != null ? countProp.intValue : 0;
                    if (count <= 0) count = 1;

                    string label = string.IsNullOrWhiteSpace(itemId) ? "<choose item>" : itemId;

                    var itemDb = FindItemDatabase();
                    if (itemDb != null && !string.IsNullOrWhiteSpace(itemId) && itemDb.TryGet(itemId, out var item) && item != null)
                    {
                        if (!string.IsNullOrWhiteSpace(item.DisplayName))
                            label = item.DisplayName;
                    }

                    SetAutoText(stepEl, count > 1 ? $"Have {count}x {label}" : $"Have {label}");
                    break;
                }
        }
    }

    private static float DrawReachLocationDropdown(SerializedProperty stepEl, float x, float y, float width)
    {
        var prop = stepEl.FindPropertyRelative("TargetLocationSceneName");
        if (prop == null || prop.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(new Rect(x, y, width, 40),
                "ReachLocation: TargetLocationSceneName missing or not a string. Check QuestStepDefinition field name/type.",
                MessageType.Error);
            return y + 40 + 4;
        }

        var db = SceneDatabase.Instance;
        if (db == null)
        {
            EditorGUI.HelpBox(new Rect(x, y, width, 40),
                "SceneDatabase.Instance is null. Ensure a SceneDatabase asset exists in the project.",
                MessageType.Warning);
            return y + 40 + 4;
        }

        // Build dropdown options from SceneDatabase.Locations
        var labels = new System.Collections.Generic.List<string>();
        var values = new System.Collections.Generic.List<string>();

        // Option 0 = None
        labels.Add("<None>");
        values.Add("");

        if (db.Locations != null)
        {
            for (int i = 0; i < db.Locations.Count; i++)
            {
                // LocationEntry is a struct, never null
                var entry = db.Locations[i];

                var sr = entry.Scene;
                if (sr == null || !sr.IsValid) continue;

                var sceneName = sr.SceneName;
                if (string.IsNullOrEmpty(sceneName)) continue;

                // Label can include Id to be friendlier
                var label = string.IsNullOrEmpty(entry.Id) ? sceneName : $"{entry.Id} ({sceneName})";

                // Avoid duplicates
                if (values.Contains(sceneName)) continue;

                labels.Add(label);
                values.Add(sceneName);
            }
        }

        if (values.Count <= 1)
        {
            EditorGUI.HelpBox(new Rect(x, y, width, 34),
                "SceneDatabase has no valid Locations entries to pick from.",
                MessageType.Warning);
            return y + 34 + 4;
        }

        // Current selection index
        string cur = prop.stringValue ?? "";
        int curIndex = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], cur, StringComparison.Ordinal))
            {
                curIndex = i;
                break;
            }
        }

        var r = new Rect(x, y, width, EditorGUIUtility.singleLineHeight);
        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(r, "Target Location", curIndex, labels.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            prop.stringValue = values[Mathf.Clamp(newIndex, 0, values.Count - 1)];

            // If you have AUTO text placeholders, refresh them here
            RefreshAutoTextForStep(stepEl);
        }

        return y + EditorGUIUtility.singleLineHeight + Spacing();
    }

    private static float DrawTalkToNpcPicker(SerializedProperty stepEl, float x, float y, float width)
    {
        var npcProp = stepEl.FindPropertyRelative("TargetNpcId");
        if (npcProp == null || npcProp.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(new Rect(x, y, width, 40),
                "TalkToNpc: TargetNpcId missing or not a string. Check QuestStepDefinition.",
                MessageType.Error);
            return y + 40 + Spacing();
        }

        BuildNpcOptions(out var labels, out var values);

        if (values.Count == 0)
        {
            EditorGUI.HelpBox(new Rect(x, y, width, 40),
                "No NPC ids found. Open Bootstrap (NpcManager) or create NpcDefinition assets with NpcId set.",
                MessageType.Warning);
            return y + 40 + Spacing();
        }

        // Current selection index
        string cur = npcProp.stringValue ?? "";
        int curIndex = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], cur, StringComparison.Ordinal))
            {
                curIndex = i;
                break;
            }
        }

        var r = new Rect(x, y, width, EditorGUIUtility.singleLineHeight);

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(r, "Target NPC", curIndex, labels.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            npcProp.stringValue = values[Mathf.Clamp(newIndex, 0, values.Count - 1)];

            // Update auto text if you're using [AUTO] placeholders
            RefreshAutoTextForStep(stepEl);
        }

        return y + EditorGUIUtility.singleLineHeight + Spacing();
    }

    private static void BuildNpcOptions(out System.Collections.Generic.List<string> labels, out System.Collections.Generic.List<string> values)
    {
        labels = new System.Collections.Generic.List<string>();
        values = new System.Collections.Generic.List<string>();

        // Always include None
        labels.Add("<None>");
        values.Add("");

        // 1) Prefer NpcManager in open scenes (edit mode safe)
        var mgr = UnityEngine.Object.FindFirstObjectByType<NpcManager>();
        if (mgr != null && mgr.Npcs != null)
        {
            for (int i = 0; i < mgr.Npcs.Count; i++)
            {
                var def = mgr.Npcs[i];
                if (def == null) continue;

                var id = def.NpcId;
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!values.Contains(id))
                {
                    labels.Add(id);
                    values.Add(id);
                }
            }

            // If we found anything, stop here (most accurate to current game config)
            if (values.Count > 1) return;
        }

        // 2) Fallback: scan project for NpcDefinition assets
        string[] guids = AssetDatabase.FindAssets("t:NpcDefinition");
        for (int g = 0; g < guids.Length; g++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[g]);
            var def = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
            if (def == null) continue;

            var id = def.NpcId;
            if (string.IsNullOrWhiteSpace(id)) continue;

            if (!values.Contains(id))
            {
                labels.Add(id);
                values.Add(id);
            }
        }
    }
    private static ItemDatabase FindItemDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDatabase");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return AssetDatabase.LoadAssetAtPath<ItemDatabase>(path);
    }
    private static float DrawTalkToDialoguePickers(SerializedProperty stepEl, float x, float y, float width)
    {
        var npcProp = stepEl.FindPropertyRelative("TargetNpcId");
        var dialogueProp = stepEl.FindPropertyRelative("TargetDialogueId");

        string oldNpcId = npcProp != null ? npcProp.stringValue : "";

        y = DrawTalkToNpcPicker(stepEl, x, y, width);

        string newNpcId = npcProp != null ? npcProp.stringValue : "";

        // If NPC changed, clear selected dialogue because the filtered list changed
        if (!string.Equals(oldNpcId, newNpcId, StringComparison.Ordinal))
        {
            if (dialogueProp != null)
                dialogueProp.stringValue = "";

            RefreshAutoTextForStep(stepEl);
        }

        y = DrawDialoguePicker(stepEl, x, y, width);
        return y;
    }

    private static float DrawDialoguePicker(SerializedProperty stepEl, float x, float y, float width)
    {
        var npcProp = stepEl.FindPropertyRelative("TargetNpcId");
        var dialogueProp = stepEl.FindPropertyRelative("TargetDialogueId");

        if (dialogueProp == null || dialogueProp.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(new Rect(x, y, width, 40),
                "TalkToDialogue: TargetDialogueId missing or not a string. Check QuestStepDefinition.",
                MessageType.Error);
            return y + 40 + Spacing();
        }

        string selectedNpcId = npcProp != null ? npcProp.stringValue : "";

        if (string.IsNullOrWhiteSpace(selectedNpcId))
        {
            EditorGUI.HelpBox(new Rect(x, y, width, 40),
                "Select Target NPC first.",
                MessageType.Info);
            return y + 40 + Spacing();
        }

        BuildDialogueOptions(selectedNpcId, out var labels, out var values);

        if (values.Count <= 1)
        {
            EditorGUI.HelpBox(new Rect(x, y, width, 40),
                $"No dialogues found in Routes for NPC '{selectedNpcId}'. Add them to that NPC's RouteSet first.",
                MessageType.Warning);
            return y + 40 + Spacing();
        }

        string cur = dialogueProp.stringValue ?? "";
        int curIndex = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], cur, StringComparison.Ordinal))
            {
                curIndex = i;
                break;
            }
        }

        var r = new Rect(x, y, width, EditorGUIUtility.singleLineHeight);

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(r, "Target Dialogue", curIndex, labels.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            dialogueProp.stringValue = values[Mathf.Clamp(newIndex, 0, values.Count - 1)];
            RefreshAutoTextForStep(stepEl);
        }

        return y + EditorGUIUtility.singleLineHeight + Spacing();
    }

    private static void BuildDialogueOptions(string npcId, out List<string> labels, out List<string> values)
    {
        labels = new List<string>();
        values = new List<string>();

        labels.Add("<None>");
        values.Add("");

        var npc = FindNpcDefinitionById(npcId);
        if (npc == null || npc.Routes == null)
            return;

        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddGraph(npc.Routes.fallback, labels, values, seen);

        if (npc.Routes.rules != null)
        {
            for (int i = 0; i < npc.Routes.rules.Count; i++)
            {
                var rule = npc.Routes.rules[i];
                if (rule == null) continue;

                AddGraph(rule.graph, labels, values, seen);

                if (rule.pool != null)
                {
                    for (int j = 0; j < rule.pool.Length; j++)
                        AddGraph(rule.pool[j], labels, values, seen);
                }

                AddGraph(rule.requireNotSeenThis, labels, values, seen);
                AddGraph(rule.requireSeenThis, labels, values, seen);
            }
        }
    }
    private static NpcDefinition FindNpcDefinitionById(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:NpcDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var npc = AssetDatabase.LoadAssetAtPath<NpcDefinition>(path);
            if (npc == null) continue;

            if (string.Equals(npc.NpcId, npcId, StringComparison.Ordinal))
                return npc;
        }

        return null;
    }
    private static void AddGraph(
    DialogueGraph graph,
    List<string> labels,
    List<string> values,
    HashSet<string> seen)
    {
        if (graph == null)
            return;

        string id = !string.IsNullOrWhiteSpace(graph.DialogueId)
            ? graph.DialogueId
            : graph.name;

        if (string.IsNullOrWhiteSpace(id))
            return;

        if (!seen.Add(id))
            return;

        labels.Add($"{id} ({graph.name})");
        values.Add(id);
    }
}
#endif