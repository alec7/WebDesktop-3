﻿using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Gma.System.MouseKeyHook;

namespace TransparentWindow.Forms
{
    public class FullScreenTransparentForm
        : XnaForm
    {
        public Screen Screen { get; private set; }

        private readonly IKeyboardMouseEvents _globalHook;
        private bool _mouseHookEnabled = false;

        private GraphicsPath _clickRegion = null;

        private bool _isClickable = true;

        protected FullScreenTransparentForm(Screen screen, bool autoInvalidate = true)
            : base(autoInvalidate)
        {
            Screen = screen;

            FormBorderStyle = FormBorderStyle.None;
            Visible = true;
            ShowInTaskbar = false;

            MakeNotClickable();
            HideFromAltTab();

            _globalHook = Hook.GlobalEvents();
        }

        #region window styling
        private void HideFromAltTab()
        {
            // http://stackoverflow.com/a/551847/108234
            var windowStyle = GetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE);
            windowStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
            SetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE, windowStyle);
        }
        #endregion

        #region click regions
        private void OnGlobalMouseMove(object sender, MouseEventArgs e)
        {
            //Hook to the *global* mouse mouse events, if the mouse enters a "clickable" region of this form change the style (of the entire form) to clickable
            //Once the mouse leaves the clickable area, change the entire form back to unclickable!

            if (_clickRegion == null)
                return;

            var p = new Point(e.X - DesktopBounds.X, e.Y - DesktopBounds.Y);
            if (p.X >= 0 && p.Y >= 0 && p.X <= Bounds.Width && p.Y <= Bounds.Width)
            {
                if (_clickRegion.IsVisible(p))
                    MakeClickable();
                else
                    MakeNotClickable();
            }
        }

        private void MakeClickable()
        {
            if (_isClickable)
                return;

            // Set the form clickable
            int initialStyle = GetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE);
            SetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE, initialStyle & ~0x20);

            _isClickable = true;
        }

        private void MakeNotClickable()
        {
            if (!_isClickable)
                return;

            // Set the form click-through
            int initialStyle = GetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE);
            SetWindowLong(Handle, (int)GetWindowLongFields.GWL_EXSTYLE, initialStyle | 0x80000 | 0x20);

            _isClickable = false;
        }

        public void AddClickRegion(GraphicsPath region)
        {
            if (_clickRegion == null)
                _clickRegion = region;
            else
                _clickRegion.AddPath(region, false);
        }

        public void ClearClickRegion()
        {
            _clickRegion = null;
        }

        public void SetClickRegion(GraphicsPath path)
        {
            _clickRegion = path;
        }
        #endregion

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            //Send to background, do not activate, set size to screen working ares
            const int BOTTOM = 1;
            SetWindowPos(
                Handle,
                (IntPtr)BOTTOM,                                                                                     //Send to bottom of screen stack
                Screen.WorkingArea.X, Screen.WorkingArea.Y, Screen.WorkingArea.Width, Screen.WorkingArea.Height,    //Set size to working area of this screen
                SWP_NOACTIVATE | SWP_NOCOPYBITS                                                                     //Don't activate the window and don't bother copying current window contents
            );
        }

        protected override void Dispose(bool disposing)
        {
            _globalHook.MouseMoveExt -= OnGlobalMouseMove;
            _globalHook.Dispose();

            base.Dispose(disposing);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);

            //Send to background but keep but remain active
            SetWindowPos(Handle, new IntPtr(1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Extend aero glass style to whole form
            int[] margins = { 0, 0, Width, Height };
            DwmExtendFrameIntoClientArea(Handle, ref margins);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // do nothing here to stop window normal background painting
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Bounds = Screen.WorkingArea;

            base.OnPaint(e);
        }

        protected override void XnaUpdate(TimeSpan deltaTime)
        {
            base.XnaUpdate(deltaTime);

            //Delay hooking the mouse move handler to prevent laggy mouse movement whilst graphics device initialises
            if (deltaTime > TimeSpan.FromSeconds(0.25) && !_mouseHookEnabled) {
                _globalHook.MouseMoveExt += OnGlobalMouseMove;
                _mouseHookEnabled = true;
            }
        }

        #region evil windows interop
        // ReSharper disable InconsistentNaming
        private enum ExtendedWindowStyles
        {
            WS_EX_TOOLWINDOW = 0x00000080
        }

        private enum GetWindowLongFields
        {
            GWL_EXSTYLE = -20
        }
        // ReSharper restore InconsistentNaming

        //private const int WM_WINDOWPOSCHANGING = 0x0046;
        //private const int WM_NCHITTEST = 0x0084;

        //const UInt32 SWP_NOZORDER = 0x0004;
        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOMOVE = 0x0002;
        private const UInt32 SWP_NOACTIVATE = 0x0010;
        private const UInt32 SWP_NOCOPYBITS = 0x0100;

        //private static readonly IntPtr HTNOWHERE = new IntPtr(0);
        //private static readonly IntPtr HTTRANSPARENT = new IntPtr(-1);

        //protected override void WndProc(ref Message m)
        //{
        //    switch (m.Msg)
        //    {
        //        case WM_NCHITTEST:
        //            //http://stackoverflow.com/questions/7913325/win-api-in-c-get-hi-and-low-word-from-intptr
        //            var xy = m.LParam;
        //            int x = unchecked((short)(long)xy);
        //            int y = unchecked((short)((long)xy >> 16));

        //        //    if (( /*m.LParam.ToInt32() >> 16 and m.LParam.ToInt32() & 0xffff fit in your transparen region*/)
        //        //    &&
        //        //    m.Result.ToInt32() == 1)
        //        //{
        //        //    m.Result = new IntPtr(2); // HTCAPTION
        //        //}

        //            m.Result = HTTRANSPARENT;
        //            break;
        //        default:
        //            base.WndProc(ref m);
        //            break;
        //    }
        //}

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("dwmapi.dll")]
        private static extern void DwmExtendFrameIntoClientArea(IntPtr hWnd, ref int[] pMargins);
        #endregion
    }
}
