using Godot;
using System;

/// <summary>
/// 小工具節點：在 _Input 階段先看到輸入事件，Handler 回傳 true 就把事件吞掉（不再往下傳）。
/// 子節點的 _Input 會比父節點先被呼叫，所以掛在 TrainingRoomRuntime 底下就能搶在原本的
/// 人物互動／鏡頭輸入之前攔截，不需要改動那些檔案。
/// </summary>
public partial class InputTap : Node
{
    public Func<InputEvent, bool> Handler;

    public override void _Input(InputEvent @event)
    {
        if (Handler != null && Handler(@event))
            GetViewport().SetInputAsHandled();
    }
}
