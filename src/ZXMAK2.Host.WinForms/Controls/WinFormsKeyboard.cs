using System;
using System.Windows.Forms;
using ZXMAK2.Host.Entities.Tools;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Tools;
using ZXMAK2.Host.WinForms.Views;
using GuestKey = ZXMAK2.Host.Entities.Key;

namespace ZXMAK2.Host.WinForms.Controls
{
    // Physical polling sees ProcessCmdKey-owned keys and releases outside the window.
    public sealed class WinFormsKeyboard : IHostKeyboard, IResettableHostKeyboard
    {
        private readonly object _sync = new object();
        private readonly Form _form;
        private readonly HostKeyboardRouter _router;
        private readonly KeyboardStateMapper<Keys> _mapper = new KeyboardStateMapper<Keys>();
        private readonly bool[] _physical = new bool[Enum.GetValues(typeof(GuestKey)).Length];
        private IntPtr _handle;
        private bool _disposed;

        public WinFormsKeyboard(Form form)
        {
            if (form == null) throw new ArgumentNullException("form");
            _form = form;
            _handle = form.Handle;
            var main = form as MainView;
            _router = main == null ? new HostKeyboardRouter() : main.KeyboardRouter;
            _mapper.LoadMapFromString(Properties.Resources.Keyboard_WinForms);
            form.Deactivate += OnDeactivate;
            form.HandleCreated += OnHandleCreated;
            form.HandleDestroyed += OnHandleDestroyed;
        }

        public IKeyboardState State { get { return _router; } }

        public void Scan()
        {
            lock (_sync)
            {
                if (_disposed || _handle == IntPtr.Zero || _handle != NativeMethods.GetForegroundWindow())
                {
                    _router.ClearState();
                    return;
                }
                Array.Clear(_physical, 0, _physical.Length);
                foreach (var key in _mapper.Keys)
                    _physical[(int)key] = (NativeMethods.GetAsyncKeyState(VirtualKey(key)) & 0x8000) != 0;
                _router.Scan(_physical);
            }
        }

        private int VirtualKey(GuestKey key)
        {
            switch (key)
            {
                case GuestKey.LeftShift: return 0xA0;
                case GuestKey.RightShift: return 0xA1;
                case GuestKey.LeftControl: return 0xA2;
                case GuestKey.RightControl: return 0xA3;
                case GuestKey.LeftAlt: return 0xA4;
                case GuestKey.RightAlt: return 0xA5;
                default: return (int)_mapper[key];
            }
        }

        public void ResetState() { lock (_sync) _router.ClearState(); }
        private void OnDeactivate(object sender, EventArgs args) { ResetState(); }
        private void OnHandleCreated(object sender, EventArgs args) { lock (_sync) _handle = _form.Handle; }
        private void OnHandleDestroyed(object sender, EventArgs args)
        {
            lock (_sync) { _handle = IntPtr.Zero; _router.ClearState(); }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _router.ClearState();
                _form.Deactivate -= OnDeactivate;
                _form.HandleCreated -= OnHandleCreated;
                _form.HandleDestroyed -= OnHandleDestroyed;
            }
        }
    }
}
