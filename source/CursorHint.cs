using Hex.Session.Battle;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.RetaliationHints;

internal sealed class CursorHint
{
    internal readonly RectTransform Root;
    private readonly RectTransform _parent;
    private readonly TextMeshProUGUI _label;
    private readonly Image _icon;
    private readonly CanvasGroup _visibility;
    private string _text = "";

    internal CursorHint(BhCursorTooltips host, Sprite icon)
    {
        // Use the game's screen-space cursor layer, not a world-space creature bar
        // or its see-through text material. The native tooltip itself may be hidden.
        _parent = host.rootCanvasRect;
        Root = Rect("RetaliationHints.CursorEstimate", _parent);
        Root.gameObject.SetActive(false);
        _visibility = Root.gameObject.AddComponent<CanvasGroup>();
        _visibility.blocksRaycasts = false; _visibility.interactable = false; _visibility.alpha = 0;
        var frame = Root.gameObject.AddComponent<Image>();
        frame.raycastTarget = false; frame.color = new(.57f, .46f, .26f, .98f);
        var inside = Rect("Background", Root);
        inside.anchorMin = Vector2.zero; inside.anchorMax = Vector2.one;
        inside.offsetMin = Vector2.one; inside.offsetMax = -Vector2.one;
        var bg = inside.gameObject.AddComponent<Image>();
        bg.raycastTarget = false; bg.color = new(.075f, .11f, .16f, .97f);
        _icon = Rect("Casualties", Root).gameObject.AddComponent<Image>();
        _icon.raycastTarget = false; _icon.preserveAspect = true; _icon.sprite = icon;
        _label = Rect("Estimate", Root).gameObject.AddComponent<TextMeshProUGUI>();
        _label.font = host.simpleTooltip.targetsText.font;
        _label.raycastTarget = false; _label.enableAutoSizing = false;
        _label.enableWordWrapping = false; _label.richText = false;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.margin = Vector4.zero;
    }

    internal void Set(string text, bool danger)
    {
        _text = text; _label.text = text;
        _label.color = danger ? new(1f, .82f, .64f, 1) : new(.84f, .89f, .76f, 1);
    }

    internal bool Place(Vector2 mouse, float setting)
    {
        if (!Root || !_parent || !_parent.gameObject.activeInHierarchy) { Hide(); return false; }
        _visibility.alpha = 0;
        var canvas = _parent.GetComponentInParent<Canvas>();
        if (!canvas || !canvas.isActiveAndEnabled || !canvas.rootCanvas.isActiveAndEnabled) { Hide(); return false; }
        if (canvas.rootCanvas.renderMode == RenderMode.WorldSpace)
            throw new NotSupportedException("Native cursor layer is not a screen-space canvas");
        Camera camera = null;
        if (canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            camera = canvas.worldCamera ? canvas.worldCamera : canvas.rootCanvas.worldCamera;
            if (!camera) throw new NotSupportedException("Native cursor canvas camera is missing");
        }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, Vector2.zero, camera, out var p0)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(100, 100), camera, out var p1))
        { Hide(); return false; }
        float unitsX = Math.Abs(p1.x - p0.x) / 100, unitsY = Math.Abs(p1.y - p0.y) / 100;
        if (unitsX <= 0 || unitsY <= 0 || !float.IsFinite(unitsX + unitsY)) { Hide(); return false; }
        // Everything below the root, including its one-pixel border, is in pixels.
        Root.localScale = new(unitsX, unitsY, 1);
        Root.gameObject.SetActive(true);
        if (!float.IsFinite(setting)) setting = 1;
        float scale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f) * Mathf.Clamp(setting, .7f, 1.8f);
        _label.fontSize = 18 * scale;
        float textWidth = _label.GetPreferredValues(_text).x;
        if (!float.IsFinite(textWidth) || textWidth < 0) { Hide(); return false; }
        float iconWidth = _icon.sprite ? 22 * scale : 0;
        var box = CursorPlacement.Place(Screen.width, Screen.height, mouse.x, mouse.y,
            Math.Clamp(textWidth + iconWidth + 18 * scale, 80 * scale, 480 * scale), 32 * scale, scale);
        if (!box.Valid) { Hide(); return false; }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent,
            new Vector2(box.X + box.Width / 2, box.Y + box.Height / 2), camera, out var center))
        { Hide(); return false; }
        Root.anchoredPosition = center - _parent.rect.center;
        Root.sizeDelta = new(box.Width, box.Height);
        _icon.enabled = _icon.sprite;
        _icon.rectTransform.sizeDelta = new(20 * scale, 20 * scale);
        _icon.rectTransform.anchoredPosition = new(-box.Width / 2 + 16 * scale, 0);
        _label.rectTransform.sizeDelta = new(box.Width - iconWidth - 12 * scale, box.Height);
        _label.rectTransform.anchoredPosition = new(iconWidth / 2, 0);
        _visibility.alpha = 1;
        return true;
    }

    internal void Hide() { if (_visibility) _visibility.alpha = 0; if (Root) Root.gameObject.SetActive(false); }
    internal void Destroy() { if (Root) UnityEngine.Object.Destroy(Root.gameObject); }
    private static RectTransform Rect(string name, RectTransform parent)
    {
        var go = new GameObject(name, new Il2CppReferenceArray<Il2CppSystem.Type>(new[] { Il2CppType.Of<RectTransform>() }));
        go.layer = parent.gameObject.layer;
        var rect = go.transform.Cast<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new(.5f, .5f); rect.localScale = Vector3.one;
        return rect;
    }
}
