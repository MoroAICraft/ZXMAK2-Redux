/* Copyright 2008-2018 Alex Makeev. GPL-3.0-or-later, part of ZXMAK2. */
using System;
using System.Windows.Forms;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.Entities.Tools;
using ZXMAK2.Host.WinForms.Tools;
using ZXMAK2.Host.WinForms.Views;
using ZXMAK2.Host.WinForms.Controls;
using ZXMAK2.DirectX;
using ZXMAK2.DirectX.DirectInput;
using GuestKey = ZXMAK2.Host.Entities.Key;
using MdxKey = ZXMAK2.DirectX.DirectInput.Key;

namespace ZXMAK2.Host.WinForms.Mdx
{
    public sealed class DirectKeyboard : IHostKeyboard, IResettableHostKeyboard
    {
        private readonly object _sync = new object();
        private readonly Form _form;
        private IntPtr _hWnd;
        private DirectInputDevice8W _device;
        private readonly KeyboardStateMapper<MdxKey> _mapper = new KeyboardStateMapper<MdxKey>();
        private readonly HostKeyboardRouter _router;
        private readonly byte[] _diState = new byte[256];
        private readonly bool[] _physical = new bool[Enum.GetValues(typeof(GuestKey)).Length];
        private bool _isAcquired;
        private Func<HRESULT> _acquire;
        private Func<byte[], HRESULT> _read;
        private Action _unacquire;
        private Action<IntPtr> _setWindow;
        private Action _disposeDevice;
        private Func<IntPtr> _foreground;
        private bool _disposed;

        public unsafe DirectKeyboard(Form form) : this(form, null, null, null, null, null, null, null)
        {
            using (var dinput = new DirectInput8W())
                _device = dinput.CreateDevice(SysGuid.GUID_SysKeyboard, null);
            _device.SetDataFormat(DIDATAFORMAT.c_dfDIKeyboard).CheckError();
            _device.SetCooperativeLevel(_hWnd, DISCL.NONEXCLUSIVE | DISCL.FOREGROUND).CheckError();
            _acquire = () => _device.Acquire();
            _read = state => _device.GetDeviceState(state);
            _unacquire = () => _device.Unacquire();
            _setWindow = handle => _device.SetCooperativeLevel(handle, DISCL.NONEXCLUSIVE | DISCL.FOREGROUND).CheckError();
            _disposeDevice = () => _device.Dispose();
        }

        // Injectable physical source for repeatable acquisition/lifecycle regression tests.
        internal DirectKeyboard(Form form, HostKeyboardRouter router, Func<HRESULT> acquire,
            Func<byte[], HRESULT> read, Action unacquire, Action<IntPtr> setWindow,
            Action disposeDevice, Func<IntPtr> foreground)
        {
            if (form == null) throw new ArgumentNullException("form");
            _form = form;
            _hWnd = form.Handle;
            var main = form as MainView;
            _router = router ?? (main == null ? new HostKeyboardRouter() : main.KeyboardRouter);
            _mapper.LoadMapFromString(Properties.Resources.Keyboard_Mdx);
            _acquire = acquire;
            _read = read;
            _unacquire = unacquire;
            _setWindow = setWindow;
            _disposeDevice = disposeDevice;
            _foreground = foreground ?? NativeMethods.GetForegroundWindow;
            form.Deactivate += WndDeactivate;
            form.HandleDestroyed += WndHandleDestroyed;
            form.HandleCreated += WndHandleCreated;
        }

        public IKeyboardState State { get { return _router; } }

        public void Scan()
        {
            lock (_sync)
            {
                if (_disposed || _hWnd == IntPtr.Zero ||
                    _hWnd != _foreground())
                {
                    ReleaseDevice();
                    return;
                }
                if (!_isAcquired)
                {
                    var acquire = _acquire();
                    _isAcquired = acquire.IsSuccess; // Failed Acquire must remain retryable.
                    if (!_isAcquired)
                    {
                        _router.ClearState();
                        return;
                    }
                }
                var result = _read(_diState);
                if (result.IsFailure)
                {
                    // NOTACQUIRED and INPUTLOST are transient. Retry next scan.
                    if (result != ErrorCode.DIERR_NOTACQUIRED && result != ErrorCode.DIERR_INPUTLOST)
                        Logger.Error("DirectKeyboard.Scan: {0}", result);
                    ReleaseDevice();
                    return;
                }
                Array.Clear(_physical, 0, _physical.Length);
                foreach (var key in _mapper.Keys)
                    _physical[(int)key] = (_diState[(int)_mapper[key]] & 0x80) != 0;
                _router.Scan(_physical);
            }
        }

        public void ResetState()
        {
            lock (_sync) ReleaseDevice();
        }

        private void ReleaseDevice()
        {
            if (!_disposed && _isAcquired) _unacquire();
            _isAcquired = false;
            Array.Clear(_diState, 0, _diState.Length);
            Array.Clear(_physical, 0, _physical.Length);
            _router.ClearState();
        }

        private void WndDeactivate(object sender, EventArgs args) { ResetState(); }

        private void WndHandleDestroyed(object sender, EventArgs args)
        {
            lock (_sync)
            {
                ReleaseDevice();
                _hWnd = IntPtr.Zero;
            }
        }

        private void WndHandleCreated(object sender, EventArgs args)
        {
            lock (_sync)
            {
                _hWnd = _form.Handle; // UI thread only; fullscreen can recreate it.
                if (!_disposed) _setWindow(_hWnd);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                ReleaseDevice();
                if (_disposed) return;
                _disposed = true;
                _form.Deactivate -= WndDeactivate;
                _form.HandleDestroyed -= WndHandleDestroyed;
                _form.HandleCreated -= WndHandleCreated;
                _disposeDevice();
                _device = null;
            }
        }
    }
}
