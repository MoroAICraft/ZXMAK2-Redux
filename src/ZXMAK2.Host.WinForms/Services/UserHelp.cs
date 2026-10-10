using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Forms;
using ZXMAK2.Dependency;
using ZXMAK2.Host.Interfaces;


namespace ZXMAK2.Host.WinForms.Services
{
    public class UserHelp : IUserHelp
    {
        private static string s_url;

        public bool CanShow(object uiControl)
        {
            return uiControl is Control;
        }
        
        public void ShowHelp(object uiControl)
        {
            var control = uiControl as Control;
            if (control == null || !CheckUrl())
            {
                return;
            }
            Process.Start(new ProcessStartInfo(new Uri(s_url).AbsoluteUri) { UseShellExecute = true });
        }

        public void ShowHelp(object uiControl, HelpNavigator navigator)
        {
            var control = uiControl as Control;
            if (control == null || !CheckUrl())
            {
                return;
            }
            ShowHelp(control);
        }

        public void ShowHelp(object uiControl, string keyword)
        {
            var control = uiControl as Control;
            if (control == null || !CheckUrl())
            {
                return;
            }
            ShowHelp(control);
        }

        public void ShowHelp(Object uiControl, HelpNavigator command, object parameter)
        {
            var control = uiControl as Control;
            if (control == null || !CheckUrl())
            {
                return;
            }
            ShowHelp(control);
        }

        private static bool CheckUrl()
        {
            if (s_url != null)
            {
                return true;
            }
            var helpFile = Path.Combine(PortablePaths.Root, "Docs", "Help", "index.html");
            if (!File.Exists(helpFile))
            {
                Locator.Resolve<IUserMessage>()
                    .Error("Help file is missing");
                return false;
            }
            s_url = helpFile;
            return true;
        }
    }
}
