using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Heph.Unity.Localizer
{
    /// <summary>
    /// Determines which type of file will be read to get local fields.
    /// </summary>
    public enum FileType
    {
        /// <summary>
        /// Local fields will be read from an xml file.
        /// </summary>
        Xml = 0,
        /// <summary>
        /// Local fields will be read from a json file.
        /// </summary>
        Json = 1,
        /// <summary>
        /// Local fields will be read from a txt file.
        /// </summary>
        Txt = 2
    }
    /// <summary>
    /// Determines what kind of file structure will be used.
    /// </summary>
    public enum LocalizerOptions
    {
        /// <summary>
        /// Use this when fields are separated by their language inside the same file.
        /// </summary>
        UseSingleFile = 0,
        /// <summary>
        /// Use this when there is a file for each language and name of the file starts with the "language_". 
        /// For instance the game menu will be localized and the files will be named as; en_Menu, tr_Menu, de_Menu.
        /// </summary>
        UseSeparateFiles = 1,
        /// <summary>
        ///  Use this when there is a folder for each language and that folders name is "language". 
        ///  For instance there is a folder called "en" and it contains the files; Menu, ItemNames, ItemDescriptions for their english translations.
        /// </summary>
        UseSeparateFolders = 2
    }
    public delegate void LanguageChangeEventHandler(string oldLanguage, string newLanguage);
    /// <summary>
    /// A simple localizer for Uinty that uses Resource.Load method to load files.
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
        private static string _pathPrefix = "Localization/";
        public static string PathPrefix
        {
            get => _pathPrefix;
            set => _pathPrefix = value[^1] == '/' ? value : value + "/";
        }
        public static string GetField(string path, string name, LocalizerOptions localizerOptions = LocalizerOptions.UseSingleFile, FileType fileType = FileType.Xml)
        {
            if (Resources.Load(GetFinalPath(ref path, ref localizerOptions)) is TextAsset textAsset)
            {
                Resources.UnloadAsset(textAsset);
                switch (fileType)
                {
                    case FileType.Xml:
                        return Xml(textAsset, ref name, ref localizerOptions);
                    case FileType.Json:
                        return Json(textAsset, ref name, ref localizerOptions);
                    case FileType.Txt:
                        return Txt(textAsset, ref name, ref localizerOptions);
                    default:
                        return string.Empty;
                }
            }
            return string.Empty;
        }
        private static string GetFinalPath(ref string path, ref LocalizerOptions localizerOptions)
        {
            string finalPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(path))
            {
                path = path.Replace('\\', '/');
                if (!string.IsNullOrEmpty(PathPrefix))
                {
                    finalPath = PathPrefix;
                }
                if (localizerOptions == LocalizerOptions.UseSingleFile)
                {
                    finalPath += path;
                }
                else if (localizerOptions == LocalizerOptions.UseSeparateFiles)
                {
                    int lastBlockIndex = path.LastIndexOf('/') + 1;
                    finalPath += $"{path[0..lastBlockIndex]}{Language}_{path[lastBlockIndex..]}";
                }
                else if (localizerOptions == LocalizerOptions.UseSeparateFolders)
                {
                    int lastBlockIndex = path.LastIndexOf('/') + 1;
                    finalPath += $"{path[0..lastBlockIndex]}{Language}/{path[lastBlockIndex..]}";
                }
            }
            return finalPath;
        }
        private static string Xml(TextAsset xmlAsset, ref string name, ref LocalizerOptions localizerOptions)
        {
            using (MemoryStream ms = new MemoryStream(xmlAsset.bytes))
            {
                using (XmlReader reader = XmlReader.Create(ms))
                {
                    string elementName = string.Empty;
                    bool readProperties = localizerOptions != LocalizerOptions.UseSingleFile;
                    bool readField = false;
                    while (reader.Read())
                    {
                        if (readField && reader.NodeType == XmlNodeType.Text)
                        {
                            return reader.Value.Replace("\t", string.Empty);
                        }
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            elementName = reader.GetAttribute("name");
                            if (!readProperties)
                            {
                                readProperties = elementName == Language;
                            }
                            readField = (readProperties || reader.GetAttribute("lang") == Language) && elementName == name;
                        }
                    }
                }
            }
            return string.Empty; // could not find the field, return an empty string.
        }
        private static string Json(TextAsset jsonAsset, ref string name, ref LocalizerOptions localizerOptions)
        {
            if (JsonConvert.DeserializeObject(jsonAsset.text) is JObject jobj)
            {
                if (localizerOptions == LocalizerOptions.UseSingleFile)
                {
                    JToken languageToken = jobj[Language];
                    if (languageToken != null)
                    {
                        return languageToken[name]?.ToString() ?? string.Empty;
                    }
                }
                else
                {
                    return jobj[name]?.ToString() ?? string.Empty;
                }
            }
            return string.Empty;
        }
        private static string Txt(TextAsset txtAsset, ref string name, ref LocalizerOptions localizerOptions)
        {
            string[] lines = txtAsset.text.Replace("\r", string.Empty).Split('\n');
            string result = string.Empty;
            string line = string.Empty;
            bool readProperties = localizerOptions != LocalizerOptions.UseSingleFile;
            bool readField = false;
            for (int i = 0; i < lines.Length; i++)
            {
                line = lines[i];
                if (readProperties)
                {
                    if (line.StartsWith("-L:")) // read all the fields of this language.
                    {
                        return result;
                    }
                    if (readField)
                    {
                        if (line.Length >= 3 && line[0..3] == "-N:")
                        {
                            return result;
                        }
                        if (line != "")
                        {
                            if (!string.IsNullOrEmpty(result))
                            {
                                result += $"\n{line}";
                            }
                            else
                            {
                                result += line;
                            }
                        }
                        if (i == lines.Length - 1) // all lines are read
                        {
                            return result;
                        }
                    }
                    if (line == $"-N:{name}")
                    {
                        readField = true;
                    }
                }
                if (line == $"-L:{Language}")
                {
                    readProperties = true;
                }
            }
            return string.Empty;
        }
    }
}