using System;
using System.Reflection;
using System.Windows.Forms;
using ZXMAK2.Host.WinForms.Mdx;
using ZXMAK2.Host.WinForms.Controls;
using GuestKey = ZXMAK2.Host.Entities.Key;

// Diagnostic fixture only; no input injection, no interaction with other apps.
internal static class KeyboardPhysicalMonitor
{
    [STAThread]
    private static void Main()
    {
        using (var form = new Form())
        {
            form.Text = "v76 physical keyboard monitor";
            form.Width = 650;
            var label = new Label { Dock = DockStyle.Fill, AutoSize = false };
            form.Controls.Add(label);
            using (var keyboard = new DirectKeyboard(form))
            using (var timer = new Timer { Interval = 1 })
            {
                var pressed = new bool[Enum.GetValues(typeof(GuestKey)).Length];
                var counts = new int[pressed.Length];
                var physical = typeof(DirectKeyboard).GetField("_physical", BindingFlags.Instance | BindingFlags.NonPublic);
                timer.Tick += delegate {
                    keyboard.Scan();
                    var state = (bool[])physical.GetValue(keyboard);
                    var lines = "Physical / routed keyboard observations:\n";
                    foreach (GuestKey key in Enum.GetValues(typeof(GuestKey)))
                    {
                        if (state[(int)key] && !pressed[(int)key]) counts[(int)key]++;
                        pressed[(int)key] = state[(int)key];
                        if (counts[(int)key] != 0)
                            lines += key + ": " + counts[(int)key] + " routed=" + keyboard.State[key] + "\n";
                    }
                    label.Text = lines;
                };
                timer.Start();
                Application.Run(form);
            }
        }
    }
}
