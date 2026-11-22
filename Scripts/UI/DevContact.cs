using Godot;

/// <summary>
/// 開發者聯絡按鈕（Discord／X）：主畫面左上角和設定頁左上角各放一組小圖示，
/// 點開會跳出視窗，顯示連結與 QR Code，可「開啟連結」「複製連結」。
/// 圖片放在 res://Resources/UI/contact/（logo_dc／qr_dc／logo_x／qr_x）。
/// 要改連結只改下面 DcUrl／XUrl。
/// </summary>
public static class DevContact
{
    public const string DcUrl = "https://discord.gg/xxPypterMU";
    public const string XUrl  = "https://x.com/Jitaiyanjiu";

    const string Dir = "res://Resources/UI/contact/";
    static readonly Color Ink    = new Color("79475f");
    static readonly Color Card   = new Color("fffafd");
    static readonly Color Border = new Color("edb8cf");

    static Texture2D Tex(string name)
        => ResourceLoader.Exists(Dir + name) ? GD.Load<Texture2D>(Dir + name) : null;

    /// <summary>在 host 的左上角放 Discord／X 兩顆圖示；點開的視窗掛在 host 上。</summary>
    public static void Attach(Control host, Vector2 pos, float iconSize = 56f)
    {
        if (host == null) return;
        var row = new HBoxContainer { Name = "DevContactRow", MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        row.Position = pos;
        host.AddChild(row);
        row.AddChild(MakeIcon(host, "logo_dc.png", "qr_dc.png", "Discord", DcUrl, iconSize));
        row.AddChild(MakeIcon(host, "logo_x.png",  "qr_x.png",  "X (Twitter)", XUrl, iconSize));
    }

    /// <summary>把 Discord／X 兩顆圖示放進一個容器（置中一排，給版面容器用，例如開發者工具視窗）；點開的視窗掛在 popupHost 上。</summary>
    public static void AttachInline(Container box, Control popupHost, float iconSize = 44f)
    {
        if (box == null || popupHost == null) return;
        var row = new HBoxContainer { Name = "DevContactRow", Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        box.AddChild(row);
        row.AddChild(MakeIcon(popupHost, "logo_dc.png", "qr_dc.png", "Discord", DcUrl, iconSize));
        row.AddChild(MakeIcon(popupHost, "logo_x.png",  "qr_x.png",  "X (Twitter)", XUrl, iconSize));
    }

    static TextureButton MakeIcon(Control host, string logo, string qr, string title, string url, float size)
    {
        var btn = new TextureButton
        {
            Name = "Contact_" + title,
            TextureNormal = Tex(logo),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size),
            FocusMode = Control.FocusModeEnum.None,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = title,
        };
        btn.MouseEntered += () => btn.Modulate = new Color(1.12f, 1.12f, 1.12f);
        btn.MouseExited  += () => btn.Modulate = Colors.White;
        btn.Pressed += () => ShowPopup(host, logo, qr, title, url);
        return btn;
    }

    static StyleBoxFlat Round(Color bg, Color border, int radius, int bw)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetCornerRadiusAll(radius); s.SetBorderWidthAll(bw);
        return s;
    }

    static void ShowPopup(Control host, string logo, string qr, string title, string url)
    {
        var old = host.GetNodeOrNull("DevContactOverlay");
        old?.QueueFree();

        var overlay = new ColorRect { Name = "DevContactOverlay", Color = new Color(0.07f, 0.04f, 0.11f, 0.7f), MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(overlay);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(center);

        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", Round(Card, Border, 28, 2));
        center.AddChild(box);
        var margin = new MarginContainer();
        foreach (var m in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" }) margin.AddThemeConstantOverride(m, 28);
        box.AddChild(margin);
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 12);
        margin.AddChild(v);

        var head = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        head.AddThemeConstantOverride("separation", 12);
        v.AddChild(head);
        head.AddChild(new TextureRect { Texture = Tex(logo), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(56, 56) });
        var tl = new Label { Text = title, VerticalAlignment = VerticalAlignment.Center };
        tl.AddThemeFontSizeOverride("font_size", 30); tl.AddThemeColorOverride("font_color", Ink);
        head.AddChild(tl);

        var ul = new Label { Text = url, HorizontalAlignment = HorizontalAlignment.Center };
        ul.AddThemeFontSizeOverride("font_size", 20); ul.AddThemeColorOverride("font_color", new Color("a7778e"));
        v.AddChild(ul);

        var qrTex = Tex(qr);
        if (qrTex != null)
            v.AddChild(new TextureRect { Texture = qrTex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(220, 220), TextureFilter = CanvasItem.TextureFilterEnum.Nearest });

        var tip = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(0, 26) };
        tip.AddThemeFontSizeOverride("font_size", 18); tip.AddThemeColorOverride("font_color", Ink);
        v.AddChild(tip);

        var btns = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btns.AddThemeConstantOverride("separation", 14);
        v.AddChild(btns);
        btns.AddChild(MakeBtn("開啟連結", () => OS.ShellOpen(url)));
        btns.AddChild(MakeBtn("複製連結", () => { DisplayServer.ClipboardSet(url); tip.Text = "已複製連結"; }));
        btns.AddChild(MakeBtn("關閉", () => overlay.QueueFree()));
    }

    static Button MakeBtn(string text, System.Action onPress)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(110, 44), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        b.AddThemeFontSizeOverride("font_size", 20);
        b.AddThemeColorOverride("font_color", Ink); b.AddThemeColorOverride("font_hover_color", Ink); b.AddThemeColorOverride("font_pressed_color", Ink);
        b.AddThemeStyleboxOverride("normal",  Round(new Color("fff0f6"), new Color("f0cede"), 16, 2));
        b.AddThemeStyleboxOverride("hover",   Round(new Color("ffe3ef"), Border, 16, 2));
        b.AddThemeStyleboxOverride("pressed", Round(new Color("f5cfe0"), Border, 16, 2));
        b.Pressed += onPress;
        return b;
    }
}
