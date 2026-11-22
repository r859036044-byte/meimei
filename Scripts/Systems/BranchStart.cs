using Godot;
using System.Collections.Generic;

/// <summary>
/// 從分支開始遊戲（通關過一次之後才能用）。
/// 入口：① 圖鑒「分支劇情」頁已解鎖卡片的「從這裡開始」 ② 主選單「開始遊戲」視窗的「從分支開始」。
/// 劇本只看 day1_blanket／day1_noBlanket、day2_blanket／day2_noBlanket 這四個劇情變數，
/// 所以「選路線」＝把對應變數設好，再進第 2 天早上（第 1 晚的路線）或第 3 天早上（第 2 晚的路線）。
/// 路線的顯示名稱：全年齡在 StoryTreeData.AABranchDefs，R18 在 GalleryScreen.BranchDefs。
/// R18 的分支是 night1／night2 ×（什麼都不做／摸了／高潮）：摸了＝碰到胸或小穴，高潮＝今晚高潮值滿過（解鎖在 TrainingRoomRuntime.Dialogue.cs UnlockR18Branch）。
/// </summary>
public static class BranchStart
{
    /// <summary>通關過一次（任何結局解鎖）才開放。開發者全解鎖開關打開時也開放。</summary>
    public static bool Available()
    {
        var g = GalleryDataManager.Instance;
        if (g == null) return false;
        if (g.DebugUnlockAllOverride) return true;
        for (int i = 1; i <= 19; i++) if (g.IsEndingUnlocked(i)) return true;
        for (int i = 1; i <= GalleryDataManager.AAEndingCount; i++) if (g.IsAAEndingUnlocked(i)) return true;
        return false;
    }

    /// <summary>路線名稱：全年齡用 StoryTreeData.AABranchDefs 的名字，R18 用上面兩個常數。</summary>
    public static string Name(string id)
    {
        if (!GalleryDataManager.AllAgesMode)
        {
            foreach (var d in GalleryScreen.BranchDefs) if (d.id == id) return d.label;
            return id;
        }
        foreach (var d in StoryTreeData.AABranchDefs) if (d.id == id) return d.label;
        return id;
    }

    /// <summary>開始。d2Cover＝null：從第 2 天早上開始（只選第 1 晚路線）；否則從第 3 天早上開始（第 1、2 晚路線都選）。</summary>
    public static void Go(Node host, bool d1Cover, bool? d2Cover)
    {
        NewGameReset.ResetAll();   // 跟開新遊戲一樣先歸零
        TrainingRoomRuntime.TutorialPending = false;   // 通關過的玩家不需要新手教學
        var pdm = PlayerDataManager.Instance;
        if (pdm != null)
        {
            var vars = new Dictionary<string, int> { [d1Cover ? "day1_blanket" : "day1_noBlanket"] = 1 };
            if (d2Cover.HasValue) vars[d2Cover.Value ? "day2_blanket" : "day2_noBlanket"] = 1;
            pdm.SetAllDialogueVars(vars);
            pdm.ResetTodayTracking();
            pdm.CurrentNight = d2Cover.HasValue ? 2 : 1;
        }
        GameFlowManager.Instance?.SetSkipPrologue(true);
        if (d2Cover.HasValue) LivingRoomRuntime.PendingDay3 = true;          // 第 3 天早上
        else LivingRoomRuntime.SkipPrologueLastOnce = true;                  // 第 2 天早上（略過 PrologueLast）
        GD.Print($"[BranchStart] 第1晚{(d1Cover ? "蓋" : "不蓋")}、第2晚{(d2Cover.HasValue ? (d2Cover.Value ? "蓋" : "不蓋") : "未選")} → {(d2Cover.HasValue ? "第3天早上" : "第2天早上")}");
        host.GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
    }

    /// <summary>R18 從分支開始。k1／k2：0＝什麼都不做、1＝摸了、2＝高潮（k2＝-1 表示只選第 1 晚，從第 2 天早上開始）；cover1／cover2＝當晚被子是否蓋著。</summary>
    public static void GoR18(Node host, int k1, bool cover1, int k2, bool cover2)
    {
        NewGameReset.ResetAll();
        TrainingRoomRuntime.TutorialPending = false;
        var pdm = PlayerDataManager.Instance;
        bool two = k2 >= 0;
        if (pdm != null)
        {
            var vars = new Dictionary<string, int> { [cover1 ? "day1_blanket" : "day1_noBlanket"] = 1 };
            if (two) vars[cover2 ? "day2_blanket" : "day2_noBlanket"] = 1;
            pdm.SetAllDialogueVars(vars);
            pdm.ResetTodayTracking();
            pdm.CurrentNight = two ? 2 : 1;
            // 連續什麼都沒做的晚數（第 3 天流程判斷用）
            pdm.IdleNightCount = two ? (k2 == 0 ? (k1 == 0 ? 2 : 1) : 0) : (k1 == 0 ? 1 : 0);
            // 第 3 天早上用到「今晚（第 2 晚）」的狀態：照第 2 晚選的路線設
            if (two)
            {
                pdm.TodayDidNothing = k2 == 0;
                pdm.TodayTouchedOrgasm = k2 == 2;
                pdm.TodayTouchedChestHole = k2 >= 1;
            }
        }
        TrainingRoomRuntime.HadBreastTouch = k1 >= 1 || (two && k2 >= 1);   // 有摸過胸／小穴（第 3 天早上選「摸胸版」劇情用）
        GameFlowManager.Instance?.SetSkipPrologue(true);
        if (two) LivingRoomRuntime.PendingDay3 = true;
        else LivingRoomRuntime.SkipPrologueLastOnce = true;
        GD.Print($"[BranchStart] R18：第1晚 {k1}（被子{(cover1 ? "蓋" : "踢")}）、第2晚 {k2} → {(two ? "第3天早上" : "第2天早上")}");
        host.GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
    }

    /// <summary>開啟選路線視窗（加在 host 上，蓋住整個畫面）。</summary>
    public static void OpenPicker(Control host)
    {
        var old = host.GetNodeOrNull("BranchStartPicker");
        old?.QueueFree();
        var g = GalleryDataManager.Instance;
        bool U(string id) => g != null && g.IsBranchUnlocked(id);

        var root = new Control { Name = "BranchStartPicker", MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 200 };
        host.AddChild(root);
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(dim);
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var sb = new StyleBoxFlat { BgColor = new Color("fffafd"), BorderColor = new Color("edb8cf") };
        sb.SetBorderWidthAll(2); sb.SetCornerRadiusAll(24);
        sb.ContentMarginLeft = 36; sb.ContentMarginRight = 36; sb.ContentMarginTop = 28; sb.ContentMarginBottom = 28;
        card.AddThemeStyleboxOverride("panel", sb);
        root.AddChild(card);
        card.SetAnchorsPreset(Control.LayoutPreset.Center);
        card.CustomMinimumSize = new Vector2(760, 0);
        card.GrowHorizontal = Control.GrowDirection.Both; card.GrowVertical = Control.GrowDirection.Both;

        var vb = new VBoxContainer();
        vb.AddThemeConstantOverride("separation", 14);
        card.AddChild(vb);

        Label L(string t, int size, string color, bool center = false)
        {
            var l = new Label { Text = t, HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", new Color(color));
            return l;
        }
        Button B(string text, bool enabled, System.Action act)
        {
            var b = new Button { Text = enabled ? text : "？  ？  ？", Disabled = !enabled, CustomMinimumSize = new Vector2(0, 56), FocusMode = Control.FocusModeEnum.None };
            b.AddThemeFontSizeOverride("font_size", 22);
            if (enabled) b.Pressed += act;
            return b;
        }

        vb.AddChild(L("從分支開始", 30, "79475f", true));
        vb.AddChild(L("選一條路線，直接從那條路線的隔天早上開始（只能選已經解鎖的路線）", 17, "a7778e", true));

        if (!GalleryDataManager.AllAgesMode)
        {
            BuildR18Picker(host, root, vb, U, (t, sz, c) => L(t, sz, c));
            return;
        }

        vb.AddChild(L("第１晚的路線  →  從第 2 天早上開始", 20, "a66d88"));
        var row1 = new HBoxContainer(); row1.AddThemeConstantOverride("separation", 14); vb.AddChild(row1);
        foreach (var (id, cover) in new[] { ("aa_n1_idle", false), ("aa_n1_cover", true) })
        {
            var b = B(Name(id), U(id), () => Go(host, cover, null));
            b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; row1.AddChild(b);
        }

        vb.AddChild(L("第２晚的路線  →  從第 3 天早上開始（要連第 1 晚一起選）", 20, "a66d88"));
        // 「第1晚蓋被子＋第2晚不做」本來就會被改成兩晚都不做（NormalizeAAEndingVars），所以只列另外三種組合
        foreach (var (d1, d2) in new[] { (false, false), (false, true), (true, true) })
        {
            string n1 = d1 ? "aa_n1_cover" : "aa_n1_idle", n2 = d2 ? "aa_n2_cover" : "aa_n2_idle";
            var b = B($"第１晚 {Name(n1)}　＋　第２晚 {Name(n2)}", U(n1) && U(n2), () => Go(host, d1, d2));
            vb.AddChild(b);
        }

        var close = new Button { Text = "關　閉", CustomMinimumSize = new Vector2(0, 48), FocusMode = Control.FocusModeEnum.None };
        close.AddThemeFontSizeOverride("font_size", 20);
        close.Pressed += () => root.QueueFree();
        vb.AddChild(close);
    }

    static readonly string[] R18Kinds = { "idle", "touch", "orgasm" };

    static void BuildR18Picker(Control host, Control root, VBoxContainer vb, System.Func<string, bool> U, System.Func<string, int, string, Label> L)
    {
        int[] sel = { -1, -1 };          // 第 1、2 晚選到的路線（-1＝沒選）
        bool[] cover = { false, false };  // 第 1、2 晚被子是否蓋著
        Button start2 = null, start3 = null;
        void Refresh()
        {
            if (start2 != null) start2.Disabled = sel[0] < 0;
            if (start3 != null) start3.Disabled = sel[0] < 0 || sel[1] < 0;
        }
        for (int night = 1; night <= 2; night++)
        {
            int ni = night - 1;
            vb.AddChild(L($"第{night}晚做了什麼", 20, "a66d88"));
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); vb.AddChild(row);
            var grp = new ButtonGroup();
            for (int k = 0; k < 3; k++)
            {
                int kk = k; string id = $"night{night}_{R18Kinds[k]}";
                bool on = U(id);
                var b = new Button
                {
                    Text = on ? Name(id) : "？  ？  ？", Disabled = !on, ToggleMode = true, ButtonGroup = grp,
                    CustomMinimumSize = new Vector2(0, 56), FocusMode = Control.FocusModeEnum.None, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                b.AddThemeFontSizeOverride("font_size", 22);
                b.Toggled += pressed => { if (pressed) { sel[ni] = kk; Refresh(); } };
                row.AddChild(b);
            }
            var cb = new CheckBox { Text = "　當晚被子蓋著", FocusMode = Control.FocusModeEnum.None };
            cb.AddThemeFontSizeOverride("font_size", 18);
            cb.Toggled += v => cover[ni] = v;
            vb.AddChild(cb);
        }

        start2 = new Button { Text = "從第 2 天早上開始（只用第 1 晚）", Disabled = true, CustomMinimumSize = new Vector2(0, 52), FocusMode = Control.FocusModeEnum.None };
        start2.AddThemeFontSizeOverride("font_size", 20);
        start2.Pressed += () => GoR18(host, sel[0], cover[0], -1, false);
        vb.AddChild(start2);
        start3 = new Button { Text = "從第 3 天早上開始（第 1、2 晚都用）", Disabled = true, CustomMinimumSize = new Vector2(0, 52), FocusMode = Control.FocusModeEnum.None };
        start3.AddThemeFontSizeOverride("font_size", 20);
        start3.Pressed += () => GoR18(host, sel[0], cover[0], sel[1], cover[1]);
        vb.AddChild(start3);

        var close = new Button { Text = "關　閉", CustomMinimumSize = new Vector2(0, 48), FocusMode = Control.FocusModeEnum.None };
        close.AddThemeFontSizeOverride("font_size", 20);
        close.Pressed += () => root.QueueFree();
        vb.AddChild(close);
    }
}
