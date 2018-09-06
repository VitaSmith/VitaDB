/*
 * VitaDB - Vita DataBase Updater © 2017 VitaSmith
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

using static VitaDB.Utilities;

namespace VitaDB
{
    public class Pkg
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public UInt32 ID { get; set; }
        public string URL { get; set; }
        public UInt64 SIZE { get; set; }
        public string SHA1 { get; set; }
        public string CATEGORY { get; set; }
        public UInt32? APP_VER { get; set; }
        public UInt32? SYS_VER { get; set; }
        public UInt32? C_DATE { get; set; }
        public UInt32? V_DATE { get; set; }
        public string COMMENTS { get; set; }

        [NotMapped]
        public string CONTENT_ID { get; set; }

        static readonly int PKG_HEADER_SIZE = 0xC0;
        static readonly int PKG_HEADER_EXT_SIZE = 0x40;
        static readonly byte[] pkg_magic = { 0x7f, (byte)'P', (byte)'K', (byte)'G' };
        static readonly byte[] ext_magic = { 0x7f, (byte)'e', (byte)'x', (byte)'t' };
        static readonly byte[] pkg_psp = { 0x07, 0xf2, 0xc6, 0x82, 0x90, 0xb5, 0x0d, 0x2c, 0x33, 0x81, 0x8d, 0x70, 0x9b, 0x60, 0xe6, 0x2b };
        static readonly byte[] pkg_vita2 = { 0xe3, 0x1a, 0x70, 0xc9, 0xce, 0x1d, 0xd7, 0x2b, 0xf3, 0xc0, 0x62, 0x29, 0x63, 0xf2, 0xec, 0xcb };
        static readonly byte[] pkg_vita3 = { 0x42, 0x3a, 0xca, 0x3a, 0x2b, 0xd5, 0x64, 0x9f, 0x96, 0x86, 0xab, 0xad, 0x6f, 0xd8, 0x80, 0x1f };
        static readonly byte[] pkg_vita4 = { 0xaf, 0x07, 0xfd, 0x59, 0x65, 0x25, 0x27, 0xba, 0xf1, 0x33, 0x89, 0x66, 0x8b, 0x17, 0xd9, 0xea };

        private static Dictionary<string, string> pkg_cache_dict = null;
        // Converts between content type and category, for cases where we can't get it from SFO
        private static readonly Dictionary<UInt32, string> content_type = new Dictionary<UInt32, string>
        {
            { 0x06, "ps1" },
            { 0x07, "psp" },    // Also PC Engine
            { 0x09, "th" },
            { 0x0a, "wdg" },
            { 0x0b, "lic" },
            { 0x0c, "vsh" },
            { 0x0d, "av" },
            { 0x0e, "go" },
            { 0x0f, "min" },
            { 0x10, "neo" },
            { 0x11, "vmc" },
            { 0x12, "ps2" },
            { 0x14, "psp" },
            { 0x15, "gd" },
            { 0x16, "ac" },
            { 0x17, "la" },
            { 0x18, "psm" },
            { 0x1D, "psm" },
            { 0x1f, "th" }
        };
        // The only URLs we allow are the ones from known Sony servers
        private static readonly List<string> pkg_start = new List<string>
        {
            "http://zeus.dl.playstation.net/",
            "http://ares.dl.playstation.net/",
            "http://gs.ww.np.dl.playstation.net/",
            "http://psm-runtime.np.dl.playstation.net/"
        };

        /// <summary>
        /// Create a new Pkg object from a PKG URL.
        /// </summary>
        /// <param name="url">The source URL.</param>
        /// <returns>A new Pkg instance, or null on error.</returns>
        public static Pkg CreatePkg(string url)
        {
            int i;
            for (i = 0; i < pkg_start.Count; i++)
                if (url.StartsWith(pkg_start[i]))
                    break;
            if (i >= pkg_start.Count)
            {
                Console.Error.WriteLine($"[ERROR] '{url}' does not match known PSN URLs");
                return null;
            }
            // Trim extra data after pkg (such as "?country=...")
            url = url.Split('?').First();
            if (!url.EndsWith(".pkg"))
            {
                Console.Error.WriteLine($"[ERROR] '{url}' does not end with '.pkg'");
                return null;
            }

            byte[] pkg_header = GetPkgData(url, 0, (UInt32)(PKG_HEADER_SIZE + PKG_HEADER_EXT_SIZE));
            if (pkg_header == null)
            {
                Console.Error.WriteLine($"[ERROR] Could not read PKG header from '{url}'");
                return null;
            }
            if (!MemCmp(pkg_header, pkg_magic, 0))
            {
                Console.WriteLine("[ERROR] '{url}' is not a PKG file");
                return null;
            }

            var pkg = new Pkg {
                URL = url,
                CONTENT_ID = GetContentIdFromPkg(url, pkg_header)
            };
            if (!App.ValidateContentID(pkg.CONTENT_ID))
            {
                Console.WriteLine("[ERROR] Could not get a valid CONTENT_ID from PKG URL '{url}'");
                return null;
            }

            // http://www.psdevwiki.com/ps3/PKG_files
            UInt32 info_offset = GetBe32(pkg_header, 0x08);
            UInt32 info_count = GetBe32(pkg_header, 0x0c);
            UInt32 item_count = GetBe32(pkg_header, 0x14);
            pkg.SIZE = GetBe64(pkg_header, 0x18);
            UInt64 data_offset = GetBe64(pkg_header, 0x20);
            byte[] iv = MemCpy(pkg_header, 0x70, 0x10);
            int key_type = pkg_header[0xe7] & 0x07;

            byte[] pkg_info = GetPkgData(url, info_offset, (UInt32)(data_offset - info_offset));
            if (pkg_info != null)
            {
                byte[] sfo_data = null;
                UInt32 type, size;
                string default_category = null;
                for (int count = 0, pos = 0; (count < info_count) && (pos < pkg_info.Length); count++, pos += (int)size)
                {
                    type = GetBe32(pkg_info, pos);
                    size = GetBe32(pkg_info, pos + 4);
                    pos += 8;
                    if (type == 0x02)
                    {
                        content_type.TryGetValue(GetBe32(pkg_info, pos), out default_category);
                    }
                    else if (type == 0x0E)
                    {
                        UInt32 sfo_pos = GetBe32(pkg_info, pos);
                        UInt32 sfo_size = GetBe32(pkg_info, pos + 4);
                        // Avoid a server round trip, as the SFO should be part of pkg_info
                        if ((sfo_pos >= info_offset) && (sfo_pos - info_offset + sfo_size <= pkg_info.Length))
                            sfo_data = MemCpy(pkg_info, (int)(sfo_pos - info_offset), (int)sfo_size);
                        else
                            sfo_data = GetPkgData(url, sfo_pos, sfo_size);
                        break;
                    }
                }
                if (sfo_data != null)
                {
                    var sfo = new Sfo(sfo_data);
                    pkg.APP_VER = sfo.AppVer;
                    pkg.CATEGORY = sfo.Category ?? default_category;
                    pkg.C_DATE = sfo.CDate;
                    pkg.SYS_VER = sfo.SysVer;
                }
                else
                {
                    pkg.CATEGORY = default_category;
                }
            }
            pkg.SHA1 = ByteArrayToHexString(GetPkgSha1(url));
            pkg.V_DATE = ((UInt32)DateTime.Now.Year) * 10000
                + ((UInt32)DateTime.Now.Month) * 100
                + ((UInt32)DateTime.Now.Day);
            return pkg;
        }

        /// <summary>
        /// Convert a Pkg cateory to App category.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="cat">The Pkg category string.</param>
        /// <returns>The integer category or null.</returns>
        public static int? PkgCatToAppCat(Database db, string cat)
        {
            switch (cat)
            {
                case "gd":
                    return db.Category["downloadable_game"];
                case "gdc":
                    return db.Category["application"];
                case "ac":
                    return db.Category["add_on"];
                case "th":
                    return db.Category["theme"];
                case "psm":
                    return db.Category["psm"];
                default:
                    return null;
            }
        }

        // Convenience method
        public override string ToString()
        {
            string str = "(Pkg Object)\n";
            foreach (var attr in typeof(Pkg).GetProperties())
            {
                if (attr.PropertyType == typeof(byte[]))
                    str += $"* {attr.Name} =\n"
                        + HexDump((byte[])this.GetType().GetProperty(attr.Name).GetValue(this));
                else
                    str += ($"* {attr.Name} = '{this.GetType().GetProperty(attr.Name).GetValue(this)}'\n");
            }
            return str;
        }

        /// <summary>
        /// Create a new instance of a Pkg from update data.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="update">The Update data to create the Pkg from.</param>
        /// <param name="recheck">(Optional) Force the Pkg to be recreated by processing the remote data.</param>
        /// <returns>A new Pkg instance.</returns>
        public static Pkg GetPkgFromUpdate(Database db, Update update, bool recheck = false)
        {
            var pkg = db.Pkgs.Where(x => x.URL == update.URL).FirstOrDefault();
            if (pkg == null)
            {
                pkg = CreatePkg(update.URL);
                if (pkg == null)
                    return null;
                db.Add(pkg);
                // Needed so that ID becomes available for reference
                db.SaveChanges();
            }
            else if (recheck)
            {
                db.Attach(pkg);
                pkg = CreatePkg(update.URL);
                if (pkg == null)
                    return null;
            }
            // Now compare the update data with our package data
            if (db.Apps.Where(x => x.PKG_ID == pkg.ID).Select(x => x.CONTENT_ID).FirstOrDefault() != update.CONTENT_ID)
                Console.Error.WriteLine($"[WARNING] CONTENT_ID of pkg {pkg.ID} is different from update CONTENT_ID!");
            if (!MemCmp(HexStringToByteArray(pkg.SHA1), update.Sha1Sum))
                Console.Error.WriteLine($"[WARNING] SHA1 of pkg {pkg.ID} is different from update SHA1!");
            if (pkg.SIZE != update.Size)
                Console.Error.WriteLine($"[WARNING] Size of pkg {pkg.ID} is different from update size!");
             return pkg;
        }

        /// <summary>
        /// Flushes the PKG cache dictionary to file.
        /// </summary>
        public static void FlushPkgCache()
        {
            if (pkg_cache_dict != null)
                SerializePkgCache(Settings.Instance.local_cache);
        }

        /// <summary>
        /// Serializes the PKG URL -> CONTENT_ID Dictionary to file.
        /// </summary>
        /// <param name="file_path">The path of the JSON file to save to.</param>
        private static void SerializePkgCache(string file_path)
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Formatting = Formatting.Indented;
            using (StreamWriter sw = new StreamWriter(file_path))
            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                Debug.WriteLine($"Saving PKG cache to '{file_path}'...");
                // Might as well sort our saved output...
                serializer.Serialize(writer, new SortedDictionary<string, string>(pkg_cache_dict));
            }
        }

        /// <summary>
        /// Deserializes the PKG URL -> CONTENT_ID Dictionary to file.
        /// </summary>
        /// <param name="file_path">The path of the JSON file to read the cache from.</param>
        /// <returns>The PKG URL cache Dictionary or null on error.</returns>
        private static Dictionary<string, string> DeserializePkgCache(string file_path)
        {
            if (!File.Exists(file_path))
                return null;
            JsonSerializer serializer = new JsonSerializer();
            try
            {
                using (StreamReader sr = new StreamReader(file_path))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    Debug.WriteLine($"Reading PKG cache from '{file_path}'...");
                    return serializer.Deserialize<Dictionary<string, string>>(reader);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[WARNING] Could not restore PKG cache from '{file_path}': {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Fetch data from a PKG.
        /// </summary>
        /// <param name="url">The PKG url.</param>
        /// <param name="start">(Optional) The start position to read from. Default is 0.</param>
        /// <param name="size">(Optional) The number of bytes to read. Default is 0x60.</param>
        /// <returns>A byte array with the content the header, or null on error.</returns>
        private static byte[] GetPkgData(string url, UInt64 start = 0, UInt32 size = 0x60)
        {
            byte[] result = null;
            byte[] buf = new byte[65536];
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            HttpWebRequest request;
            request = WebRequest.Create(url) as HttpWebRequest;
            request.AddRange((long)start, (long)start + size - 1);
            request.ReadWriteTimeout = 30000;

            try
            {
                using (WebResponse resp = request.GetResponse())
                using (Stream resp_stream = resp.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    result = new byte[size];
                    int read;
                    while ((read = resp_stream.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, read);
                    }
                    result = ms.ToArray();
                }
                if (result.Length < size)
                {
                    Console.Error.WriteLine($"[ERROR] Only {result.Length} bytes of PKG header were read");
                    return null;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[ERROR] {e.Message} - {url}");
                return null;
            }
            return result;
        }

        /// <summary>
        /// Fetch the PKG SHA-1.
        /// </summary>
        /// <param name="url">The PKG url.</param>
        /// <returns>The 20-byte SHA-1 value from the PKG, or null on error.</returns>
        private static byte[] GetPkgSha1(string url)
        {
            byte[] data = GetPkgData(url, 0x18, 8);
            if (data == null)
                return null;
            UInt64 length = GetBe64(data);
            return GetPkgData(url, length - 0x20, 0x14);
        }

        /// <summary>
        /// Fetch the CONTENT_ID from a PKG (with caching)
        /// </summary>
        /// <param name="url">The PKG url.</param>
        /// <param name="header">(Optional) The header data if already cached locally.</param>
        /// <returns>A 36 character string containing the CONTENT_ID, or null on error.</returns>
        public static string GetContentIdFromPkg(string url, byte[] header = null)
        {
            if (String.IsNullOrEmpty(url))
                return null;
            // Trim unneeded query content
            url = url.Split('?').First();

            // Try to get the value from our cache where possible
            if (pkg_cache_dict == null)
                pkg_cache_dict = DeserializePkgCache(Settings.Instance.local_cache);
            if (pkg_cache_dict == null)
                pkg_cache_dict = new Dictionary<string, string>();
            if (pkg_cache_dict.TryGetValue(url, out string value))
                return value;

            byte[] content_id = null;
            if (header == null)
                content_id = GetPkgData(url, 0x30, 0x24);
            else
                content_id = MemCpy(header, 0x30, 0x24);
            if (content_id == null)
                return null;
            try
            {
                value = System.Text.Encoding.Default.GetString(content_id);
                pkg_cache_dict[url] = value;
                return value;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("[WARNING] Could not convert PKG header to CONTENT_ID: " + e);
                return null;
            }
        }
    }
}
