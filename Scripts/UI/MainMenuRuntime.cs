using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// MainMenuRuntime 主進入點（partial class 主檔，供 MainMenu.tscn 掛載）
/// 實際邏輯分散於：
///   MainMenuRuntime.Fields.cs    — 欄位 [Export] 與 _Ready()
///   MainMenuRuntime.BuildUI.cs   — UI 建構
///   MainMenuRuntime.Character.cs — 角色立繪 / 選項面板
///   MainMenuRuntime.Dialogue.cs  — 對話播放 / _Process / _Input
///   MainMenuRuntime.Fade.cs      — 淡入淡出 / CG 層
///   MainMenuRuntime.Helpers.cs   — 座標轉換 / 輔助方法
///   MainMenuRuntime.Intro.cs     — 開場動畫
/// </summary>
public partial class MainMenuRuntime : Control
{
    // 本檔僅作為 Godot 場景指令碼掛載點；所有欄位與方法在其他 partial 檔中定義。
}
