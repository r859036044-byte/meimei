using Godot;

/// <summary>
/// 莉奈頭旁的氣泡表情（EmoteBubble.cs）：
///   愛心　好感事件：心情累積上升 EmoteMoodStep 以上（安撫成功、摸頭回心情、不理正常話…）
///   毛線團　不好的事：心情累積下降 EmoteMoodStep 以上、濕度不夠就突破
///   驚嘆號　突破（濕度足夠）、高潮、剛碰到乳頭
///   問號　　剛開始摸頭
/// 要在別的地方觸發：ShowEmote(EmoteBubble.Kind.Heart)。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("氣泡表情")]
    [Export] public bool    EmoteEnabled   = true;
    [Export] public float   EmoteCooldown  = 4.0f;                    // 驚嘆號／問號／毛線團：兩個表情之間最短間隔（秒）。想更少就調大
    [Export] public float   EmoteHeartCooldown = 3.0f;                // 愛心：兩顆愛心之間最短間隔（秒）。0＝不限制（舊行為，可以一直疊）
    [Export] public float   EmoteNearInsetPx = 10f;                   // 表情氣泡離對話框靠莉奈那一端的內縮（螢幕 px）
    [Export] public float   EmoteBelowGapPx = 8f;                     // 表情氣泡離對話框下緣的距離（螢幕 px）
    [Export] public float   EmoteGapPx     = 10f;                     // 氣泡離前髮邊緣的距離（螢幕 px）；左右邊由 GirlBubbleSideMode 與對話氣泡決定
    [Export] public float   EmoteMoodStep  = 4f;                      // 心情累積變化多少才冒愛心／毛線團（越大越少冒）
    [Export] public float   EmoteHoldSec   = 1.2f;                    // 驚嘆號／問號／毛線團停留秒數
    [Export(PropertyHint.Range, "0.3,3,0.05")] public float EmoteZoomMul = 1f;   // 表情氣泡大小的額外倍率；氣泡會跟著鏡頭縮放（跟對話氣泡同一套曲線，見 BubbleK）。覺得太小就調大

    float _emoteCd, _emoteHeartCd;
    float _emoteMoodPrev = float.NaN, _emoteMoodAcc;
    float _emoteWarm = 1.0f;   // 剛進場（讀檔把心情設成存檔值）先不要冒

    /// <summary>頭旁邊冒出一個氣泡表情。</summary>
    public void ShowEmote(EmoteBubble.Kind kind)
    {
        if (!EmoteEnabled || UiRoot == null) return;
        if (kind == EmoteBubble.Kind.Heart)
        {
            if (_emoteHeartCd > 0f) return;
            _emoteHeartCd = EmoteHeartCooldown;
        }
        else
        {
            if (_emoteCd > 0f) return;
            _emoteCd = EmoteCooldown;
        }
        if (kind == EmoteBubble.Kind.Heart) TriggerMouthSmile();   // 出現愛心：嘴巴換成笑（Mouth.cs）
        // 有對話框：表情氣泡在對話框正下方（不蓋到字）；沒有對話框：放頭的左右側
        int side = PickGirlSide();
        EmoteBubble.Spawn(UiRoot, kind, () => EmoteAnchor(side), EmoteHoldSec, EmoteFaceTarget, () => BubbleK() * EmoteZoomMul);   // 大小跟著鏡頭縮放
    }

    /// <summary>表情氣泡尾巴要指向的點：莉奈的臉中心（跟對話框尾巴用同一個點）。</summary>
    Vector2 EmoteFaceTarget()
    {
        if (_canvas != null && Layers.TryGetValue("前髮", out var hair) && hair != null && IsInstanceValid(hair))
            return CanvasToScreen(hair, new Vector2((HeadEdgeLeftX + HeadEdgeRightX) * 0.5f, HeadCenterY));
        return GetViewport().GetVisibleRect().Size * 0.5f;
    }

    /// <summary>表情氣泡底部中心。對話框顯示中 → 對話框正下方（水平對齊對話框中心）；否則 → 頭的左／右側。</summary>
    Vector2 EmoteAnchor(int side)
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        if (_bubbleGirl != null && IsInstanceValid(_bubbleGirl) && _bubbleGirl.Visible && _bubbleGirl.Modulate.A > 0.05f)
        {
            // 在對話框正下方，水平靠「靠近莉奈」那一端（對話框在她左邊 → 靠右端；在她右邊 → 靠左端），不要離她太遠
            float nearX = _girlSide > 0 ? _bubbleGirl.Position.X + 36f + EmoteNearInsetPx
                                        : _bubbleGirl.Position.X + _bubbleGirl.Size.X - 36f - EmoteNearInsetPx;
            Vector2 p = new Vector2(nearX, _bubbleGirl.Position.Y + _bubbleGirl.Size.Y + EmoteBelowGapPx + 72f);   // 氣泡高 76、底部中心在 y+72
            return new Vector2(Mathf.Clamp(p.X, 40f, vp.X - 40f), Mathf.Min(p.Y, vp.Y - 8f));
        }
        Vector2 edge = HeadSideScreen(side) ?? new Vector2(vp.X * 0.5f + side * 220f, vp.Y * 0.3f);
        Vector2 q = edge + new Vector2(side * (EmoteGapPx + 36f), 38f);   // 氣泡寬 72 → 中心離邊緣 36
        return new Vector2(Mathf.Clamp(q.X, 40f, vp.X - 40f), Mathf.Clamp(q.Y, 90f, vp.Y - 20f));
    }

    void UpdateEmotes(float dt)
    {
        _emoteCd -= dt;
        _emoteHeartCd -= dt;
        if (_emoteWarm > 0f) { _emoteWarm -= dt; _emoteMoodPrev = Mood; return; }
        if (float.IsNaN(_emoteMoodPrev)) { _emoteMoodPrev = Mood; return; }

        float d = Mood - _emoteMoodPrev;
        _emoteMoodPrev = Mood;
        if (Mathf.IsZeroApprox(d)) return;
        if ((d > 0f) != (_emoteMoodAcc > 0f)) _emoteMoodAcc = 0f;   // 方向反了就重算
        _emoteMoodAcc += d;
        if (_emoteMoodAcc >= EmoteMoodStep) { ShowEmote(EmoteBubble.Kind.Heart); _emoteMoodAcc = 0f; }
        else if (_emoteMoodAcc <= -EmoteMoodStep) { ShowEmote(EmoteBubble.Kind.Tangle); _emoteMoodAcc = 0f; }
    }

    /// <summary>UpdateReactions 偵測到「剛開始碰某個部位」時呼叫（Play.cs）。</summary>
    void EmoteOnTouchStart(bool prevNipple, bool nipple, bool prevHead, bool head)
    {
        if (nipple && !prevNipple) ShowEmote(EmoteBubble.Kind.Exclaim);
        else if (head && !prevHead) ShowEmote(EmoteBubble.Kind.Question);
    }
}
