using Godot;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// TrainingRoomRuntime — 待播對話（PlayPendingDialogue）、日夜切換流程
/// （ConsumePendingFlags / EndToday / TrainingEndFlow）、簡化版第三晚小遊戲、淡場轉場。
///
/// 【已知簡化】Unity 原版對話框支援多段 JSON（陣列＝分支選項，如「算了不跟你吵」
/// vs「教訓妹妹」）、CG 圖層淡入淡出、立繪、背景切換、BGM/SE、打字機快進/自動播放等
/// 完整對話引擎（與 LivingRoomRuntime 的對話系統同級）。這裡使用 Godot 專案既有的
/// DialogueSystem.DialogueData（只支援單一物件、不支援頂層陣列分支），故只播放
/// 主線對話文字（speaker + text，打字機效果、點擊/空白鍵推進），不處理分支選項、
/// CG/背景/立繪動畫。若 JSON 是分支陣列格式，只播放陣列第一段。
/// </summary>
public partial class TrainingRoomRuntime : Node
{
    // ── 進場時消費 Pending 旗標，決定第幾晚 ─────────────────────────
    void ConsumePendingFlags()
    {
        var pdm = PlayerDataManager.Instance;
        if (pdm == null) return;

        SnapshotDayStart();   // 記下進房間時的狀態（AgeSwitch.cs：切換版本時重新開始這一天用）
        StoryPlaying = false;   // 進入 troom（含第二晚）一律解除劇情凍結，機制／音效恢復

        if (SaveLoadHost.PendingDuvetState >= 0)
        {   // 讀檔回來：還原存檔當時的被子狀態（0＝蓋著、1＝踢開）
            DuvetState = AllAgesLocked ? 0 : SaveLoadHost.PendingDuvetState;
            SaveLoadHost.PendingDuvetState = -1;
        }

        if (PendingDay2First)
        {
            PendingDay2First = false;
            pdm.CurrentNight = 1;
            pdm.ResetTodayTracking();
        }
        else if (PendingDay3First)
        {
            PendingDay3First = false;
            pdm.CurrentNight = 2;
            pdm.ResetTodayTracking();
        }
        else if (PendingNight3)
        {
            PendingNight3 = false;
            pdm.CurrentNight = 3;
            pdm.ResetTodayTracking();

            if (PendingMiniGameOnly)
            {
                PendingMiniGameOnly = false;
                _pendingDialogueDone = true;
                TaskLogger.Run(MiniGameNight3Async(), "TrainingRoom.MiniGameNight3Async");
            }
            else if (pdm.IdleNightCount >= 2)
            {
                _pendingDialogueDone = true;
                TaskLogger.Run(MiniGameNight3Async(), "TrainingRoom.TimingChallenge");
            }
            else
            {
                _pendingDialogueDone = true;
            }
        }
    }

    async Task PlayPendingDialogueThenSignalAsync(string dialogueName)
    {
        await PlayPendingDialogueAsync(dialogueName);
        _pendingDialogueDone = true;
        await MiniGameNight3Async();
    }

    // ── 待播 JSON 對話（簡化版對話框：speaker + 打字機 + 點擊推進）───
    public async Task PlayPendingDialogueAsync(string dialogueName)
    {
        var path = $"res://Resources/Data/Dialogues/{dialogueName}.json";
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[TrainingRoom] 找不到對話檔：{path}");
            return;
        }
        string raw;
        using (var f = FileAccess.Open(path, FileAccess.ModeFlags.Read))
            raw = f.GetAsText();
        raw = DialogueSystem.PreprocessJson(raw);

        DialogueSystem.DialogueData data = null;
        try
        {
            data = DialogueSystem.ParseDialogueData(raw);
        }
        catch
        {
            try
            {
                var arr = JsonSerializer.Deserialize<List<DialogueSystem.DialogueData>>(raw,
                    GameJson.Options());
                if (arr != null && arr.Count > 0) data = arr[0];
            }
            catch (System.Exception e)
            {
                GD.PushWarning($"[TrainingRoom] {dialogueName} 解析失敗：{e.Message}");
            }
        }

        if (data == null || data.Lines == null || data.Lines.Count == 0) return;

        var container = new Control { Name = "PendingDialogueContainer", MouseFilter = Control.MouseFilterEnum.Stop };
        container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        UiRoot.AddChild(container);   // 對話框不跟著鏡頭縮放

        var boxHeight = 220f;
        var box = new Panel { Name = "DialogueBox" };
        box.AnchorLeft = 0; box.AnchorRight = 1; box.AnchorTop = 1; box.AnchorBottom = 1;
        box.OffsetTop = -boxHeight; box.OffsetBottom = 0;
        box.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.88f) });
        container.AddChild(box);

        var nameBar = new Panel { Name = "NameBar" };
        nameBar.Position = new Vector2(50, -boxHeight - 44); nameBar.Size = new Vector2(260, 40);
        nameBar.AnchorTop = 1; nameBar.AnchorBottom = 1;
        nameBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.65f, 0.12f, 0.25f, 0.95f) });
        container.AddChild(nameBar);
        var spkLbl = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        spkLbl.AddThemeFontSizeOverride("font_size", 22);
        spkLbl.AddThemeColorOverride("font_color", Colors.White);
        spkLbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        nameBar.AddChild(spkLbl);

        var contentLbl = new RichTextLabel { BbcodeEnabled = false };
        contentLbl.Position = new Vector2(60, 20); contentLbl.Size = new Vector2(0, boxHeight - 40);
        contentLbl.AnchorRight = 1; contentLbl.OffsetRight = -60;
        contentLbl.AddThemeFontSizeOverride("normal_font_size", 24);
        contentLbl.AddThemeColorOverride("default_color", new Color(0.98f, 0.96f, 1f));
        box.AddChild(contentLbl);

        var hint = new Label { Text = "▼ 點擊繼續", HorizontalAlignment = HorizontalAlignment.Right };
        hint.AnchorLeft = 1; hint.AnchorRight = 1; hint.AnchorTop = 1; hint.AnchorBottom = 1;
        hint.OffsetLeft = -200; hint.OffsetTop = -30; hint.OffsetRight = -20; hint.OffsetBottom = -6;
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.4f));
        box.AddChild(hint);

        bool skipTyping = false, typing = false, waiting = false;
        if (_menuReader != null) _menuReader.AlwaysShowMenu = false;   // 對話期間用對話自己的選單鈕，避免重複
        var reader = NovelReader.Install(container, box, contentLbl, null,
            () => typing, () => waiting, () => false,
            () => skipTyping = true, () => waiting = false);
        reader.SetSpeakerBadge(nameBar, spkLbl);
        foreach (var line in data.Lines)
        {
            spkLbl.Text = line.speaker ?? "";
            nameBar.Visible = !string.IsNullOrEmpty(line.speaker);
            string full = line.text ?? "";
            reader.BeginLine(line.speaker, full);
            contentLbl.Text = "";
            skipTyping = false;
            typing = true;
            for (int i = 0; i < full.Length; i++)
            {
                while (reader.Paused) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (skipTyping) { contentLbl.Text = full; break; }
                contentLbl.Text += full[i];
                await ToSignal(GetTree().CreateTimer(0.025f), SceneTreeTimer.SignalName.Timeout);
            }
            typing = false;
            waiting = true;
            while (waiting) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        reader.Stop();
        container.QueueFree();
        if (_menuReader != null && IsInstanceValid(_menuReader)) _menuReader.AlwaysShowMenu = true;
    }

    // ── 常駐選單鈕：沿用 NovelReader 的「☰ 選單」（保存／讀檔／設定），沒有對話時也顯示 ──
    NovelReader _menuReader;
    void SetupMenuReader()
    {
        var dummyBox = new Control { Name = "MenuReaderDummyBox", Visible = false };
        AddChild(dummyBox);
        var dummyText = new Label { Visible = false };
        dummyBox.AddChild(dummyText);
        _menuReader = NovelReader.Install(this, dummyBox, dummyText, null,
            () => false, () => false, () => false, () => { }, () => { });
        _menuReader.AlwaysShowMenu = true;
    }

    // ── EndToday：判斷分支並轉場 ─────────────────────────────────
    bool _endingDay;
    bool _timingChallengeActive;
    /// <summary>劇情播放中：troom 機制（心情／雌小鬼值結算、莉奈台詞）應檢查此旗標並暫停。</summary>
    public bool StoryPlaying { get; private set; }
    public void EndToday()
    {
        if (_endingDay || _timingChallengeActive) return;
        if (!TissueAllowLeave()) return;   // 精液沒擦乾淨不能離開（Tissue.cs）
        var pdm = PlayerDataManager.Instance;
        if (pdm == null) return;

        _endingDay = true;
        RecordBlanketState(pdm);   // 離開時記錄今晚蓋沒蓋被子：dayN_blanket／dayN_noBlanket（劇本條件用）
        UnlockR18Branch(pdm);      // R18：依今晚做了什麼解鎖圖鑒分支（什麼都不做／摸了／高潮）
        if (pdm.TodayDidNothing)
        {
            TaskLogger.Run(IdleEndFlowAsync(), "TrainingRoom.IdleEndFlowAsync");
            return;
        }

        if (pdm.TodayInserted && !pdm.TodayInsertOrgasm)
            pdm.TodayInsertNoOrg = true;

        var branch = pdm.GetDay2Branch();
        bool success = branch != PlayerDataManager.Day2Branch.InsertNoOrgasm;
        TaskLogger.Run(TrainingEndFlowAsync(success), "TrainingRoom.TrainingEndFlowAsync");
    }

    [Export(PropertyHint.Range, "0,100,1")] public float AADay2BlanketMood = 60f;        // 全年齡第 1 晚：心情要「大於」這個值，才有機會走 Day2 蓋被子劇情
    [Export(PropertyHint.Range, "0,100,1")] public float AADay2BlanketBratBelow = 30f;   // 全年齡第 1 晚：雌小鬼值要「小於」這個值（兩個條件都達到才走蓋被子劇情，只要有一個沒達到就走不蓋被子劇情；R18 版不受影響，仍照實際被子狀態）

    /// <summary>離開 TrainingRoom 時，依被子狀態設定劇情變數：蓋著＝dayN_blanket、踢開＝dayN_noBlanket（N＝今晚是第幾晚，只記第 1、2 晚）。
    /// 兩個變數互斥：設其中一個為 1、另一個歸 0，劇本的 varStart 條件就能判斷。</summary>
    void RecordBlanketState(PlayerDataManager pdm)
    {
        int night = pdm.CurrentNight;
        if (night < 1 || night > 2) return;
        bool covered = DuvetState == 0;   // 0＝蓋著、1＝踢開（R18 照被子狀態）
        if (!pdm.R18ContentEnabled)
        {   // 全年齡：蓋不蓋被子完全不影響劇情，只看心情／雌小鬼值
            if (night == 1)
            {   // 第 1 晚：決定隔天 Day2 早上走哪一段劇情——心情大於 AADay2BlanketMood「而且」雌小鬼值低於 AADay2BlanketBratBelow 走第一段，任何一個條件沒達到走第二段
                covered = Mood > AADay2BlanketMood && BratValue < AADay2BlanketBratBelow;
                GD.Print($"[Troom] 全年齡第 1 晚：心情 {Mood:F0}（要 > {AADay2BlanketMood:F0}）、雌小鬼值 {BratValue:F0}（要 < {AADay2BlanketBratBelow:F0}）→ Day2 走{(covered ? "蓋被子" : "不蓋被子")}劇情");
            }
            else
            {   // 第 2 晚：結局只由「第 2 晚結束時的心情／雌小鬼值」決定（離哪個結局的理想點最近）。
                // 劇本是看「第 1 晚＋第 2 晚」兩個變數的組合挑結局，所以這裡把兩個一起寫成這個結局要的組合，第 1 晚的結果不會再拖著結局走
                // （Day2 早上的劇情已經播完了，覆蓋第 1 晚變數不影響劇情）。
                int ending = StoryTreeData.PickAAEndingByStats(Mood, BratValue);
                var path = StoryTreeData.AAEndingPath(ending);
                WriteBlanketVars(pdm, 1, path.d1Cover);
                covered = path.d2Cover;
                if (StoryTreeData.AAEndingIsBadEnd(ending))
                {   // 結局 7（斷線的風箏，已融合無聲的拒絕）：離開調教室後直接進離家出走結局，跟「心情歸 0」走同一條路（BratEnd＝1 → Ending4 場景）
                    BratSetVar(BratVarEnd, 1);
                    GD.Print("[Troom] 全年齡第 2 晚：心情偏低 → 結局 7（斷線的風箏）");
                }
                GD.Print($"[Troom] 全年齡第 2 晚：心情 {Mood:F0}、雌小鬼值 {BratValue:F0} → 結局 {ending}（劇本變數：第1晚{(path.d1Cover ? "蓋" : "不蓋")}、第2晚{(path.d2Cover ? "蓋" : "不蓋")}）");
            }
        }
        WriteBlanketVars(pdm, night, covered);
    }

    /// <summary>R18 圖鑒分支：高潮＝今晚高潮過（高潮值滿觸發）；摸了＝有碰到胸或小穴（或舔、或插入）；其餘＝什麼都不做。只記第 1、2 晚。</summary>
    void UnlockR18Branch(PlayerDataManager pdm)
    {
        if (!pdm.R18ContentEnabled) return;
        int n = pdm.CurrentNight;
        if (n < 1 || n > 2) return;
        string kind = (pdm.TodayTouchedOrgasm || pdm.TodayInsertOrgasm) ? "orgasm"
                    : (pdm.TodayTouchedChestHole || pdm.TodayInserted) ? "touch" : "idle";
        GalleryDataManager.Instance?.UnlockBranch($"night{n}_{kind}");
        GD.Print($"[Troom] 圖鑒分支解鎖：night{n}_{kind}");
    }

    /// <summary>把「第 N 晚蓋／不蓋」寫進劇情變數：兩個變數互斥，設其中一個為 1、另一個歸 0，劇本的 varStart 條件就能判斷。</summary>
    static void WriteBlanketVars(PlayerDataManager pdm, int night, bool covered)
    {
        string yes = $"day{night}_" + (covered ? "blanket" : "noBlanket");
        string no  = $"day{night}_" + (covered ? "noBlanket" : "blanket");
        pdm.AddDialogueVar(yes, 1 - pdm.GetDialogueVar(yes));
        pdm.AddDialogueVar(no, -pdm.GetDialogueVar(no));
        GD.Print($"[Troom] 劇情變數：{yes}=1、{no}=0");
    }

    async Task IdleEndFlowAsync()
    {
        var pdmCheck = PlayerDataManager.Instance;
        if (pdmCheck != null && !pdmCheck.R18ContentEnabled && pdmCheck.CurrentNight == 1
            && PendingBratEnding() == 0 && LivingRoomRuntime.PrologueLastPending)
        {
            await PlayNight1AllAgesEndingAsync(didInteract: false, isGoodRoute: false);
        }
        else
        {
            await FadeToBlackAsync();
        }

        if (PendingBratEnding() == 1)
        {   // 心情歸 0（莉奈搬走）→ 一定觸發 Day2_Start(bad end)（Ending4）；用掉就清掉旗標
            BratSetVar(BratVarEnd, 0);
            GD.Print("[Troom] 心情歸 0 → 進 Ending4（Day2_Start bad end）");
            GetTree().ChangeSceneToFile("res://Scenes/Ending4.tscn");
            return;
        }

        if (PendingBratEnding() == 2)
        {   // 雌小鬼值滿 100（心情沒歸 0）→ 隔天早上直接播雌小鬼大小姐結局
            BratSetVar(BratVarEnd, 0);
            LivingRoomRuntime.PendingMesugaki = true;
            GD.Print("[Troom] 雌小鬼值滿 100 → 隔天早上播雌小鬼大小姐結局");
            GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
            return;
        }

        var pdm = PlayerDataManager.Instance;
        if (pdm == null) { GetTree().ChangeSceneToFile("res://Scenes/Ending4.tscn"); return; }

        int night = pdm.CurrentNight;
        if (pdm.TodayDidNothing) pdm.IdleNightCount++;

        if (night == 1 && LivingRoomRuntime.PrologueLastPending)
        {   // 第一次從 TrainingRoom 出來：先接 PrologueLast（LivingRoom 自己會播），再進 Day2，不直接跳 Day2
            pdm.ResetTodayTracking();
            GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
        }
        else if (night == 1 || night == 2)
        {
            pdm.ResetTodayTracking();
            LivingRoomRuntime.PendingDay3 = true;
            GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
        }
        else
        {
            pdm.ResetTodayTracking();
            GetTree().ChangeSceneToFile("res://Scenes/Ending4.tscn");
        }
    }

    async Task TrainingEndFlowAsync(bool success)
    {
        var pdmCheck = PlayerDataManager.Instance;
        if (success && pdmCheck != null && !pdmCheck.R18ContentEnabled && pdmCheck.CurrentNight == 1
            && PendingBratEnding() == 0 && LivingRoomRuntime.PrologueLastPending)
        {
            bool isGoodRoute = pdmCheck.GetDialogueVar("day1_blanket") > 0;
            await PlayNight1AllAgesEndingAsync(didInteract: true, isGoodRoute: isGoodRoute);
        }
        else if (success && pdmCheck != null && pdmCheck.R18ContentEnabled && PendingBratEnding() == 0
                 && (pdmCheck.CurrentNight == 1 || pdmCheck.CurrentNight == 2))
        {   // R18：結束時男主說話劇情（第 1 晚 Night1/LinaiLast、第 2 晚 Day2/Day2_Last）。播之前所有動作停止、畫面先暗下來、氣泡與莉奈語音全清掉
            await BeginStoryFreezeAsync();
            await PlayPendingDialogueAsync(pdmCheck.CurrentNight == 1 ? "Night1/LinaiLast" : "Day2/Day2_Last");
            await FadeToBlackAsync();
        }
        else
        {
            await FadeToBlackAsync();
        }

        if (PendingBratEnding() == 1)
        {   // 心情歸 0（莉奈搬走）→ 一定觸發 Day2_Start(bad end)（Ending4）；用掉就清掉旗標
            BratSetVar(BratVarEnd, 0);
            GD.Print("[Troom] 心情歸 0 → 進 Ending4（Day2_Start bad end）");
            GetTree().ChangeSceneToFile("res://Scenes/Ending4.tscn");
            return;
        }

        if (PendingBratEnding() == 2)
        {   // 雌小鬼值滿 100（心情沒歸 0）→ 隔天早上直接播雌小鬼大小姐結局
            BratSetVar(BratVarEnd, 0);
            LivingRoomRuntime.PendingMesugaki = true;
            GD.Print("[Troom] 雌小鬼值滿 100 → 隔天早上播雌小鬼大小姐結局");
            GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
            return;
        }

        var pdm = PlayerDataManager.Instance;
        if (pdm == null) { GetTree().ChangeSceneToFile("res://Scenes/Ending4.tscn"); return; }

        pdm.IdleNightCount = 0; // 只有連續未操作才累積。
        int night = pdm.CurrentNight;
        pdm.ResetTodayTracking();

        if (!success)
        {
            GetTree().ChangeSceneToFile("res://Scenes/Ending4.tscn");
            return;
        }

        if (night == 1 && LivingRoomRuntime.PrologueLastPending)
        {   // 第一次從 TrainingRoom 出來：先接 PrologueLast（LivingRoom 自己會播），再進 Day2，不直接跳 Day2
            GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
        }
        else if (night == 1 || night == 2)
        {
            // LivingRoomRuntime 只公開 PendingDay3 旗標；日夜文本內容由
            // LivingRoomRuntime 依 PlayerDataManager.CurrentNight 自行判斷。
            LivingRoomRuntime.PendingDay3 = true;
            GetTree().ChangeSceneToFile("res://Scenes/LivingRoom.tscn");
        }
        else
        {
            GetTree().ChangeSceneToFile("res://Scenes/Ending2.tscn");
        }
    }

    // ── 第三天非露骨時機挑戰 ──
    async Task MiniGameNight3Async()
    {
        if (_timingChallengeActive) return;
        _timingChallengeActive = true;
        bool won;
        try { won = await TimingChallenge.Play(this); }
        finally { _timingChallengeActive = false; }
        if (won && (PlayerDataManager.Instance?.IdleNightCount ?? 0) >= 2)
        {
            Ending2Runtime.UseNothingRoute = true;
            GetTree().ChangeSceneToFile("res://Scenes/Ending2.tscn");
        }
        else GetTree().ChangeSceneToFile((PlayerDataManager.Instance?.IdleNightCount ?? 0) >= 2 ? "res://Scenes/Ending1.tscn" : "res://Scenes/Ending4.tscn");
    }

    void SetDuvetCovered(bool covered)
    {
        int targetState = (covered || AllAgesLocked) ? 0 : 1;   // 全年齡：被子永遠蓋著（劇情裡的「踢開」也不會真的掀開）
        if (DuvetState == targetState) return;
        DuvetState = targetState;
        if (_duvet != null) { _duvet.FoldSnap = true; _duvet.FoldTarget = 0f; }
        if (!covered && _duvetKicked != null) { _duvetKicked.Drag = Vector2.Zero; _duvetKicked.DragSnap = true; }
    }

    async Task PlayNight1AllAgesEndingAsync(bool didInteract, bool isGoodRoute)
    {
        if (PlayerDataManager.Instance?.R18ContentEnabled ?? false) return;   // R18：完全不執行（含靜音／凍結）
        StoryPlaying = true;   // 劇情期間凍結 troom 機制，結束後不再恢復（隨即轉場）
        _voiceSeq.Clear();
        if (_trVoicePlayer != null && IsInstanceValid(_trVoicePlayer)) _trVoicePlayer.Stop();   // 立刻停掉正在播的莉奈語音
        await BeginStoryFreezeAsync();   // 動作全停＋畫面先暗下來＋清掉氣泡
        var lines = new List<(string spk, string txt, string action)>();

        if (didInteract)
        {
            lines.Add(("男主", "足夠了吧……", ""));
            lines.Add(("男主", "萬一莉奈醒來就完了", ""));
        }

        if (didInteract && isGoodRoute)
        {
            lines.Add(("男主", "……睡得這麼香，臉頰鼓鼓的，跟小時候一模一樣。", ""));   // 全年齡：拿掉「看著穿睡衣的莉奈／稍微碰一下」那幾句
            lines.Add(("", "男主摸了摸捏了捏莉奈的臉", ""));
            lines.Add(("男主", "哇~", ""));
            lines.Add(("男主", "臉還是那麼柔軟，果然妹妹的臉就是用來捏的啊❤", ""));
            lines.Add(("莉奈", "嗯……呀……", ""));
            lines.Add(("男主", "啊！糟糕……", ""));
            lines.Add(("", "男主趕緊躡手躡腳地退出了房間", "coverAndLivingRoom"));
            lines.Add(("男主", "呼……真危險，要是起來了一定罵我是變態雜魚的……", ""));
            lines.Add(("男主", "……", ""));
        }
        else
        {
            lines.Add(("男主", "……算了，她睡得正熟，還是別打擾她了。", ""));
            lines.Add(("男主", "要是把她吵醒，肯定又要被她罵雜魚了……晚安，莉奈。", ""));
        }

        var container = new Control { Name = "Night1EndingDialogue", MouseFilter = Control.MouseFilterEnum.Stop };
        container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        UiRoot.AddChild(container);

        TextureRect livingRoomBg = null;
        var bgTex = ResourceHelper.LoadTexAny("Backgrounds/bg_LivingRoom");
        if (bgTex != null)
        {
            livingRoomBg = new TextureRect
            {
                Texture = bgTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                Visible = false,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            livingRoomBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            container.AddChild(livingRoomBg);
        }

        var boxHeight = 220f;
        var box = new Panel { Name = "DialogueBox" };
        box.AnchorLeft = 0; box.AnchorRight = 1; box.AnchorTop = 1; box.AnchorBottom = 1;
        box.OffsetTop = -boxHeight; box.OffsetBottom = 0;
        box.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.88f) });
        container.AddChild(box);

        var nameBar = new Panel { Name = "NameBar" };
        nameBar.Position = new Vector2(50, -boxHeight - 44); nameBar.Size = new Vector2(260, 40);
        nameBar.AnchorTop = 1; nameBar.AnchorBottom = 1;
        nameBar.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.65f, 0.12f, 0.25f, 0.95f) });
        container.AddChild(nameBar);
        var spkLbl = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        spkLbl.AddThemeFontSizeOverride("font_size", 22);
        spkLbl.AddThemeColorOverride("font_color", Colors.White);
        spkLbl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        nameBar.AddChild(spkLbl);

        var contentLbl = new RichTextLabel { BbcodeEnabled = false };
        contentLbl.Position = new Vector2(60, 20); contentLbl.Size = new Vector2(0, boxHeight - 40);
        contentLbl.AnchorRight = 1; contentLbl.OffsetRight = -60;
        contentLbl.AddThemeFontSizeOverride("normal_font_size", 24);
        contentLbl.AddThemeColorOverride("default_color", new Color(0.98f, 0.96f, 1f));
        box.AddChild(contentLbl);

        var hint = new Label { Text = "▼ 點擊繼續", HorizontalAlignment = HorizontalAlignment.Right };
        hint.AnchorLeft = 1; hint.AnchorRight = 1; hint.AnchorTop = 1; hint.AnchorBottom = 1;
        hint.OffsetLeft = -200; hint.OffsetTop = -30; hint.OffsetRight = -20; hint.OffsetBottom = -6;
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.4f));
        box.AddChild(hint);

        bool skipTyping = false, typing = false, waiting = false;
        if (_menuReader != null) _menuReader.AlwaysShowMenu = false;
        var reader = NovelReader.Install(container, box, contentLbl, null,
            () => typing, () => waiting, () => false,
            () => skipTyping = true, () => waiting = false);
        reader.SetSpeakerBadge(nameBar, spkLbl);

        foreach (var (spk, txt, action) in lines)
        {
            if (action == "cover")
            {
                SetDuvetCovered(true);
            }
            else if (action == "uncover")
            {
                SetDuvetCovered(false);
            }
            else if (action == "coverAndLivingRoom")
            {
                SetDuvetCovered(true);
            }

            spkLbl.Text = spk;
            nameBar.Visible = !string.IsNullOrEmpty(spk);
            reader.BeginLine(spk, txt);
            contentLbl.Text = "";
            skipTyping = false;
            typing = true;
            for (int i = 0; i < txt.Length; i++)
            {
                while (reader.Paused) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (skipTyping) { contentLbl.Text = txt; break; }
                contentLbl.Text += txt[i];
                await ToSignal(GetTree().CreateTimer(0.025f), SceneTreeTimer.SignalName.Timeout);
            }
            typing = false;

            if (action == "coverAndLivingRoom")
            {
                await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
                if (livingRoomBg != null && IsInstanceValid(livingRoomBg))
                    livingRoomBg.Visible = true;
            }

            waiting = true;
            while (waiting) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        reader.Stop();
        box.Visible = false;
        nameBar.Visible = false;
        // 保持 livingRoomBg 顯示（若有切換），在客廳背景下慢慢黑屏，不再露出 Troom 畫面
        await FadeToBlackAsync();
        container.QueueFree();
        if (_menuReader != null && IsInstanceValid(_menuReader)) _menuReader.AlwaysShowMenu = true;
    }

    // ── 男主劇情開始：整個 TR 凍結 ─────────────────────────────────
    bool _storyFrozen;   // true＝_Process 整個跳過（呼吸、拖動、抽插、鏡頭、語音全停），ShowBubble 也不再冒氣泡
    ColorRect _storyDim;
    [Export(PropertyHint.Range, "0,1,0.05")] public float StoryDimAlpha = 0.6f;     // 劇情開始時畫面變暗的程度
    [Export(PropertyHint.Range, "0.1,3,0.1")] public float StoryDimSec = 0.7f;      // 變暗花幾秒
    async Task BeginStoryFreezeAsync()
    {
        if (_storyFrozen) return;
        StoryPlaying = true;
        _storyFrozen = true;
        _voiceSeq.Clear(); _seqCurrent = false;
        if (_trVoicePlayer != null && IsInstanceValid(_trVoicePlayer)) _trVoicePlayer.Stop();
        if (_trAmbientPlayer != null && IsInstanceValid(_trAmbientPlayer)) _trAmbientPlayer.Stop();
        foreach (var bb in new[] { _bubblePlayer, _bubbleGirl })
        {
            if (bb == null || !IsInstanceValid(bb)) continue;
            if (bb.HasMeta("hide_t")) bb.RemoveMeta("hide_t");
            bb.Visible = false;
        }
        _storyDim = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Stop };
        _storyDim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        UiRoot.AddChild(_storyDim);   // 在對話框容器之前加入，所以對話框會蓋在這層暗幕上面
        float t = 0f;
        while (t < 1f)
        {
            t += (float)GetProcessDeltaTime() / Mathf.Max(0.05f, StoryDimSec);
            _storyDim.Color = new Color(0, 0, 0, Mathf.Clamp(t, 0f, 1f) * StoryDimAlpha);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    // ── 淡場 ─────────────────────────────────────────────────────
    async Task FadeToBlackAsync()
    {
        var overlay = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        UiRoot.AddChild(overlay);

        float t = 0f;
        while (t < 1f)
        {
            t += (float)GetProcessDeltaTime() * 1.5f;
            overlay.Color = new Color(0, 0, 0, Mathf.Clamp(t, 0f, 1f));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    async Task FadeAndLoadAsync(string scene)
    {
        await FadeToBlackAsync();
        GetTree().ChangeSceneToFile($"res://Scenes/{scene}.tscn");
    }
}
