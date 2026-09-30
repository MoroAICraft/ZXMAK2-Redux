namespace ZXMAK2.Host.Interfaces
{
    /// <summary>Host-side save prompt for a folder-backed SD card.</summary>
    public interface IWritableSdSession
    {
        bool HasPendingChanges { get; }
        bool PrepareForMediaChange();
        void PromptForRecoveredSession();
        string RestoreWarning { get; }
    }
}
