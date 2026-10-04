using System;


namespace ZXMAK2.Host.Interfaces
{
    public interface IHostKeyboard : IDisposable
    {
        void Scan();
        IKeyboardState State { get; }
    }

    // Optional: legacy/XNA hosts need not implement a resettable physical reader.
    public interface IResettableHostKeyboard
    {
        void ResetState();
    }
}
