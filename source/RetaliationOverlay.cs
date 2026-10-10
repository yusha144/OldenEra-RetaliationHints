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
            var context = PreviewCapture.Current;
            if (!Plugin.Instance.Enabled.Value || !elb.ckia || context == null || !context.CanCounter
                || !PreviewCapture.Same(elb.ckia.ckid, context.Hover)
                || !PreviewCapture.Same(context.Hover.clcx, context.Defender.ctvn)
                || !PreviewCapture.Same(context.Hover.cldm, context.Attacker)
                || !PreviewCapture.Same(context.Hover.cldl, context.Attack)
                || !context.TargetView || !context.TargetView.gameObject.activeInHierarchy
                || !PreviewCapture.Samples.TryGetValue(context.TargetView.Pointer, out var sample)
                || !sample.Damage.Valid)
            { Hide(); return; }

            var forecast = context.TargetView.healDamageForecast;
            if (!forecast || !forecast.gameObject.activeInHierarchy || !forecast.cg
                || !forecast.cg.gameObject.activeInHierarchy || forecast.cg.alpha < .01f)
            { Hide(); return; }
            var battleUi = BattleUIController.me;
            if (!battleUi || !battleUi.gameObject.activeInHierarchy || battleUi.ccgc != TypeBattleUI.Normal)
            { Hide(); return; }
            var cursorUi = battleUi.cszk;
            if (!cursorUi || !cursorUi.gameObject.activeInHierarchy || !cursorUi.rootCanvasRect
                || !cursorUi.simpleTooltip || !cursorUi.simpleTooltip.targetsText)
            { Hide(); return; }
            if (!_cursorUi || _cursorUi.Pointer != cursorUi.Pointer || _hint == null || !_hint.Root)
            {
                _hint?.Destroy(); _cursorUi = cursorUi;
                _hint = new(cursorUi, forecast.killedSquadsIcon); _nextCalculation = 0;
            }
            if (_lastAttacker != context.Attacker.Pointer || _lastTarget != context.Defender.Pointer
                || _lastAbility != context.Attack.Pointer || _lastDestination != (context.Destination?.Pointer ?? IntPtr.Zero)
                || _lastCounter != (context.Counter?.Pointer ?? IntPtr.Zero) || _lastDamage != sample.Damage) _nextCalculation = 0;
            if (Time.unscaledTime >= _nextCalculation)
            {
                _nextCalculation = Time.unscaledTime + .12f;
                _lastAttacker = context.Attacker.Pointer; _lastTarget = context.Defender.Pointer;
                _lastAbility = context.Attack.Pointer;
                _lastDestination = context.Destination?.Pointer ?? IntPtr.Zero;
                _lastCounter = context.Counter?.Pointer ?? IntPtr.Zero; _lastDamage = sample.Damage;
                try
                {
                    var loss = NativeForecast.Calculate(context, sample.Damage);
                    _hint.Set((loss.BeforeAttack ? "До атаки ~ " : "Ответка ~ ") + loss.Numbers, loss.Max > 0);
                }
                catch (Exception error)
                {
                    _hint.Set("Ответка: расчёт недоступен", true);
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

    private void Hide() { _hint?.Hide(); _nextCalculation = 0; }
    public void OnDestroy() { _hint?.Destroy(); PreviewCapture.Current = null; PreviewCapture.Samples.Clear(); }
}
