using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinUI3TwikitDeck.Bridge;

namespace WinUI3TwikitDeck.Helpers;

/// <summary>
/// ウィンドウの位置・サイズの復元と保存。
/// <list type="bullet">
/// <item>初回は、ウィンドウを出すモニターの作業領域に合わせた大きさで中央に置く
/// （既定の CW_USEDEFAULT は解像度を見ないため、縦長モニターでは 810x1395 のような縦長になる）。</item>
/// <item>2 回目以降は保存した位置へ移してから、そのモニターの DPI で DIP のサイズを物理ピクセルに直して復元する。</item>
/// <item>モニター構成が変わって保存位置が画面外になったときは、最も近いモニターの作業領域に収める。</item>
/// </list>
/// </summary>
internal sealed class WindowPlacementManager
{
    /// <summary>初回のウィンドウサイズ（作業領域に対する比率）。</summary>
    private const double DefaultWorkAreaRatio = 0.9;

    /// <summary>復元時にこれより小さくはしない（DIP）。</summary>
    private const double MinWidthDip = 480;
    private const double MinHeightDip = 360;

    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private readonly DispatcherQueueTimer _saveTimer;
    private WindowPlacement _placement = new();
    private bool _restoring;

    public WindowPlacementManager(Window window)
    {
        _window = window;
        _appWindow = window.AppWindow;

        // 移動・サイズ変更のたびに書かず、少し落ち着いてから保存する
        _saveTimer = window.DispatcherQueue.CreateTimer();
        _saveTimer.Interval = TimeSpan.FromMilliseconds(500);
        _saveTimer.IsRepeating = false;
        _saveTimer.Tick += (_, _) => Save();
    }

    /// <summary>ウィンドウがあるモニターの表示倍率（96 DPI = 1.0）。PerMonitorV2 なのでモニター間を移動すると変わる。</summary>
    public double Scale
    {
        get
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            var dpi = GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }
    }

    /// <summary><c>Activate()</c> の前に呼ぶ。保存があれば復元し、無ければモニターに合わせた既定の配置にする。</summary>
    public void Restore()
    {
        _restoring = true;
        try
        {
            var saved = WindowPlacementStore.Load();
            if (saved is { Width: > 0, Height: > 0 })
            {
                _placement = saved;
                RestoreSaved(saved);
            }
            else
            {
                ApplyDefault();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ウィンドウ配置の復元に失敗: {ex.Message}");
        }
        finally
        {
            _restoring = false;
        }

        // 復元中の Changed は無視しているので、決まった配置をここで取り込む（動かさずに閉じても保存されるように）
        Capture();

        _appWindow.Changed += OnAppWindowChanged;
        _window.Closed += (_, _) =>
        {
            _appWindow.Changed -= OnAppWindowChanged;
            _saveTimer.Stop();
            Save();
        };
    }

    private void ApplyDefault()
    {
        var display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
        var work = display.WorkArea;
        var width = Math.Max(1, (int)(work.Width * DefaultWorkAreaRatio));
        var height = Math.Max(1, (int)(work.Height * DefaultWorkAreaRatio));
        var x = work.X + (work.Width - width) / 2;
        var y = work.Y + (work.Height - height) / 2;
        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void RestoreSaved(WindowPlacement saved)
    {
        // 保存時の倍率で物理ピクセルの矩形を推定し、保存位置に最も近いモニターを探す
        var savedScale = saved.Scale > 0 ? saved.Scale : 1.0;
        var guess = new RectInt32(
            saved.X,
            saved.Y,
            Math.Max(1, (int)(saved.Width * savedScale)),
            Math.Max(1, (int)(saved.Height * savedScale)));
        var display = DisplayArea.GetFromRect(guess, DisplayAreaFallback.Nearest);
        var work = display.WorkArea;

        // まず位置だけ移す。拡大率の違うモニターへ移ると Windows が WM_DPICHANGED でウィンドウを拡縮するので、
        // サイズは移した後の DPI で決める（同時に指定すると二重に拡縮される）。
        var x = Clamp(saved.X, work.X, work.X + work.Width - 1);
        var y = Clamp(saved.Y, work.Y, work.Y + work.Height - 1);
        _appWindow.Move(new PointInt32(x, y));

        var scale = Scale;
        var width = Clamp((int)Math.Round(saved.Width * scale), (int)(MinWidthDip * scale), work.Width);
        var height = Clamp((int)Math.Round(saved.Height * scale), (int)(MinHeightDip * scale), work.Height);

        // 作業領域からはみ出す分は中へ戻す
        x = Clamp(x, work.X, work.X + work.Width - width);
        y = Clamp(y, work.Y, work.Y + work.Height - height);
        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));

        if (saved.IsMaximized && _appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_restoring || !(args.DidPositionChange || args.DidSizeChange || args.DidPresenterChange))
        {
            return;
        }

        Capture();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>
    /// 現在の配置をメモリに取り込む。最大化中はフラグだけ更新して、通常状態のときの位置・サイズを保つ
    /// （最大化を解除したときに戻る場所）。最小化中は何もしない（位置が -32000 になる）。
    /// </summary>
    private void Capture()
    {
        var state = (_appWindow.Presenter as OverlappedPresenter)?.State ?? OverlappedPresenterState.Restored;
        if (state == OverlappedPresenterState.Minimized)
        {
            return;
        }

        _placement.IsMaximized = state == OverlappedPresenterState.Maximized;
        if (_placement.IsMaximized)
        {
            return;
        }

        var position = _appWindow.Position;
        var size = _appWindow.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var scale = Scale;
        _placement.X = position.X;
        _placement.Y = position.Y;
        _placement.Width = size.Width / scale;
        _placement.Height = size.Height / scale;
        _placement.Scale = scale;
    }

    private void Save()
    {
        if (_placement.Width <= 0 || _placement.Height <= 0)
        {
            return;
        }

        WindowPlacementStore.Save(_placement.Clone());
    }

    /// <summary><see cref="Math.Clamp(int, int, int)"/> と違い、min &gt; max でも例外にしない（作業領域が最小サイズより小さいときなど）。</summary>
    private static int Clamp(int value, int min, int max)
    {
        if (min > max)
        {
            min = max;
        }

        return Math.Max(min, Math.Min(max, value));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
