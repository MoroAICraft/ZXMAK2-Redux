/* 
 *  Copyright 2008-2018 Alex Makeev
 * 
 *  This file is part of ZXMAK2 (ZX Spectrum virtual machine).
 *
 *  ZXMAK2 is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  ZXMAK2 is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with ZXMAK2.  If not, see <http://www.gnu.org/licenses/>.
 *
 *  Description: Sound player
 *  Date: 10.07.2018
 */
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ZXMAK2.Host.Interfaces;
using ZXMAK2.Host.WinForms.Tools;
using ZXMAK2.DirectX;
using ZXMAK2.DirectX.DirectSound;
using ZXMAK2.DirectX.Native;



namespace ZXMAK2.Host.WinForms.Mdx
{
    public sealed unsafe class DirectSound : IHostSound
    {
        #region Fields

        // Native objects belong to the playback worker, including recovery.
        private DirectSound8 _device;
        private SecondarySoundBuffer _soundBuffer;
        private readonly IntPtr _windowHandle;
        private readonly ConcurrentQueue<uint[]> _fillQueue = new ConcurrentQueue<uint[]>();
        private readonly ConcurrentQueue<uint[]> _playQueue = new ConcurrentQueue<uint[]>();
        private readonly AutoResetEvent _fillEvent = new AutoResetEvent(true);
        private readonly AutoResetEvent _frameEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _cancelEvent = new AutoResetEvent(false);
        private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
        private readonly int _sampleRate;
        private readonly int _bufferSize;
        private readonly int _bufferCount;
        private readonly uint _zeroValue;

        private Thread _wavePlayThread;
        private volatile bool _isFinished;
        private volatile bool _isAvailable;
        private uint? _lastSample;

        #endregion Fields


        public DirectSound(
            Form form,
            int sampleRate,
            int bufferCount)
        {
            if ((sampleRate % 50) != 0)
            {
                throw new ArgumentOutOfRangeException("sampleRate", "Sample rate must be a multiple of 50!");
            }
            if (sampleRate <= 0 || bufferCount < 2)
                throw new ArgumentOutOfRangeException("bufferCount", "A positive sample rate and at least two buffers are required.");
            _sampleRate = sampleRate;
            var bufferSize = sampleRate / 50;
            for (int i = 0; i < bufferCount; i++)
            {
                _fillQueue.Enqueue(new uint[bufferSize]);
            }
            _bufferSize = bufferSize;
            _bufferCount = bufferCount;
            _zeroValue = 0;

            _windowHandle = form != null ? form.Handle : IntPtr.Zero;

            _wavePlayThread = new Thread(WavePlayThreadProc);
            _wavePlayThread.IsBackground = true;
            _wavePlayThread.Name = "WavePlay";
            _wavePlayThread.Priority = ThreadPriority.Highest;
            _wavePlayThread.Start();
        }

        private void CreateOutput()
        {
            _device = new DirectSound8();
            _device.SetCooperativeLevel(_windowHandle, DSSCL.PRIORITY);

            // we always use 16 bit stereo (uint per sample)
            const short channels = 2;
            const short bitsPerSample = 16;
            const int sampleSize = 4; // channels * (bitsPerSample / 8);
            var wf = new WaveFormat(_sampleRate, bitsPerSample, channels);

            // Create a buffer
            var bufferDesc = new DSBUFFERDESC();
            bufferDesc.dwBufferBytes = _bufferSize * sampleSize * _bufferCount;
            bufferDesc.dwFlags =
                DSBCAPS_FLAGS.CTRLVOLUME |
                DSBCAPS_FLAGS.CTRLPOSITIONNOTIFY |
                DSBCAPS_FLAGS.GLOBALFOCUS |
                DSBCAPS_FLAGS.GETCURRENTPOSITION2;
            bufferDesc.Format = wf;
            _soundBuffer = new SecondarySoundBuffer(_device, bufferDesc);

            var posNotify = new DSBPOSITIONNOTIFY[_bufferCount];
            for (int i = 0; i < posNotify.Length; i++)
            {
                posNotify[i] = new DSBPOSITIONNOTIFY();
                posNotify[i].dwOffset = i * _bufferSize * sampleSize;
                posNotify[i].WaitHandle = _fillEvent;
            }
            _soundBuffer.SetNotificationPositions(posNotify);
        }

        public void Dispose()
        {
            if (_wavePlayThread == null)
            {
                return;
            }
            try
            {
                _isFinished = true;
                _stopEvent.Set();
                _fillEvent.Set();
                _cancelEvent.Set();
                _wavePlayThread.Join();
                if (_fillEvent != null)
                {
                    _fillEvent.Dispose();
                }
                if (_frameEvent != null)
                {
                    _frameEvent.Dispose();
                }
                if (_cancelEvent != null)
                {
                    _cancelEvent.Dispose();
                }
                _stopEvent.Dispose();
            }
            finally
            {
                _wavePlayThread = null;
            }
        }


        #region WavePlay

        private unsafe void WavePlayThreadProc()
        {
            var hBuffer = IntPtr.Zero;
            var failed = false;
            try
            {
                const int sampleSize = 4;
                var rawBufferLength = _bufferSize * sampleSize;
                hBuffer = Marshal.AllocHGlobal(rawBufferLength);
                while (!_isFinished)
                {
                    try
                    {
                        CreateOutput();
                        NativeHelper.INITBLK((void*)hBuffer, 0, rawBufferLength);
                        _lastSample = _zeroValue;
                        uint[] stale;
                        while (_playQueue.TryDequeue(out stale)) _fillQueue.Enqueue(stale);
                        // A newly-created DirectSound buffer is not guaranteed to
                        // contain PCM silence.  Clear every segment before starting
                        // playback so host memory cannot be heard during start-up.
                        for (var i = 0; i < _bufferCount; i++)
                        {
                            _soundBuffer.Write(
                                i * rawBufferLength,
                                (void*)hBuffer,
                                rawBufferLength,
                                DSBLOCK.None);
                        }
                        _soundBuffer.Play(0, DSBPLAY_FLAGS.LOOPING);
                        _isAvailable = true;
                        if (failed) Logger.Info("DirectSound output recovered.");
                        failed = false;

                        var lastWrittenBuffer = -1;
                        do
                        {
                            // A lost device may stop notifications entirely.
                            if (!_fillEvent.WaitOne(1000) && !_isFinished)
                            {
                                var status = _soundBuffer.Status;
                                if ((status & DSBSTATUS.PLAYING) == 0 || (status & DSBSTATUS.BUFFERLOST) != 0)
                                    throw new InvalidOperationException("DirectSound playback stopped or its buffer was lost.");
                            }
                            if (_isFinished) break;
                            var nextIndex = (lastWrittenBuffer + 1) % _bufferCount;
                            var playPos = _soundBuffer.PlayPosition % (_bufferCount * rawBufferLength);
                            var playingIndex = playPos / rawBufferLength;
                            for (var i = nextIndex; i != playingIndex && !_isFinished; i = ++i % _bufferCount)
                            {
                                if (!OnBufferRequest((uint*)hBuffer, _bufferSize))
                                {
                                    NativeHelper.INITBLK((void*)hBuffer, 0, rawBufferLength);
                                }
                                var writePos = i * rawBufferLength;
                                _soundBuffer.Write(writePos, (void*)hBuffer, rawBufferLength, DSBLOCK.None);
                                lastWrittenBuffer = i;
                            }
                        } while (!_isFinished);
                    }
                    catch (Exception ex)
                    {
                        if (!failed && !_isFinished) Logger.Error(ex);
                        failed = true;
                    }
                    finally
                    {
                        _isAvailable = false;
                        _frameEvent.Set();
                        ReleaseOutput();
                    }
                    // Keep the VM running on time sync while unavailable.
                    // Retry at most once per second, never spin or grow queues.
                    if (!_isFinished) _stopEvent.WaitOne(1000);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                _isAvailable = false;
                _frameEvent.Set();
                ReleaseOutput();
                if (hBuffer != IntPtr.Zero) Marshal.FreeHGlobal(hBuffer);
            }
        }

        private void ReleaseOutput()
        {
            var buffer = _soundBuffer;
            var device = _device;
            _soundBuffer = null;
            _device = null;
            // Even a failed Stop must not leak the buffer/device or prevent exit.
            if (buffer != null)
            {
                try { buffer.Stop(); }
                catch (Exception ex) { Logger.Warn("DirectSound stop: {0}", ex.Message); }
                try { buffer.Dispose(); }
                catch (Exception ex) { Logger.Warn("DirectSound buffer cleanup: {0}", ex.Message); }
            }
            if (device != null)
            {
                try { device.Dispose(); }
                catch (Exception ex) { Logger.Warn("DirectSound device cleanup: {0}", ex.Message); }
            }
        }

        private bool OnBufferRequest(uint* pBuffer, int sampleCount)
        {
            uint[] source;
            if (!_playQueue.TryDequeue(out source))
            {
                // Holding the last non-zero sample across an underrun creates a
                // DC plateau followed by an audible click when emulation resumes.
                // Ramp both channels to digital silence within this buffer.
                var sample = _lastSample.HasValue ? _lastSample.Value : _zeroValue;
                var left = (short)(sample & 0xFFFF);
                var right = (short)(sample >> 16);
                for (var i = 0; i < sampleCount; i++)
                {
                    var remain = sampleCount - i - 1;
                    var outLeft = (short)(left * remain / sampleCount);
                    var outRight = (short)(right * remain / sampleCount);
                    pBuffer[i] = (uint)(ushort)outLeft |
                        ((uint)(ushort)outRight << 16);
                }
                _lastSample = _zeroValue;
                return true;
            }
            try
            {
                fixed (uint* psrc = source)
                {
                    NativeHelper.CPBLK(pBuffer, psrc, sampleCount * 4);
                    //NativeMethods.CopyMemory(pBuffer, psrc, sampleCount * 4);
                    _lastSample = pBuffer[sampleCount - 1];
                }
                return true;
            }
            finally
            {
                _fillQueue.Enqueue(source);
                _frameEvent.Set();
            }
        }

        private uint[] LockBuffer()
        {
            uint[] buffer;
            if (_fillQueue.TryDequeue(out buffer))
            {
                return buffer;
            }
            return null;
        }

        private void UnlockBuffer(uint[] buffer)
        {
            _playQueue.Enqueue(buffer);
        }

        public double GetLoadLevel()
        {
            return _playQueue.Count / (double)_bufferCount;
        }

        #endregion WavePlay


        #region IHostSound

        public int SampleRate
        {
            get { return _sampleRate; }
        }

        public bool IsSyncSupported 
        {
            get { return _isAvailable; }
        }

        public bool IsSynchronized { get; set; }

        private void WaitFrame()
        {
            _frameEvent.Reset();
            _cancelEvent.Reset();
            Thread.MemoryBarrier();
            if (!_isAvailable || _isFinished || _playQueue.Count == 0)
            {
                return;
            }
            WaitHandle.WaitAny(new[] { _frameEvent, _cancelEvent }, 40);
        }

        public void CancelWait()
        {
            _cancelEvent.Set();
        }

        public void PushFrame(IFrameInfo info, IFrameSound frame)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }
            if (!_isAvailable || _isFinished) return;
            if (IsSynchronized)
            {
                WaitFrame();
            }
            if (!_isAvailable || _isFinished) return;
            var buffer = LockBuffer();
            if (buffer == null)
            {
                return;
            }
            var srcBuffer = frame.GetBuffer();
            Array.Copy(srcBuffer, buffer, buffer.Length);
            UnlockBuffer(buffer);
        }

        #endregion IHostSound
    }
}
