using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// MainMenuRuntime — 啟動時的語言選擇畫面。
/// 流程：「吉太研究部」LOGO 淡出之後、進標題畫面（或開場動畫）之前，先問玩家要用什麼語言開啟遊戲：
/// 黑底＋五個按鈕（繁體中文／简体中文／English／日本語／한국어），按下去就立刻套用並存進設定檔（user://settings.cfg）。
/// 預設只在「第一次啟動（還沒選過語言）」時問；AskLanguageEveryLaunch 打開＝每次啟動都問。
/// 之後想改語言，一樣可以在設定裡改。從遊戲中「返回主畫面」不會再問。
/// </summary>
public partial class MainMenuRuntime
{
    [ExportGroup("語言選擇")]
    [Export] public bool AskLanguageEveryLaunch = false;   // false＝只有第一次啟動（設定檔裡還沒有選過語言的紀錄）才問；true＝每次啟動遊戲都問
    [Export] public float LanguagePickFadeSec = 0.45f;     // 語言選擇畫面淡入／淡出秒數

    static bool _languagePromptedThisRun;   // 同一次執行只問一次（返回主畫面不會再問）

    /// <summary>需要的話顯示語言選擇畫面，等玩家選完才返回；不需要就直接返回。</summary>
    async Task AskLanguageIfNeeded(CancellationToken ct)
    {
        if (_languagePromptedThisRun) return;
        var sm = SettingsManager.Instance;
        if (sm != null && sm.LanguageChosen && !AskLanguageEveryLaunch) return;
        _languagePromptedThisRun = true;

        // ── 建立畫面（蓋在 LOGO 面板上，LOGO 此時已淡出成全黑）──
        var root = new Control { Name = "LanguagePicker", MouseFilter = MouseFilterEnum.Stop, Modulate = new Color(1f, 1f, 1f, 0f) };
        _logoPanel.AddChild(root);
        ApplyStretchAnchor(root, V2(0, 0), V2(1, 1), V2(0, 0), V2(0, 0));

        Txt(root, "Title", "Language  /  語言  /  语言  /  言語  /  언어", 46, new Color(1f, 0.92f, 0.96f), UAlign.MiddleCenter,
            V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0, -340), V2(1500, 80));
        Txt(root, "Hint", "請選擇遊戲語言 ・ Select your language ・ 请选择语言 ・ 言語を選んでください ・ 언어를 선택하세요", 24,
            new Color(1f, 1f, 1f, 0.55f), UAlign.MiddleCenter,
            V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0, -270), V2(1700, 50));

        const int btnW = 600, btnH = 86, gap = 20;
        int n = SettingsManager.Languages.Length;
        var box = new VBoxContainer { Name = "Buttons", MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", gap);
        root.AddChild(box);
        ApplyPointAnchor(box, V2(0.5f, 0.5f), V2(0.5f, 0.5f), V2(0, 50), V2(btnW, n * btnH + (n - 1) * gap));

        var tcs = new TaskCompletionSource<int>();
        using var reg = ct.Register(() => tcs.TrySetCanceled());
        Button first = null;
        int current = sm != null ? sm.LanguageIndex : 0;
        for (int i = 0; i < n; i++)
        {
            int idx = i;
            var b = UIStyle.CreateStyledButton(SettingsManager.Languages[i], 38);
            b.Name = "Lang_" + SettingsManager.Locales[i];
            b.CustomMinimumSize = new Vector2(btnW, btnH);
            b.Pressed += () => tcs.TrySetResult(idx);
            box.AddChild(b);
            if (first == null || i == current) first = b;   // 預設停在目前的語言（沒有就第一個）
        }
        first?.GrabFocus();   // 鍵盤／手把也能選：預設停在目前的語言上，上下鍵切換、Enter／確認鍵決定

        await Lerp(LanguagePickFadeSec, p => root.Modulate = new Color(1f, 1f, 1f, p), ct);
        int chosen = await tcs.Task;   // 等玩家選

        // ── 套用並記住 ──
        if (sm != null) { sm.SetLanguage(chosen); sm.MarkLanguageChosen(); }
        GD.Print($"[Language] 玩家選擇：{SettingsManager.Languages[chosen]} ({SettingsManager.Locales[chosen]})");

        await Lerp(LanguagePickFadeSec, p => root.Modulate = new Color(1f, 1f, 1f, 1f - p), ct);
        Kill(root);
    }
}
