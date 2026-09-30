using System;

using System.IO;
using System.Xml;
using ZXMAK2.Hardware.Circuits.SecureDigital;
using ZXMAK2.Engine;
using ZXMAK2.Host.Entities;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Dependency;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Mvvm;


namespace ZXMAK2.Hardware.Evo
{
    public enum SdSourceType
    {
        Image,
        HostFolder,
    }

    public class ZsdPentEvo : BusDeviceBase, IMediaStatusDevice,
        ISecureDigitalMediaStatus, IWritableSdSession
    {
        #region Fields

        private IMemoryDevice mem;
        private bool m_sandbox;
        private SdCard card;
        private byte buf;
        private bool card_cs;
        private readonly object cardSync = new object();
        private SdMediaCommand openImageCommand;
        private SdMediaCommand mountFolderCommand;
        private SdMediaCommand refreshFolderCommand;
        private SdMediaCommand exportFolderCommand;
        private SdMediaCommand ejectImageCommand;
        private SdSourceType configuredSourceType = SdSourceType.Image;
        private string configuredSourcePath = string.Empty;
        private string m_restoreWarning;

        public string RestoreWarning { get { return m_restoreWarning; } }

        public void PromptForRecoveredSession()
        {
            lock (cardSync)
            {
                var folder = card == null ? null : card.HostFolderStream;
                var image = card == null ? null : card.ImageSessionStream;
                if ((folder == null || !folder.WasRecovered) &&
                    (image == null || !image.WasRecovered))
                    return;
                var result = Locator.Resolve<IUserQuery>().Show(
                    "An unfinished SD session was found after the previous run.\n\n" +
                    "Restore its changes?\n\n" +
                    "Yes: keep the changes for this session.\n" +
                    "No: discard them; the source remains unchanged.",
                    "Recover SD changes?",
                    DlgButtonSet.YesNo,
                    DlgIcon.Question);
                // Closing the prompt is treated as keeping the data.
                if (result == DlgResult.No)
                {
                    if (folder != null)
                        folder.DiscardChanges();
                    else
                        image.DiscardChanges();
                    card.Reset();
                }
                if (folder != null)
                    folder.WasRecovered = false;
                if (image != null)
                    image.WasRecovered = false;
            }
        }

        public bool HasPendingChanges
        {
            get
            {
                lock (cardSync)
                {
                    var folder = card == null ? null : card.HostFolderStream;
                    var image = card == null ? null : card.ImageSessionStream;
                    return folder != null && folder.HasChanges ||
                        image != null && image.HasChanges;
                }
            }
        }

        public bool PrepareForMediaChange()
        {
            lock (cardSync)
            {
                var folder = card == null ? null : card.HostFolderStream;
                var image = card == null ? null : card.ImageSessionStream;
                if ((folder == null || !folder.HasChanges) &&
                    (image == null || !image.HasChanges))
                    return true;

                var query = Locator.Resolve<IUserQuery>();
                var choice = query.Show(
                    "The mounted SD card has unsaved changes.\n\n" +
                    "Yes: save them to the original " +
                        (folder != null ? "Windows folder" : "SD image") + ".\n" +
                    "No: discard them.\n" +
                    "Cancel: keep the SD card mounted.",
                    "Save SD changes?",
                    DlgButtonSet.YesNoCancel,
                    DlgIcon.Question);
                if (choice == DlgResult.Cancel)
                    return false;
                if (choice == DlgResult.Yes)
                {
                    if (folder != null)
                        folder.CommitChanges();
                    else
                        image.CommitChanges();
                }
                else if (choice == DlgResult.No)
                {
                    if (folder != null)
                        folder.DiscardChanges();
                    else
                        image.DiscardChanges();
                }
                else
                    return false;
                return true;
            }
        }

        #endregion


        public ZsdPentEvo()
        {
            Category = BusDeviceCategory.Disk;
            Name = "SD Z-controller";
            Description = "PentEvo SD Card\r\nWritten by ZEK";
        }


        public bool SHADOW
        {
            get { return (mem == null) ? false : mem.SYSEN; }
        }


        #region IBusDevice

        public override void BusInit(IBusManager bmgr)
        {
            m_sandbox = bmgr.IsSandbox;
            mem = bmgr.FindDevice<IMemoryDevice>();
            openImageCommand = new SdMediaCommand(
                CommandUi_OnExecute,
                CommandUi_OnCanExecute,
                "Open SD Card image...",
                MediaCommandAction.Load);
            mountFolderCommand = new SdMediaCommand(
                MountFolderCommandUi_OnExecute,
                MountFolderCommandUi_OnCanExecute,
                "Mount folder as SD...",
                MediaCommandAction.MountFolder);
            refreshFolderCommand = new SdMediaCommand(
                RefreshFolderCommandUi_OnExecute,
                RefreshFolderCommandUi_OnCanExecute,
                "Refresh mounted folder",
                MediaCommandAction.Refresh);
            exportFolderCommand = new SdMediaCommand(
                ExportFolderCommandUi_OnExecute,
                ExportFolderCommandUi_OnCanExecute,
                "Save SD session as image...",
                MediaCommandAction.Export);
            ejectImageCommand = new SdMediaCommand(
                EjectCommandUi_OnExecute,
                CommandUi_OnCanExecute,
                "Eject SD Card",
                MediaCommandAction.Eject);
            bmgr.AddCommandUi(openImageCommand);
            bmgr.AddCommandUi(mountFolderCommand);
            bmgr.AddCommandUi(refreshFolderCommand);
            bmgr.AddCommandUi(exportFolderCommand);
            bmgr.AddCommandUi(ejectImageCommand);

            bmgr.Events.SubscribeReset(Reset);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x0057, WrXX57);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x0057, RdXX57);
            bmgr.Events.SubscribeWrIo(0x00FF, 0x0077, WrXX77);
            bmgr.Events.SubscribeRdIo(0x00FF, 0x0077, RdXX77);
        }

        public override void BusConnect()
        {
            lock (cardSync)
            {
                if (card != null)
                {
                    card.Close();
                }
                card = new SdCard();
                card_cs = false;
                buf = 0xFF;
            }
            // Machine Settings builds its editable profile in a sandbox bus.
            // Like IDE, that bus must retain only the media descriptor: opening
            // the same writable image there would contend with the live card.
            if (!m_sandbox)
            {
                RestoreConfiguredCard();
            }
        }

        public override void BusDisconnect()
        {
            lock (cardSync)
            {
                if (card != null)
                {
                    card.Close();
                    card = null;
                }
                card_cs = false;
                buf = 0xFF;
            }
        }

        #endregion


        protected override void OnConfigLoad(XmlNode itemNode)
        {
            base.OnConfigLoad(itemNode);
            var legacyImage = Utils.GetXmlAttributeAsString(
                itemNode, "sdImage", string.Empty);
            configuredSourcePath = Utils.GetXmlAttributeAsString(
                itemNode, "sdPath", legacyImage);
            var source = Utils.GetXmlAttributeAsString(
                itemNode, "sdSource", "Image");
            configuredSourceType = string.Equals(
                source, "HostFolder", StringComparison.OrdinalIgnoreCase) ?
                SdSourceType.HostFolder : SdSourceType.Image;
        }

        protected override void OnConfigSave(XmlNode itemNode)
        {
            base.OnConfigSave(itemNode);
            Utils.SetXmlAttribute(
                itemNode,
                "sdImage",
                configuredSourceType == SdSourceType.Image ?
                    configuredSourcePath ?? string.Empty : string.Empty);
            Utils.SetXmlAttribute(
                itemNode, "sdSource", configuredSourceType.ToString());
            Utils.SetXmlAttribute(
                itemNode, "sdPath", configuredSourcePath ?? string.Empty);
        }


        #region Bus handlers

        protected virtual void Reset()
        {
            lock (cardSync)
            {
                card_cs = false;
                if (card != null)
                {
                    card.Reset();
                }
                buf = 0xFF;
            }
        }

        protected virtual void WrXX57(ushort addr, byte val, ref bool handled)
        {
            if (handled)
                return;
            handled = true;

            if (SHADOW)
            {
                if ((addr & 0x8000) != 0)
                {
                    lock (cardSync)
                    {
                        card_cs = ((val & 0x02) != 0);
                    }
                }
                else
                    CardWr(val);
            }
            else
                CardWr(val);
        }

        protected virtual void RdXX57(ushort addr, ref byte val, ref bool handled)
        {
            if (handled)
                return;
            handled = true;

            // BaseConf zports.v: all xx57 reads return SPI data and clock FF.
            // In Shadow, A15 distinguishes configuration/data writes only.
            val = CardRd();
        }

        protected virtual void WrXX77(ushort addr, byte val, ref bool handled)
        {
            if (!handled && !SHADOW)
            {
                handled = true;
                lock (cardSync)
                {
                    card_cs = ((val & 0x02) != 0);
                }
            }
        }

        protected virtual void RdXX77(ushort addr, ref byte val, ref bool handled)
        {
            if (!handled && !SHADOW)
            {
                handled = true;
                val = 0;
            }
        }

        #endregion


        #region SD Card Emu

        protected void CardWr(byte val)
        {
            lock (cardSync)
            {
                if (card == null)
                {
                    return;
                }
                buf = card.Rd();
                card.Wr(val);
            }
        }

        protected byte CardRd()
        {
            lock (cardSync)
            {
                if (card == null)
                {
                    return 0xFF;
                }
                var tmp = buf;
                buf = card.Rd();
                card.Wr(0xff);
                return tmp;
            }
        }

        #endregion


        #region CommandUi

        private bool CommandUi_OnCanExecute(Object arg)
        {
            var viewResolver = Locator.Resolve<IResolver>("View");
            return viewResolver.CheckAvailable<IOpenFileDialog>();
        }

        private bool MountFolderCommandUi_OnCanExecute(Object arg)
        {
            var viewResolver = Locator.Resolve<IResolver>("View");
            return viewResolver.CheckAvailable<IFolderBrowserDialog>();
        }

        private bool RefreshFolderCommandUi_OnCanExecute(Object arg)
        {
            return configuredSourceType == SdSourceType.HostFolder &&
                !string.IsNullOrEmpty(configuredSourcePath) &&
                Directory.Exists(configuredSourcePath);
        }

        private bool ExportFolderCommandUi_OnCanExecute(Object arg)
        {
            var viewResolver = Locator.Resolve<IResolver>("View");
            lock (cardSync)
            {
                return configuredSourceType == SdSourceType.HostFolder &&
                    card != null && card.HostFolderStream != null &&
                    viewResolver.CheckAvailable<ISaveFileDialog>();
            }
        }

        private void ExportFolderCommandUi_OnExecute(Object arg)
        {
            if (!ExportFolderCommandUi_OnCanExecute(arg))
                return;
            try
            {
                var viewResolver = Locator.Resolve<IResolver>("View");
                using (var dialog = viewResolver.TryResolve<ISaveFileDialog>())
                {
                    if (dialog == null)
                        return;
                    dialog.Title = "Save virtual SD card as an image";
                    dialog.Filter = "SD image (*.img)|*.img";
                    dialog.DefaultExt = "img";
                    dialog.OverwritePrompt = true;
                    if (dialog.ShowDialog(arg) != DlgResult.OK)
                        return;
                    lock (cardSync)
                    {
                        var folder = card == null ? null : card.HostFolderStream;
                        if (folder == null)
                            return;
                        folder.ExportImage(dialog.FileName);
                    }
                    Locator.Resolve<IUserMessage>().Info(
                        "The SD session was saved as an image. The mounted folder remains unchanged.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>().Error(
                    "Cannot save SD session as an image!\n\n{0}", ex.Message);
            }
        }

        private void CommandUi_OnExecute(Object arg)
        {
            if (!CommandUi_OnCanExecute(arg))
            {
                return;
            }
            try
            {
                var viewResolver = Locator.Resolve<IResolver>("View");
                var dlg = viewResolver.TryResolve<IOpenFileDialog>();
                if (dlg == null)
                {
                    return;
                }
                dlg.CheckFileExists = true;
                //dlg.CheckPathExists = true;
                //dlg.DefaultExt = "img|ima|vhd";
                dlg.Filter = "Disk image file (*.img, *.ima, *.vhd)|*.img;*.ima;*.vhd";
                dlg.Multiselect = false;
                if (dlg.ShowDialog(arg) != DlgResult.OK)
                {
                    return;
                }

                if (!MountCard(dlg.FileName))
                    return;
                openImageCommand.NotifyExecutedSuccessfully();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot open SD Card image!\n\n{0}", ex.Message);
            }
        }

        private void EjectCommandUi_OnExecute(Object arg)
        {
            try
            {
                if (!EjectCard())
                    return;
                ejectImageCommand.NotifyExecutedSuccessfully();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot eject SD Card!\n\n{0}", ex.Message);
            }
        }

        private void MountFolderCommandUi_OnExecute(Object arg)
        {
            if (!MountFolderCommandUi_OnCanExecute(arg))
                return;
            try
            {
                var viewResolver = Locator.Resolve<IResolver>("View");
                using (var dialog = viewResolver.TryResolve<IFolderBrowserDialog>())
                {
                    if (dialog == null)
                        return;
                    dialog.Description = "Select a folder to mount as an SD card";
                    if (configuredSourceType == SdSourceType.HostFolder &&
                        Directory.Exists(configuredSourcePath))
                    {
                        dialog.SelectedPath = configuredSourcePath;
                    }
                    if (dialog.ShowDialog(arg) != DlgResult.OK)
                        return;
                    var warningCount = MountHostFolder(dialog.SelectedPath);
                    if (warningCount < 0)
                        return;
                    mountFolderCommand.NotifyExecutedSuccessfully();
                    ShowFolderWarnings(warningCount);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot mount folder as SD card!\n\n{0}", ex.Message);
            }
        }

        private void RefreshFolderCommandUi_OnExecute(Object arg)
        {
            if (!RefreshFolderCommandUi_OnCanExecute(arg))
                return;
            try
            {
                var warningCount = MountHostFolder(configuredSourcePath);
                if (warningCount < 0)
                    return;
                refreshFolderCommand.NotifyExecutedSuccessfully();
                ShowFolderWarnings(warningCount);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                Locator.Resolve<IUserMessage>()
                    .Error("Cannot refresh mounted SD folder!\n\n{0}", ex.Message);
            }
        }

        private static void ShowFolderWarnings(int warningCount)
        {
            if (warningCount <= 0)
                return;
            Locator.Resolve<IUserMessage>().Warning(
                "The folder was mounted, but {0} inaccessible, unsupported, or oversized item(s) were skipped. See the log for details.",
                warningCount);
        }

        public MediaStatusKind MediaStatusKind
        {
            get { return MediaStatusKind.SecureDigital; }
        }

        public bool IsMediaMounted
        {
            get
            {
                lock (cardSync)
                {
                    return card != null &&
                        !string.IsNullOrEmpty(card.MountedFileName);
                }
            }
        }

        public int SecureDigitalIndex
        {
            get { return 0; }
        }

        public string ConfiguredImageFileName
        {
            get
            {
                return configuredSourceType == SdSourceType.Image ?
                    configuredSourcePath ?? string.Empty : string.Empty;
            }
        }

        public SdSourceType ConfiguredSourceType
        {
            get { return configuredSourceType; }
        }

        public string ConfiguredSourcePath
        {
            get { return configuredSourcePath ?? string.Empty; }
        }

        /// <summary>
        /// Updates the saved configuration on a detached settings bus. A live
        /// replacement must go through MountCard so the previous handle is
        /// closed before the new image is opened.
        /// </summary>
        public void ConfigureCard(string fileName)
        {
            ConfigureSource(SdSourceType.Image, fileName);
        }

        public void ConfigureSource(SdSourceType sourceType, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                configuredSourceType = SdSourceType.Image;
                configuredSourcePath = string.Empty;
                return;
            }
            configuredSourceType = sourceType;
            configuredSourcePath = sourceType == SdSourceType.HostFolder ?
                NormalizeFolderPath(path) : NormalizeImageFileName(path);
        }

        public bool MountCard(string fileName)
        {
            var fullPath = NormalizeImageFileName(fileName);
            if (string.IsNullOrEmpty(fullPath))
            {
                return EjectCard();
            }

            lock (cardSync)
            {
                if (configuredSourceType == SdSourceType.Image &&
                    string.Equals(configuredSourcePath, fullPath,
                        StringComparison.OrdinalIgnoreCase) &&
                    card != null && !string.IsNullOrEmpty(card.MountedFileName))
                {
                    card.Reset();
                    return true;
                }
            }

            if (!PrepareForMediaChange())
                return false;

            // Build and validate the replacement before swapping it in. A
            // failed open therefore leaves the current source operational.
            SdCard replacement = null;
            SdCard previous = null;
            try
            {
                Logger.Info("SD hot swap: build '{0}'", fullPath);
                replacement = new SdCard();
                replacement.Open(fullPath);
                lock (cardSync)
                {
                    previous = card;
                    card = replacement;
                    replacement = null;
                    card_cs = false;
                    buf = 0xFF;
                    configuredSourceType = SdSourceType.Image;
                    configuredSourcePath = fullPath;
                }
                if (previous != null)
                    previous.Close();
                Logger.Info("SD hot swap completed: '{0}'", fullPath);
                return true;
            }
            finally
            {
                if (replacement != null)
                    replacement.Close();
            }
        }

        public int MountHostFolder(string folderPath)
        {
            var fullPath = NormalizeFolderPath(folderPath);
            if (!PrepareForMediaChange())
                return -1;
            SdCard replacement = null;
            HostFolderFat32Stream folderStream = null;
            try
            {
                Logger.Info("SD folder mount: build '{0}'", fullPath);
                replacement = new SdCard();
                folderStream = replacement.OpenHostFolder(fullPath);

                SdCard previous;
                lock (cardSync)
                {
                    previous = card;
                    card = replacement;
                    replacement = null;
                    card_cs = false;
                    buf = 0xFF;
                    configuredSourceType = SdSourceType.HostFolder;
                    configuredSourcePath = fullPath;
                }
                if (previous != null)
                    previous.Close();
                Logger.Info(
                    "SD folder mount completed: '{0}', warnings={1}",
                    fullPath,
                    folderStream.Warnings.Count);
                return folderStream.Warnings.Count;
            }
            finally
            {
                if (replacement != null)
                    replacement.Close();
            }
        }

        public bool EjectCard()
        {
            if (!PrepareForMediaChange())
                return false;
            SdCard previous;
            lock (cardSync)
            {
                previous = card;
                card = new SdCard();
                card_cs = false;
                buf = 0xFF;
                configuredSourceType = SdSourceType.Image;
                configuredSourcePath = string.Empty;
            }
            if (previous != null)
            {
                previous.Close();
            }
            Logger.Info("SD card ejected");
            return true;
        }

        private void RestoreConfiguredCard()
        {
            var configured = configuredSourcePath;
            if (string.IsNullOrEmpty(configured))
            {
                return;
            }
            try
            {
                if (configuredSourceType == SdSourceType.HostFolder)
                    MountHostFolder(configured);
                else
                    MountCard(configured);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Cannot restore configured SD source: {0}", configured);
                m_restoreWarning = "The configured SD source could not be opened: " +
                    ex.Message;
                lock (cardSync)
                {
                    if (card != null)
                        card.Close();
                    card = new SdCard();
                    card_cs = false;
                    buf = 0xFF;
                }
            }
        }

        private static string NormalizeImageFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }
            var fullPath = Path.GetFullPath(fileName.Trim());
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("SD Card image not found", fullPath);
            }
            return fullPath;
        }

        private static string NormalizeFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return string.Empty;
            var fullPath = Path.GetFullPath(folderPath.Trim());
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException(
                    "SD source folder not found: " + fullPath);
            return fullPath;
        }

        #endregion CommandUi

        private sealed class SdMediaCommand : CommandDelegate, IMediaCommand
        {
            public SdMediaCommand(
                Action<object> action,
                Func<object, bool> canExecute,
                string text,
                MediaCommandAction mediaAction)
                : base(action, canExecute, text)
            {
                MediaAction = mediaAction;
            }

            public MediaCommandKind MediaKind
            {
                get { return MediaCommandKind.SecureDigital; }
            }

            public MediaCommandAction MediaAction { get; private set; }

            public int DriveIndex
            {
                get { return 0; }
            }

            public event EventHandler ExecutedSuccessfully;

            public void NotifyExecutedSuccessfully()
            {
                var handler = ExecutedSuccessfully;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }
    }
}
