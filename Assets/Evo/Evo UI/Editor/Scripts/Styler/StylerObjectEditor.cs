using UnityEditor;
using UnityEngine;
using Evo.EditorTools;

namespace Evo.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(StylerObject))]
    public class StylerObjectEditor : Editor
    {
        // Target
        StylerObject soTarget;

        // Properties
        SerializedProperty preset;
        SerializedProperty targetGraphic;
        SerializedProperty targetText;
        SerializedProperty targetGradient;
        SerializedProperty objectType;
        SerializedProperty colorID;
        SerializedProperty fontID;
        SerializedProperty spriteID;
        SerializedProperty gradientID;
        SerializedProperty useCustomColor;
        SerializedProperty overrideAlpha;
        SerializedProperty alphaOverride;

        // Interaction properties
        SerializedProperty enableInteraction;
        SerializedProperty interactableObject;
        SerializedProperty disabledColor;
        SerializedProperty normalColor;
        SerializedProperty highlightedColor;
        SerializedProperty pressedColor;
        SerializedProperty selectedColor;

        void OnEnable()
        {
            soTarget = (StylerObject)target;

            preset = serializedObject.FindProperty("preset");
            targetGraphic = serializedObject.FindProperty("targetGraphic");
            targetText = serializedObject.FindProperty("targetText");
            targetGradient = serializedObject.FindProperty("targetGradient");
            objectType = serializedObject.FindProperty("objectType");
            colorID = serializedObject.FindProperty("colorID");
            fontID = serializedObject.FindProperty("fontID");
            spriteID = serializedObject.FindProperty("spriteID");
            gradientID = serializedObject.FindProperty("gradientID");
            useCustomColor = serializedObject.FindProperty("useCustomColor");
            overrideAlpha = serializedObject.FindProperty("overrideAlpha");
            alphaOverride = serializedObject.FindProperty("alphaOverride");

            enableInteraction = serializedObject.FindProperty("enableInteraction");
            interactableObject = serializedObject.FindProperty("interactableObject");
            disabledColor = serializedObject.FindProperty("disabledColor");
            normalColor = serializedObject.FindProperty("normalColor");
            highlightedColor = serializedObject.FindProperty("highlightedColor");
            pressedColor = serializedObject.FindProperty("pressedColor");
            selectedColor = serializedObject.FindProperty("selectedColor");

            EvoEditorGUI.RegisterEditor(this);
        }

        void OnDisable()
        {
            EvoEditorGUI.UnregisterEditor(this);
        }

        public override void OnInspectorGUI()
        {
            if (!EvoEditorSettings.IsCustomEditorEnabled(Constants.CustomEditorID)) { DrawDefaultInspector(); }
            else
            {
                DrawCustomGUI();
                EvoEditorGUI.HandleInspectorGUI();
            }
        }

        void DrawCustomGUI()
        {
            serializedObject.Update();
            EvoEditorGUI.BeginCenteredInspector();

            DrawReferences();
            DrawSettings();
            DrawInteraction();

            EvoEditorGUI.EndCenteredInspector();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawReferences()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref soTarget.referencesFoldout, "References", EvoEditorGUI.GetIcon("UI_References")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    EvoEditorGUI.DrawProperty(preset, "Styler Preset", "The preset containing style definitions.", true, true, true);
                    if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Graphic) { EvoEditorGUI.DrawProperty(targetGraphic, "Target Graphic", null, false, true, true); }
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.TMPText) { EvoEditorGUI.DrawProperty(targetText, "Target Text", null, false, true, true); }
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Image) { EvoEditorGUI.DrawProperty(targetGraphic, "Target Image", null, false, true, true); }
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient) { EvoEditorGUI.DrawProperty(targetGradient, "Target Gradient", "", false, true, true); }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawSettings()
        {
            EvoEditorGUI.BeginVerticalBackground();
            if (EvoEditorGUI.DrawFoldout(ref soTarget.settingsFoldout, "Settings", EvoEditorGUI.GetIcon("UI_Settings")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    // Object Type
                    EvoEditorGUI.DrawProperty(objectType, "Object Type", null, true, true, true);

                    bool isGradient = objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient;
                    GUI.enabled = preset.objectReferenceValue;

                    // Draw fields
                    if (objectType.enumValueIndex == (int)StylerObject.ObjectType.TMPText)
                        StylerEditor.DrawItemDropdown(preset, fontID, Styler.ItemType.Font, "Font ID", true, true, true);
                    else if (objectType.enumValueIndex == (int)StylerObject.ObjectType.Image)
                        StylerEditor.DrawItemDropdown(preset, spriteID, Styler.ItemType.Sprite, "Sprite ID", true, true, true);
                    else if (isGradient)
                        StylerEditor.DrawItemDropdown(preset, gradientID, Styler.ItemType.Gradient, "Gradient ID", false, true, true);

                    GUI.enabled = !useCustomColor.boolValue && !enableInteraction.boolValue;

                    // Hide color-specific settings entirely if working with a gradient
                    if (!isGradient)
                    {
                        StylerEditor.DrawItemDropdown(preset, colorID, Styler.ItemType.Color, "Color ID", true, true, true);

                        // Override Alpha section
                        GUI.enabled = !useCustomColor.boolValue;
                        EvoEditorGUI.BeginVerticalBackground(true);
                        EvoEditorGUI.DrawToggle(overrideAlpha, "Override Alpha", overrideAlpha.tooltip, false, true, true, bypassNormalBackground: true);
                        if (overrideAlpha.boolValue)
                        {
                            EvoEditorGUI.BeginContainer(3);
                            EvoEditorGUI.DrawProperty(alphaOverride, "Alpha", alphaOverride.tooltip, false, true);
                            EvoEditorGUI.EndContainer();
                        }
                        EvoEditorGUI.EndVerticalBackground(true);
                        GUI.enabled = true;

                        EvoEditorGUI.DrawToggle(useCustomColor, "Use Custom Color", useCustomColor.tooltip, false, true, true);
                    }
                    else
                    {
                        // Reset GUI enabled state if skipping color fields
                        GUI.enabled = true;
                    }

                    // Info Boxes
                    if (!preset.objectReferenceValue && (!useCustomColor.boolValue || isGradient))
                    {
                        GUILayout.Space(4);
                        EvoEditorGUI.DrawInfoBox("No preset attached. Please assign a valid Styler Preset to use the Styler system.", null, true);
                    }
                    else if (enableInteraction.boolValue && interactableObject.objectReferenceValue && !isGradient)
                    {
                        GUILayout.Space(4);
                        EvoEditorGUI.DrawInfoBox("Interaction is enabled; color will be handled by the interaction system.", null, true);
                    }
                }
                EvoEditorGUI.EndContainer();
            }
            EvoEditorGUI.EndVerticalBackground();
            EvoEditorGUI.AddFoldoutSpace();
        }

        void DrawInteraction()
        {
            EvoEditorGUI.BeginVerticalBackground();

            if (EvoEditorGUI.DrawFoldout(ref soTarget.interactionFoldout, "Interaction", EvoEditorGUI.GetIcon("UI_Event")))
            {
                EvoEditorGUI.BeginContainer();
                {
                    bool isGradient = objectType.enumValueIndex == (int)StylerObject.ObjectType.Gradient;

                    // Disable properties & inform the user if in Gradient Mode
                    if (isGradient)
                    {
                        EvoEditorGUI.DrawInfoBox("Interaction is currently not supported for the Gradient type.", null, true);
                        GUILayout.Space(4);
                        GUI.enabled = false;
                    }

                    EvoEditorGUI.BeginVerticalBackground(true);
                    EvoEditorGUI.DrawToggle(enableInteraction, "Enable Interaction", enableInteraction.tooltip, false, true, true, bypassNormalBackground: true);

                    if (!enableInteraction.boolValue)
                    {
                        EvoEditorGUI.EndVerticalBackground();
                    }
                    else
                    {
                        EvoEditorGUI.BeginContainer(3);
                        EvoEditorGUI.DrawProperty(interactableObject, "Target Object", null, false, true);
                        EvoEditorGUI.EndContainer();
                        EvoEditorGUI.EndVerticalBackground();

                        if (soTarget.interactableObject)
                        {
                            EvoEditorGUI.AddPropertySpace();
                            EvoEditorGUI.BeginVerticalBackground(true);
                            EvoEditorGUI.BeginContainer("Color When", 3);
                            DrawInteractionColors();
                            EvoEditorGUI.EndContainer();
                            EvoEditorGUI.EndVerticalBackground();
                        }
                    }

                    if (isGradient)
                        GUI.enabled = true;
                }
                EvoEditorGUI.EndContainer();
            }

            EvoEditorGUI.EndVerticalBackground();
        }

        void DrawInteractionColors()
        {
            // Array of ColorMapping properties matching InteractionState enum order
            SerializedProperty[] colorMappings = new[]
            {
                disabledColor,
                normalColor,
                highlightedColor,
                pressedColor,
                selectedColor
            };

            string[] stateNames = Interactive.GetInteractionStateIDs();

            // Draw each state's color mapping
            for (int i = 0; i < stateNames.Length; i++)
            {
                SerializedProperty mapping = colorMappings[i];
                SerializedProperty color = mapping.FindPropertyRelative("color");
                SerializedProperty stylerID = mapping.FindPropertyRelative("stylerID");

                bool isLastItem = i >= stateNames.Length - 1;

                // If preset is assigned, use dropdown
                if (preset.objectReferenceValue != null && !useCustomColor.boolValue)
                {
                    StylerEditor.DrawItemDropdown(preset, stylerID, Styler.ItemType.Color, stateNames[i], !isLastItem);
                }
                else
                {
                    // Fallback to custom color field when preset is missing
                    EvoEditorGUI.DrawProperty(color, stateNames[i], null, !isLastItem, true);
                }
            }
        }
    }
}