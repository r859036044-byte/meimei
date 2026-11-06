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
    [Export] public float   EmoteCooldown  = 1.0f;                    // 兩個表情之間最短間隔（愛心可以疊）
    [Export] public float   EmoteGapPx     = 10f;                     // 氣泡離前髮邊緣的距離（螢幕 px）；左右邊由 GirlBubbleSideMode 與對話氣泡決定
    [Export] public float   EmoteMoodStep  = 2f;                      // 心情累積變化多少才冒愛心／毛線團
    [Export] public float   EmoteHoldSec   = 1.2f;                    // 驚嘆號／問號／毛線團停留秒數

    float _emoteCd;
    float _emoteMoodPrev = float.NaN, _emoteMoodAcc;
    float _emoteWarm = 1.0f;   // 剛進場（讀檔把心情設成存檔值）先不要冒

    /// <summary>頭旁邊冒出一個氣泡表情。</summary>
    public void ShowEmote(EmoteBubble.Kind kind)
    {
        if (!EmoteEnabled || UiRoot == null) return;
        if (kind != EmoteBubble.Kind.Heart && _emoteCd > 0f) return;
        if (kind != EmoteBubble.Kind.Heart) _emoteCd = EmoteCooldown;
        // 表情放頭的左右側；女主對話氣泡正在顯示就放另一邊，免得疊在一起
        bool girlShown = _bubbleGirl != null && IsInstanceValid(_bubbleGirl) && _bubbleGirl.Visible && _bubbleGirl.Modulate.A > 0.1f;
        int side = girlShown ? -_girlSide : PickGirlSide();
        EmoteBubble.Spawn(UiRoot, kind, () => EmoteAnchor(side), EmoteHoldSec);
    }

    /// <summary>表情氣泡底部中心（愛心＝飄出起點）：頭的左／右側，氣泡垂直中心大約對臉中心。</summary>
    Vector2 EmoteAnchor(int side)
    {
        Vector2 vp = GetViewport().GetVisibleRect().Size;
        Vector2 edge = HeadSideScreen(side) ?? new Vector2(vp.X * 0.5f + side * 220f, vp.Y * 0.3f);
        Vector2 p = edge + new Vector2(side * (EmoteGapPx + 36f), 38f);   // 氣泡寬 72 → 中心離邊緣 36
        return new Vector2(Mathf.Clamp(p.X, 40f, vp.X - 40f), Mathf.Clamp(p.Y, 90f, vp.Y - 20f));
    }

    void UpdateEmotes(float dt)
    {
        _emoteCd -= dt;
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
