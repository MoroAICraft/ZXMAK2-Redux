using ZXMAK2.Host.Entities;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Tools;


namespace ZXMAK2.Host.WinForms.Services
{
    public sealed class FolderBrowserDialog : IFolderBrowserDialog
    {
        private readonly System.Windows.Forms.FolderBrowserDialog m_dialog =
            new System.Windows.Forms.FolderBrowserDialog();

        public string Description
        {
            get { return m_dialog.Description; }
            set { m_dialog.Description = value; }
        }

        public string SelectedPath
        {
            get { return m_dialog.SelectedPath; }
            set { m_dialog.SelectedPath = value; }
        }

        public DlgResult ShowDialog(object owner)
        {
            var window = owner as System.Windows.Forms.IWin32Window;
            return EnumMapper.GetDlgResult(
                window == null ? m_dialog.ShowDialog() : m_dialog.ShowDialog(window));
        }

        public void Dispose()
        {
            m_dialog.Dispose();
        }
    }
}
