using System;
using ZXMAK2.Host.Entities;


namespace ZXMAK2.Host.Interfaces
{
    public interface IFolderBrowserDialog : IDisposable
    {
        string Description { get; set; }
        string SelectedPath { get; set; }
        DlgResult ShowDialog(object owner);
    }
}
