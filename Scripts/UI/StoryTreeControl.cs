using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 故事樹狀圖渲染控制項
/// 使用 DFS 後序計算布局，_Draw() 繪製連線，子節點 Panel 顯示節點框
/// </summary>
public partial class StoryTreeControl : Control
{
    // ─── 布局常數 ────────────────────────────────────────────────
    const float BOX_W    = 210f;
    const float BOX_H    = 58f;
    const float COL_GAP  = 55f;    // 橫向間距（節點框之間）
    const float ROW_GAP  = 12f;    // 縱向間距（節點框之間）
    const float COL_STEP = BOX_W + COL_GAP;
    const float ROW_STEP = BOX_H + ROW_GAP;
    const float PAD      = 20f;    // 四周留白

    // ─── 顏色（配合遊戲風格）────────────────────────────────────
    static readonly Color COL_STORY      = new("4a3060");   // 紫
    static readonly Color COL_CHOICE     = new("1e3a6a");   // 藍
    static readonly Color COL_ENDING     = new("5a3800");   // 金/棕
    static readonly Color COL_CHALLENGE  = new("1a4430");   // 綠
    static readonly Color COL_ROUTE      = new("2a2a48");   // 灰
    static readonly Color COL_INCOMPLETE = new("3a1e2e");   // 暗紅
    static readonly Color COL_BORDER_ENDING  = new("c8982e");
    static readonly Color COL_BORDER_DEFAULT = new("6040a8");
    static readonly Color COL_LINE       = new(0.5f, 0.4f, 0.7f, 0.5f);
    static readonly Color COL_LINE_SHARED= new(0.8f, 0.5f, 0.3f, 0.5f);
    static readonly Color COL_UNLOCKED_GLOW = new("7c5de8");

    // ─── 狀態 ────────────────────────────────────────────────────
    readonly Dictionary<string, Vector2> _pos   = new();  // 節點左上角位置
    readonly HashSet<string>             _visitedLayout = new();
    float _nextLeafY = PAD;

    // 連線資料（for _Draw）
    readonly List<(Vector2 from, Vector2 to, bool shared)> _lines = new();

    // 是否正在顯示（用於 RefreshPanels 呼叫）
    bool _built = false;

    // 外部回呼：解鎖後通知 GalleryScreen 刷新
    public Action OnUnlockChanged;

    // ─── 生命週期 ────────────────────────────────────────────────
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        ComputeLayout(StoryTreeData.CurRoot, PAD);
        BuildLines();
        SetSize();
        BuildNodePanels();
        if (!GalleryDataManager.AllAgesMode) BuildMiniGamePanel(); // 小遊戲說明只屬於 R18 版
        _built = true;
    }

    public override void _Draw()
    {
        foreach (var (from, to, shared) in _lines)
        {
            var col = shared ? COL_LINE_SHARED : COL_LINE;
            DrawLine(from, to, col, 1.5f, true);
        }
    }

    // ─── 布局計算（DFS 後序）────────────────────────────────────

    void ComputeLayout(string id, float x)
    {
        if (_visitedLayout.Contains(id)) return;
        _visitedLayout.Add(id);

        var edges = StoryTreeData.CurEdges.GetValueOrDefault(id);

        if (edges == null || edges.Count == 0)
        {
            _pos[id] = new Vector2(x, _nextLeafY);
            _nextLeafY += ROW_STEP;
            return;
        }

        // 遞迴放置尚未放置的子節點
        float startY = _nextLeafY;
        foreach (var edge in edges)
        {
            if (!_visitedLayout.Contains(edge.To))
                ComputeLayout(edge.To, x + COL_STEP);
        }
        float endY = _nextLeafY;

        // 取得有位置的子節點 Y 範圍
        var childYs = edges
            .Where(e => _pos.ContainsKey(e.To))
            .Select(e => _pos[e.To].Y + BOX_H / 2f)
            .ToList();

        float myY;
        if (childYs.Count > 0)
            myY = (childYs.Min() + childYs.Max()) / 2f - BOX_H / 2f;
        else
        {
            // 全部都是共享節點，直接放在目前位置
            myY = _nextLeafY;
            _nextLeafY += ROW_STEP;
        }

        _pos[id] = new Vector2(x, myY);
    }

    // ─── 計算連線（節點框右中 → 子節點框左中，折線）────────────

    void BuildLines()
    {
        foreach (var (parentId, parentPos) in _pos)
        {
            var edges = StoryTreeData.CurEdges.GetValueOrDefault(parentId);
            if (edges == null) continue;

            var pRight = new Vector2(parentPos.X + BOX_W, parentPos.Y + BOX_H / 2f);

            foreach (var edge in edges)
            {
                if (!_pos.TryGetValue(edge.To, out var childPos)) continue;
                var cLeft = new Vector2(childPos.X, childPos.Y + BOX_H / 2f);

                float midX = (pRight.X + cLeft.X) / 2f;

                // 折線：右出 → 垂直 → 左入
                _lines.Add((pRight, new Vector2(midX, pRight.Y), edge.Shared));
                _lines.Add((new Vector2(midX, pRight.Y), new Vector2(midX, cLeft.Y), edge.Shared));
                _lines.Add((new Vector2(midX, cLeft.Y), cLeft, edge.Shared));
            }
        }
    }

    void SetSize()
    {
        float maxX = _pos.Values.Max(v => v.X) + BOX_W + PAD;
        float maxY = _pos.Values.Max(v => v.Y) + BOX_H + PAD;
        CustomMinimumSize = new Vector2(maxX, maxY);
        Size = new Vector2(maxX, maxY);
    }

    // ─── 節點框 ──────────────────────────────────────────────────

    void BuildNodePanels()
    {
        foreach (var (id, pos) in _pos)
        {
            if (!StoryTreeData.CurNodes.TryGetValue(id, out var node)) continue;
            var panel = BuildNodePanel(node);
            AddChild(panel);
            panel.Position = pos;
        }
    }

    Panel BuildNodePanel(StoryTreeData.StoryNode node)
    {
        var gdm = GalleryDataManager.Instance;
        bool unlocked = IsNodeUnlocked(node, gdm);

        // 外框
        var panel = new Panel
        {
            Name            = $"Node_{node.Id}",
            CustomMinimumSize = new Vector2(BOX_W, BOX_H),
            Size            = new Vector2(BOX_W, BOX_H),
            MouseFilter     = MouseFilterEnum.Stop,
            TooltipText     = BuildTooltip(node),
        };
        panel.AddThemeStyleboxOverride("panel", NodeSty(node, unlocked));

        // 內容 HBox
        var hb = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        hb.AddThemeConstantOverride("separation", 4);
        panel.AddChild(hb);
        hb.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        hb.OffsetLeft = 8; hb.OffsetRight = -48;
        hb.OffsetTop = 6; hb.OffsetBottom = -6;

        // 類型條
        var typeBar = new ColorRect
        {
            CustomMinimumSize = new Vector2(4, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Color             = TypeColor(node.Type),
            MouseFilter       = MouseFilterEnum.Ignore,
        };
        hb.AddChild(typeBar);

        // 文字區
        var vb = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter         = MouseFilterEnum.Ignore,
        };
        vb.AddThemeConstantOverride("separation", 2);
        hb.AddChild(vb);

        // 節點標籤
        var nameLbl = new Label
        {
            Text         = node.Label,
            AutowrapMode = TextServer.AutowrapMode.Word,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        nameLbl.AddThemeFontSizeOverride("font_size", 11);
        nameLbl.AddThemeColorOverride("font_color", unlocked
            ? new Color("f0e8ff")
            : new Color("887ca8"));
        vb.AddChild(nameLbl);

        // 解鎖按鈕（始終顯示，在 dev 模式才有實際效果的解鎖，其他情況只唯讀）
        var unlockBtn = new Button
        {
            Text            = unlocked ? "✓" : "○",
            CustomMinimumSize = new Vector2(36, BOX_H - 8),
            TooltipText     = unlocked ? "已解鎖（點擊重新鎖定）" : "點擊解鎖",
            MouseFilter     = MouseFilterEnum.Stop,
        };
        unlockBtn.AddThemeFontSizeOverride("font_size", 14);
        var ubSty = new StyleBoxFlat();
        ubSty.BgColor      = unlocked ? new Color("3a2060") : new Color("1e1830");
        ubSty.BorderColor  = unlocked ? new Color("7c5de8") : new Color("3d3d60");
        ubSty.SetBorderWidthAll(1);
        ubSty.SetCornerRadiusAll(4);
        unlockBtn.AddThemeStyleboxOverride("normal", ubSty);
        var node_captured = node;
        var panel_captured = panel;
        unlockBtn.Pressed += () => ToggleNodeUnlock(node_captured, panel_captured);

        panel.AddChild(unlockBtn);
        unlockBtn.SetAnchorsAndOffsetsPreset(LayoutPreset.RightWide);
        unlockBtn.OffsetLeft   = -44;
        unlockBtn.OffsetRight  = -4;
        unlockBtn.OffsetTop    = 4;
        unlockBtn.OffsetBottom = -4;

        return panel;
    }

    // ─── 解鎖邏輯 ────────────────────────────────────────────────

    bool IsNodeUnlocked(StoryTreeData.StoryNode node, GalleryDataManager gdm)
    {
        if (gdm == null) return false;
        if (node.Type == StoryTreeData.NodeType.Ending && node.EndingNum > 0)
            return (IsAANode(node) ? gdm.IsAAEndingUnlocked(node.EndingNum) : gdm.IsEndingUnlocked(node.EndingNum))
                || gdm.IsStoryNodeUnlocked(node.Id);
        return gdm.IsStoryNodeUnlocked(node.Id);
    }

    // 全年齡版節點（AANodes 裡的）→ 結局解鎖記在全年齡那份
    static bool IsAANode(StoryTreeData.StoryNode node) => StoryTreeData.AANodes.ContainsKey(node.Id);

    void ToggleNodeUnlock(StoryTreeData.StoryNode node, Panel panel)
    {
        var gdm = GalleryDataManager.Instance;
        if (gdm == null) return;

        bool wasUnlocked = IsNodeUnlocked(node, gdm);

        if (wasUnlocked)
        {
            // 重新鎖定（只能在 dev 模式下操作）
            if (DevModeManager.Instance?.Unlocked != true) return;
            gdm.LockStoryNode(node.Id);
            // Ending 鎖定
            if (node.Type == StoryTreeData.NodeType.Ending && node.EndingNum > 0)
            {
                if (IsAANode(node)) gdm.LockAAEnding(node.EndingNum); else gdm.LockEnding(node.EndingNum);
            }
        }
        else
        {
            gdm.UnlockStoryNode(node.Id);
            if (node.Type == StoryTreeData.NodeType.Ending && node.EndingNum > 0)
            {
                if (IsAANode(node)) gdm.UnlockAAEnding(node.EndingNum); else gdm.UnlockEnding(node.EndingNum);
            }
        }

        // 重新建立這個節點的面板
        var old = GetNodeOrNull<Panel>($"Node_{node.Id}");
        if (old != null)
        {
            var pos = old.Position;
            old.QueueFree();
            var newPanel = BuildNodePanel(node);
            AddChild(newPanel);
            newPanel.Position = pos;
        }

        OnUnlockChanged?.Invoke();
    }

    // ─── 工具 ────────────────────────────────────────────────────

    string BuildTooltip(StoryTreeData.StoryNode node)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{node.Id}] {node.Label}");
        sb.AppendLine($"類型：{NodeTypeName(node.Type)}　時間：{node.Time}");
        if (node.File != null) sb.AppendLine($"檔案：{node.File}");
        if (node.EndingNum > 0) sb.AppendLine($"結局編號：{node.EndingNum}");
        if (!string.IsNullOrEmpty(node.Preview) && node.Preview != node.Label)
            sb.Append($"\n{node.Preview.Substring(0, Math.Min(80, node.Preview.Length))}…");
        return sb.ToString().Trim();
    }

    static string NodeTypeName(StoryTreeData.NodeType t) => t switch
    {
        StoryTreeData.NodeType.Story      => "劇情",
        StoryTreeData.NodeType.Choice     => "選擇",
        StoryTreeData.NodeType.Ending     => "結局",
        StoryTreeData.NodeType.Challenge  => "勝敗判定",
        StoryTreeData.NodeType.Route      => "路由",
        StoryTreeData.NodeType.Incomplete => "尚未填寫",
        _ => "未知",
    };

    static Color TypeColor(StoryTreeData.NodeType t) => t switch
    {
        StoryTreeData.NodeType.Ending     => new Color("c8982e"),
        StoryTreeData.NodeType.Choice     => new Color("3a7ad4"),
        StoryTreeData.NodeType.Challenge  => new Color("3a9a58"),
        StoryTreeData.NodeType.Route      => new Color("555570"),
        StoryTreeData.NodeType.Incomplete => new Color("884444"),
        _                                 => new Color("8855cc"),
    };

    StyleBoxFlat NodeSty(StoryTreeData.StoryNode node, bool unlocked)
    {
        var sb = new StyleBoxFlat();
        sb.BgColor = node.Type switch
        {
            StoryTreeData.NodeType.Ending     => new Color("2a1a04"),
            StoryTreeData.NodeType.Choice     => new Color("0e1f40"),
            StoryTreeData.NodeType.Challenge  => new Color("081a10"),
            StoryTreeData.NodeType.Route      => new Color("141424"),
            StoryTreeData.NodeType.Incomplete => new Color("1a0a14"),
            _                                 => new Color("1a1030"),
        };
        sb.BorderColor = unlocked
            ? TypeColor(node.Type)
            : new Color(TypeColor(node.Type), 0.35f);
        sb.SetBorderWidthAll(unlocked ? 2 : 1);
        sb.SetCornerRadiusAll(6);
        sb.AntiAliasing = true;
        if (unlocked)
        {
            sb.ShadowColor = new Color(TypeColor(node.Type), 0.4f);
            sb.ShadowSize = 6;
        }
        return sb;
    }

    // ─── 小遊戲獨立測試面板（顯示在樹狀圖右側）────────────────────
    void BuildMiniGamePanel()
    {
        if (_pos.Count == 0) return;

        float panelX = _pos.Values.Max(v => v.X) + BOX_W + PAD * 2f;
        const float PW = 220f;
        const float PH = 230f;

        var sty = new StyleBoxFlat
        {
            BgColor     = new Color("0b1828"),
            BorderColor = new Color("3a7ad4"),
        };
        sty.SetBorderWidthAll(2);
        sty.SetCornerRadiusAll(8);

        var panel = new Panel { Name = "MiniGamePanel" };
        panel.Position = new Vector2(panelX, PAD);
        panel.Size     = new Vector2(PW, PH);
        panel.AddThemeStyleboxOverride("panel", sty);
        AddChild(panel);

        // 標題
        var title = new Label
        {
            Text = "🎮 小遊戲 · 獨立測試",
            Position = new Vector2(0, 8),
            Size     = new Vector2(PW, 26),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        title.AddThemeFontSizeOverride("font_size", 12);
        title.AddThemeColorOverride("font_color", new Color("ffd060"));
        panel.AddChild(title);

        // 節點說明
        var info = new Label
        {
            Text = "N020 挑戰結果（勝敗判定）\nN021 → 結局3（挑戰成功）\nN022 → 結局2（挑戰失敗）",
            Position = new Vector2(8, 42),
            Size     = new Vector2(PW - 16, 62),
            AutowrapMode = TextServer.AutowrapMode.Word,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        info.AddThemeFontSizeOverride("font_size", 10);
        info.AddThemeColorOverride("font_color", new Color("7888aa"));
        panel.AddChild(info);

        // 啟動按鈕
        var btnNormal = new StyleBoxFlat { BgColor = new Color("0e2040"), BorderColor = new Color("4488ee") };
        btnNormal.SetBorderWidthAll(2); btnNormal.SetCornerRadiusAll(6);
        var btnHover  = new StyleBoxFlat { BgColor = new Color("1a3a60"), BorderColor = new Color("66aaff") };
        btnHover.SetBorderWidthAll(2);  btnHover.SetCornerRadiusAll(6);

        var btn = new Button
        {
            Name   = "BtnLaunchMiniGame",
            Text   = "▶  啟動小遊戲",
            Position = new Vector2(8, 114),
            Size     = new Vector2(PW - 16, 44),
            MouseFilter = MouseFilterEnum.Stop,
        };
        btn.AddThemeFontSizeOverride("font_size", 13);
        btn.AddThemeStyleboxOverride("normal", btnNormal);
        btn.AddThemeStyleboxOverride("hover",  btnHover);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.Pressed += () =>
        {
            TrainingRoomRuntime.PendingMiniGameOnly = true;
            GetTree().ChangeSceneToFile("res://Scenes/TrainingRoom.tscn");
        };
        panel.AddChild(btn);

        // 備注
        var note = new Label
        {
            Text = "※ 進入後小遊戲直接啟動\n   勝敗分支由遊戲結果決定",
            Position = new Vector2(8, 168),
            Size     = new Vector2(PW - 16, 52),
            AutowrapMode = TextServer.AutowrapMode.Word,
            MouseFilter  = MouseFilterEnum.Ignore,
        };
        note.AddThemeFontSizeOverride("font_size", 10);
        note.AddThemeColorOverride("font_color", new Color("554466"));
        panel.AddChild(note);

        // 更新 Control 尺寸以涵蓋小遊戲面板
        float newW = panelX + PW + PAD;
        CustomMinimumSize = new Vector2(newW, CustomMinimumSize.Y);
        Size               = new Vector2(newW, Size.Y);
    }
}
