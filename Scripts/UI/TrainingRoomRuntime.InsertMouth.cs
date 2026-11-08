using Godot;
using System.Collections.Generic;

/// <summary>
/// TrainingRoomRuntime — 插入時與拔出後的表情輔助：
/// 嘴巴圖層的顯示與縮放（小嘴巴、小嘴吐舌、200% 大叫放大、倒笑壓扁說話）
/// 已統一由 TrainingRoomRuntime.Mouth.cs 全權控制，此處不再覆蓋嘴巴的 Scale，
/// 避免把倒笑壓扁（負 Y 縮放）或 200% 放大效果重置掉。
/// </summary>
public partial class TrainingRoomRuntime
{
    [ExportGroup("插入時嘴巴大小")]
    [Export(PropertyHint.Range, "1,3,0.01")]   public float InsertMouthBigScale = 1.5f;
    [Export(PropertyHint.Range, "1,200,1")]    public float InsertMouthComfortFull = 100f;
    [Export(PropertyHint.Range, "0.5,20,0.1")] public float InsertMouthLerpSpeed = 5f;

    [Export(PropertyHint.Range, "0.5,1,0.01")] public float MouthUndressScale = 0.85f;
    [Export(PropertyHint.Range, "0.2,3,0.05")] public float MouthUndressFull = 1f;
    [Export(PropertyHint.Range, "0.4,1,0.01")] public float MouthFullUndressScaleX = 0.8f;

    void UpdateInsertMouth(float dt)
    {
        // 嘴巴的縮放與切換統一由 Mouth.cs 控制，此處不覆蓋 c.Scale。
    }

    // ── 拔出後的表情：維持小嘴巴（插入2）──
    [ExportGroup("拔出後表情")]
    [Export] public string[] PullOutExprSeq  = { "插入2" };
    [Export] public float[]  PullOutExprSecs = { 2f };
    int _pullExprGen;

    async System.Threading.Tasks.Task PullOutExprSeqAsync()
    {
        int gen = ++_pullExprGen;
        string prev = null;
        for (int i = 0; i < PullOutExprSeq.Length; i++)
        {
            if (!IsInstanceValid(this) || FaceController == null || gen != _pullExprGen) return;
            if (i > 0 && FaceController.CurrentExpression != prev) return;
            FaceController.SetExpression(PullOutExprSeq[i]);
            prev = PullOutExprSeq[i];
            if (i >= PullOutExprSeq.Length - 1) return;
            float sec = i < PullOutExprSecs.Length ? PullOutExprSecs[i] : 2f;
            await ToSignal(GetTree().CreateTimer(Mathf.Max(0.1f, sec)), SceneTreeTimer.SignalName.Timeout);
            if (!IsInstanceValid(this) || _jijiPhase == 3) return;
        }
    }
}
