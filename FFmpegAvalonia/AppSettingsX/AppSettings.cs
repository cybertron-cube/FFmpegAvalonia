using ExtensionMethods;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FFmpegAvalonia.AppSettingsX
{
    public class AppSettings
    {
        internal readonly Dictionary<string, Profile> Profiles;
        internal readonly Settings Settings;
        private readonly string SettingsXMLPath = Path.Combine(AppContext.BaseDirectory, "settings.xml");
        private readonly string ProfilesXMLPath = Path.Combine(AppContext.BaseDirectory, "profiles.xml");
        private readonly bool IsLinux;
        
        public AppSettings()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                IsLinux = true;
            }
            if (File.Exists(SettingsXMLPath))
            {
                Settings = new Settings();
                ImportSettingsXML();
                if (Settings.FFmpegPath == null || Settings.FFmpegPath == String.Empty)
                {
                    FindFFPath();
                }
            }
            else
            {
                Settings = new Settings();
                FindFFPath();
            }
            if (File.Exists(ProfilesXMLPath))
            {
                Profiles = new Dictionary<string, Profile>();
                ImportProfilesXML();
            }
            else
            {
                Profiles = new Dictionary<string, Profile>
                {
                    { "Test Profile", new Profile() { Name = "Test Profile", Arguments = "-map 0:v:0 -map 0:a:2", OutputExtension = ".mkv" } }
                };
            }
        }
        
        public void ImportProfilesXML()
        {
            var text = File.ReadAllText(ProfilesXMLPath);
            ImportProfilesXML(ref text);
        }
        
        public void ImportSettingsXML()
        {
            var text = File.ReadAllText(SettingsXMLPath);
            ImportSettingsXML(ref text);
        }
        
        public string GetXMLText(string name)
        {
            Type type = Type.GetType(name, true);
            if (type == typeof(Profile))
            {
                return GetProfilesXElement().ToString();
            }
            else if (type == typeof(Settings))
            {
                return GetSettingsXElement().ToString();
            }
            else throw new ArgumentException(name);
        }
        
        public void Save(string name, ref string text)
        {
            Type type = Type.GetType(name, true);
            if (type == typeof(Profile))
            {
                ImportProfilesXML(ref text);
            }
            else if (type == typeof(Settings))
            {
                ImportSettingsXML(ref text);
            }
            else throw new ArgumentException(name);
        }
        
        private void ImportProfilesXML(ref string text)
        {
            XDocument doc = XDocument.Parse(text);
            PropertyInfo[] properties = typeof(Profile).GetProperties();
            foreach (XElement element in doc.Root.Descendants("Profile"))
            {
                Profile profile = new() { Name = element.Attribute("Name").Value };
                foreach (PropertyInfo property in properties.Where(x => x.Name != "Name"))
                {
                    property.SetValue(profile, element.Element(property.Name).Value);
                }
                Profiles[profile.Name] = profile;
            }
        }
        
        private void ImportSettingsXML(ref string text)
        {
            XDocument doc = XDocument.Parse(text);
            PropertyInfo[] properties = typeof(Settings).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                if (doc.Root.Element(property.Name) != null)
                {
                    bool? boolean = doc.Root.Element(property.Name).Value.TryParseToBool(out string str);
                    if (boolean != null)
                    {
                        property.SetValue(Settings, boolean);
                        continue;
                    }
                    bool isInt32 = Int32.TryParse(str, out int num);
                    if (isInt32)
                    {
                        property.SetValue(Settings, num);
                        continue;
                    }
                    property.SetValue(Settings, str);
                }
            }
        }
        
        private XElement GetProfilesXElement()
        {
            XElement result = new("Profiles", new XAttribute("AppVersion", Assembly.GetExecutingAssembly().GetName().Version!.ToString()));
            PropertyInfo[] properties = typeof(Profile).GetProperties();
            foreach (var profileName in Profiles.Keys)
            {
                XElement profileElement = new("Profile", new XAttribute("Name", profileName));
                foreach (PropertyInfo property in properties.Where(x => x.Name != "Name"))
                {
                    profileElement.Add(new XElement(property.Name, Profiles[profileName].GetPropVal(property.Name)));
                }
                result.Add(profileElement);
            }
            return result;
        }
        
        private XElement GetSettingsXElement()
        {
            XElement result = new("Settings", new XAttribute("AppVersion", Assembly.GetExecutingAssembly().GetName().Version!.ToString()));
            PropertyInfo[] properties = typeof(Settings).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                result.Add(new XElement(property.Name, Settings.GetPropVal(property.Name)));
            }
            return result;
        }
        
        public void ExportProfilesXML()
        {
            GetProfilesXElement().Save(ProfilesXMLPath);
        }
        
        public void ExportSettingsXML()
        {
            GetSettingsXElement().Save(SettingsXMLPath);
        }
        
        public void Save()
        {
            ExportSettingsXML();
            ExportProfilesXML();
        }
        
        private void FindFFPath()
        {
            if (IsLinux)
            {
                string pathOne = @"/usr/bin/ffmpeg";
                if (File.Exists(pathOne))
                {
                    Settings.FFmpegPath = Path.GetDirectoryName(pathOne)!;
                }
            }
            else
            {
                string pathOne = @"C:\FFmpeg\ffmpeg.exe";
                if (File.Exists(pathOne))
                {
                    Settings.FFmpegPath = Path.GetDirectoryName(pathOne)!;
                }
            }
        }
    }
}