using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace ToolSlice;

public sealed class SelectionOverlay
{
    private readonly List<OverlayWindow> _windows = [];
    private Point _start;
    private bool _dragging;
    private bool _finished;
    public event Action<CaptureRegion?>? Completed;

    public void Show()
    {
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var window = new OverlayWindow(screen.Bounds, this);
            _windows.Add(window);
            window.Show();
        }
        if (_windows.Count > 0) _windows[0].Activate();
    }

    internal void Down(OverlayWindow owner)
    {
        if (_dragging) return;
        GetCursorPos(out _start);
        _dragging = true;
        Mouse.Capture(owner, CaptureMode.SubTree);
        Update();
    }

    internal void Move() { if (_dragging) Update(); }

    internal void Up()
    {
        if (!_dragging) return;
        GetCursorPos(out var end);
        Mouse.Capture(null);
        var region = CaptureRegion.FromPoints(_start, end);
        Finish(region.Width >= 8 && region.Height >= 8 ? region : null);
    }

    internal void Cancel() => Finish(null);

    private void Update()
    {
        GetCursorPos(out var end);
        var region = CaptureRegion.FromPoints(_start, end);
        foreach (var window in _windows) window.Draw(region.Bounds);
    }

    private void Finish(CaptureRegion? region)
    {
        if (_finished) return;
        _finished = true;
        Mouse.Capture(null);
        foreach (var window in _windows) window.Close();
        Completed?.Invoke(region);
    }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);

    internal sealed class OverlayWindow : Window
    {
        private readonly Rectangle _screen;
        private readonly SelectionOverlay _owner;
        private readonly Canvas _canvas = new();
        private readonly System.Windows.Shapes.Rectangle _selection = new()
        {
            Stroke = Brushes.White, StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Visibility = Visibility.Collapsed
        };

        public OverlayWindow(Rectangle screen, SelectionOverlay owner)
        {
            _screen = screen;
            _owner = owner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = true;
            Topmost = true;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
            Cursor = Cursors.Cross;
            Focusable = true;
            _canvas.Children.Add(_selection);
            Content = _canvas;
            SourceInitialized += (_, _) => PositionPhysical();
            MouseLeftButtonDown += (_, _) => _owner.Down(this);
            MouseMove += (_, _) => _owner.Move();
            MouseLeftButtonUp += (_, _) => _owner.Up();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) _owner.Cancel(); };
        }

        private void PositionPhysical()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowPos(hwnd, new IntPtr(-1), _screen.X, _screen.Y, _screen.Width, _screen.Height, 0x0040);
        }

        public void Draw(Rectangle bounds)
        {
            var intersection = Rectangle.Intersect(_screen, bounds);
            _selection.Visibility = intersection.Width > 0 && intersection.Height > 0 ? Visibility.Visible : Visibility.Collapsed;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget is null) return;
            var scale = source.CompositionTarget.TransformFromDevice;
            var topLeft = scale.Transform(new System.Windows.Point(intersection.X - _screen.X, intersection.Y - _screen.Y));
            var size = scale.Transform(new System.Windows.Point(intersection.Width, intersection.Height));
            Canvas.SetLeft(_selection, topLeft.X);
            Canvas.SetTop(_selection, topLeft.Y);
            _selection.Width = size.X;
            _selection.Height = size.Y;
        }

        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }
}
