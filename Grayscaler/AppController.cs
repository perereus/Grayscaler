using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Grayscaler
{
    internal class AppController : ApplicationContext
    {
        private readonly NotifyIcon _tray;
        private readonly OverlayForm _overlay;
        private readonly Timer _trackTimer;
        private readonly ToolStripMenuItem _miToggle;

        private IntPtr _targetHwnd = IntPtr.Zero;
        private bool _grayEnabled = false;
        private Native.RECT _lastRect;

        private Native.LowLevelMouseProc _mouseProcDelegate;
        private IntPtr _mouseHookHandle = IntPtr.Zero;

        public AppController()
        {
            _overlay = new OverlayForm();
            // Force creation of the overlay handle right now (without showing it).
            // Otherwise the first _overlay.BeginInvoke(...) from the mouse hook would
            // throw InvalidOperationException because the handle doesn't exist yet
            // (it's only created on the first Show(), which happens AFTER selection).
            _ = _overlay.Handle;

            _trackTimer = new Timer { Interval = 30 };
            _trackTimer.Tick += TrackTimer_Tick;
            _trackTimer.Start();

            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Select window...", null, (s, e) => StartWindowPicker()));
            _miToggle = new ToolStripMenuItem("Enable grayscale", null, (s, e) => ToggleGray());
            menu.Items.Add(_miToggle);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitApp()));

            _tray = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(System.IO.Path.Combine(System.AppContext.BaseDirectory, "grayscaler.ico")),
                Visible = true,
                Text = "Grayscaler — right-click for options",
                ContextMenuStrip = menu
            };
            _tray.DoubleClick += (s, e) => StartWindowPicker();
        }

        private void StartWindowPicker()
        {
            if (_mouseHookHandle != IntPtr.Zero) return;

            _tray.Text = "Click the window you want to turn grayscale...";
            _mouseProcDelegate = MouseHookCallback;
            _mouseHookHandle = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProcDelegate, IntPtr.Zero, 0);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == Native.WM_LBUTTONDOWN)
            {
                var hookStruct = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                IntPtr hwnd = Native.WindowFromPoint(hookStruct.pt);
                hwnd = Native.GetAncestor(hwnd, Native.GA_ROOT);

                Native.UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;

                _overlay.BeginInvoke(new Action(() => OnWindowPicked(hwnd)));

                return (IntPtr)1;
            }

            return Native.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private void OnWindowPicked(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || hwnd == _overlay.Handle)
            {
                _tray.Text = "No window recognized, please try again";
                return;
            }

            _targetHwnd = hwnd;
            _grayEnabled = true;
            _lastRect = default;
            _miToggle.Text = "Disable grayscale";
            _tray.Text = "Grayscale active";
            SyncOverlay(force: true);
        }

        private void ToggleGray()
        {
            if (_targetHwnd == IntPtr.Zero)
            {
                StartWindowPicker();
                return;
            }

            _grayEnabled = !_grayEnabled;
            _miToggle.Text = _grayEnabled ? "Disable grayscale" : "Enable grayscale";

            if (_grayEnabled)
            {
                SyncOverlay(force: true);
            }
            else
            {
                _overlay.Hide();
            }
        }

        private void TrackTimer_Tick(object sender, EventArgs e)
        {
            if (!_grayEnabled || _targetHwnd == IntPtr.Zero) return;

            if (!Native.IsWindow(_targetHwnd))
            {
                _targetHwnd = IntPtr.Zero;
                _grayEnabled = false;
                _overlay.Hide();
                _miToggle.Text = "Activar gris";
                _tray.Text = "Ventana en gris — clic derecho para opciones";
                return;
            }

            if (Native.IsIconic(_targetHwnd))
            {
                _overlay.Hide();
                return;
            }

            SyncOverlay(force: false);
        }

        private void SyncOverlay(bool force)
        {
            Native.GetWindowRect(_targetHwnd, out var rect);

            bool changed = force
                || rect.Left != _lastRect.Left || rect.Top != _lastRect.Top
                || rect.Right != _lastRect.Right || rect.Bottom != _lastRect.Bottom;

            if (changed)
            {
                _lastRect = rect;
                _overlay.UpdateBoundsAndSource(rect);
            }

            if (!_overlay.Visible) _overlay.Show();

            // Reassert the Magnifier source on EVERY tick (it doesn't auto-update).
            _overlay.RefreshSource(rect);

            // Keep the overlay JUST above the target (not global topmost).
            // SetWindowPos(overlay, target) would put it BELOW the target (hWndInsertAfter
            // ends up in front). To stay above, we insert it after the window directly
            // above the target (GW_HWNDPREV). If that window is already the overlay
            // itself, it's correctly placed and we do nothing.
            IntPtr above = Native.GetWindow(_targetHwnd, Native.GW_HWNDPREV);
            if (above != _overlay.Handle)
            {
                Native.SetWindowPos(_overlay.Handle, above, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }
        }

        private void ExitApp()
        {
            _trackTimer.Stop();
            _tray.Visible = false;
            _overlay.Close();
            ExitThread();
        }
    }
}
