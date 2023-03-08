using Avalonia.Extensions.Controls;
using ExtensionMethods;
using HarfBuzzSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FFmpegAvalonia
{
    public class AppSettings
    {
        internal readonly Dictionary<string, Profile> Profiles;
        internal readonly Settings Settings;
        private readonly string SettingsXML = Path.Combine(AppContext.BaseDirectory, "settings.xml");
        private readonly string ProfilesXML = Path.Combine(AppContext.BaseDirectory, "profiles.xml");
        private readonly bool _IsLinux;
        public AppSettings()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _IsLinux = true;
            }
            if (File.Exists(SettingsXML))
            {
                Settings = new Settings();
                ImportSettingsXML();
                if (Settings.FFmpegPath is null)
                {
                    FindFFPath();
                }
            }
            else
            {
                Settings = new Settings();
                FindFFPath();
            }
            if (File.Exists(ProfilesXML))
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
            var text = File.ReadAllText(ProfilesXML);
            XDocument doc = XDocument.Parse(text);
            var test = doc.Root.Descendants();
            PropertyInfo[] properties = typeof(Profile).GetProperties();
            foreach (XElement element in doc.Root.Descendants("Profile"))
            {
                //string name;
                Profile profile = new() { Name = element.Attribute("Name").Value };
                foreach (PropertyInfo property in properties.Where(x => x.Name != "Name"))
                {
                    property.SetValue(profile, element.Element(property.Name).Value);
                }
                Profiles.Add(profile.Name, profile);
            }
        }
        public void ImportSettingsXML()
        {
            var text = File.ReadAllText(SettingsXML);
            XDocument doc = XDocument.Parse(text);
            PropertyInfo[] properties = typeof(Settings).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                object value;
                bool? boolean = doc.Root.Element(property.Name).Value.TryParseToBool(out string str);
                if (boolean is null)
                {
                    value = str;
                }
                else
                {
                    value = boolean;
                }
                property.SetValue(Settings, value);
            }
        }
        public void ExportProfilesXML()
        {
            XElement export = new("Profiles", new XAttribute("AppVersion", Assembly.GetExecutingAssembly().GetName().Version!.ToString()));
            PropertyInfo[] properties = typeof(Profile).GetProperties();
            foreach (var profileName in Profiles.Keys)
            {
                XElement profileElement = new("Profile", new XAttribute("Name", profileName));
                foreach (PropertyInfo property in properties.Where(x => x.Name != "Name"))
                {
                    profileElement.Add(new XElement(property.Name, Profiles[profileName].GetPropVal(property.Name)));
                }
                export.Add(profileElement);
            }
            export.Save(ProfilesXML);
        }
        public void ExportSettingsXML()
        {
            XElement export = new("Settings", new XAttribute("AppVersion", Assembly.GetExecutingAssembly().GetName().Version!.ToString()));
            PropertyInfo[] properties = typeof(Settings).GetProperties();
            foreach (PropertyInfo property in properties)
            {
                export.Add(new XElement(property.Name, Settings.GetPropVal(property.Name)));
            }
            export.Save(SettingsXML);
        }
        public void Save()
        {
            ExportSettingsXML();
            ExportProfilesXML();
        }
        private void FindFFPath()
        {
            if (_IsLinux)
            {
                string pathOne = @"/home/linuxbrew/.linuxbrew/opt/ffmpeg";
                if (File.Exists(pathOne))
                {
                    Settings.FFmpegPath = Path.GetDirectoryName(pathOne);
                }
            }
            else
            {
                string pathOne = @"C:\FFmpeg\ffmpeg.exe";
                if (File.Exists(pathOne))
                {
                    Settings.FFmpegPath = Path.GetDirectoryName(pathOne);
                }
            }
        }
        public void ReadProfiles(string dir)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(dir);
            foreach (FileInfo file in dirInfo.GetFiles())
            {
                string profileName = Path.GetFileNameWithoutExtension(file.FullName);
                string args = "";
                string ext = "";
                int mo = 0;
                int dr = 0;
                //extract extension, datarate, arguments, and name of profile
                var lines = File.ReadAllLines(file.FullName);
                Trace.TraceInformation("name: " + profileName);
                foreach (string line in lines)
                {
                    if (line.StartsWith("mo="))
                    {
                        string moStr = line.Split('=')[1].Trim();
                        moStr = moStr.Replace("k", "000");
                        mo = Int32.Parse(moStr);
                    }
                    if (line.StartsWith("dr="))
                    {
                        if (line.Contains("$mo"))
                        {
                            if (mo == 0)
                            {
                                Trace.TraceInformation("ERROR: " + line);
                                Trace.TraceInformation("ERROR: " + file.FullName);
                                return;
                            }
                            else
                            {
                                dr *= mo;
                            }
                        }
                        else
                        {
                            string drStr = line.Split("=")[1].Trim();
                            dr = Int32.Parse(drStr);
                        }
                    }
                    if (line.StartsWith("ffmpeg"))
                    {
                        //
                        if (!line.Contains("$etn"))
                        {
                            Trace.TraceInformation("name: " + profileName);
                            Trace.TraceInformation("args: " + args);
                            Trace.TraceInformation("ext: " + ext);
                            Trace.TraceInformation("mo: " + mo);
                            Trace.TraceInformation("dr: " + dr);
                            Trace.TraceInformation("ERROR: LINE DOES NOT CONTAIN $etn");
                        }
                        Trace.TraceInformation("ffmpeg line: " + line);
                        //
                        if (line.Contains(@"$dr"))
                        {
                            if (dr == 0)
                            {
                                Trace.TraceInformation("ERROR: " + line);
                                Trace.TraceInformation("ERROR: " + file.FullName);
                                return;
                            }
                            else
                            {
                                Regex reg = new("(?:ffmpeg.*\\$etn\\s+)(?<args1>(?:(?!\\s+\\$).)*).*(?<dr>\\$dr)(?<args2>(?:(?!\\s+\\$).)*).*(?<ext>\\..*)");
                                Match match = reg.Match(line);
                                args = $"{match.Groups["args1"].Value.Trim()} {dr/*match.Groups["dr"].Value this returns $dr so use calculated datarate instead*/} {match.Groups["args2"].Value.Trim()}";
                                ext = match.Groups["ext"].Value;
                            }
                        }
                        else
                        {
                            Regex reg = new("(?:ffmpeg.*\\$etn\\s+)(?<args>(?:(?!\\s+\\$).)*).*(?<ext>\\..*)");
                            Match match = reg.Match(line);
                            args = match.Groups["args"].Value.Trim();
                            ext = match.Groups["ext"].Value;
                        }
                    }
                }
                Trace.TraceInformation("args: " + args);
                Trace.TraceInformation("ext: " + ext);
                Trace.TraceInformation("mo: " + mo);
                Trace.TraceInformation("dr: " + dr);
                if (!String.IsNullOrEmpty(args) && !String.IsNullOrEmpty(ext) && !String.IsNullOrEmpty(profileName))
                {
                    Profiles.Add(profileName, new Profile() { Name = profileName, Arguments = args, OutputExtension = ext });
                }
                else
                {
                    Trace.TraceInformation("ERROR: Failed to parse file, " + file.FullName);
                }
            }
        }
    }
}