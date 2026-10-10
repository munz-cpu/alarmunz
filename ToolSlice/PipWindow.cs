using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Drawing.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Cursors = System.Windows.Input.Cursors;

namespace ToolSlice;

public sealed class PipWindow : Window
{
    private readonly WindowTarget _target;
    private readonly InputForwarder _input;
    private readonly Image _image = new() { Stretch = Stretch.Uniform, Cursor = Cursors.Hand };
    private readonly TextBlock _status = new() { Foreground = Brushes.Gainsboro, FontSize = 11, Margin = new Thickness(8, 2, 8, 4) };
    private readonly DispatcherTimer _health;
    private GraphicsCaptureService? _capture;
    private int _frameQueued;
    private int _frameWidth, _frameHeight;
    private long _lastFrame;
    private bool _shownInputWarning;
    private bool _captureStarted;
    private Point? _lastPoint;
    private int _blackFrames;

    public PipWindow(WindowTarget target)
    {
        _target = target;
        _input = new InputForwarder(target.Handle);
        Title = $"Tool Slice · {target.Title}";
        Icon = new BitmapImage(new Uri("pack://application:,,,/ToolSlice;component/Assets/logo.png"));
        Width = Math.Clamp(target.Bounds.Width * target.Crop.Width * 0.7 + 20, 280, 900);
        Height = Math.Clamp(target.Bounds.Height * target.Crop.Height * 0.7 + 66, 190, 650);
        MinWidth = 220;
        MinHeight = 150;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;

        var border = new Border { Background = new SolidColorBrush(Color.FromRgb(23, 25, 31)), BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), SnapsToDevicePixels = true };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

        var bar = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(39, 42, 49)), LastChildFill = true };
        var close = new Button { Content = "×", Width = 30, ToolTip = "PIP 닫기", Margin = new Thickness(2) };
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right);
        bar.Children.Add(close);
        var name = new TextBlock { Text = target.Title, Foreground = Brushes.White, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        bar.Children.Add(name);
        bar.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock or DockPanel) DragMove(); };
        Grid.SetRow(bar, 0);
        layout.Children.Add(bar);

        var viewport = new Border { Background = Brushes.Black, Child = _image, ClipToBounds = true };
        Grid.SetRow(viewport, 1);
        layout.Children.Add(viewport);
        _image.MouseLeftButtonDown += (_, e) => ForwardDown(e, false);
        _image.MouseLeftButtonUp += (_, e) => ForwardUp(e, false);
        _image.MouseRightButtonDown += (_, e) => ForwardDown(e, true);
        _image.MouseRightButtonUp += (_, e) => ForwardUp(e, true);
        _image.MouseMove += (_, e) => ForwardMove(e);
        _image.MouseWheel += (_, e) => ForwardWheel(e);

        var foot = new Grid();
        _status.Text = "캡처 시작 중 · 입력은 놓을 때 원본에 전달됩니다";
        foot.Children.Add(_status);
        var grip = new Thumb { Width = 18, Height = 18, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = System.Windows.VerticalAlignment.Bottom, Cursor = Cursors.SizeNWSE, ToolTip = "크기 조절" };
        grip.DragDelta += (_, e) => { Width = Math.Max(MinWidth, Width + e.HorizontalChange); Height = Math.Max(MinHeight, Height + e.VerticalChange); };
        foot.Children.Add(grip);
        Grid.SetRow(foot, 2);
        layout.Children.Add(foot);
        border.Child = layout;
        Content = border;

        _health = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _health.Tick += (_, _) => CheckHealth();
        Loaded += (_, _) => StartCapture();
        Closed += (_, _) => { _health.Stop(); _capture?.Dispose(); };
    }

    private void StartCapture()
    {
        if (_captureStarted) return;
        _captureStarted = true;
        try
        {
            _capture = new GraphicsCaptureService(_target.Handle, _target.Crop);
            _capture.FrameReady += OnFrame;
            _capture.CaptureFailed += message => Dispatcher.BeginInvoke(() => _status.Text = $"캡처 실패: {message}");
            _capture.SourceClosed += () => Dispatcher.BeginInvoke(Close);
            _lastFrame = Environment.TickCount64;
            _health.Start();
        }
        catch (Exception ex)
        {
            _status.Text = $"캡처 불가: {ex.Message}";
        }
    }

    private void OnFrame(byte[] pixels, int width, int height)
    {
        if (Interlocked.Exchange(ref _frameQueued, 1) != 0) return;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (!IsLoaded) return;
                _image.Source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
                _frameWidth = width;
                _frameHeight = height;
                _lastFrame = Environment.TickCount64;
                _blackFrames = LooksBlack(pixels) ? _blackFrames + 1 : 0;
                _status.Text = _blackFrames >= 5
                    ? "검은 화면 · 보호 콘텐츠 또는 실제 검은 화면일 수 있습니다"
                    : _shownInputWarning ? "영상 정상 · 이 앱은 전달된 입력을 받지 않을 수 있습니다" : "영상 정상 · 클릭/드래그는 놓을 때 원본에 전달됩니다";
            }
            catch (Exception ex) { _status.Text = $"프레임 표시 실패: {ex.Message}"; }
            finally { Interlocked.Exchange(ref _frameQueued, 0); }
        }, DispatcherPriority.Render);
    }

    private void CheckHealth()
    {
        if (!WindowCatalog.IsAlive(_target.Handle)) { Close(); return; }
        if (Environment.TickCount64 - _lastFrame > 2500)
            _status.Text = "영상이 오지 않습니다 · 최소화 또는 캡처 보호 상태를 확인하세요";
    }

    private Point? Map(MouseEventArgs e)
    {
        if (_frameWidth <= 0 || _frameHeight <= 0 || !WindowCatalog.TryGetWindow(_target.Handle, out _, out var bounds)) return null;
        var control = e.GetPosition(_image);
        var scale = Math.Min(_image.ActualWidth / _frameWidth, _image.ActualHeight / _frameHeight);
        if (scale <= 0) return null;
        var drawWidth = _frameWidth * scale;
        var drawHeight = _frameHeight * scale;
        var nx = (control.X - (_image.ActualWidth - drawWidth) / 2) / drawWidth;
        var ny = (control.Y - (_image.ActualHeight - drawHeight) / 2) / drawHeight;
        if (nx < 0 || nx > 1 || ny < 0 || ny > 1) return null;
        return new Point(
            bounds.Left + (int)Math.Round((_target.Crop.X + nx * _target.Crop.Width) * bounds.Width),
            bounds.Top + (int)Math.Round((_target.Crop.Y + ny * _target.Crop.Height) * bounds.Height));
    }

    private void ForwardDown(MouseButtonEventArgs e, bool right)
    {
        var point = Map(e);
        if (point is null) return;
        _lastPoint = point;
        _image.CaptureMouse();
        _input.Down(point.Value, right);
        e.Handled = true;
    }

    private void ForwardUp(MouseButtonEventArgs e, bool right)
    {
        var point = Map(e) ?? _lastPoint;
        _image.ReleaseMouseCapture();
        if (point is not null)
        {
            Hide();
            try { if (!_input.Up(point.Value, right)) InputFailed(); }
            catch (Exception ex) { InputFailed(ex.Message); }
            finally { Show(); Topmost = true; }
        }
        _lastPoint = null;
        e.Handled = true;
    }

    private void ForwardMove(MouseEventArgs e)
    {
        var point = Map(e);
        if (point is not null) { _lastPoint = point; _input.Move(point.Value); }
    }

    private void ForwardWheel(MouseWheelEventArgs e)
    {
        var point = Map(e);
        if (point is not null)
        {
            Hide();
            try { if (!_input.Wheel(point.Value, e.Delta)) InputFailed(); }
            catch (Exception ex) { InputFailed(ex.Message); }
            finally { Show(); Topmost = true; }
        }
        e.Handled = true;
    }

    private void InputFailed(string? detail = null)
    {
        _shownInputWarning = true;
        _status.Text = detail is null
            ? "입력 전달 불가 · 권한이나 앱 입력 방식이 원인일 수 있습니다"
            : $"입력 전달 불가: {detail}";
    }

    private static bool LooksBlack(byte[] pixels)
    {
        var step = Math.Max(4, pixels.Length / 64 / 4 * 4);
        for (var i = 0; i + 2 < pixels.Length; i += step)
            if (pixels[i] > 8 || pixels[i + 1] > 8 || pixels[i + 2] > 8) return false;
        return true;
    }
}
