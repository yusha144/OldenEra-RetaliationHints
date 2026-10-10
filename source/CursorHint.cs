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
    private bool _layoutDirty = true;
    private float _layoutScale, _layoutMaxWidth, _width, _height;
    private int _visibleGlyphs;

    internal string Diagnostic => $"text='{_text}', visible glyphs={_visibleGlyphs}, font={_label.font.name}, material={_label.fontSharedMaterial.name}";

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
        _label.fontSharedMaterial = host.simpleTooltip.targetsText.fontSharedMaterial;
        _label.raycastTarget = false; _label.enableAutoSizing = false;
        _label.enableWordWrapping = false; _label.richText = false;
        // The game's font can have line metrics taller than a compact row. Ellipsis
        // can discard the entire first line. Fit real glyph bounds instead.
        _label.overflowMode = TextOverflowModes.Overflow;
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.margin = Vector4.zero;
    }

    internal void Set(string text, bool danger)
    {
        if (_text != text) { _text = text; _label.text = text; _layoutDirty = true; }
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
        if (!Root.gameObject.activeSelf) _layoutDirty = true;
        Root.gameObject.SetActive(true);
        if (!float.IsFinite(setting)) setting = 1;
        float scale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f) * Mathf.Clamp(setting, .7f, 1.8f);
        float maxWidth = Math.Min(480 * scale, Screen.width - 12 * scale);
        if (_layoutDirty || scale != _layoutScale || maxWidth != _layoutMaxWidth)
            Layout(scale, maxWidth);
        var box = CursorPlacement.Place(Screen.width, Screen.height, mouse.x, mouse.y,
            _width, _height, scale);
        if (!box.Valid) { Hide(); return false; }
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent,
            new Vector2(box.X + box.Width / 2, box.Y + box.Height / 2), camera, out var center))
        { Hide(); return false; }
        Root.anchoredPosition = center - _parent.rect.center;
        _visibility.alpha = 1;
        return true;
    }

    private void Layout(float scale, float maxWidth)
    {
        float iconWidth = _icon.sprite ? 22 * scale : 0;
        float available = maxWidth - iconWidth - 18 * scale;
        if (available <= 0) throw new NotSupportedException("Viewport too narrow for retaliation text");
        _label.fontSize = 18 * scale;
        // Keep this measuring rect stable. Its line metrics may exceed the panel,
        // but only the generated glyph bounds determine the visible layout.
        _label.rectTransform.sizeDelta = new(maxWidth, 64 * scale);
        _label.rectTransform.anchoredPosition = Vector2.zero;
        _label.ForceMeshUpdate(false, true);
        var bounds = VisibleBounds();
        float fit = Math.Min(1, Math.Min(available / bounds.size.x, 22 * scale / bounds.size.y));
        if (fit < 1)
        {
            _label.fontSize *= fit;
            _label.ForceMeshUpdate(false, true);
            bounds = VisibleBounds();
        }
        _width = Math.Min(maxWidth, Math.Max(80 * scale, bounds.size.x + iconWidth + 18 * scale));
        _height = Math.Max(32 * scale, bounds.size.y + 10 * scale);
        Root.sizeDelta = new(_width, _height);
        _icon.enabled = _icon.sprite;
        _icon.rectTransform.sizeDelta = new(20 * scale, 20 * scale);
        _icon.rectTransform.anchoredPosition = new(-_width / 2 + 16 * scale, 0);
        _label.rectTransform.anchoredPosition = new(-_width / 2 + 8 * scale + iconWidth - bounds.min.x, -bounds.center.y);
        _layoutScale = scale; _layoutMaxWidth = maxWidth; _layoutDirty = false;
    }

    private Bounds VisibleBounds()
    {
        _visibleGlyphs = 0;
        var info = _label.textInfo;
        for (int i = 0; i < info.characterCount; i++)
            if (info.characterInfo[i].isVisible) _visibleGlyphs++;
        var bounds = _label.textBounds;
        if (_visibleGlyphs == 0 || bounds.size.x <= 0 || bounds.size.y <= 0
            || !float.IsFinite(bounds.size.x + bounds.size.y + bounds.center.x + bounds.center.y))
            throw new InvalidOperationException($"Retaliation text produced no visible glyphs: '{_text}'");
        return bounds;
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
