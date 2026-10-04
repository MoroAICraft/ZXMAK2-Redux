using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ZXMAK2.DirectX;
using ZXMAK2.Host.WinForms.Controls;
using ZXMAK2.Host.WinForms.Mdx;
using ZXMAK2.Host.WinForms.Views;
using ZXMAK2.Engine;
using ZXMAK2.Hardware.Evo;
using ZXMAK2.Host.WinForms.Views.Configuration.Devices;
using GuestKey = ZXMAK2.Host.Entities.Key;
using DiKey = ZXMAK2.DirectX.DirectInput.Key;

internal static class KeyboardRoutingProbe
{
    private static int _checks;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static bool[] Physical(params GuestKey[] keys)
    {
        var state = new bool[Enum.GetValues(typeof(GuestKey)).Length];
        foreach (var key in keys) state[(int)key] = true;
        return state;
    }
    private static void Assert(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyOwnership();
            VerifyMenuLifecycle();
            VerifyPhysicalRecovery();
            VerifyMaxApply();
            Console.WriteLine("Keyboard/Max v76: PASS ({0} assertions)", _checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static HostKeyboardRouter Router(MainView view)
    {
        return (HostKeyboardRouter)typeof(MainView).GetProperty("KeyboardRouter", Private).GetValue(view, null);
    }
    private static void VerifyOwnership()
    {
        using (var view = new MainView(null))
        {
            var router = Router(view);
            foreach (GuestKey key in Enum.GetValues(typeof(GuestKey)))
            {
                router.Scan(Physical());
                router.Scan(Physical(key));
                Assert(router[key] == (key != GuestKey.F11 && key != GuestKey.F12),
                    "Plain guest key unexpectedly reserved: " + key);
            }
            var chords = new[] {
                Physical(GuestKey.LeftAlt, GuestKey.P),
                Physical(GuestKey.RightAlt, GuestKey.LeftControl, GuestKey.P),
                Physical(GuestKey.LeftAlt, GuestKey.Return),
                Physical(GuestKey.RightAlt, GuestKey.NumPadEnter),
                Physical(GuestKey.LeftControl, GuestKey.S),
                Physical(GuestKey.RightControl, GuestKey.S),
                Physical(GuestKey.LeftControl, GuestKey.F12),
                Physical(GuestKey.LeftControl, GuestKey.LeftAlt, GuestKey.F12),
                Physical(GuestKey.LeftControl, GuestKey.RightAlt, GuestKey.F12),
                Physical(GuestKey.LeftControl, GuestKey.LeftAlt, GuestKey.F1),
                Physical(GuestKey.LeftControl, GuestKey.RightAlt, GuestKey.F8)
            };
            foreach (var chord in chords)
            {
                router.Scan(Physical());
                router.Scan(chord);
                foreach (GuestKey key in Enum.GetValues(typeof(GuestKey)))
                    if (chord[(int)key]) Assert(!router[key], "Host chord leaked: " + key);
            }
            foreach (var modifiers in new[] { GuestKey.LeftControl, GuestKey.RightControl, GuestKey.LeftAlt })
                foreach (var key in new[] { GuestKey.D, GuestKey.P })
                {
                    if (modifiers == GuestKey.LeftAlt && key == GuestKey.P) continue;
                    router.Scan(Physical());
                    router.Scan(Physical(modifiers, key));
                    Assert(router[key], "Unassigned combination was stolen: " + modifiers + "+" + key);
                }
            router.Scan(Physical());
            router.Scan(Physical(GuestKey.LeftControl, GuestKey.LeftShift, GuestKey.S));
            Assert(router[GuestKey.S], "Ctrl+Shift+S was stolen");
            router.Scan(Physical());
            router.Scan(Physical(GuestKey.LeftControl, GuestKey.LeftAlt, GuestKey.P));
            Assert(router[GuestKey.P], "Real Ctrl+Alt+P was confused with AltGr");

            // Every menu key, not a separate Escape/arrows workaround.
            foreach (GuestKey key in Enum.GetValues(typeof(GuestKey)))
            {
                router.Scan(Physical());
                router.SetUiOwnership(true);
                router.Scan(Physical(key));
                Assert(!router[key], "Menu-owned key leaked: " + key);
                router.SetUiOwnership(false);
                router.Scan(Physical(key));
                Assert(!router[key], "Held key leaked on menu close: " + key);
                router.Scan(Physical());
                router.Scan(Physical(key));
                Assert(router[key] == (key != GuestKey.F11 && key != GuestKey.F12),
                    "Released key remained blocked: " + key);
            }
            router.Scan(Physical());
            router.SetUiOwnership(true);
            router.ObserveKeyDown(Keys.Escape, false);
            router.SetUiOwnership(false); // ToolStrip closes before next physical scan.
            router.Scan(Physical(GuestKey.Escape));
            Assert(!router[GuestKey.Escape], "Closing Escape leaked between WM_KEYDOWN and Scan");
            router.Scan(Physical(GuestKey.Escape, GuestKey.UpArrow));
            Assert(router[GuestKey.UpArrow], "One held key blocked an unrelated arrow");
            router.ClearState();
            Assert(!router[GuestKey.UpArrow], "Reset failed to clear guest state");
            router.Scan(Physical());
            router.Scan(Physical(GuestKey.UpArrow));
            Assert(router[GuestKey.UpArrow], "Arrow failed to return after reset/release");
            var calls = 0;
            router.RegisterShortcut(Keys.Control | Keys.D, () => calls++);
            router.Scan(Physical());
            router.Scan(Physical(GuestKey.RightControl, GuestKey.D));
            Assert(!router[GuestKey.D], "New registry entry was not applied to physical polling");
            Assert(router.ExecuteShortcut(Keys.Control | Keys.D, false) && calls == 1,
                "Registry dispatch failed");
        }
    }
    private sealed class TestForm : Form
    {
        public void LoseFocus() { OnDeactivate(EventArgs.Empty); }
        public void ReplaceHandle() { RecreateHandle(); }
    }
    private static void VerifyMenuLifecycle()
    {
        using (var view = new MainView(null))
        using (var menu = new ToolStripMenuItem("Test"))
        {
            var router = Router(view);
            var filter = typeof(MainView).GetField("_keyboardMessages", Private).GetValue(view);
            var filterType = filter.GetType();
            filterType.GetMethod("TrackMenu").Invoke(filter, new object[] { menu });
            var opened = filterType.GetMethod("OnOpened", Private);
            var closed = filterType.GetMethod("OnClosed", Private);
            var child = new ToolStripMenuItem("Nested");
            menu.DropDownItems.Add(child);
            opened.Invoke(filter, new object[] { menu, EventArgs.Empty });
            opened.Invoke(filter, new object[] { child, EventArgs.Empty });
            opened.Invoke(filter, new object[] { child, EventArgs.Empty }); // duplicate notification
            router.Scan(Physical(GuestKey.Escape, GuestKey.DownArrow));
            Assert(!router[GuestKey.Escape] && !router[GuestKey.DownArrow], "Nested menu leaked input");
            closed.Invoke(filter, new object[] { child, EventArgs.Empty });
            router.Scan(Physical());
            router.Scan(Physical(GuestKey.UpArrow));
            Assert(!router[GuestKey.UpArrow], "Closing child released parent menu ownership");
            closed.Invoke(filter, new object[] { menu, EventArgs.Empty });
            router.Scan(Physical());
            router.Scan(Physical(GuestKey.UpArrow));
            Assert(router[GuestKey.UpArrow], "Repeated menu event latched all arrows");
            opened.Invoke(filter, new object[] { menu, EventArgs.Empty });
            menu.Dispose();
            router.Scan(Physical());
            router.Scan(Physical(GuestKey.DownArrow));
            Assert(router[GuestKey.DownArrow], "Disposed toolbar menu latched ownership");
            var strip = typeof(MainView).GetField("mnuStrip", Private).GetValue(view);
            filterType.GetMethod("OnStripEntered", Private).Invoke(filter, new object[] { strip, EventArgs.Empty });
            router.Scan(Physical(GuestKey.Escape));
            Assert(!router[GuestKey.Escape], "Alt/F10 menu mode leaked Escape");
            filterType.GetMethod("OnDeactivate", Private).Invoke(filter, new object[] { view, EventArgs.Empty });
            router.Scan(Physical());
            router.Scan(Physical(GuestKey.LeftArrow));
            Assert(router[GuestKey.LeftArrow], "Focus loss kept stale toolbar ownership");
        }
    }
    private static void VerifyPhysicalRecovery()
    {
        using (var form = new TestForm())
        {
            var router = new HostKeyboardRouter();
            var acquireCount = 0;
            var failAcquires = 2;
            var result = new HRESULT(0);
            var foreground = form.Handle;
            var windowChanges = 0;
            var disposeCount = 0;
            var ctor = typeof(DirectKeyboard).GetConstructors(Private)[0];
            Func<HRESULT> acquire = () => {
                acquireCount++;
                return failAcquires-- > 0 ? (HRESULT)ErrorCode.DIERR_OTHERAPPHASPRIO : new HRESULT(0);
            };
            Func<byte[], HRESULT> read = state => {
                Array.Clear(state, 0, state.Length);
                state[(int)DiKey.UpArrow] = 0x80;
                return result;
            };
            using (var keyboard = (DirectKeyboard)ctor.Invoke(new object[] {
                form, router, acquire, read, (Action)(() => {}),
                (Action<IntPtr>)(handle => { foreground = handle; windowChanges++; }),
                (Action)(() => disposeCount++), (Func<IntPtr>)(() => foreground)
            }))
            {
                keyboard.Scan(); keyboard.Scan();
                Assert(acquireCount == 2 && !keyboard.State[GuestKey.UpArrow], "Acquire failure latched acquired");
                keyboard.Scan();
                Assert(acquireCount == 3 && keyboard.State[GuestKey.UpArrow], "Failed Acquire did not recover");
                foreach (var error in new[] { ErrorCode.DIERR_NOTACQUIRED, ErrorCode.DIERR_INPUTLOST })
                {
                    result = (HRESULT)error;
                    keyboard.Scan();
                    Assert(!keyboard.State[GuestKey.UpArrow], "Stale key after " + error);
                    result = new HRESULT(0);
                    var before = acquireCount;
                    keyboard.Scan();
                    Assert(acquireCount == before + 1 && keyboard.State[GuestKey.UpArrow], "No reacquire after " + error);
                }
                form.LoseFocus();
                Assert(!keyboard.State[GuestKey.UpArrow], "Deactivate kept a pressed arrow");
                foreground = IntPtr.Zero;
                keyboard.Scan();
                Assert(!keyboard.State[GuestKey.UpArrow], "Background window received input");
                foreground = form.Handle;
                keyboard.Scan();
                Assert(keyboard.State[GuestKey.UpArrow], "Focus return failed");
                keyboard.ResetState();
                Assert(!keyboard.State[GuestKey.UpArrow], "Reset kept stale state");
                keyboard.Scan();
                Assert(keyboard.State[GuestKey.UpArrow], "Reset prevented reacquisition");
                form.ReplaceHandle();
                keyboard.Scan();
                Assert(windowChanges > 0 && keyboard.State[GuestKey.UpArrow], "Recreated HWND lost keyboard");
                Exception workerError = null;
                var worker = new Thread(() => { try { for (int i = 0; i < 1000; i++) keyboard.Scan(); } catch (Exception ex) { workerError = ex; } });
                worker.Start();
                for (int i = 0; i < 100; i++) { router.SetUiOwnership(true); keyboard.ResetState(); router.SetUiOwnership(false); }
                worker.Join();
                Assert(workerError == null, "Concurrent reset/scan failed");
            }
            Assert(disposeCount == 1, "Physical device disposed incorrectly");
        }
    }
    private static T Field<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, Private).GetValue(target);
    }
    private static void VerifyMaxApply()
    {
        var bus = new BusManager();
        bus.Init(null, true);
        bus.Disconnect();
        bus.Clear();
        var ula = new UlaPentEvo();
        bus.Add(ula);
        bus.Add(new MemoryPentEvo());
        bus.Add(new AYCHRV());
        using (var settings = new CtlSettingsPentEvoZxBus())
        {
            settings.Initialize(bus, null, ula);
            Field<ComboBox>(settings, "m_slot1Device").SelectedIndex = 5;
            Field<CheckBox>(settings, "m_slot1Enabled").Checked = true;
            settings.Apply();
            Assert(bus.FindDevice<ZxMultiSoundMaxDevice>() != null, "Max not added by Apply");
            Assert(bus.FindDevice<ZxmMoonSoundDevice>() == null && bus.FindDevice<ZxOmniSoundDevice>() == null,
                "Max gained an unintended MoonSound/OmniSound block");
            Assert(bus.Connect(), "Max Apply + Connect failed");
            bus.Disconnect();
        }
    }
}
