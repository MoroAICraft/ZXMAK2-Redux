using System;
using System.IO;


namespace ZXMAK2.Hardware.Circuits.Ata
{
    public class AtaPasser : IDisposable
    {
        private FileStream _hDevice;
        private PhysicalDeviceInfo _deviceInfo;
        private bool _isReadOnly;
        public string ImagePath { get { return _deviceInfo == null ? string.Empty : _deviceInfo.filename; } }


        public void Dispose()
        {
            Close();
        }


        public bool Open(PhysicalDeviceInfo deviceInfo, bool isReadOnly)
        {
            try
            {
                _deviceInfo = deviceInfo;
                _isReadOnly = isReadOnly;
                _hDevice = new FileStream(
                    PortablePaths.Resolve(_deviceInfo.filename),
                    FileMode.Open,
                    _isReadOnly || !PortablePaths.IsInternal(_deviceInfo.filename) ? FileAccess.Read : FileAccess.ReadWrite,
                    FileShare.ReadWrite);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
        }

        public void Close()
        {
            try
            {
                if (_hDevice != null)
                {
                    _hDevice.Dispose();
                }
                _hDevice = null;
                _deviceInfo = null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public bool IsLoaded()
        {
            return _hDevice != null;
        }

        public bool Flush()
        {
            try
            {
                if (_isReadOnly)
                {
                    return true;
                }
                if (_hDevice != null)
                {
                    _hDevice.Flush();
                }
                return _hDevice != null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
        }

        public bool Seek(uint nsector)
        {
            try
            {
                if (_hDevice == null)
                {
                    return false;
                }
                var offset = ((long)nsector) << 9;
                var newOffset = _hDevice.Seek(offset, SeekOrigin.Begin);
                return newOffset == offset && offset >= 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
        }

        public bool ReadSector(byte[] dst, int offset)
        {
            try
            {
                if (_hDevice == null)
                {
                    return false;
                }
                var read = _hDevice.Read(dst, offset, 512);
                for (var i = read; i < 512; i++)
                {
                    dst[i + offset] = 0;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
        }

        public bool WriteSector(byte[] src, int offset)
        {
            try
            {
                if (_isReadOnly)
                {
                    return true;
                }
                if (_hDevice != null)
                {
                    if (!PortablePaths.IsInternal(_deviceInfo.filename))
                    {
                        // First actual guest write, never on mount: retain a
                        // complete local writable image without touching source.
                        var target = PortablePaths.NewSavePath(_deviceInfo.filename, "HDD");
                        var position = _hDevice.Position;
                        PortablePaths.WriteAtomic(target, output =>
                        {
                            var previous = _hDevice.Position;
                            try { _hDevice.Position = 0; _hDevice.CopyTo(output); }
                            finally { _hDevice.Position = previous; }
                        });
                        var replacement = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                        replacement.Position = position;
                        _hDevice.Dispose();
                        _hDevice = replacement;
                        _deviceInfo.filename = target;
                        Logger.Info("External HDD saved locally on first guest write: {0}", target);
                    }
                    _hDevice.Write(src, offset, 512);
                }
                return _hDevice != null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
        }
    }
}
