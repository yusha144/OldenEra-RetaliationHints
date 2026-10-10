using Hex.Session.Battle;
using Hex.UI;
using UnityEngine;

namespace OldenEra.RetaliationHints;

public sealed class RetaliationOverlay : MonoBehaviour
{
    private CursorHint _hint;
    private BhCursorTooltips _cursorUi;
    private float _nextCalculation;
    private IntPtr _lastAttacker, _lastTarget, _lastAbility, _lastDestination, _lastCounter;
    private DamageRange _lastDamage;
    private bool _reportedPreview;

    public RetaliationOverlay(IntPtr pointer) : base(pointer) { }

    public void LateUpdate()
    {
        try
        {
            PreviewCapture.ObserveBattle();
            if (!Plugin.Instance.Enabled.Value || !elb.ckia) { Hide(); return; }
            Plugin.Instance.TraceOnce("battle-update", "Retaliation display updater is running in a battle.");
            var context = PreviewCapture.Current;
            if (context == null || !context.CanCounter) { Hide(); return; }
            if (!PreviewCapture.Same(elb.ckia.ckid, context.Hover)
                || !PreviewCapture.Same(context.Hover.clcx, context.TargetHex)
                || !PreviewCapture.Occupies(context.Defender, context.TargetHex)
                || !PreviewCapture.Same(context.Hover.cldm, context.Attacker)
                || !PreviewCapture.Same(context.Hover.cldl, context.Attack))
            { Hide("hover-changed"); return; }
            if (!context.TargetView || !context.TargetView.gameObject.activeInHierarchy || !context.Damage.Valid)
            { Hide("target-panel-unavailable"); return; }

            var forecast = context.TargetView.healDamageForecast;
            if (!forecast || !forecast.gameObject.activeInHierarchy || !forecast.cg
                || !forecast.cg.gameObject.activeInHierarchy || forecast.cg.alpha < .01f)
            { Hide("native-forecast-hidden"); return; }
            var battleUi = BattleUIController.me;
            if (!battleUi || !battleUi.gameObject.activeInHierarchy || battleUi.ccgc != TypeBattleUI.Normal)
            { Hide("battle-ui-not-normal"); return; }
            var cursorUi = battleUi.cszk;
            if (!cursorUi || !cursorUi.rootCanvasRect
                || !cursorUi.simpleTooltip || !cursorUi.simpleTooltip.targetsText)
            { Hide("cursor-resources-unavailable"); return; }
            if (!_cursorUi || _cursorUi.Pointer != cursorUi.Pointer || _hint == null || !_hint.Root)
            {
                _hint?.Destroy(); _cursorUi = cursorUi;
                _hint = new(cursorUi, forecast.killedSquadsIcon); _nextCalculation = 0;
            }
            if (_lastAttacker != context.Attacker.Pointer || _lastTarget != context.Defender.Pointer
                || _lastAbility != context.Attack.Pointer || _lastDestination != (context.Destination?.Pointer ?? IntPtr.Zero)
                || _lastCounter != (context.Counter?.Pointer ?? IntPtr.Zero) || _lastDamage != context.Damage) _nextCalculation = 0;
            if (Time.unscaledTime >= _nextCalculation)
            {
                _nextCalculation = Time.unscaledTime + .12f;
                _lastAttacker = context.Attacker.Pointer; _lastTarget = context.Defender.Pointer;
                _lastAbility = context.Attack.Pointer;
                _lastDestination = context.Destination?.Pointer ?? IntPtr.Zero;
                _lastCounter = context.Counter?.Pointer ?? IntPtr.Zero; _lastDamage = context.Damage;
                try
                {
                    var loss = NativeForecast.Calculate(context, context.Damage);
                    _hint.Set((loss.BeforeAttack ? "Потери до атаки ~ " : "Потери ~ ") + loss.Numbers, loss.Max > 0);
                    Plugin.Instance.TraceOnce("loss-calculated", $"First casualty range: {loss.Numbers}, preemptive={loss.BeforeAttack}.");
                }
                catch (ForecastUnavailableException error)
                {
                    _hint.Set("Потери: " + error.Message, true);
                    Plugin.Instance.TraceOnce("unsupported-" + error.Message, "Not simulated: " + error.Message);
                }
                catch (Exception error)
                {
                    _hint.Set("Потери: расчёт недоступен", true);
                    Plugin.Instance.Report("Unsupported retaliation estimate", error);
                }
            }
            if (_hint.Place(Input.mousePosition, Plugin.Instance.TextScale.Value) && !_reportedPreview)
            {
                _reportedPreview = true;
                Plugin.Instance.Log.LogInfo("First retaliation hint prepared beside attack cursor: " + _hint.Diagnostic);
            }
        }
        catch (Exception error) { Hide(); Plugin.Instance.Report("Retaliation display", error); }
    }

    private void Hide(string reason = null)
    {
        _hint?.Hide(); _nextCalculation = 0;
        if (reason != null) Plugin.Instance.TraceOnce(reason, "Captured preview hidden: " + reason);
    }
    public void OnDestroy() { _hint?.Destroy(); PreviewCapture.Reset(); }
}
