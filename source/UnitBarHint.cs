using Hex.Session.Battle;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.RetaliationHints;

internal sealed class UnitBarHint
{
    internal readonly RectTransform Root;
    private readonly RectTransform _parent;
    private readonly BhBattleUnitInfoView _bar;
    private readonly TextMeshProUGUI _label;
    private readonly Image _icon;
    private readonly CanvasGroup _visibility;
    private readonly Il2CppStructArray<Vector3> _corners = new(4);
    private string _text = "";

    internal UnitBarHint(BhBattleUnitInfoView bar)
    {
        _bar = bar; _parent = bar.transform.Cast<RectTransform>();
        Root = Rect("RetaliationHints.LocalEstimate", _parent);
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
        _icon.raycastTarget = false; _icon.preserveAspect = true;
        _icon.sprite = bar.healDamageForecast?.killedSquadsIcon;
        _label = Rect("Estimate", Root).gameObject.AddComponent<TextMeshProUGUI>();
        _label.font = bar.count.textMesh.font; _label.fontSharedMaterial = bar.count.textMesh.fontSharedMaterial;
        _label.raycastTarget = false; _label.enableAutoSizing = false;
        _label.enableWordWrapping = false; _label.richText = false;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.margin = Vector4.zero;
        Hide();
    }

    internal void Set(string text, bool danger)
    {
        _text = text; _label.text = text;
        _label.color = danger ? new(1f, .82f, .64f, 1) : new(.84f, .89f, .76f, 1);
    }

    internal bool Place(float setting)
    {
        if (!Root || !_parent || !_bar.count.textMesh) { Hide(); return false; }
        _visibility.alpha = 0;
        var canvas = _parent.GetComponentInParent<Canvas>();
        if (!canvas || !canvas.isActiveAndEnabled || !canvas.rootCanvas.isActiveAndEnabled) { Hide(); return false; }
        Camera camera = null;
        if (canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            camera = canvas.worldCamera ? canvas.worldCamera : canvas.rootCanvas.worldCamera;
            // A world-space canvas may omit its event camera while the scene camera
            // still renders it. Null would interpret world coordinates as pixels.
            if (!camera) camera = Camera.main;
            if (!camera || !camera.isActiveAndEnabled) { Hide(); return false; }
        }
        var countRect = _bar.count.textMesh.rectTransform;
        countRect.GetWorldCorners(_corners);
        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue;
        foreach (var corner in _corners)
        {
            var p = RectTransformUtility.WorldToScreenPoint(camera, corner);
            left = Math.Min(left, p.x); right = Math.Max(right, p.x); bottom = Math.Min(bottom, p.y);
        }
        // The count sits below the health strip. Its visible rect is the anchor;
        // the health container may include empty space used by native animations.
        if (!float.IsFinite(left + right + bottom) || right <= 0 || left >= Screen.width
            || bottom < 0 || bottom > Screen.height) { Hide(); return false; }
        if (!float.IsFinite(setting)) setting = 1;
        float scale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f) * Mathf.Clamp(setting, .7f, 1.8f);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(left, bottom), camera, out var p0)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(left + 100, bottom), camera, out var px)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(left, bottom + 100), camera, out var py))
        { Hide(); return false; }
        float unitsX = Math.Abs(px.x - p0.x) / 100f, unitsY = Math.Abs(py.y - p0.y) / 100f;
        if (unitsX <= 0 || unitsY <= 0 || !float.IsFinite(unitsX + unitsY)) { Hide(); return false; }

        // Normalize ONCE at the root. All child dimensions, font sizes and border
        // offsets below are pixels. A one-unit inset on the native world-space bar
        // can exceed the whole widget and invert/stretch its background rectangle.
        Root.localScale = new(unitsX, unitsY, 1);
        Root.gameObject.SetActive(true);
        _label.fontSize = 18 * scale;
        float textWidth = _label.GetPreferredValues(_text).x;
        if (!float.IsFinite(textWidth) || textWidth < 0) { Hide(); return false; }
        float iconWidth = _icon.sprite ? 22 * scale : 0;
        float width = Math.Min(Math.Clamp(textWidth + iconWidth + 18 * scale, 80 * scale, 480 * scale), Screen.width - 6);
        float height = 30 * scale;
        float x = Mathf.Clamp((left + right - width) / 2, 3, Math.Max(3, Screen.width - width - 3));
        float y = Mathf.Clamp(bottom - height - 4 * scale, 3, Math.Max(3, Screen.height - height - 3));
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(x + width / 2, y + height / 2), camera, out var center))
        { Hide(); return false; }
        Root.anchoredPosition = center - _parent.rect.center;
        Root.sizeDelta = new(width, height);
        _icon.enabled = _icon.sprite;
        _icon.rectTransform.sizeDelta = new(20 * scale, 20 * scale);
        _icon.rectTransform.anchoredPosition = new(-width / 2 + 16 * scale, 0);
        _label.rectTransform.sizeDelta = new(width - iconWidth - 12 * scale, height);
        _label.rectTransform.anchoredPosition = new(iconWidth / 2, 0);

        // Validate the actual projection before revealing any graphics. A bad native
        // transform must hide this hint, never cover the battlefield with a rectangle.
        Root.GetWorldCorners(_corners);
        var lower = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
        var upper = RectTransformUtility.WorldToScreenPoint(camera, _corners[2]);
        var actual = upper - lower;
        if (!float.IsFinite(actual.x + actual.y) || Math.Abs(actual.x - width) > 2 * scale
            || Math.Abs(actual.y - height) > 2 * scale)
        {
            Hide();
            Plugin.Instance.Report("Retaliation layout", new InvalidOperationException(
                $"Unexpected projection {actual.x:F1}x{actual.y:F1}; expected {width:F1}x{height:F1}; units={unitsX:G4},{unitsY:G4}"));
            return false;
        }
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
