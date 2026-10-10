using System;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using ZXMAK2.Host.WinForms.Controls;
using ZXMAK2.Host.Entities;
using ZXMAK2.Host.WinForms.Tools;
using ZXMAK2.Host.Presentation.Interfaces;


namespace ZXMAK2.Host.WinForms.Services
{
    public class SettingService : ISettingService
    {
        private const string RegistryPath = "SOFTWARE\\ZXMAK2";
        private readonly Dictionary<string, int> m_values = new Dictionary<string, int>();
        private readonly object m_sync = new object();
        private readonly string m_path = PortablePaths.ConfigFile("interface.xml");

        public SettingService()
        {
            if (File.Exists(m_path))
            {
                try
                {
                    var xml = new XmlDocument();
                    xml.Load(m_path);
                    foreach (XmlElement node in xml.SelectNodes("/Interface/Value"))
                    {
                        int value;
                        if (int.TryParse(node.GetAttribute("value"), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out value)) m_values[node.GetAttribute("name")] = value;
                    }
                }
                catch (Exception ex) { Logger.Error(ex, "Portable interface settings could not be read; original retained"); }
                return;
            }
            // One-time read-only import. Never create or write a registry key.
            try
            {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false))
            {
                if (key != null)
                    foreach (var name in new[] { "WindowWidth", "WindowHeight", "ViewShowToolBar", "ViewShowStatusBar",
                        "SyncSource", "RenderScaleMode", "RenderNoBorder", "RenderVideoFilter", "RenderSmoothing",
                        "RenderMimicTv", "RenderDisplayIcon", "RenderDebugInfo" })
                    {
                        var value = key.GetValue(name);
                        if (value is int) m_values[name] = (int)value;
                    }
            }
            }
            catch (Exception ex) { Logger.Warn("Legacy interface settings unavailable; using portable defaults: {0}", ex.Message); }
            try { Persist(); }
            catch (Exception ex) { Logger.Error(ex, "Cannot create portable interface settings"); }
        }

        private void Persist()
        {
            var xml = new XmlDocument();
            var root = xml.CreateElement("Interface");
            xml.AppendChild(root);
            foreach (var pair in m_values)
            {
                var node = xml.CreateElement("Value");
                node.SetAttribute("name", pair.Key);
                node.SetAttribute("value", pair.Value.ToString(CultureInfo.InvariantCulture));
                root.AppendChild(node);
            }
            PortablePaths.WriteAtomic(m_path, stream => xml.Save(stream));
        }

        
        public int WindowWidth
        {
            get { return GetValue("WindowWidth", 640); }
            set { SetValue("WindowWidth", value); }
        }

        public int WindowHeight
        {
            get { return GetValue("WindowHeight", 512); }
            set { SetValue("WindowHeight", value); }
        }

        public bool IsToolBarVisible
        {
            get { return GetValue("ViewShowToolBar", true); }
            set { SetValue("ViewShowToolBar", value); }
        }

        public bool IsStatusBarVisible
        {
            get { return GetValue("ViewShowStatusBar", true); }
            set { SetValue("ViewShowStatusBar", value); }
        }


        public SyncSource SyncSource 
        {
            get { return GetValue("SyncSource", default(SyncSource)); }
            set { SetValue("SyncSource", value); }
        }

        public ScaleMode RenderScaleMode
        {
            get { return GetValue("RenderScaleMode", default(ScaleMode)); }
            set { SetValue("RenderScaleMode", value); }
        }

        public bool RenderNoBorder
        {
            get { return GetValue("RenderNoBorder", false); }
            set { SetValue("RenderNoBorder", value); }
        }

        public VideoFilter RenderVideoFilter
        {
            get { return GetValue("RenderVideoFilter", default(VideoFilter)); }
            set { SetValue("RenderVideoFilter", value); }
        }

        public bool RenderSmooth
        {
            get { return GetValue("RenderSmoothing", false); }
            set { SetValue("RenderSmoothing", value); }
        }

        public bool RenderMimicTv
        {
            get { return GetValue("RenderMimicTv", true); }
            set { SetValue("RenderMimicTv", value); }
        }

        public bool RenderDisplayIcon
        {
            get { return GetValue("RenderDisplayIcon", true); }
            set { SetValue("RenderDisplayIcon", value); }
        }

        public bool RenderDebugInfo
        {
            get { return GetValue("RenderDebugInfo", false); }
            set { SetValue("RenderDebugInfo", value); }
        }



        #region Private

        private bool GetValue(string name, bool defValue)
        {
            try
            {
                return GetValue(name, defValue ? 1 : 0) != 0;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return defValue;
            }
        }

        private void SetValue(string name, bool value)
        {
            try
            {
                SetValue(name, value ? 1 : 0);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private SyncSource GetValue(string name, SyncSource defValue) 
        {
            try
            {
                var iValue = GetValue(name, (int)defValue);
                if (Enum.IsDefined(typeof(SyncSource), iValue))
                {
                    return (SyncSource)iValue;
                }
                return defValue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return defValue;
            }
        }

        private void SetValue(string name, SyncSource value)
        {
            try
            {
                SetValue(name, (int)value);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private ScaleMode GetValue(string name, ScaleMode defValue)
        {
            try
            {
                var iValue = GetValue(name, (int)defValue);
                if (Enum.IsDefined(typeof(ScaleMode), iValue))
                {
                    return (ScaleMode)iValue;
                }
                return defValue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return defValue;
            }
        }

        private void SetValue(string name, ScaleMode value)
        {
            try
            {
                SetValue(name, (int)value);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private VideoFilter GetValue(string name, VideoFilter defValue)
        {
            try
            {
                var iValue = GetValue(name, (int)defValue);
                if (Enum.IsDefined(typeof(VideoFilter), iValue))
                {
                    return (VideoFilter)iValue;
                }
                return defValue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return defValue;
            }
        }

        private void SetValue(string name, VideoFilter value)
        {
            try
            {
                SetValue(name, (int)value);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private int GetValue(string name, int defValue)
        {
            lock (m_sync)
            {
                int value;
                return m_values.TryGetValue(name, out value) ? value : defValue;
            }
        }

        private void SetValue(string name, int value)
        {
            try
            {
                lock (m_sync)
                {
                    int previous;
                    if (m_values.TryGetValue(name, out previous) && previous == value) return;
                    m_values[name] = value;
                    Persist();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        #endregion Private
    }
}
