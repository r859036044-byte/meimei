using Godot;
using System;

/// <summary>
/// TrainingRoomRuntime — 男主對話框：
///   1) 選項：「教訓她／不理她」這類選擇做在男主的對話框裡（按鈕放在氣泡內、字的下面）。
///   2) 動態縮小：男主氣泡原本是固定倍率（BubbleScalePlayer），字多或有選項時會變得很大。
///      現在每幀看氣泡實際大小，超過畫面寬高的上限（BubblePlayerMaxW／MaxH）就自動縮小，
///      最多縮到 BubblePlayerMinFit 倍；變回小的時候自動放大回來。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("男主對話框")]
    [Export(PropertyHint.Range, "0.1,0.9,0.01")] public float BubblePlayerMaxW   = 0.30f;   // 男主氣泡最寬佔畫面寬度的比例，超過就縮小
    [Export(PropertyHint.Range, "0.05,0.9,0.01")] public float BubblePlayerMaxH  = 0.20f;   // 男主氣泡最高佔畫面高度的比例，超過就縮小
    [Export(PropertyHint.Range, "0.2,1,0.01")] public float BubblePlayerMinFit   = 0.50f;   // 最多縮到原倍率的幾成
    [Export] public float BubblePlayerFitSpeed = 10f;                                         // 縮放變化的速度（越大越快）
    [Export] public string BratChoicePrompt = "要怎麼回應她？";                                // 選項出現時，男主氣泡上的提示字

    float _playerFit = 1f;                 // 動態縮小係數（0~1），乘在 BubbleScalePlayer 上
    HBoxContainer _playerChoiceRow;
    bool _playerChoiceActive, _playerChoiceSetting;

    /// <summary>每幀（LayoutBubbles 開頭）：依男主氣泡的實際大小調整縮小係數。</summary>
    void UpdatePlayerFit(float dt)
    {
        if (_bubblePlayer == null || !IsInstanceValid(_bubblePlayer) || !_bubblePlayer.Visible || _bubblePlayer.Size.X < 1f) return;
        float kCur = Mathf.Max(0.01f, BubbleScalePlayer * _playerFit);
        Vector2 nat = _bubblePlayer.Size / kCur;                      // 每 1 倍 K 的氣泡大小（字、邊距都跟 K 成正比）
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        float kMax = Mathf.Min(BubblePlayerMaxW * vp.X / Mathf.Max(1f, nat.X), BubblePlayerMaxH * vp.Y / Mathf.Max(1f, nat.Y));
        float target = Mathf.Clamp(kMax / Mathf.Max(0.01f, BubbleScalePlayer), BubblePlayerMinFit, 1f);
        _playerFit = Mathf.Lerp(_playerFit, target, 1f - Mathf.Exp(-dt * BubblePlayerFitSpeed));
    }

    /// <summary>在男主氣泡裡顯示選項（按鈕在字的下面）。選了之後由各按鈕的 action 處理，並自己呼叫 HidePlayerChoices()。</summary>
    void ShowPlayerChoices(string prompt, string[] labels, Action[] actions, float holdSec)
    {
        _playerChoiceSetting = true;
        _playerChoiceActive = false;   // 讓 ShowBubble 的「選項中不被蓋掉」檢查放行這一次
        ShowBubble(ref _bubblePlayer, ref _bubblePlayerLbl, string.IsNullOrEmpty(prompt) ? "…" : prompt, true);
        _playerChoiceSetting = false;
        if (_bubblePlayer == null || !IsInstanceValid(_bubblePlayer)) return;

        // 第一次：字（Label）搬進 VBox，底下放按鈕列。外形（BubbleShape）是 ShowBehindParent，不受影響。
        if (_playerChoiceRow == null || !IsInstanceValid(_playerChoiceRow))
        {
            var vbox = new VBoxContainer { Name = "Content", MouseFilter = Control.MouseFilterEnum.Ignore };
            vbox.AddThemeConstantOverride("separation", 6);
            _bubblePlayer.AddChild(vbox);
            _bubblePlayerLbl.GetParent()?.RemoveChild(_bubblePlayerLbl);
            vbox.AddChild(_bubblePlayerLbl);
            _playerChoiceRow = new HBoxContainer { Name = "Choices", MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
            vbox.AddChild(_playerChoiceRow);
        }
        foreach (var c in _playerChoiceRow.GetChildren()) { _playerChoiceRow.RemoveChild(c); c.QueueFree(); }
        for (int i = 0; i < labels.Length; i++)
        {
            var act = actions[i];
            _playerChoiceRow.AddChild(BratMakeBtn(labels[i], () => { act?.Invoke(); }));
        }
        _playerChoiceRow.Visible = true;
        _playerChoiceActive = true;

        _bubblePlayer.SetMeta("hide_t", holdSec);   // 選項留到回應時間結束
        _bubblePlayer.RemoveMeta("bk");             // 強制下一幀重算大小（按鈕跟著倍率）
        RescalePlayerChoiceRow(BubbleK(true));
    }

    /// <summary>收掉選項。hideBubble＝false 時氣泡留著（選完馬上要換成男主說的那句話，不能先淡出）。</summary>
    void HidePlayerChoices(bool hideBubble = true)
    {
        if (!_playerChoiceActive) return;
        _playerChoiceActive = false;
        if (_playerChoiceRow != null && IsInstanceValid(_playerChoiceRow)) _playerChoiceRow.Visible = false;
        if (_bubblePlayer != null && IsInstanceValid(_bubblePlayer))
        {
            _bubblePlayer.RemoveMeta("bk");
            _bubblePlayer.RemoveMeta("hide_t");
            if (hideBubble) HideBubble(_bubblePlayer);
        }
    }

    /// <summary>男主氣泡大小改變時，選項按鈕的字與大小一起跟著縮放。</summary>
    void RescalePlayerChoiceRow(float K)
    {
        if (_playerChoiceRow == null || !IsInstanceValid(_playerChoiceRow)) return;
        _playerChoiceRow.AddThemeConstantOverride("separation", Mathf.RoundToInt(8f * K));
        foreach (var n in _playerChoiceRow.GetChildren())
            if (n is Button b)
            {
                b.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(18f * K));
                b.CustomMinimumSize = new Vector2(84f, 30f) * K;
            }
        _bubblePlayer?.ResetSize();
    }
}
