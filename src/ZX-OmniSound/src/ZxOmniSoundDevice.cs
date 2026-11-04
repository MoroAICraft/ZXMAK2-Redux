using System;
using System.Collections.Generic;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Interfaces;
using ZXMAK2.Host.Interfaces;

namespace ZXMAK2.Hardware.Evo
{
    /// <summary>
    /// One ZXBUS card with one owner for every emulated sound interface.
    /// Reuses the accepted YM/SAA/SoundDrive, NeoGS and OPL4 engines.
    /// </summary>
    public sealed class ZxOmniSoundDevice : ZxMultiSoundCore,
        IMediaStatusDevice, ISecureDigitalMediaStatus,
        ISoundSourceGainConfiguration
    {
        private readonly NeoGsDevice m_neoGs = new NeoGsDevice();
        private readonly ZxmMoonSoundDevice m_moonSound =
            new ZxmMoonSoundDevice();

        public ZxOmniSoundDevice() : base(true, true)
        {
            Name = "ZX OmniSound";
            Description = "One ZXBUS card: 2 x YM2203, SAA1099, " +
                "SoundDrive, MIDI, NeoGS (GS/MOD/MP3), " +
                "Max-compatible OPL3 and MoonSound OPL4.";
            AutomaticConfiguration = true;
            YmEnabled = true;
            SaaEnabled = true;
            GeneralSoundEnabled = false;
            SoundDriveEnabled = true;
            TsFmSaaPortCompatibility = true;
            m_moonSound.ResampleHostOutput = true;
            m_moonSound.MaxOpl3Interface = true;
        }

        public NeoGsDevice NeoGs { get { return m_neoGs; } }
        public int OutputGainPercent { get { return 140; } }
        public ZxmMoonSoundDevice MoonSound { get { return m_moonSound; } }
        public MediaStatusKind MediaStatusKind
        {
            get { return MediaStatusKind.SecureDigital; }
        }
        public bool IsMediaMounted { get { return m_neoGs.IsMediaMounted; } }
        public int SecureDigitalIndex { get { return 1; } }
        public string SdImageFileName
        {
            get { return m_neoGs.SdImageFileName; }
            set { m_neoGs.SdImageFileName = value; }
        }

        public override IEnumerable<ISoundRenderer> SoundRenderers
        {
            get
            {
                foreach (var renderer in base.SoundRenderers)
                    yield return renderer;
                yield return m_neoGs;
                yield return m_moonSound;
            }
        }

        public override void BusInit(IBusManager bmgr)
        {
            if (bmgr.FindDevice<NeoGsDevice>() != null ||
                bmgr.FindDevice<ZxMultiSoundDevice>() != null ||
                bmgr.FindDevice<ZxMultiSoundMaxDevice>() != null ||
                bmgr.FindDevice<ZxmMoonSoundDevice>() != null)
                throw new InvalidOperationException(
                    "ZX OmniSound already owns NeoGS, MultiSound and " +
                    "MoonSound ports; remove the separate audio board.");
            if (bmgr.FindDevice<AYCHRV>() != null ||
                bmgr.FindDevice<TurboSoundFmPro>() != null)
                throw new InvalidOperationException(
                    "ZX OmniSound YM requires Internal Music: None.");
            // NeoGS owns GS; the shared MoonSound FM core exposes Max's
            // OPL3 interface, so each port and FM voice has one owner.
            GeneralSoundEnabled = false;
            TsFmSaaPortCompatibility = true;
            base.BusInit(bmgr);
            m_neoGs.BusInit(bmgr);
            m_moonSound.BusInit(bmgr);
        }

        public override void BusConnect()
        {
            base.BusConnect();
            m_neoGs.BusConnect();
            m_moonSound.BusConnect();
        }

        public override void BusDisconnect()
        {
            m_moonSound.BusDisconnect();
            m_neoGs.BusDisconnect();
            base.BusDisconnect();
        }

        public override void ResetState()
        {
            base.ResetState();
            m_neoGs.ResetState();
            m_moonSound.ResetState();
        }

        protected override void OnConfigLoad(XmlNode node)
        {
            base.OnConfigLoad(node);
            GeneralSoundEnabled = false;
            TsFmSaaPortCompatibility = true;
            SdImageFileName = Utils.GetXmlAttributeAsString(
                node, "neoGsSdImage", string.Empty);
        }

        protected override void OnConfigSave(XmlNode node)
        {
            base.OnConfigSave(node);
            Utils.SetXmlAttribute(node, "neoGsSdImage", SdImageFileName);
        }
    }
}
