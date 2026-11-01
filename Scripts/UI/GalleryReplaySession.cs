using Godot;
using System;
using System.Collections.Generic;

/// <summary>保留圖鑑所在場景，以獨立場景覆蓋播放；關閉時還原原場景。</summary>
public partial class GalleryReplaySession : CanvasLayer
{
    public static GalleryReplaySession Active { get; private set; }
    Node _menu, _playback;
    Control _focus;
    Node.ProcessModeEnum _previousMode;
    bool _previousVisible, _closing;
    Action _restoreReading;
    readonly List<(AudioStreamPlayer Player, bool Paused)> _audio = new();

    public static void Open(Control gallery, string scenePath)
    {
        if (Active != null) return;
        var packed = ResourceLoader.Load<PackedScene>(scenePath);
        if (packed == null) { GD.PushWarning("無法開啟圖鑑回放：" + scenePath); return; }
        var tree = gallery.GetTree();
        var menu = tree.CurrentScene;
        if (menu == null) return;
        var session = new GalleryReplaySession { Layer = 2000, Name = "GalleryReplay" };
        session._menu = menu;
        session._playback = packed.Instantiate();
        session._previousMode = menu.ProcessMode;
        session._previousVisible = menu is CanvasItem item && item.Visible;
        session._focus = gallery.GetViewport().GuiGetFocusOwner();
        session._restoreReading = NovelReader.BeginIsolatedReading();
        Active = session;
        session.PauseAudio(menu);
        menu.ProcessMode = Node.ProcessModeEnum.Disabled;
        if (menu is CanvasItem canvas) canvas.Hide();
        session._focus?.ReleaseFocus();
        tree.Root.AddChild(session);
        tree.Root.AddChild(session._playback);
        tree.CurrentScene = session._playback;
    }

    void PauseAudio(Node node)
    {
        if (node is AudioStreamPlayer player)
        {
            _audio.Add((player, player.StreamPaused));
            player.StreamPaused = true;
        }
        foreach (Node child in node.GetChildren()) PauseAudio(child);
    }

    public override void _Ready()
    {
        var close = new Button { Name = "CloseReplay", Text = "×  關閉回放", TooltipText = "返回原本的圖鑑（Esc）" };
        AddChild(close);
        close.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        close.OffsetLeft = -244; close.OffsetRight = -28;
        close.OffsetTop = 24; close.OffsetBottom = 80;
        close.AddThemeFontSizeOverride("font_size", 24);
        UIStyle.ApplyButtonStyle(close);
        close.Pressed += Close;
    }

    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Keycode: Key.Escape, Pressed: true, Echo: false })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }

    public void Close()
    {
        if (_closing) return;
        _closing = true;
        Callable.From(Finish).CallDeferred();
    }

    void Finish()
    {
        var tree = GetTree();
        if (IsInstanceValid(_playback))
        {
            _playback.GetParent()?.RemoveChild(_playback);
            _playback.QueueFree();
        }
        _restoreReading?.Invoke();
        EndingBaseRuntime.FromGallery = false;
        if (IsInstanceValid(_menu))
        {
            tree.CurrentScene = _menu;
            _menu.ProcessMode = _previousMode;
            if (_menu is CanvasItem canvas) canvas.Visible = _previousVisible;
            foreach (var (player, paused) in _audio)
                if (IsInstanceValid(player)) player.StreamPaused = paused;
            if (IsInstanceValid(_focus) && _focus.IsVisibleInTree()) _focus.GrabFocus();
        }
        Active = null;
        QueueFree();
    }
}
