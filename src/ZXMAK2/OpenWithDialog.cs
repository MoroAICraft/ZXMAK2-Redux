using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ZXMAK2
{
    // Lightweight dispatch UI: no Locator, VM, configuration or media initialization.
    internal sealed class OpenWithDialog : Form
    {
        private readonly ListView list = new ListView();
        private readonly Button open = new Button();
        private readonly TextBox path = new TextBox();
        internal PortableLaunch.Target Selected { get; private set; }

        internal OpenWithDialog(IList<PortableLaunch.Target> targets)
        {
            Text = "Open with ZXMAK2";
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            ClientSize = new Size(740, 300);
            MinimumSize = new Size(540, 300);
            MaximizeBox = false; MinimizeBox = false;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { AutoSize = true, Text = "Choose the running ZXMAK2 window:", Margin = new Padding(0, 0, 0, 8) });
            list.Dock = DockStyle.Fill; list.View = View.Details;
            list.FullRowSelect = true; list.HideSelection = false; list.MultiSelect = false;
            list.Columns.Add("Folder", 180); list.Columns.Add("Window", 190); list.Columns.Add("Full path", 520);
            foreach (var target in targets)
            {
                var item = new ListViewItem(string.IsNullOrEmpty(target.Folder) ? "Process " + target.ProcessId : new DirectoryInfo(target.Folder).Name) { Tag = target };
                item.SubItems.Add(target.Unavailable ? "Not responding" : target.Closing ? "Closing..." : string.IsNullOrEmpty(target.Title) ? "ZXMAK2" : target.Title);
                item.SubItems.Add(target.Folder);
                list.Items.Add(item);
            }
            path.Dock = DockStyle.Fill; path.ReadOnly = true; path.Multiline = true;
            path.BorderStyle = BorderStyle.None; path.BackColor = SystemColors.Control; path.TabStop = false;
            list.SelectedIndexChanged += delegate
            {
                var target = list.SelectedItems.Count == 1 ? (PortableLaunch.Target)list.SelectedItems[0].Tag : null;
                path.Text = target == null ? "" : target.Folder;
                open.Enabled = target != null && !target.Closing && !target.Unavailable;
            };
            open.Text = "Open here"; open.AutoSize = true; open.Enabled = false;
            open.Click += delegate { SelectTarget(); };
            list.DoubleClick += delegate { if (open.Enabled) SelectTarget(); };
            var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(cancel); buttons.Controls.Add(open);
            layout.Controls.Add(list); layout.Controls.Add(path); layout.Controls.Add(buttons);
            Controls.Add(layout); AcceptButton = open; CancelButton = cancel;
        }

        private void SelectTarget()
        {
            if (list.SelectedItems.Count != 1) return;
            Selected = (PortableLaunch.Target)list.SelectedItems[0].Tag;
            if (Selected.Closing || Selected.Unavailable) return;
            DialogResult = DialogResult.OK;
        }

        internal static PortableLaunch.Target Choose(IList<PortableLaunch.Target> targets)
        {
            Application.EnableVisualStyles();
            using (var dialog = new OpenWithDialog(targets))
                return dialog.ShowDialog() == DialogResult.OK ? dialog.Selected : null;
        }

        internal static void Error(Exception ex)
        {
            MessageBox.Show("The file could not be handed to ZXMAK2.\n\n" + ex.Message +
                "\n\nNo other emulator window was selected automatically.", "Open with ZXMAK2",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
