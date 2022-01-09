using System.IO;
using System.Xml;
using UnityEngine;

namespace Heph.Unity.Localizer
{
    public delegate void LanguageChangeEventHandler(string oldLanguage, string newLanguage);
    /// <summary>
    /// A simple localizer for Uinty that uses Resource.Load method to load an xml file and reads from it. 
    /// </summary>
    public static class Localizer
    {
        public static event LanguageChangeEventHandler OnLanguageChange;
        private static string _language = "en";
        public static string Language
        {
            get => _language;
            set
            {
                if (value != _language)
                {
                    string oldLanguage = _language;
                    _language = value;
                    OnLanguageChange?.Invoke(oldLanguage, value);
                }
            }
        }
        public static string GetField(string path, string name)
        {
            TextAsset xmlFile = Resources.Load(path) as TextAsset;
            if (xmlFile != null)
            {
                Resources.UnloadAsset(xmlFile);
                using (MemoryStream ms = new MemoryStream(xmlFile.bytes))
                {
                    using (XmlReader reader = XmlReader.Create(ms))
                    {
                        bool readProperties = false;
                        bool readField = false;
                        while (reader.Read())
                        {
                            if (readProperties && reader.NodeType == XmlNodeType.Element) // Found specified field, start reading the text.
                            {
                                readField = reader.GetAttribute("name") == name;
                            }
                            if (readField && reader.NodeType == XmlNodeType.Text)
                            {
                                return reader.Value;
                            }
                            if (!readProperties && reader.NodeType == XmlNodeType.Element && reader.Name == "Language") // If found specified language, start reading.
                            {
                                string lang = reader.GetAttribute("name");
                                if (lang != null && lang == Language)
                                {
                                    readProperties = true;
                                }
                            }
                        }
                    }
                }
            }
            return string.Empty; // could not find the field, return an empty string.
        }
    }
}