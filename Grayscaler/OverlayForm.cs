using System;
using System.Drawing;
using System.Windows.Forms;

namespace Grayscaler
{
    internal class OverlayForm : Form
    {
        private IntPtr _magHwnd = IntPtr.Zero;

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Black;
            Native.MagInitialize();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED
                             | Native.WS_EX_TRANSPARENT
                             | Native.WS_EX_TOOLWINDOW
                             | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            Native.SetLayeredWindowAttributes(Handle, 0, 255, Native.LWA_ALPHA);

            _magHwnd = Native.CreateWindowEx(
                0, "Magnifier", "MagnifierWindow",
                Native.WS_CHILD | Native.WS_VISIBLE,
                0, 0, Math.Max(Width, 1), Math.Max(Height, 1),
                Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_magHwnd != IntPtr.Zero)
            {
                var effect = Native.MagColorEffectGrayscale();
                Native.MagSetColorEffect(_magHwnd, ref effect);
            }
        }

        // Adjusts position/size of the overlay and the Magnifier control (only when they change).
        public void UpdateBoundsAndSource(Native.RECT rect)
        {
            int w = Math.Max(rect.Width, 1);
            int h = Math.Max(rect.Height, 1);

            Bounds = new Rectangle(rect.Left, rect.Top, w, h);

            if (_magHwnd != IntPtr.Zero)
            {
                Native.SetWindowPos(_magHwnd, IntPtr.Zero, 0, 0, w, h,
                    Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            }
            // ponytail: the source is reasserted by SyncOverlay on every tick; not needed here.
        }

        // The Magnifier control does NOT auto-update: the source must be reasserted
        // on every tick so it recaptures the region and repaints the grayscale content.
        public void RefreshSource(Native.RECT rect)
        {
            if (_magHwnd == IntPtr.Zero) return;
            Native.MagSetWindowSource(_magHwnd, rect);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            Native.MagUninitialize();
        }
    }
}
