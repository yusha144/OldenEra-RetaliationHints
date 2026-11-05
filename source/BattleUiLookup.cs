using Hex.Session.Battle;
using Hex.UI;
using UnityEngine;

namespace OldenEra.RetaliationHints;

internal sealed class BattleUiLookup
{
    private BattleUIController _current;
    private float _nextSearch;

    internal void Reset() { _current = null; _nextSearch = 0; }

    internal BattleUIController Find()
    {
        // OnEnable updates the singleton, but an old/inactive object must not pin
        // the mod to the first battle. Never create or activate native UI here.
        var singleton = BattleUIController.me;
        if (Usable(singleton)) _current = singleton;
        if (Usable(_current)) return _current;
        if (Time.unscaledTime < _nextSearch) return null;
        _nextSearch = Time.unscaledTime + 1;
        foreach (var view in UnityEngine.Object.FindObjectsOfType<BattleUIController>())
            if (Usable(view)) { _current = view; return view; }
        return null;
    }

    private static bool Usable(BattleUIController view)
    {
        if (!view || !view.gameObject.activeInHierarchy || !view.cvrr || !view.cvrr.rootCanvasRect
            || !view.cvrr.rootCanvasRect.gameObject.activeInHierarchy) return false;
        var canvas = view.cvrr.rootCanvasRect.GetComponentInParent<Canvas>();
        return canvas && canvas.isActiveAndEnabled && canvas.rootCanvas && canvas.rootCanvas.isActiveAndEnabled;
    }

    internal static bool ModalVisible(BattleUIController view)
    {
        // cegf is a remembered mode, not visibility: pooled UI can retain it
        // between battles. Inspect actual windows without resetting game state.
        var log = view.battleLog;
        if (log && log.cpqd && log.logWindowCanvasGroup && log.logWindowCanvasGroup.gameObject.activeInHierarchy
            && log.logWindowCanvasGroup.alpha > .01f) return true;
        var windows = cqo.cvsr;
        if (windows?.cepj != null)
            foreach (var pair in windows.cepj) if (Blocks(pair.Value)) return true;
        if (windows?.cepk != null)
            foreach (var window in windows.cepk) if (Blocks(window)) return true;
        return false;
    }

    private static bool Blocks(BhScreen window)
    {
        if (!window || !window.gameObject.activeInHierarchy || !window.cvsd
            || !window.canvas || !window.canvas.isActiveAndEnabled || (window.cg && window.cg.alpha <= .01f))
            return false;
        string name = window.GetIl2CppType().Name;
        return name == "BhUnitWindow" || name == "BhMagicBook" || name == "BhBattleEnd";
    }
}
