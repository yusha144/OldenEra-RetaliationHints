using Hex.Session.Battle;
using UnityEngine;

namespace OldenEra.RetaliationHints;

public sealed class RetaliationOverlay : MonoBehaviour
{
    private UnitBarHint _hint;
    private BhBattleUnitInfoView _bar;
    private float _nextCalculation;
    private IntPtr _lastAttacker, _lastTarget, _lastAbility;
    private bool _reportedPreview;

    public RetaliationOverlay(IntPtr pointer) : base(pointer) { }

    public void LateUpdate()
    {
        try
        {
            var context = PreviewCapture.Current;
            if (!Plugin.Instance.Enabled.Value || !elb.ckia || context == null
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
            var bar = context.Attacker.cmww?.TryCast<eml>()?.ckom?.cmot;
            if (!bar || !bar.gameObject.activeInHierarchy || !bar.count || !bar.count.textMesh)
            { Hide(); return; }
            if (!_bar || _bar.Pointer != bar.Pointer || _hint == null || !_hint.Root)
            {
                _hint?.Destroy(); _bar = bar; _hint = new(bar); _nextCalculation = 0;
            }
            if (_lastAttacker != context.Attacker.Pointer || _lastTarget != context.Defender.Pointer
                || _lastAbility != context.Attack.Pointer) _nextCalculation = 0;
            if (Time.unscaledTime >= _nextCalculation)
            {
                _nextCalculation = Time.unscaledTime + .12f;
                _lastAttacker = context.Attacker.Pointer; _lastTarget = context.Defender.Pointer;
                _lastAbility = context.Attack.Pointer;
                try
                {
                    var loss = NativeForecast.Calculate(context, sample.Damage);
                    _hint.Set(!context.CanCounter ? "Ответки нет" :
                        (loss.BeforeAttack ? "До атаки ~ " : "Ответка ~ ") + loss.Numbers, loss.Max > 0);
                    if (!_reportedPreview)
                    {
                        _reportedPreview = true;
                        Plugin.Instance.Log.LogInfo("First retaliation preview displayed successfully.");
                    }
                }
                catch (Exception error)
                {
                    _hint.Set("Ответка: расчёт недоступен", true);
                    Plugin.Instance.Report("Unsupported retaliation estimate", error);
                }
            }
            _hint.Place(Plugin.Instance.TextScale.Value);
        }
        catch (Exception error) { Hide(); Plugin.Instance.Report("Retaliation display", error); }
    }

    private void Hide() { _hint?.Hide(); _nextCalculation = 0; }
    public void OnDestroy() { _hint?.Destroy(); PreviewCapture.Current = null; PreviewCapture.Samples.Clear(); }
}
