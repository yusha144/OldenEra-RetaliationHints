using Hex.Session.Battle;
using Hex.UI;
using UnityEngine;

namespace OldenEra.RetaliationHints;

public sealed class RetaliationOverlay : MonoBehaviour
{
    private CursorHint _hint;
    private readonly BattleUiLookup _uiLookup = new();
    private HintBinding _binding;
    private int _battleGeneration = -1;
    private float _nextCalculation;
    private IntPtr _lastAttacker, _lastTarget, _lastAbility, _lastDestination, _lastCounter;
    private DamageRange _lastDamage;
    private bool _lastCanCounter;
    private bool _reportedPreview;

    public RetaliationOverlay(IntPtr pointer) : base(pointer) { }

    public void LateUpdate()
    {
        try
        {
            PreviewCapture.ObserveBattle();
            CombatAudit.Tick();
            if (_battleGeneration != PreviewCapture.BattleGeneration)
            {
                ReleaseHint(); _uiLookup.Reset();
                _battleGeneration = PreviewCapture.BattleGeneration;
            }
            if (!Plugin.Instance.Enabled.Value || !elb.ckia) { Hide(); return; }
            Plugin.Instance.TraceOnce("battle-update", "Retaliation display updater is running in a battle.");
            var context = PreviewCapture.Current;
            if (context == null) { Hide(); return; }
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
            var battleUi = _uiLookup.Find();
            if (!battleUi) { Hide("battle-ui-unavailable"); return; }
            if (BattleUiLookup.ModalVisible(battleUi)) { Hide("battle-window-visible"); return; }
            Plugin.Instance.TraceOnce("battle-ui-resolved", $"Active battle UI: {battleUi.Pointer}, remembered mode={battleUi.ccgc}, battle={elb.lastBattleId}.");
            var cursorUi = battleUi.cszk;
            if (!cursorUi || !cursorUi.rootCanvasRect
                || !cursorUi.simpleTooltip || !cursorUi.simpleTooltip.targetsText)
            { Hide("cursor-resources-unavailable"); return; }
            var text = cursorUi.simpleTooltip.targetsText;
            if (!text.font || !text.fontSharedMaterial) { Hide("cursor-font-unavailable"); return; }
            var binding = new HintBinding(_battleGeneration, cursorUi.Pointer, cursorUi.rootCanvasRect.Pointer,
                text.font.Pointer, text.fontSharedMaterial.Pointer);
            if (_binding != binding || _hint == null || !_hint.Root)
            {
                ReleaseHint(); _binding = binding;
                _hint = new(cursorUi, forecast.killedSquadsIcon);
            }
            if (_lastAttacker != context.Attacker.Pointer || _lastTarget != context.Defender.Pointer
                || _lastAbility != context.Attack.Pointer || _lastDestination != (context.Destination?.Pointer ?? IntPtr.Zero)
                || _lastCounter != (context.Counter?.Pointer ?? IntPtr.Zero) || _lastDamage != context.Damage
                || _lastCanCounter != context.CanCounter) _nextCalculation = 0;
            if (Time.unscaledTime >= _nextCalculation)
            {
                _nextCalculation = Time.unscaledTime + .12f;
                _lastAttacker = context.Attacker.Pointer; _lastTarget = context.Defender.Pointer;
                _lastAbility = context.Attack.Pointer;
                _lastDestination = context.Destination?.Pointer ?? IntPtr.Zero;
                _lastCounter = context.Counter?.Pointer ?? IntPtr.Zero; _lastDamage = context.Damage;
                _lastCanCounter = context.CanCounter;
                AuditTrace trace = CombatAudit.Enabled ? new() : null;
                try
                {
                    LossRange? losses = context.CanCounter ? null : new LossRange(0, 0, false);
                    var result = HintResult.ForPreview(context.CanCounter,
                        () =>
                        {
                            var value = NativeForecast.Calculate(context, context.Damage, trace);
                            losses = value;
                            return value;
                        });
                    _hint.Set(result.Text, result.Danger);
                    CombatAudit.Calculated(context, losses, null, trace);
                    Plugin.Instance.TraceOnce(context.CanCounter ? "loss-calculated" : "no-counter-shown",
                        "First retaliation result: " + result.Text);
                }
                catch (ForecastUnavailableException error)
                {
                    _hint.Set(HintResult.Unavailable, true);
                    CombatAudit.Calculated(context, null, error.Message, trace);
                    Plugin.Instance.TraceOnce("unsupported-" + error.Message, "Not simulated: " + error.Message);
                }
                catch (Exception error)
                {
                    _hint.Set(HintResult.Unavailable, true);
                    CombatAudit.Calculated(context, null, error.GetType().Name, trace);
                    Plugin.Instance.Report("Unsupported retaliation estimate", error);
                }
            }
            bool placed = _hint.Place(Input.mousePosition, Plugin.Instance.TextScale.Value);
            if (placed) CombatAudit.Presented(context); else CombatAudit.Hidden();
            if (placed && !_reportedPreview)
            {
                _reportedPreview = true;
                Plugin.Instance.Log.LogInfo($"Retaliation hint prepared in battle {elb.lastBattleId}: " + _hint.Diagnostic);
            }
        }
        catch (Exception error) { Hide(); Plugin.Instance.Report("Retaliation display", error); }
    }

    private void Hide(string reason = null)
    {
        CombatAudit.Hidden();
        _hint?.Hide(); _nextCalculation = 0;
        if (reason != null) Plugin.Instance.TraceOnce(reason, "Captured preview hidden: " + reason);
    }
    private void ReleaseHint()
    {
        _hint?.Hide(); _hint?.Destroy(); _hint = null;
        _binding = default; _nextCalculation = 0; _reportedPreview = false;
    }
    public void OnDestroy() { ReleaseHint(); PreviewCapture.Reset(); }
}
