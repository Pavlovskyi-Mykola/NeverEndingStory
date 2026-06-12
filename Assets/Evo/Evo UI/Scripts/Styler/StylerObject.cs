using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Evo.UI
{
    [DisallowMultipleComponent]
    [HelpURL(Constants.HelpUrl + "styler")]
    [AddComponentMenu("Evo/UI/Styler/Styler Object")]
    public class StylerObject : MonoBehaviour, IStylerHandler
    {
        [EvoHeader("References", Constants.CustomEditorID)]
        public StylerPreset preset;
        [UnityEngine.Serialization.FormerlySerializedAs("targetImage")]
        [Tooltip("Graphic component to style")]
        public Graphic targetGraphic;
        [Tooltip("TextMeshPro component to style.")]
        public TMP_Text targetText;
        [Tooltip("ImageGradient component to style.")]
        public ImageGradient targetGradient;

        [EvoHeader("Settings", Constants.CustomEditorID)]
        [Tooltip("Type of UI object to style.")]
        public ObjectType objectType = ObjectType.Graphic;
        public string colorID = "Primary";
        public string fontID = "Regular";
        public string gradientID = "";
        public string spriteID = "";
        [Tooltip("Set a current color instead of getting from the preset.")]
        public bool useCustomColor = false;
        [Tooltip("Override the alpha channel with a custom value.")]
        public bool overrideAlpha = false;
        [Tooltip("Alpha value to apply (0 = transparent, 1 = opaque).")]
        [Range(0f, 1f)] public float alphaOverride = 1f;

        [EvoHeader("Interaction", Constants.CustomEditorID)]
        [Tooltip("Enable state-based color animation.")]
        public bool enableInteraction = false;
        public Interactive interactableObject;
        public ColorMapping disabledColor = new() { stylerID = "Primary" };
        public ColorMapping normalColor = new() { stylerID = "Primary" };
        public ColorMapping highlightedColor = new() { stylerID = "Primary" };
        public ColorMapping pressedColor = new() { stylerID = "Primary" };
        public ColorMapping selectedColor = new() { stylerID = "Primary" };

        // Styler Interface
        public StylerPreset Preset
        {
            get => preset;
            set
            {
                if (preset == value)
                    return;

                preset = value;
                UpdateStyler();
            }
        }

        // Enums
        public enum ObjectType
        {
            [Tooltip("Sets the color of the Graphic variable.")]
            Graphic = 0,

            [Tooltip("Sets the font and color of the TMP variable.")]
            TMPText = 1,

            [Tooltip("Sets the sprite and color of the Image variable.")]
            [InspectorName("Image (Sprite)")] Image = 2,

            [Tooltip("Sets the gradient of the ImageGradient variable.")]
            Gradient = 3
        }

        // Cache
        readonly List<string> cachedColorIDs = new();
        readonly List<string> cachedFontIDs = new();
        readonly List<string> cachedGradientIDs = new();
        readonly List<string> cachedSpriteIDs = new();
        [System.NonSerialized] Gradient cachedGradientCopy;

        // Interaction Cache
        InteractionState currentState;
        Coroutine tweenCoroutine;

        void Awake() => Styler.RegisteredObjects.Add(this);

        void OnEnable()
        {
            tweenCoroutine = null;

            // Subscribe to catch future states
            if (enableInteraction && interactableObject != null)
            {
                interactableObject.OnStateChanged += OnInteractableStateChanged;
                currentState = interactableObject.interactionState;
            }

            // Apply the visuals
            UpdateStyler();
        }

        void OnDisable()
        {
            // Unsubscribe to bypass animating hidden objects
            if (enableInteraction && interactableObject != null)
                interactableObject.OnStateChanged -= OnInteractableStateChanged;

            // Cleanup tweens
            if (tweenCoroutine != null)
            {
                StopCoroutine(tweenCoroutine);
                tweenCoroutine = null;

                if (currentState != InteractionState.Selected && currentState != InteractionState.Disabled)
                    currentState = InteractionState.Normal;
            }
        }

        void OnDestroy() => Styler.RegisteredObjects.Remove(this);

        void CheckComponents()
        {
            switch (objectType)
            {
                case ObjectType.Graphic:
                case ObjectType.Image:
                default:
                    if (targetGraphic == null) { TryGetComponent(out targetGraphic); }
                    break;
                case ObjectType.TMPText:
                    if (targetText == null) { TryGetComponent(out targetText); }
                    break;
                case ObjectType.Gradient:
                    if (targetGradient == null) { TryGetComponent(out targetGradient); }
                    if (targetGraphic == null && targetGradient != null) { targetGradient.TryGetComponent(out targetGraphic); }
                    break;
            }
        }

        void OnInteractableStateChanged(InteractionState newState)
        {
            if (currentState == newState || !gameObject.activeInHierarchy || objectType == ObjectType.Gradient)
                return;

            currentState = newState;
            AnimateToState(newState);
        }

        void AnimateToState(InteractionState state)
        {
            if (objectType == ObjectType.Gradient)
                return;

            if ((objectType == ObjectType.TMPText && targetText == null) ||
                ((objectType == ObjectType.Graphic || objectType == ObjectType.Image) && targetGraphic == null))
                return;

            float tDuration = Mathf.Max(0f, interactableObject != null ? interactableObject.transitionDuration : 0f);
            Graphic tGraphic = objectType == ObjectType.TMPText ? targetText : targetGraphic;
            Color tColor = GetInteractionColor(state, tGraphic.color);

            if (tweenCoroutine != null) { StopCoroutine(tweenCoroutine); }
            tweenCoroutine = StartCoroutine(Utilities.CrossFadeGraphic(tGraphic, tColor, tDuration));
        }

        Color GetTargetColor(Color currentColor)
        {
            if (enableInteraction && interactableObject != null)
                return GetInteractionColor(interactableObject.interactionState, currentColor);

            if (string.IsNullOrEmpty(colorID) || preset == null)
                return Color.clear;

            if (preset.TryGetColor(colorID, out Color baseColor))
            {
                if (overrideAlpha)
                    return new Color(baseColor.r, baseColor.g, baseColor.b, alphaOverride);

                return baseColor;
            }

            // Fallback: keep the visual state if ID is missing in preset
            return currentColor;
        }

        Color GetInteractionColor(InteractionState state, Color currentColor)
        {
            ColorMapping mapping = GetColorMappingForState(state);
            if (mapping == null) { return currentColor; }

            Color targetColor;

            // Use Styler Preset color
            if (!useCustomColor && preset != null)
            {
                if (string.IsNullOrEmpty(mapping.stylerID))
                    targetColor = Color.clear;
                else if (preset.TryGetColor(mapping.stylerID, out Color baseColor))
                    targetColor = baseColor;
                else
                    targetColor = mapping.color; // Fallback to custom color if missing ID or TryGetColor fails
            }
            else
            {
                // Fallback to custom color if useCustomColor is true or preset is null
                targetColor = mapping.color;
            }

            if (overrideAlpha)
                return new Color(targetColor.r, targetColor.g, targetColor.b, alphaOverride);

            return targetColor;
        }

        ColorMapping GetColorMappingForState(InteractionState state)
        {
            return state switch
            {
                InteractionState.Disabled => disabledColor,
                InteractionState.Normal => normalColor,
                InteractionState.Highlighted => highlightedColor,
                InteractionState.Pressed => pressedColor,
                InteractionState.Selected => selectedColor,
                _ => null
            };
        }

        void ApplySpriteSettings(Image targetImage, Styler.SpriteItem item)
        {
            if (targetImage == null)
                return;

            Sprite targetSprite = item?.spriteAsset;

            if (targetImage.sprite != targetSprite)
                targetImage.sprite = targetSprite;

            if (item != null && item.applyImageSettings)
            {
                if (targetImage.type != item.imageType) { targetImage.type = item.imageType; }
                if (targetImage.preserveAspect != item.preserveAspect) { targetImage.preserveAspect = item.preserveAspect; }
                if (targetImage.pixelsPerUnitMultiplier != item.pixelsPerUnitMultiplier) { targetImage.pixelsPerUnitMultiplier = item.pixelsPerUnitMultiplier; }

                if (item.imageType == Image.Type.Filled)
                {
                    if (targetImage.fillMethod != item.fillMethod) { targetImage.fillMethod = item.fillMethod; }
                    if (targetImage.fillOrigin != item.fillOrigin) { targetImage.fillOrigin = item.fillOrigin; }
                    if (targetImage.fillAmount != item.fillAmount) { targetImage.fillAmount = item.fillAmount; }
                    if (targetImage.fillClockwise != item.fillClockwise) { targetImage.fillClockwise = item.fillClockwise; }
                }
            }
        }

        /// <summary>
        /// Force-updates the styler object. Useful to call when any of its parameters are changed.
        /// </summary>
        public void UpdateStyler()
        {
            CheckComponents();

            if (targetGraphic == null && targetText == null && targetGradient == null)
                return;

            // Handle Gradient completely independently
            if (objectType == ObjectType.Gradient)
            {
                if (preset != null && targetGradient != null && preset.TryGetGradient(gradientID, out var grad))
                {
                    // Reuse the cached Gradient instance instead of instantiating 'new Gradient()' every update
                    cachedGradientCopy ??= new Gradient();
                    cachedGradientCopy.SetKeys(grad.colorKeys, grad.alphaKeys);
                    cachedGradientCopy.mode = grad.mode;

                    targetGradient.SetGradient(cachedGradientCopy);

                    // Force targetGraphic to redraw immediately.
                    if (targetGraphic != null)
                        targetGraphic.SetVerticesDirty();

#if UNITY_EDITOR
                    // Tell Unity the object changed so the Inspector visually updates the gradient block
                    if (!Application.isPlaying)
                        UnityEditor.EditorUtility.SetDirty(targetGradient);
#endif
                }

                // Gradients don't process standard colors or interaction
                return;
            }

            // Handle Fonts & Sprites
            if (preset != null)
            {
                if (objectType == ObjectType.TMPText && targetText != null)
                {
                    if (preset.TryGetFont(fontID, out var targetFont) && targetText.font != targetFont)
                        targetText.font = targetFont;
                }
                else if (objectType == ObjectType.Image && targetGraphic is Image targetImage)
                {
                    if (preset.TryGetSpriteItem(spriteID, out var spriteItem))
                        ApplySpriteSettings(targetImage, spriteItem);
                }
            }

            // Handle Colors
            if (!enableInteraction && preset == null && !useCustomColor)
                return;

            if (useCustomColor && !enableInteraction)
                return; // User is manually managing the graphic's color

            // Stop any active interaction transitions to prevent color fighting
            if (tweenCoroutine != null)
            {
                StopCoroutine(tweenCoroutine);
                tweenCoroutine = null;
            }

            Color currentColor = objectType == ObjectType.TMPText && targetText != null
                ? targetText.color
                : (targetGraphic != null ? targetGraphic.color : Color.white);

            Color targetColor = GetTargetColor(currentColor);

            if (objectType == ObjectType.TMPText && targetText != null && targetText.color != targetColor)
                targetText.color = targetColor;
            else if (targetGraphic != null && targetGraphic.color != targetColor)
                targetGraphic.color = targetColor;
        }

        public List<string> GetAvailableColorIDs()
        {
            cachedColorIDs.Clear();

            if (preset == null)
                return cachedColorIDs;

            for (int i = 0; i < preset.colorItems.Count; i++)
                cachedColorIDs.Add(preset.colorItems[i].itemID);

            return cachedColorIDs;
        }

        public List<string> GetAvailableFontIDs()
        {
            cachedFontIDs.Clear();

            if (preset == null)
                return cachedFontIDs;

            for (int i = 0; i < preset.fontItems.Count; i++)
                cachedFontIDs.Add(preset.fontItems[i].itemID);

            return cachedFontIDs;
        }

        public List<string> GetAvailableSpriteIDs()
        {
            cachedSpriteIDs.Clear();

            if (preset == null)
                return cachedSpriteIDs;

            for (int i = 0; i < preset.spriteItems.Count; i++)
                cachedSpriteIDs.Add(preset.spriteItems[i].itemID);

            return cachedSpriteIDs;
        }

        public List<string> GetAvailableGradientIDs()
        {
            cachedGradientIDs.Clear();

            if (preset == null)
                return cachedGradientIDs;

            for (int i = 0; i < preset.gradientItems.Count; i++)
                cachedGradientIDs.Add(preset.gradientItems[i].itemID);

            return cachedGradientIDs;
        }

        #region Obsolete
        [System.Obsolete("Use UpdateStyler() instead.")]
        public void UpdateStyle() => UpdateStyler();

        [System.Obsolete("Use targetGraphic instead.")]
        public Image targetImage
        {
            get => targetGraphic as Image;
            set => targetGraphic = value;
        }
        #endregion

#if UNITY_EDITOR
        [HideInInspector] public bool referencesFoldout = true;
        [HideInInspector] public bool settingsFoldout = true;
        [HideInInspector] public bool interactionFoldout = true;

        void Reset()
        {
            CheckComponents();
            preset = Styler.GetDefaultPreset(false);

            if (preset != null)
            {
                if (preset.fontItems[0] != null)
                    fontID = preset.fontItems[0].itemID;

                if (preset.colorItems[0] != null)
                    colorID = preset.colorItems[0].itemID;
            }
        }

        void OnValidate()
        {
            if (!this.enabled)
                return;

            UpdateStyler();
        }
#endif
    }
}