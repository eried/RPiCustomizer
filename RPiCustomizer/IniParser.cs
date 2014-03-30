﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConfigurationParser
{
    /// <summary>
    /// Source: http://bytes.com/topic/net/insights/797169-reading-parsing-ini-file-c
    /// </summary>
    public class IniFile
    {
        private readonly Dictionary<SectionPair, String> _keyPairs = new Dictionary<SectionPair, string>();
        private readonly String _iniFilePath;

        private struct SectionPair
        {
            public String Section;
            public String Key;
        }

        /// <summary>
        /// Opens the INI file at the given path and enumerates the values in the IniParser.
        /// </summary>
        /// <param name="ini">Path to INI file or INI contents.</param>
        /// <param name="isContents"></param>
        /// <param name="encoding">File encoding</param>
        public IniFile(String ini, bool isContents = false, Encoding encoding=null)
        {
            TextReader iniFile;
            String currentRoot = null;

            _iniFilePath = ini;

            if (!File.Exists(ini))
            {
                var tmp = Path.GetFullPath(ini);
                if (!String.IsNullOrEmpty(tmp) && File.Exists(tmp))
                    ini = tmp;
            }

            if (isContents || !File.Exists(ini))
            {
                iniFile = new StringReader((String) ini.Clone());
                ini = Path.GetTempFileName();
                ConfigurationFile = ini;
            }
            else
            {
                iniFile = new StreamReader(ini, encoding ?? Encoding.Default);
                ConfigurationFile = ini;
            }

            try
            {
                var strLine = iniFile.ReadLine();

                while (strLine != null)
                {
                    strLine = strLine.Trim();

                    if (!strLine.StartsWith(";") && strLine.Length > 1)
                    {
                        if (strLine.StartsWith("[") && strLine.EndsWith("]"))
                        {
                            currentRoot = strLine.Substring(1, strLine.Length - 2);
                        }
                        else
                        {
                            var keyPair = strLine.Split(new char[] {'='}, 2);

                            SectionPair sectionPair;
                            String value = null;

                            sectionPair.Section = currentRoot;
                            sectionPair.Key = keyPair[0];

                            if (keyPair.Length > 1)
                                value = keyPair[1];

                            _keyPairs.Add(sectionPair, value);
                        }
                    }

                    strLine = iniFile.ReadLine();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                iniFile.Close();
            }
            /*}
            else
                throw new FileNotFoundException("Unable to locate " + iniPath);*/

        }

        public IniFile()
        {
        }

        /// <summary>
        /// Returns the value for the given section, key pair.
        /// </summary>
        /// <param name="sectionName">Section name</param>
        /// <param name="settingName">Key name</param>
        /// <param name="defaultValue">Default value</param>
        public String GetSetting(String sectionName, String settingName, String defaultValue=null)
        {
            SectionPair sectionPair;
            sectionPair.Section = sectionName;
            sectionPair.Key = settingName;

            return _keyPairs.ContainsKey(sectionPair) ? _keyPairs[sectionPair] : defaultValue;
        }

        public IEnumerable<string> GetSections()
        {
            var o = new List<String>();
            foreach (var p in _keyPairs.Keys)
                if (!o.Contains(p.Section))
                    o.Add(p.Section);
            return o;
        }

        /// <summary>
        /// Enumerates all lines for given section.
        /// </summary>
        /// <param name="sectionName">Section to enum.</param>
        public String[] EnumSection(String sectionName)
        {
            var tmpArray = new ArrayList();

            foreach (var pair in _keyPairs.Keys)
            {
                if (pair.Section == sectionName)
                    tmpArray.Add(pair.Key);
            }

            return (String[])tmpArray.ToArray(typeof(String));
        }

        /// <summary>
        /// Adds or replaces a setting to the table to be saved.
        /// </summary>
        /// <param name="sectionName">Section to add under.</param>
        /// <param name="settingName">Key name to add.</param>
        /// <param name="settingValue">Value of key.</param>
        public void AddSetting(String sectionName, String settingName, String settingValue)
        {
            SectionPair sectionPair;
            sectionPair.Section = sectionName;
            sectionPair.Key = settingName;

            if (_keyPairs.ContainsKey(sectionPair))
                _keyPairs.Remove(sectionPair);

            _keyPairs.Add(sectionPair, settingValue);
        }

        /// <summary>
        /// Adds or replaces a setting to the table to be saved with a null value.
        /// </summary>
        /// <param name="sectionName">Section to add under.</param>
        /// <param name="settingName">Key name to add.</param>
        public void AddSetting(String sectionName, String settingName)
        {
            AddSetting(sectionName, settingName, null);
        }

        /// <summary>
        /// Remove a setting.
        /// </summary>
        /// <param name="sectionName">Section to add under.</param>
        /// <param name="settingName">Key name to add.</param>
        public void DeleteSetting(String sectionName, String settingName)
        {
            SectionPair sectionPair;
            sectionPair.Section = sectionName;
            sectionPair.Key = settingName;

            if (_keyPairs.ContainsKey(sectionPair))
                _keyPairs.Remove(sectionPair);
        }

        /// <summary>
        /// Save settings to new file.
        /// </summary>
        /// <param name="newFilePath">New file path.</param>
        public void SaveSettings(String newFilePath)
        {
            var sections = new ArrayList();
            var strToSave = "";

            foreach (var sectionPair in _keyPairs.Keys)
            {
                if (!sections.Contains(sectionPair.Section))
                    sections.Add(sectionPair.Section);
            }

            foreach (var section in sections)
            {
                strToSave += ("[" + section + "]\r\n");

                foreach (var sectionPair in _keyPairs.Keys)
                {
                    if (sectionPair.Section == (string) section)
                    {
                        var tmpValue = _keyPairs[sectionPair];

                        if (tmpValue != null)
                            tmpValue = "=" + tmpValue;

                        strToSave += (sectionPair.Key + tmpValue + "\r\n");
                    }
                }

                strToSave += "\r\n";
            }

            try
            {
                TextWriter tw = new StreamWriter(newFilePath);
                tw.Write(strToSave);
                tw.Close();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Save settings back to ini file.
        /// </summary>
        public void SaveSettings()
        {
            SaveSettings(_iniFilePath);
        }

        public string ConfigurationFile { get; set; }

        public bool GetSettingAsBoolean(string sectionName, string settingName, bool defaultValue)
        {
            return GetSettingAsInteger(sectionName, settingName, defaultValue?1:0)==1;
        }

        public int GetSettingAsInteger(string sectionName, string settingName, int defaultValue)
        {
            var s = GetSetting(sectionName, settingName);
            int r;

            return int.TryParse(s,out r) ? r : defaultValue;
        }
    }
}