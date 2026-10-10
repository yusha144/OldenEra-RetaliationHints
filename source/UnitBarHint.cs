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
    private readonly Il2CppStructArray<Vector3> _corners = new(4);
    private string _text = "";

    internal UnitBarHint(BhBattleUnitInfoView bar)
    {
        _bar = bar; _parent = bar.transform.Cast<RectTransform>();
        Root = Rect("RetaliationHints.LocalEstimate", _parent);
        var group = Root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false; group.interactable = false;
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
        _label.alignment = TextAlignmentOptions.MidlineLeft;
        _label.margin = Vector4.zero;
        Hide();
    }

    internal void Set(string text, bool danger)
    {
        _text = text; _label.text = text;
        _label.color = danger ? new(1f, .82f, .64f, 1) : new(.84f, .89f, .76f, 1);
    }

    internal void Place(float setting)
    {
        if (!Root || !_parent || !_bar.count.textMesh) return;
        Root.gameObject.SetActive(true);
        var canvas = _parent.GetComponentInParent<Canvas>();
        var camera = !canvas || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null
            : canvas.worldCamera ? canvas.worldCamera : canvas.rootCanvas.worldCamera;
        var countRect = _bar.count.textMesh.rectTransform;
        countRect.GetWorldCorners(_corners);
        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue;
        foreach (var corner in _corners)
        {
            var p = RectTransformUtility.WorldToScreenPoint(camera, corner);
            left = Math.Min(left, p.x); right = Math.Max(right, p.x); bottom = Math.Min(bottom, p.y);
        }
        if (_bar.healthInfo && _bar.healthInfo.gameObject.activeInHierarchy)
        {
            _bar.healthInfo.transform.Cast<RectTransform>().GetWorldCorners(_corners);
            foreach (var corner in _corners) bottom = Math.Min(bottom, RectTransformUtility.WorldToScreenPoint(camera, corner).y);
        }
        float scale = Mathf.Clamp(Screen.height / 1080f, .8f, 2f) * Mathf.Clamp(setting, .7f, 1.8f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(left, bottom), camera, out var p0);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(left, bottom + 100), camera, out var p1);
        float units = Math.Abs(p1.y - p0.y) / 100f;
        if (units <= 0 || !float.IsFinite(units)) { Hide(); return; }
        _label.fontSize = 18 * scale * units;
        float textWidth = _label.GetPreferredValues(_text).x / units;
        float iconWidth = _icon.sprite ? 22 * scale : 0;
        float width = textWidth + iconWidth + 18 * scale, height = 30 * scale;
        float x = Mathf.Clamp((left + right - width) / 2, 3, Math.Max(3, Screen.width - width - 3));
        float y = Mathf.Clamp(bottom - height - 4 * scale, 3, Math.Max(3, Screen.height - height - 3));
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, new Vector2(x + width / 2, y + height / 2), camera, out var center);
        Root.anchoredPosition = center - _parent.rect.center;
        Root.sizeDelta = new(width * units, height * units);
        _icon.enabled = _icon.sprite;
        _icon.rectTransform.sizeDelta = new(20 * scale * units, 20 * scale * units);
        _icon.rectTransform.anchoredPosition = new((-width / 2 + 16 * scale) * units, 0);
        _label.rectTransform.sizeDelta = new((width - iconWidth - 12 * scale) * units, height * units);
        _label.rectTransform.anchoredPosition = new(iconWidth / 2 * units, 0);
    }

    internal void Hide() { if (Root) Root.gameObject.SetActive(false); }
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
