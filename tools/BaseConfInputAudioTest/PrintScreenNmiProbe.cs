using System;
using System.Reflection;
using System.Windows.Forms;
using ZXMAK2.Host.WinForms.Controls;
using ZXMAK2.Host.WinForms.Views;

class PrintScreenNmiProbe {
    static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    [STAThread] static int Main(){try{
        using(var view=new MainView(null))using(var menu=new ToolStripMenuItem("Test context")){
            int count=0;
            var nmi=(ToolStripMenuItem)typeof(MainView).GetField("menuVmNmi",F).GetValue(view);
            nmi.Enabled=true;nmi.Click+=delegate{count++;};
            var router=(HostKeyboardRouter)typeof(MainView).GetProperty("KeyboardRouter",F).GetValue(view,null);
            var filter=(IMessageFilter)typeof(MainView).GetField("_keyboardMessages",F).GetValue(view);
            // A tracked menu establishes main-window context without showing a window.
            filter.GetType().GetMethod("TrackMenu").Invoke(filter,new object[]{menu});
            filter.GetType().GetMethod("OnOpened",F).Invoke(filter,new object[]{menu,EventArgs.Empty});
            var down=Message.Create(IntPtr.Zero,0x100,(IntPtr)(int)Keys.PrintScreen,IntPtr.Zero);
            var up=Message.Create(IntPtr.Zero,0x101,(IntPtr)(int)Keys.PrintScreen,IntPtr.Zero);
            Check(filter.PreFilterMessage(ref down)&&count==0,"PrtSc fired on key-down");
            Check(filter.PreFilterMessage(ref up)&&count==1,"PrtSc release did not invoke VM NMI");
            Check(filter.PreFilterMessage(ref up)&&count==2,"KEYUP-only PrintScreen did not invoke NMI");
            for(int i=0;i<5;i++)Check(filter.PreFilterMessage(ref down)&&count==2,"Held PrtSc repeated NMI");
            Check(filter.PreFilterMessage(ref up)&&count==3,"Held PrtSc release count");
            nmi.Enabled=false;Check(filter.PreFilterMessage(ref up)&&count==3,"Disabled VM NMI still executed");
            Check(nmi.ShortcutKeyDisplayString=="PrintScreen","NMI menu shortcut label missing");
            Check(!router.ExecuteShortcut(Keys.F10,false),"F10 stolen from BC");
            Check(router.ExecuteShortcut(Keys.F12,false),"F12 shortcut lost");
            Console.WriteLine("PASS: PrintScreen KEYUP-only, down/up, hold, disabled command, menu label, F10 and F12");
        }return 0;
    }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
