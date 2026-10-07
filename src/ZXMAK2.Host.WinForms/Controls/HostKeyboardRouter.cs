using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ZXMAK2.Host.Entities.Tools;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Tools;
using GuestKey = ZXMAK2.Host.Entities.Key;

namespace ZXMAK2.Host.WinForms.Controls
{
    // One ownership policy for WinForms messages and both physical readers.
    // A UI-owned key stays blocked until physical release, not until menu close.
    public sealed class HostKeyboardRouter : IKeyboardState
    {
        private readonly object _sync = new object();
        private readonly Dictionary<Keys, Action> _shortcuts = new Dictionary<Keys, Action>();
        private readonly KeyboardStateMapper<Keys> _mapper = new KeyboardStateMapper<Keys>();
        private readonly bool[] _raw = new bool[Enum.GetValues(typeof(GuestKey)).Length];
        private readonly bool[] _blocked = new bool[Enum.GetValues(typeof(GuestKey)).Length];
        private readonly bool[] _guest = new bool[Enum.GetValues(typeof(GuestKey)).Length];
        private bool _uiOwnsKeyboard;

        public HostKeyboardRouter()
        {
            _mapper.LoadMapFromString(Properties.Resources.Keyboard_WinForms);
        }

        public bool this[GuestKey key]
        {
            get { lock (_sync) return _guest[(int)key]; }
        }

        public void RegisterShortcut(Keys keys, Action execute)
        {
            lock (_sync) _shortcuts[keys] = execute;
        }

        private Keys NormalizeShortcut(Keys keys, bool rightAlt)
        {
            // Preserve the existing AltGr convention without stealing real Ctrl+Alt.
            if (rightAlt && (keys & (Keys.Alt | Keys.Control)) == (Keys.Alt | Keys.Control) &&
                _shortcuts.ContainsKey(keys & ~Keys.Control))
                keys &= ~Keys.Control;
            return keys;
        }

        public bool ExecuteShortcut(Keys keys, bool rightAlt)
        {
            Action execute;
            lock (_sync)
            {
                keys = NormalizeShortcut(keys, rightAlt);
                if (!_shortcuts.TryGetValue(keys, out execute)) return false;
                BlockVirtualKey(keys & Keys.KeyCode);
                BlockModifiers(keys);
            }
            execute(); // May open a modal window/reset the VM. Never hold the lock here.
            return true;
        }

        public void SetUiOwnership(bool ownsKeyboard)
        {
            lock (_sync)
            {
                _uiOwnsKeyboard = ownsKeyboard;
                if (!ownsKeyboard) return;
                for (var i = 0; i < _guest.Length; i++)
                {
                    _blocked[i] |= _raw[i];
                    _guest[i] = false;
                }
            }
        }

        // Called before WinForms dispatch, so Escape cannot leak when it closes a menu.
        public void ObserveKeyDown(Keys keys, bool rightAlt)
        {
            lock (_sync)
            {
                var shortcut = NormalizeShortcut(keys, rightAlt);
                if (!_uiOwnsKeyboard && !_shortcuts.ContainsKey(shortcut)) return;
                BlockVirtualKey(keys & Keys.KeyCode);
                BlockModifiers(keys);
            }
        }

        private void BlockVirtualKey(Keys key)
        {
            foreach (var guestKey in _mapper.Keys)
                if (_mapper[guestKey] == key)
                {
                    _blocked[(int)guestKey] = true;
                    _guest[(int)guestKey] = false;
                }
        }

        private void BlockModifiers(Keys keys)
        {
            if ((keys & Keys.Control) != 0) BlockPair(GuestKey.LeftControl, GuestKey.RightControl);
            if ((keys & Keys.Alt) != 0) BlockPair(GuestKey.LeftAlt, GuestKey.RightAlt);
            if ((keys & Keys.Shift) != 0) BlockPair(GuestKey.LeftShift, GuestKey.RightShift);
        }

        private void BlockPair(GuestKey left, GuestKey right)
        {
            _blocked[(int)left] = _blocked[(int)right] = true;
            _guest[(int)left] = _guest[(int)right] = false;
        }

        public void Scan(bool[] physical)
        {
            lock (_sync)
            {
                Array.Copy(physical, _raw, _raw.Length);
                var modifiers = Keys.None;
                if (physical[(int)GuestKey.LeftControl] || physical[(int)GuestKey.RightControl]) modifiers |= Keys.Control;
                if (physical[(int)GuestKey.LeftAlt] || physical[(int)GuestKey.RightAlt]) modifiers |= Keys.Alt;
                if (physical[(int)GuestKey.LeftShift] || physical[(int)GuestKey.RightShift]) modifiers |= Keys.Shift;
                var rightAlt = physical[(int)GuestKey.RightAlt];
                foreach (var key in _mapper.Keys)
                    if (physical[(int)key] && _shortcuts.ContainsKey(NormalizeShortcut(_mapper[key] | modifiers, rightAlt)))
                    {
                        _blocked[(int)key] = true;
                        BlockModifiers(modifiers);
                    }
                for (var i = 0; i < _guest.Length; i++)
                {
                    if (!physical[i]) _blocked[i] = false;
                    else if (_uiOwnsKeyboard) _blocked[i] = true;
                    _guest[i] = physical[i] && !_blocked[i];
                }
            }
        }

        public void ClearState()
        {
            lock (_sync)
            {
                // Do not clear ownership/latches on a lost acquisition or reset:
                // a still-held menu/shortcut key must wait for a real release.
                Array.Clear(_raw, 0, _raw.Length);
                Array.Clear(_guest, 0, _guest.Length);
            }
        }
    }

    internal sealed class HostKeyboardMessageFilter : IMessageFilter, IDisposable
    {
        private readonly Form _form;
        private readonly HostKeyboardRouter _router;
        private readonly HashSet<ToolStripDropDownItem> _menus = new HashSet<ToolStripDropDownItem>();
        private readonly HashSet<ToolStripDropDownItem> _openMenus = new HashSet<ToolStripDropDownItem>();
        private readonly HashSet<ToolStrip> _strips = new HashSet<ToolStrip>();
        private readonly HashSet<ToolStrip> _focusedStrips = new HashSet<ToolStrip>();
        private bool _disposed;

        public HostKeyboardMessageFilter(Form form, HostKeyboardRouter router)
        {
            _form = form;
            _router = router;
            form.Deactivate += OnDeactivate;
            Application.AddMessageFilter(this);
        }

        public void TrackStrip(ToolStrip strip)
        {
            if (!_strips.Add(strip)) return;
            strip.Enter += OnStripEntered;
            strip.Leave += OnStripLeft;
            var menu = strip as MenuStrip;
            if (menu != null)
            {
                menu.MenuActivate += OnStripEntered;
                menu.MenuDeactivate += OnStripLeft;
            }
        }

        private void OnStripEntered(object sender, EventArgs args)
        {
            _focusedStrips.Add((ToolStrip)sender);
            UpdateOwnership();
        }

        private void OnStripLeft(object sender, EventArgs args)
        {
            _focusedStrips.Remove((ToolStrip)sender);
            UpdateOwnership();
        }

        private void UpdateOwnership()
        {
            _router.SetUiOwnership(_openMenus.Count != 0 || _focusedStrips.Count != 0);
        }

        public void TrackMenu(ToolStripDropDownItem item)
        {
            if (!_menus.Add(item)) return;
            item.DropDownOpened += OnOpened;
            item.DropDownClosed += OnClosed;
            item.Disposed += OnClosed;
        }

        private void OnOpened(object sender, EventArgs args)
        {
            var item = (ToolStripDropDownItem)sender;
            _openMenus.Add(item);
            foreach (ToolStripItem child in item.DropDownItems)
            {
                var submenu = child as ToolStripDropDownItem;
                if (submenu != null) TrackMenu(submenu);
            }
            UpdateOwnership();
        }

        private void OnClosed(object sender, EventArgs args)
        {
            _openMenus.Remove((ToolStripDropDownItem)sender);
            if (((ToolStripDropDownItem)sender).IsDisposed)
                _menus.Remove((ToolStripDropDownItem)sender);
            UpdateOwnership();
        }

        private void OnDeactivate(object sender, EventArgs args)
        {
            _openMenus.Clear();
            _focusedStrips.Clear();
            UpdateOwnership();
            _router.ClearState();
        }

        public bool PreFilterMessage(ref Message message)
        {
            var keyDown = message.Msg == 0x100 || message.Msg == 0x104; // WM_(SYS)KEYDOWN
            var keyUp = message.Msg == 0x101 || message.Msg == 0x105; // WM_(SYS)KEYUP
            if (!keyDown && !keyUp) return false;
            if (Form.ActiveForm != _form && _openMenus.Count == 0) return false;
            // Windows uses F10 to select/toggle the menu, including on key-up.
            // Suppress only its UI messages; physical polling still sends F10
            // to the guest. Do not register it as a guest-blocking shortcut.
            if ((Keys)message.WParam.ToInt32() == Keys.F10) return true;
            if (!keyDown) return false;
            var control = Control.FromHandle(message.HWnd);
            var toolbar = control as ToolStrip;
            // MenuActivate also covers Alt selection without an open dropdown.
            _router.SetUiOwnership(_openMenus.Count != 0 || _focusedStrips.Count != 0 ||
                (toolbar != null && toolbar.ContainsFocus));
            _router.ObserveKeyDown((Keys)message.WParam.ToInt32() | Control.ModifierKeys,
                (NativeMethods.GetKeyState(0xA5) & 0x8000) != 0);
            return false; // WinForms still navigates/closes its menus normally.
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Application.RemoveMessageFilter(this);
            _form.Deactivate -= OnDeactivate;
            foreach (var strip in _strips)
            {
                strip.Enter -= OnStripEntered;
                strip.Leave -= OnStripLeft;
                var menu = strip as MenuStrip;
                if (menu != null)
                {
                    menu.MenuActivate -= OnStripEntered;
                    menu.MenuDeactivate -= OnStripLeft;
                }
            }
            _strips.Clear();
            _focusedStrips.Clear();
            foreach (var item in _menus)
            {
                item.DropDownOpened -= OnOpened;
                item.DropDownClosed -= OnClosed;
                item.Disposed -= OnClosed;
            }
            _menus.Clear();
            _openMenus.Clear();
            _router.SetUiOwnership(false);
            _router.ClearState();
        }
    }
}
