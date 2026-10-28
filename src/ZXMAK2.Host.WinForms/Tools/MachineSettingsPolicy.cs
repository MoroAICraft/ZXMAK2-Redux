using System;
using System.Linq;
using System.Xml;
using ZXMAK2.Engine;
using ZXMAK2.Engine.Entities;
using ZXMAK2.Engine.Interfaces;

namespace ZXMAK2.Host.WinForms.Tools
{
    public static class MachineSettingsPolicy
    {
        private static string TypeName(XmlNode node)
        {
            // Saved config uses "Full.Type.Name, Assembly", while old configs
            // may contain only the type name.
            var value = node.Attributes["type"].Value;
            return value.Split(',')[0].Trim();
        }
        private static XmlElement FindDevice(XmlNode root, string type)
        { return root.SelectNodes("Device").Cast<XmlElement>().FirstOrDefault(n => TypeName(n) == type); }

        public static bool SameFloppies(XmlNode current, XmlNode pending, string type)
        {
            var a = FindDevice(current, type);
            var b = FindDevice(pending, type);
            if (a == null || b == null) return false;
            return string.Join("", a.SelectNodes("Drive").Cast<XmlNode>().Select(n => n.OuterXml)) ==
                string.Join("", b.SelectNodes("Drive").Cast<XmlNode>().Select(n => n.OuterXml));
        }

        public static void RefreshSavedMediaPaths(XmlNode pending, XmlNode saved, bool sdChanged, bool neoChanged)
        {
            foreach (XmlElement node in pending.SelectNodes("Device"))
            {
                var type = TypeName(node);
                var live = FindDevice(saved, type);
                if (live == null) continue;
                string[] names = null;
                if (!sdChanged && type.EndsWith(".ZsdPentEvo")) names = new[] { "sdSource", "sdPath", "sdImage" };
                if (!neoChanged && type.EndsWith(".ZxOmniSoundDevice")) names = new[] { "neoGsSdImage" };
                if (!neoChanged && type.EndsWith(".NeoGsDevice")) names = new[] { "sdImage" };
                if (names != null) foreach (var name in names)
                { if (live.HasAttribute(name)) node.SetAttribute(name, live.GetAttribute(name)); else node.RemoveAttribute(name); }
            }
        }
        public static XmlNode Snapshot(IBus bus)
        {
            var doc = new XmlDocument();
            var root = doc.AppendChild(doc.CreateElement("Bus"));
            bus.SaveConfigXml(root);
            return root;
        }

        private static string WithoutVolume(XmlNode node)
        {
            var copy = node.CloneNode(true);
            foreach (XmlAttribute attribute in copy.SelectNodes(".//@volume | .//@masterVolume"))
                attribute.OwnerElement.RemoveAttributeNode(attribute);
            return copy.OuterXml;
        }

        public static bool SoundHardwareChanged(IBus current, IBus pending)
        { return SoundSignature(current) != SoundSignature(pending); }

        private static string SoundSignature(IBus bus)
        {
            return string.Join("\n", bus.FindDevices<BusDeviceBase>()
                .Where(d => d.Category == BusDeviceCategory.Sound || d.Category == BusDeviceCategory.Music)
                .Select(d => {
                    var doc = new XmlDocument(); var node = doc.AppendChild(doc.CreateElement("Device"));
                    d.SaveConfigXml(node);
                    return d.GetType().FullName + WithoutVolume(node);
                }).OrderBy(s => s));
        }

        public static bool TryApplyVolumes(BusManager current, IBus pending)
        {
            var oldState = Snapshot(current);
            var newState = Snapshot(pending);
            if (WithoutVolume(oldState) != WithoutVolume(newState)) return false;
            var live = current.FindDevices<BusDeviceBase>();
            var staged = pending.FindDevices<BusDeviceBase>();
            if (live.Count != staged.Count) return false;
            // Check the entire operation before changing any live property.
            for (int i = 0; i < live.Count; i++)
            {
                if (live[i].GetType() != staged[i].GetType()) return false;
                var property = live[i].GetType().GetProperty("Volume");
                if (property != null && (!property.CanWrite || property.PropertyType != typeof(int))) return false;
            }
            for (int i = 0; i < live.Count; i++)
            {
                var property = live[i].GetType().GetProperty("Volume");
                if (property != null) property.SetValue(live[i], property.GetValue(staged[i], null), null);
            }
            var pendingBus = pending as BusManager;
            if (pendingBus != null) current.SetMasterVolumeLive(pendingBus.MasterVolume);
            return true;
        }
    }
}
