using System;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Tools;


namespace ZXMAK2.Host.WinForms.Services
{
    public class UserQuery : IUserQuery, IActionUserQuery, IUserOperation
    {
        public void Run(string caption, Action<Action<string>> operation)
        {
            // Headless hosts keep the synchronous contract. GUI save runs
            // off the UI thread; no DoEvents reentrancy into machine commands.
            if (!Application.MessageLoop || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                operation(message => { });
                return;
            }
            using (var form = new OperationDialog(caption, operation))
            {
                var owner = Form.ActiveForm;
                if (owner != null) form.ShowDialog(owner); else form.ShowDialog();
                if (form.Failure != null)
                    throw new System.IO.IOException(form.Failure.Message, form.Failure);
            }
        }

        private sealed class OperationDialog : Form
        {
            private readonly Label status;
            private readonly System.Windows.Forms.Timer timer;
            private readonly ManualResetEvent completed = new ManualResetEvent(false);
            private readonly object sync = new object();
            private string message = "Preparing save...";
            private Thread worker;
            internal Exception Failure;
            public OperationDialog(string caption, Action<Action<string>> operation)
            {
                Text = caption; FormBorderStyle = FormBorderStyle.FixedDialog;
                ControlBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(520, 145); AutoScaleMode = AutoScaleMode.Dpi;
                status = new Label { Left = 16, Top = 16, Width = 488, Height = 50, Text = message };
                var bar = new ProgressBar { Left = 16, Top = 72, Width = 488, Height = 20,
                    Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
                var note = new Label { Left = 16, Top = 104, Width = 488, Height = 34,
                    Text = "Please wait. Large external folders are copied inside the emulator.\nThe emulated machine remains paused until the operation finishes." };
                Controls.Add(status); Controls.Add(bar); Controls.Add(note);
                timer = new System.Windows.Forms.Timer { Interval = 100 };
                timer.Tick += delegate {
                    lock (sync) status.Text = message;
                    if (completed.WaitOne(0)) { timer.Stop(); DialogResult = DialogResult.OK; Close(); }
                };
                Shown += delegate {
                    worker = new Thread(() => {
                        try { operation(text => { lock(sync) message = text; }); }
                        catch (Exception ex) { Failure = ex; }
                        finally { completed.Set(); }
                    });
                    worker.IsBackground = true; worker.Name = "ZXMAK2 media save";
                    worker.Start(); timer.Start();
                };
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) {
                    timer.Dispose();
                    if (worker != null) worker.Join();
                    completed.Dispose();
                }
                base.Dispose(disposing);
            }
            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                // Alt+F4 must not abandon a transaction or hide its progress.
                if (!completed.WaitOne(0)) e.Cancel = true;
                base.OnFormClosing(e);
            }
        }

        public DlgResult Show(
            String message,
            String caption,
            DlgButtonSet buttonSet,
            DlgIcon icon)
        {
            return EnumMapper.GetDlgResult(MessageBox.Show(
                message, caption,
                EnumMapper.GetMessageBoxButtons(buttonSet),
                EnumMapper.GetMessageBoxIcons(icon)));
        }

        public DlgResult ShowActions(string message, string caption,
            string acceptText, string discardText, string cancelText)
        {
            using (var form = new ActionChoiceDialog(message, caption, acceptText, discardText, cancelText))
            {
                var owner = Form.ActiveForm;
                return EnumMapper.GetDlgResult(owner != null ? form.ShowDialog(owner) : form.ShowDialog());
            }
        }

        private sealed class ActionChoiceDialog : Form
        {
            public ActionChoiceDialog(string message, string caption,
                string acceptText, string discardText, string cancelText)
            {
                Text = caption;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = MinimizeBox = ShowInTaskbar = ShowIcon = false;
                StartPosition = FormStartPosition.CenterParent;
                AutoScaleMode = AutoScaleMode.Font;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                var layout = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill,
                    Padding = new Padding(16), ColumnCount = 1, RowCount = 2 };
                layout.Controls.Add(new Label { AutoSize = true, Text = message,
                    MaximumSize = new System.Drawing.Size(600, 0), Margin = new Padding(0, 0, 0, 16) });
                var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false,
                    Dock = DockStyle.Fill, Margin = new Padding(0) };
                var save = new Button { Text = acceptText, AutoSize = true, DialogResult = DialogResult.Yes,
                    Padding = new Padding(8, 4, 8, 4) };
                var discard = new Button { Text = discardText, AutoSize = true, DialogResult = DialogResult.No,
                    Padding = new Padding(8, 4, 8, 4) };
                var cancel = new Button { Text = cancelText, AutoSize = true, DialogResult = DialogResult.Cancel,
                    Padding = new Padding(8, 4, 8, 4) };
                buttons.Controls.AddRange(new Control[] { save, discard, cancel });
                layout.Controls.Add(buttons);
                Controls.Add(layout);
                AcceptButton = save;
                CancelButton = cancel;
            }
        }

        public object ObjectSelector(object[] objArray, string caption)
        {
            if (objArray.Length < 1)
            {
                return null;
            }
            if (objArray.Length == 1)
            {
                return objArray[0];
            }
            using (var form = new ObjectSelectorDialog())
            {
                form.ItemArray = objArray;
                form.ItemSelected = objArray[0];
                form.Text = caption;
                form.ShowDialog();
                if (form.DialogResult == DialogResult.OK)
                {
                    return form.ItemSelected;
                }
                return null;
            }
        }

        public bool QueryText(string caption, string text, ref string value)
        {
            return InputBox.Query(caption, text, ref value);
        }

        public bool QueryValue(string caption, string text, string format, ref int value, int min, int max)
        {
            return InputBox.InputValue(caption, text, format, ref value, min, max);
        }


        #region InputBox

        private class InputBox : Form
        {
            private InputBox(string Caption, string Text)
            {
                this.label = new System.Windows.Forms.Label();
                this.textValue = new System.Windows.Forms.TextBox();
                this.buttonOK = new System.Windows.Forms.Button();
                this.buttonCancel = new System.Windows.Forms.Button();
                this.SuspendLayout();

                // 
                // label
                // 
                this.label.AutoSize = true;
                this.label.Location = new System.Drawing.Point(9, 13);
                this.label.Name = "label";
                this.label.Size = new System.Drawing.Size(31, 13);
                this.label.TabIndex = 1;
                this.label.Text = Text;
                // 
                // textValue
                // 
                this.textValue.Location = new System.Drawing.Point(12, 31);
                this.textValue.Name = "textValue";
                this.textValue.Size = new System.Drawing.Size(245, 20);
                this.textValue.TabIndex = 2;
                this.textValue.WordWrap = false;
                // 
                // buttonOK
                // 
                this.buttonOK.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.buttonOK.Location = new System.Drawing.Point(57, 67);
                this.buttonOK.Name = "buttonOK";
                this.buttonOK.Size = new System.Drawing.Size(75, 23);
                this.buttonOK.TabIndex = 3;
                this.buttonOK.Text = "OK";
                this.buttonOK.UseVisualStyleBackColor = true;
                // 
                // buttonCancel
                // 
                this.buttonCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
                this.buttonCancel.Location = new System.Drawing.Point(138, 67);
                this.buttonCancel.Name = "buttonCancel";
                this.buttonCancel.Size = new System.Drawing.Size(75, 23);
                this.buttonCancel.TabIndex = 4;
                this.buttonCancel.Text = "Cancel";
                this.buttonCancel.UseVisualStyleBackColor = true;
                // 
                // Form
                // 
                this.AcceptButton = this.buttonOK;
                this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                this.CancelButton = this.buttonCancel;
                this.ClientSize = new System.Drawing.Size(270, 103);
                this.Controls.Add(this.buttonCancel);
                this.Controls.Add(this.buttonOK);
                this.Controls.Add(this.textValue);
                this.Controls.Add(this.label);
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.Name = "InputBox";
                this.ShowIcon = false;
                this.ShowInTaskbar = false;
                this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                this.Text = Caption;
                this.ResumeLayout(false);
                this.PerformLayout();
            }

            public static bool Query(
                //IWin32Window owner,
                string Caption,
                string Text,
                ref string s_val)
            {
                using (var ib = new InputBox(Caption, Text))
                {
                    ib.textValue.Text = s_val;
                    if (ib.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    {
                        return false;
                    }
                    s_val = ib.textValue.Text;
                }
                return true;
            }

            public static bool InputValue(
                //IWin32Window owner,
                string Caption,
                string Text,
                string format,
                ref int value,
                int min,
                int max)
            {
                int val = value;
                string s_val = string.Format(format, value);
                bool OKVal;
                do
                {
                    OKVal = true;
                    if (!Query(Caption, Text, ref s_val))
                    {
                        return false;
                    }
                    try
                    {
                        var sTr = s_val.Trim();
                        if ((sTr.Length > 0) && (sTr[0] == '#'))
                        {
                            sTr = sTr.Remove(0, 1);
                            val = Convert.ToInt32(sTr, 16);
                            //                  s_val = "0x" + s_val;
                        }
                        else if ((sTr.Length > 1) && ((sTr[1] == 'x') && (sTr[0] == '0')))
                        {
                            sTr = sTr.Remove(0, 2);
                            val = Convert.ToInt32(sTr, 16);
                        }
                        else
                        {
                            val = Convert.ToInt32(sTr, 10);
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Numeric value required!");
                        OKVal = false;
                    }
                    if ((val < min) || (val > max))
                    {
                        MessageBox.Show("Numeric value should be int the following range: " + min.ToString() + "..." + max.ToString() + " !");
                        OKVal = false;
                    }
                } while (!OKVal);
                value = val;
                return true;
            }

            private System.Windows.Forms.Label label;
            private System.Windows.Forms.TextBox textValue;
            private System.Windows.Forms.Button buttonOK;
            private System.Windows.Forms.Button buttonCancel;
        }

        #endregion InputBox
    }
}
