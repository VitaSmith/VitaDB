/*
 * VitaDB - Vita DataBase Updater © 2017 VitaSmith
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using CsvHelper;
using CsvHelper.Configuration;
using HtmlAgilityPack;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DrawingCore;
using System.DrawingCore.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using static VitaDB.Utilities;

namespace VitaDB
{
    class VitaDB
    {
        private static string version = "v"
            + Assembly.GetEntryAssembly().GetName().Version.Major.ToString() + "."
            + Assembly.GetEntryAssembly().GetName().Version.Minor.ToString();
        private static bool cancel_requested = false;
        // Technically zRIFs can exist that don't start with "KO5i" (if the zlib window size is custom)
        // but for convenience, we assume that they all do.
        private static readonly string zrif_start = "KO5i";
        private static readonly string psn_store_start = "https://store.playstation.com/";
        private static int verbosity = 0;
        private static bool wait_for_key = false;
        private static bool purge_pkgcache = false;
        public sealed class AppCsvClassMap : ClassMap<App>
        {
            public AppCsvClassMap()
            {
            }
        }


        /// <summary>
        /// Convenience calls to add entries from a Chihiro link list.
        /// </summary>
        /// <param name="links">A List of Chihiro links.</param>
        /// <param name="lang">(Optiona) The language to use when querying Chihiro.</param>
        /// <param name="reparse">(Optional) Force a reparse of existing DB entries.</param>
        static void AddLinks(List<Chihiro.Link> links, string lang = null, bool reparse = false)
        {
            using (var db = new Database())
            {
                foreach (var link in links)
                {
                    if (cancel_requested)
                        break;
                    var app = db.Apps.Find(link.id);
                    if (app == null)
                    {
                        Console.WriteLine($"{link.id}");
                        app = new App
                        {
                            TITLE_ID = link.id.Substring(7, 9),
                            CONTENT_ID = link.id,
                        };
                        app.UpdateFromChihiro(db, lang);
                    }
                    else if (reparse)
                        app.UpdateFromChihiro(db, lang);
                }
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Return the content root for a specific region.
        /// </summary>
        /// <param name="region">A region name.</param>
        /// <returns>The CONTENT_ID for the root search.</returns>
        static string GetRootContentId(string region)
        {
            string[] root_names =
            {
                "STORE-MSF77008-PSVITAALLGAMES",
                "STORE-MSF75508-PLATFORMPSVITA",
                "STORE-MSF86012-PSVITAGAMES",
                "PN.CH.CN-PN.CH.MIXED.CN-PSVITAGAMES",
                "PN.CH.JP-PN.CH.MIXED.JP-PSVAPPLICATION",
            };
            List<string>[] root = new List<string>[root_names.Length];
            root[0] = new List<string>() { "us", "ca", "mx", "br", "ar" };
            root[1] = new List<string>() { "ie", "gb", "cz", "dk", "fi",
                "no", "se", "gr", "hu", "ro", "sk", "si", "tr", "fr", "de",
                "it", "es", "nl", "pt", "pl", "il", "at", "au", "ua", "ru" };
            root[2] = new List<string>() { "sg", "hk" };
            root[3] = new List<string>() { "cn" };
            root[4] = new List<string>() { "jp" };

            var country = region.Substring(3, 2);
            for (int i = 0; i < root_names.Length; i++)
            {
                if (root[i].Contains(country))
                    return root_names[i];
            }
            Console.Error.WriteLine($"[ERROR] No root defined for '{region}'");
            return null;
        }


        /// <summary>
        /// Gets all Vita titles by performing a search for all regions against Chihiro.
        /// </summary>
        /// <param name="reparse">(Optional) Force a reparse of existing DB entries.</param>
        static void RefreshDBFromChihiro(bool reparse = false)
        {
            string[] regions =
            {
                "en-us", "en-ca", "es-mx", "pt-br", "es-ar",
                "en-gb", "en-ie", "en-pl", "en-se", "en-no", "en-fi", "en-dk",
                "en-cz", "en-gr", "en-hu", "en-ro", "en-sk", "en-si", "en-tr",
                "en-il", "en-au", "fr-fr", "it-it", "es-es", "de-de", "nl-nl",
                "pt-pt", "de-at", "ru-ru", "en-sg", "en-hk", "zh-hk", "ja-jp"
            };

            foreach (var region in regions)
            {
                if (cancel_requested)
                    return;
                var data = Chihiro.GetData(GetRootContentId(region), region, true);
                if ((data == null) || (Nullable(data.links).Count() == 0))
                {
                    Console.Error.WriteLine($"[WARNING] No titles found for region '{region}'");
                    continue;
                }
                Console.WriteLine($"[Checking {data.links.Count()} titles in '{region}']");
                AddLinks(data.links, region, reparse);
            }

            // Japan is weird...
            string[] groups = { "A", "KA", "SA", "TA", "NA", "HA", "MA", "YA", "RA", "WA" };
            foreach (var group in groups)
            {
                if (cancel_requested)
                    return;
                var data = Chihiro.GetData("PN.CH.JP-PN.CH.MIXED.JP-FROM" + group, "ja-jp", true);
                if ((data == null) || (Nullable(data.links).Count() == 0))
                {
                    Console.Error.WriteLine($"[WARNING] No titles found for JP/{group}");
                    continue;
                }
                Console.WriteLine($"[Checking {data.links.Count()} titles for JP/{group}]");
                AddLinks(data.links, "ja-jp", reparse);
            }
        }

        /// <summary>
        /// Internal check for titles that exist in one region but not another.
        /// </summary>
        /// <param name="regions">An array of region, the first element of the array being the prime one.</param>
        static void CheckRegion(string[] regions)
        {
            Console.WriteLine($"Retrieving content from '{regions[0]}'...");
            var main_data = Chihiro.GetData(GetRootContentId(regions[0]), regions[0], true);
            var main_content_ids = main_data.links.Where(x => x.id[7] == 'P').Select(x => x.id);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            for (int i = 1; i < regions.Length; i++)
            {
                Console.WriteLine($"Checking for content that exists in '{regions[i]}' and not in previous region(s)...");
                var data = Chihiro.GetData(GetRootContentId(regions[i]), regions[i], true);
                var content_ids = data.links.Where(x => x.id[7] == 'P').Select(x => x.id);
                var result = content_ids.Where(x => !main_content_ids.Any(y => y == x));
                foreach (var content_id in result)
                {
                    if (cancel_requested)
                        return;
                    if (!dict.Keys.Contains(content_id))
                    {
                        dict[content_id] = regions[i];
                        Console.WriteLine($"* {content_id}");
                    }
                }
            }
            using (var db = new Database())
            {
                Console.WriteLine("Updating DB...");
                foreach (var content_id in dict.Keys)
                {
                    if (cancel_requested)
                        break;
                    var app = db.Apps.Find(content_id);
                    if (app == null)
                    {
                        Console.Error.WriteLine($"[WARNING] Ignoring non existing app '{content_id}'");
                        continue;
                    }
                    app.UpdateFromChihiro(db, dict[content_id], true);
                }
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Update the Database for Apps and DLC by querying Bing and PSN.
        /// Uses the settings from the .ini file to search content for TITLE_ID's.
        /// </summary>
        static void RefreshDBFromPSN(bool search = false)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = null;

            using (WebClient wc = new WebClient())
            using (var db = new Database())
            {
                foreach (string region in Settings.Instance.regions_to_check)
                {
                    string lang = Settings.Instance.GetLanguage(region);
                    for (int i = Settings.Instance.range[0]; i <= Settings.Instance.range[1]; i++)
                    {
                        if (cancel_requested)
                            break;
                        string title_id = region + i.ToString("D5");
                        string content_id = "";
                        Console.Write(title_id + ": ");
                        Console.SetCursorPosition(0, Console.CursorTop);
                        List<App> apps = new List<App>();

                        if (search)
                        {
                            // Use Bing as it seems to have better indexing of PSN titles than google.
                            // Also try search from page 2 if page 1 failed
                            for (int sp = 1; (sp <= 2) && (content_id == ""); sp++)
                            {
                                doc = web.Load("http://www.bing.com/search?q=%22" + lang + "%22+" +
                                    title_id + "+site%3Astore.playstation.com&first=" + sp);
                                foreach (HtmlNode link in doc.DocumentNode.SelectNodes("//a[@href]"))
                                {
                                    string href_value = WebUtility.UrlDecode(link.GetAttributeValue("href", ""));
                                    if (href_value.StartsWith(psn_store_start) && href_value.Contains(title_id))
                                    {
                                        content_id = href_value.Split('/')
                                            .Single(x => x.Contains(title_id))
                                            .Split(':')
                                            .First()
                                            .Replace("cid=", "");
                                        if (!App.ValidateContentID(content_id))
                                        {
                                            Console.Error.WriteLine($"[ERROR] {content_id} does not match expected format (href='{href_value}')");
                                            content_id = "";
                                        }
                                        break;
                                    }
                                }
                            }
                            if (content_id == "")
                                continue;
                            var app = new App
                            {
                                TITLE_ID = title_id,
                                CONTENT_ID = content_id,
                            };
                            apps.Add(app);
                        }
                        else
                        {
                            apps = db.Apps.Where(x => x.TITLE_ID == title_id && x.CATEGORY < 100).ToList();
                        }

                        // Query Chihiro to fill our content
                        foreach (var app in apps)
                            app.UpdateFromChihiro(db, app.COMMENTS ?? lang);

                        // Also run a check for the TITLE_ID against the patch servers
                        if ((apps.Count() > 0) || search)
                            Update.Check(db, title_id);
                    }
                }
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Update the Database from a local or remote CSV spreadsheet.
        /// The CSV must have a header that matches the mapping from the .ini file.
        /// </summary>
        /// <param name="uri">The URI of the CSV data to read.</param>
        static void ImportCSV(string uri, int type = 0)
        {
            string tmp_file = null;
            if (String.IsNullOrEmpty(uri))
                return;
            if (type < 0 || type >= Settings.nps_type.Length)
                return;
            Console.WriteLine($"Importing {Settings.nps_type[type]} CSV data from '{uri}':");
            if (uri.StartsWith("http:") || uri.StartsWith("https:"))
            {
                tmp_file = Path.GetTempFileName();
                try
                {
                    using (var client = new WebClient())
                        client.DownloadFile(uri, tmp_file);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[ERROR] {e.Message}");
                    return;
                }
                uri = tmp_file;
            }
            if (!File.Exists(uri))
            {
                Console.Error.WriteLine($"Could not open {uri}");
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            using (var reader = File.OpenText(uri))
            using (var db = new Database())
            {
                int total_nr = File.ReadLines(uri).Count();
                string format = "D" + (int)(Math.Log10((double)total_nr) + 0.99999);

                var csv = new CsvReader(reader);
                // Create the CSV mapping
                var csv_map = new AppCsvClassMap();
                // https://github.com/JoshClose/CsvHelper/issues/719#issuecomment-342837614
                foreach (string key in Settings.Instance.csv_mapping.Keys)
                {
                    var property = typeof(App).GetProperty(key);
                    var mapping = MemberMap.CreateGeneric(typeof(App), property);
                    mapping.Data.Names.Add(Settings.Instance.csv_mapping[key]);
                    csv_map.MemberMaps.Add(mapping);
                }
                csv.Configuration.AllowComments = true;
                csv.Configuration.IgnoreBlankLines = true;
                csv.Configuration.TrimOptions = TrimOptions.Trim;
                csv.Configuration.HeaderValidated = null;
                csv.Configuration.MissingFieldFound = null;
                csv.Configuration.IgnoreQuotes = true;
                csv.Configuration.RegisterClassMap(csv_map);
                csv.Configuration.Delimiter = Settings.Instance.csv_separator;

                while (csv.Read())
                {
                    if (cancel_requested)
                        break;
                    string source = "???";
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write($"[{csv.Parser.Context.Row.ToString(format)}/{total_nr}] ");
                    var app = csv.GetRecord<App>();
                    if ((app == null) || (app.TITLE_ID == "CAU"))
                        continue;

                    // Remove bad data
                    if ((app.PKG_URL != null) && !app.PKG_URL.StartsWith("http"))
                        app.PKG_URL = null;
                    if (!String.IsNullOrEmpty(app.ZRIF))
                    {
                        if (app.ZRIF.ToLower().Contains("not required"))
                            app.SetFlag(db, "FREE_APP");
                        if (!app.ZRIF.StartsWith(zrif_start))
                            app.ZRIF = null;
                    }
                    // Remove extra data
                    if (app.PKG_URL != null)
                        app.PKG_URL = app.PKG_URL.Split('?').First();

                    // Try to direct read the CONTENT_ID
                    if (!String.IsNullOrEmpty(app.CONTENT_ID))
                    {
                        if (!App.ValidateContentID(app.CONTENT_ID))
                        {
                            Console.Error.WriteLine($"[WARNING] CONTENT_ID ({app.CONTENT_ID}) is invalid");
                            app.CONTENT_ID = null;
                        }
                        else
                        {
                            source = "CSV";
                        }
                    }

                    // Try to read the CONTENT_ID from zRIF
                    if (!String.IsNullOrEmpty(app.ZRIF))
                    {
                        string content_id = RIF.GetContentId(app.ZRIF);
                        if (!App.ValidateContentID(content_id))
                            Console.Error.WriteLine($"[WARNING] CONTENT_ID from {app.ZRIF} is invalid");
                        else if (!String.IsNullOrEmpty(app.CONTENT_ID) && (app.CONTENT_ID != content_id))
                            Console.Error.WriteLine($"[WARNING] " +
                                $"CONTENT_ID mismatch between zRIF ({content_id}) and {source} one ({app.CONTENT_ID})");
                        else
                        {
                            app.CONTENT_ID = content_id;
                            source = "zRIF";
                        }
                    }

                    // Try to read the CONTENT_ID from URL
                    if (!String.IsNullOrEmpty(app.PKG_URL))
                    {
                        string content_id = Pkg.GetContentIdFromPkg(app.PKG_URL);
                        if (!App.ValidateContentID(content_id))
                        {
                            Console.Error.WriteLine($"[ERROR] CONTENT_ID from {app.PKG_URL} is invalid");
                            continue;
                        }
                        else if (!String.IsNullOrEmpty(app.CONTENT_ID) && (app.CONTENT_ID != content_id))
                        {
                            Console.Error.WriteLine($"[WARNING] " +
                                $"CONTENT_ID mismatch between PKG url ({content_id}) and {source} one ({app.CONTENT_ID})");
                            continue;
                        }
                        else
                        {
                            app.CONTENT_ID = content_id;
                            // Anything we get from the PSN servers is final
                            app.SetReadOnly(db, nameof(App.PKG_ID));
                            source = "PKG url";
                        }
                    }

                    // If we don't have a TITLE_ID, try to create one from CONTENT_ID
                    if (String.IsNullOrEmpty(app.TITLE_ID) && !String.IsNullOrEmpty(app.CONTENT_ID))
                        app.TITLE_ID = app.CONTENT_ID.Substring(7, 9);

                    // Fix title
                    app.TITLE_ID = app.TITLE_ID.Replace("(3.61+!)", "").Trim();
                    app.TITLE_ID = Regex.Replace(app.TITLE_ID, @"\s+", " ");

                    // Set the category
                    if (app.TITLE_ID.ToLower().Contains("dlc"))
                        app.CATEGORY = 101;
                    else if (app.TITLE_ID.ToLower().Contains("theme"))
                        app.CATEGORY = 201;
                    app.CATEGORY = app.CATEGORY ?? Settings.nps_category[type];

                    // Now we can validate TITLE_ID
                    if (app.TITLE_ID.Length > 9)
                        app.TITLE_ID = app.TITLE_ID.Substring(0, 9);
                    if (!App.ValidateTitleID(app.TITLE_ID))
                    {
                        Console.Error.WriteLine($"[WARNING] TITLE_ID ({app.TITLE_ID}) is invalid");
                        continue;
                    }

                    if (String.IsNullOrEmpty(app.CONTENT_ID))
                    {
                        if (app.CATEGORY < 100)
                        {
                            // Select most likely or create dummy
                            var existing_app = db.Apps
                                .Where(x => (x.TITLE_ID == app.TITLE_ID) && ((x.CATEGORY ?? 0) < 100))
                                .FirstOrDefault();
                            app.CONTENT_ID = ((existing_app != null)) ?
                                existing_app.CONTENT_ID :
                                "??????-" + app.TITLE_ID + "_??-????????????????";
                        }
                        else
                        {
                            // Don't bother with Add-ons if we didn't get a CONTENT_ID
                            continue;
                        }
                    }

                    // Since we have CONTENT_ID, try to locate the existing entry
                    var db_app = db.Apps.Find(app.CONTENT_ID);
                    if (db_app != null)
                        app.PARENT_ID = db_app.PARENT_ID;

                    if (!app.CONTENT_ID.Contains(app.TITLE_ID))
                    {
                        if ((app.CATEGORY < 100) || (app.PARENT_ID == null) || (!app.PARENT_ID.Contains(app.TITLE_ID)))
                        {
                            Console.Error.WriteLine($"[WARNING] TITLE_ID ({app.TITLE_ID}) and CONTENT_ID ({app.CONTENT_ID}) do not match");
                            continue;
                        }
                        app.TITLE_ID = app.CONTENT_ID.Substring(7, 9);
                    }

                    if (app.CATEGORY >= 100)
                    {
                        var likely_parent = db.Apps
                            .Where(x => (x.TITLE_ID == app.TITLE_ID) && ((x.CATEGORY ?? 0) < 100))
                            .FirstOrDefault();
                        if ((likely_parent != null) && String.IsNullOrEmpty(app.PARENT_ID))
                            app.PARENT_ID = likely_parent.CONTENT_ID;
                    }

                    // Insert or update record
                    if (verbosity > 0)
                        Console.WriteLine($"{app.TITLE_ID}:" +
                            $" {app.CONTENT_ID} {((db_app == null) ? "(I)" : "(U)")}");
                    if (app.PKG_URL != null)
                    {
                        var pkg = db.Pkgs.Where(x => x.URL == app.PKG_URL).FirstOrDefault();
                        if (pkg == null)
                        {
                            pkg = Pkg.CreatePkg(app.PKG_URL);
                            if (pkg == null)
                                continue;
                            db.Pkgs.Add(pkg);
                            // Must save the changes to get our ID
                            db.SaveChanges();
                        }
                        app.PKG_ID = pkg.ID;
                    }
                    app.Upsert(db);
                }
                db.SaveChanges();
            }
            watch.Stop();
            Console.WriteLine($"{(cancel_requested ? "CANCELLED after": "DONE in")}" +
                $" {watch.ElapsedMilliseconds / 1000.0}s.");
            if (tmp_file != null)
                File.Delete(tmp_file);
        }

        /// <summary>
        /// Save the database to a CSV spreadsheet.
        /// </summary>
        /// <param name="file_path">The path of the CSV file to write.</param>
        /// <param name="type">(Optional) The type of content to export.</param>
        static void ExportCSV(string file_path, int type = 0)
        {
            if (type < 0 || type >= Settings.nps_type.Length)
                return;
            Console.Write($"Dumping all {Settings.nps_type[type]} entries to '{file_path}'... ");
            using (var writer = File.CreateText(file_path)) // UTF-8 by default
            using (var db = new Database())
            {
                var csv = new CsvWriter(writer);
                writer.WriteLine("TITLE_ID,REGION,NAME,PKG_URL,CONTENT_ID");
                foreach (var app in db.Apps.Where(
                    x => (x.CATEGORY >= Settings.nps_category[type]) && (x.CATEGORY < Settings.nps_category[type] + 99))
                )
                {
                    if (cancel_requested)
                        break;
                    //bool free_app = ((app.FLAGS & db.Flag["FREE_APP"]) != 0);
                    csv.WriteField(app.TITLE_ID);
                    csv.WriteField(Settings.Instance.GetRegionName(app.CONTENT_ID));
                    csv.WriteField(app.NAME);
                    csv.WriteField((app.PKG_ID == null) ? "" : db.Pkgs.Find(app.PKG_ID).URL);
                    csv.WriteField(app.CONTENT_ID);
//                    csv.WriteField(free_app ? "NOT REQUIRED" : app.ZRIF);
                    csv.NextRecord();
                }
            }
            Console.WriteLine("DONE");
        }

        /// <summary>
        /// Import a bunch of zRIFs from a text file.
        /// The file should contain one zRIF per line.
        /// </summary>
        /// <param name="file_path">The file containing the zRIFs.</param>
        static void ImportZRif(string file_path)
        {
            if (!File.Exists(file_path))
            {
                Console.Error.WriteLine($"Could not open {file_path}");
                return;
            }

            Console.WriteLine($"Importing zRIFs from '{file_path}':");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using (var db = new Database())
            {
                var lines = File.ReadLines(file_path);
                string format = "D" + (int)(Math.Log10((double)lines.Count()) + 0.99999);
                int line_nr = 0;
                foreach (var line in lines)
                {
                    if (cancel_requested)
                        break;
                    ++line_nr;
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write($"[{line_nr.ToString(format)}/{lines.Count()}] ");
                    var zrif = line.Trim();
                    var content_id = RIF.GetContentId(zrif);
                    if (!App.ValidateContentID(content_id))
                    {
                        Console.Error.WriteLine($"[WARNING] Line {line_nr}: Decoded '{content_id}' is not a valid CONTENT_ID");
                        continue;
                    }
                    var app = db.Apps.Find(content_id);
                    if (app == null)
                    {
                        app = new App
                        {
                            TITLE_ID = content_id.Substring(7, 9),
                            CONTENT_ID = content_id,
                            ZRIF = zrif
                        };
                    }
                    else
                    {
                        app.ZRIF = zrif;
                    }
                    app.Upsert(db);
                }
                db.SaveChanges();
            }
            watch.Stop();
            Console.WriteLine($"{(cancel_requested ? "CANCELLED after" : "DONE in")}" +
                $" {watch.ElapsedMilliseconds / 1000.0}s.");
        }

        /// <summary>
        /// Export all zRIFs to a text file.
        /// </summary>
        /// <param name="file_path">The name of the file to create.</param>
        static void ExportZRif(string file_path)
        {
            Console.Write($"Exporting zRIFs to '{file_path}'... ");
            using (var db = new Database())
            using (var writer = File.CreateText(file_path))
            {
                foreach (var app in db.Apps.Where(x => x.ZRIF != null))
                {
                    // Only export actual zRIFs
                    if (app.ZRIF.StartsWith(zrif_start))
                        writer.WriteLine(app.ZRIF);
                }
            }
            Console.WriteLine("DONE");
        }

        /// <summary>
        /// Perform maintenance on bundles.
        /// </summary>
        static void BundleMaintenance()
        {
            using (var db = new Database())
            {
                foreach (var app in db.Apps.Where(x => (x.PARENT_ID != null) && (x.PARENT_ID.Contains("-CUSA"))))
                {
                    if (cancel_requested)
                        break;

                    string old_content_id = null;
                    Console.WriteLine($"{app.CONTENT_ID}: {app.PARENT_ID}");
                    // TODO: Make this work with multiple PARENT_IDs
                    var data = Chihiro.GetData(app.PARENT_ID);
                    if ((data == null) || (data.default_sku == null) || (data.default_sku.entitlements == null))
                    {
                        Console.WriteLine("* NO DICE");
                        continue;
                    }

                    string content_id = null;
                    foreach (var ent in data.default_sku.entitlements)
                    {
                        if (ent.id.Contains(app.TITLE_ID) && App.ValidateContentID(ent.id))
                        {
                            content_id = ent.id;
                            break;
                        }
                    }
                    if (content_id == null)
                        continue;
                    if (app.CONTENT_ID != content_id)
                    {
                        Console.WriteLine($"* {app.CONTENT_ID} -> {content_id}");
                        old_content_id = app.CONTENT_ID;
                        app.CONTENT_ID = content_id;
                    }
                    app.NAME = data.name;
                    app.CATEGORY = db.Category[data.top_category];
                    app.SetReadOnly(db, nameof(App.NAME), nameof(App.CATEGORY));
                    app.Upsert(db);
                    if (old_content_id != null)
                    {
                        db.Remove(db.Apps.Find(old_content_id));
                        db.SaveChanges();
                    }
                }
            }
        }

        /// <summary>
        /// Update DB from PKG or PSN Store URL(s).
        /// </summary>
        /// <param name="url">The PKG URL.</param>
        static void UpdateFromUrl(string uri)
        {
            string[] urls = null;
            if ((!uri.StartsWith("http:")) && (!uri.StartsWith("https:")))
            {
                // Try to read from local file
                if (!File.Exists(uri))
                {
                    Console.Error.WriteLine($"Could not open {uri}");
                    return;
                }
                urls = File.ReadAllLines(uri);
            }
            else
            {
                urls = new string[] { uri };
            }
            using (var db = new Database())
            {
                foreach (var _url in urls)
                {
                    string lang = null;
                    var url = _url.Trim();
                    if ((url == "") || (url.StartsWith("#")) || (url.StartsWith(";")))
                        continue;
                    App app = null;
                    if (url.StartsWith(psn_store_start))
                    {
                        if (!url.Contains("/cid="))
                        {
                            Console.Error.WriteLine($"{url} is not a valid PSN store URL");
                            continue;
                        }
                        lang = url.Substring(url.IndexOf("/#!/") + 4, 5);
                        if (lang[2] != '-')
                            lang = null;
                        string content_id = url.Substring(url.IndexOf("/cid=") + 5, 36);
                        if (!App.ValidateContentID(content_id))
                        {
                            Console.Error.WriteLine($"[ERROR] {content_id} from {url} does not match expected format");
                            return;
                        }
                        app = new App
                        {
                            TITLE_ID = content_id.Substring(7, 9),
                            CONTENT_ID = content_id,
                        };
                    }
                    else
                    {
                        if (db.Pkgs.Any(x => x.URL == url))
                        {
                            Console.WriteLine($"PKG URL '{url}' already exists");
                            continue;
                        }
                        var pkg = Pkg.CreatePkg(url);
                        if (pkg == null)
                            continue;
                        Console.WriteLine($"Adding new Pkg for '{pkg.CONTENT_ID}'");
                        db.Pkgs.Add(pkg);
                        db.SaveChanges();
                        app = db.Apps.Find(pkg.CONTENT_ID);
                        if (app == null)
                        {
                            // No existing app => Create one
                            app = new App
                            {
                                CONTENT_ID = pkg.CONTENT_ID,
                                TITLE_ID = pkg.CONTENT_ID.Substring(7, 9),
                                PKG_ID = pkg.ID,
                                CATEGORY = Pkg.PkgCatToAppCat(db, pkg.CATEGORY)
                            };
                            Console.WriteLine($"Adding new App for '{pkg.CONTENT_ID}'");
                            db.Apps.Add(app);
                        }
                        else
                        {
                            // Update PKG reference
                            app.PKG_ID = pkg.ID;
                            app.Upsert(db);
                        }
                    }
                    if (app != null)
                    {
                        app.UpdateFromChihiro(db, lang);
                        db.SaveChanges();
                        Update.Check(db, app.TITLE_ID);
                        db.SaveChanges();
                    }
                }
            }
        }

        /// <summary>
        /// Check the database for inconsitencies.
        /// </summary>
        static void Maintenance()
        {
            using (var db = new Database())
            {
                Console.Write("Checking for TITLE_IDs that don't match their CONTENT_IDs... ");
                Dictionary<string, string> list = new Dictionary<string, string>();
                foreach (var app in db.Apps)
                {
                    if (!app.CONTENT_ID.Contains(app.TITLE_ID))
                        list.Add(app.CONTENT_ID, app.TITLE_ID);
                }
                if (list.Count() == 0)
                {
                    Console.WriteLine("[PASS]");
                }
                else
                {
                    Console.WriteLine("[FAIL]");
                    foreach (var entry in list)
                    {
                        Console.WriteLine($"* {entry.Key} -> {entry.Value} [FIXED]");
                        var app = db.Apps.Find(entry.Key);
                        app.TITLE_ID = app.CONTENT_ID.Substring(7, 9);
                        db.SaveChanges();
                    }
                }
            }

            using (var db = new Database())
            {
                Console.Write("Checking for CONTENT_IDs that can be resolved... ");
                Dictionary<string, string> list = new Dictionary<string, string>();
                var groups = db.Apps.GroupBy(x => x.TITLE_ID);
                foreach (var group in groups)
                {
                    if (cancel_requested)
                        return;
                    var unknown_app = group
                        .Where(x => x.CONTENT_ID.StartsWith("???"))
                        .FirstOrDefault();
                    var app = group
                        .Where(x => (!x.CONTENT_ID.StartsWith("???")) && ((x.CATEGORY ?? 0) < 100))
                        .FirstOrDefault();
                    if (app != null && unknown_app != null && !app.CONTENT_ID.StartsWith("???"))
                        list.Add(unknown_app.CONTENT_ID, app.CONTENT_ID);
                }
                if (list.Count() == 0)
                {
                    Console.WriteLine("[PASS]");
                }
                else
                {
                    Console.WriteLine("[FAIL]");
                    foreach (var entry in list)
                    {
                        Console.WriteLine($"* {entry.Key} -> {entry.Value} [FIXED]");
                        var app = db.Apps.Find(entry.Key);
                        db.Remove(app);
                        db.SaveChanges();
                    }
                }
            }

            using (var db = new Database())
            {
                Console.Write("Checking for NAME/ALT_NAME that should be trimmed or that contain LF's... ");
                bool pass = true;
                foreach (var app in db.Apps)
                {
                    List<string> list = new List<string>();
                    if (app.NAME.Contains((char)0x0A))
                    {
                        app.NAME = app.NAME.Replace((char)0x0A, ' ');
                        list.Add($"NAME = '{app.NAME}'");
                    }
                    if (!String.IsNullOrEmpty(app.ALT_NAME) && app.ALT_NAME.Contains((char)0x0A))
                    {
                        app.ALT_NAME = app.ALT_NAME.Replace((char)0x0A, ' ');
                        list.Add($"ALT_NAME = '{app.ALT_NAME}'");
                    }
                    if (app.NAME.Trim() != app.NAME)
                    {
                        db.Apps.Find(app.CONTENT_ID);
                        list.Add($"NAME = '{app.NAME}'");
                        app.NAME = app.NAME.Trim();
                    }
                    if (!String.IsNullOrEmpty(app.ALT_NAME) && (app.ALT_NAME.Trim() != app.ALT_NAME))
                    {
                        db.Apps.Find(app.CONTENT_ID);
                        list.Add($"ALT_NAME = '{app.ALT_NAME}'");
                        app.ALT_NAME = app.ALT_NAME.Trim();
                    }
                    if (list.Count != 0)
                    {
                        if (pass)
                            Console.WriteLine("[FAIL]");
                        Console.Write($"{app.CONTENT_ID}: { String.Join(",", list)}...");
                        db.SaveChanges();
                        Console.WriteLine(" [FIXED]");
                        pass = false;
                    }
                }
                if (pass)
                    Console.WriteLine("[PASS]");
            }

            //using (var db = new Database())
            //{
            //    Console.Write("Checking for RO attributes with empty values... ");
            //    bool pass = true;
            //    int i = 0;
            //    foreach (var app in db.Apps.Where(x => x.FLAGS != 0))
            //    {
            //        foreach (var attr in typeof(App).GetProperties())
            //        {
            //            List<string> list = new List<string>();
            //            UInt16 val;
            //            if (db.Flag.TryGetValue(attr.Name + "_RO", out val))
            //            {
            //                if ((app.GetType().GetProperty(attr.Name).GetValue(app, null) == null) &&
            //                        ((app.FLAGS & val) != 0))
            //                {
            //                    list.Add(attr.Name);
            //                    db.Apps.Find(app.CONTENT_ID);
            //                    app.FLAGS &= (UInt16)~db.Flag[attr.Name + "_RO"];
            //                }
            //            }
            //            if (list.Count != 0)
            //            {
            //                if (pass)
            //                    Console.WriteLine("[FAIL]");
            //                Console.Write($"{app.CONTENT_ID}: { String.Join(",", list)}...");
            //                Console.WriteLine(" [FIXED]");
            //                pass = false;
            //            }
            //        }
            //        if (++i % 100 == 0)
            //            db.SaveChanges();
            //    }
            //    if (pass)
            //        Console.WriteLine("[PASS]");
            //    db.SaveChanges();
            //}

            using (var db = new Database())
            {
                Console.Write("Checking for PARENT_IDs that don't exist as CONTENT_ID... ");
                List<string> list = new List<string>();
                var parent_ids = db.Apps
                    .Where(x => !String.IsNullOrEmpty(x.PARENT_ID))
                    .Select(x => x.PARENT_ID);
                foreach (var multi_parent_id in parent_ids)
                {
                    foreach (var parent_id in multi_parent_id.Split(' '))
                    {
                        if (cancel_requested)
                            return;
                        // Ignore bundles
                        if (!App.IsVitaContentID(parent_id))
                            continue;
                        if (!db.Apps.Any(x => x.CONTENT_ID == parent_id))
                            list.Add(parent_id);
                    }
                }
                if (list.Count() == 0)
                {
                    Console.WriteLine("[PASS]");
                }
                else
                {
                    Console.WriteLine("[FAIL]");
                    foreach (var entry in list.OrderBy(x => x.Substring(7, 9)))
                        Console.WriteLine($"* {entry}");
                }
            }

            using (var db = new Database())
            {
                Console.Write("Checking for CONTENT_IDs that are included in their own PARENT_IDs... ");
                var content_ids = db.Apps
                    .Where(x => (x.PARENT_ID != null && x.PARENT_ID.Contains(x.CONTENT_ID)))
                    .Select(x => x.CONTENT_ID);
                if (content_ids.Count() == 0)
                {
                    Console.WriteLine("[PASS]");
                }
                else
                {
                    Console.WriteLine("[FAIL]");
                    foreach (var content_id in content_ids)
                    {
                        Console.Write($"* {content_id}...");
                        var app = db.Apps.Find(content_id);
                        var parent_ids = app.PARENT_ID.Split(' ').Where(x => x != content_id);
                        app.PARENT_ID = string.Join(' ', parent_ids);
                        db.SaveChanges();
                        Console.WriteLine("[FIXED]");
                    }
                }
            }

        //    using (var db = new Database())
        //    {
        //        Console.Write("Checking for PARENT_IDs that have Add-on type... ");
        //        HashSet<string> list = new HashSet<string>();
        //        foreach (var app_with_parent in db.Apps.Where(x => x.PARENT_ID != null))
        //        {
        //            foreach (var parent_id in app_with_parent.PARENT_ID.Split(' '))
        //            {
        //                if (cancel_requested)
        //                    return;
        //                var app = db.Apps
        //                    .Where(x => (x.CONTENT_ID == parent_id) && (x.CATEGORY > 100))
        //                    .FirstOrDefault();
        //                if (app != null)
        //                    list.Add(parent_id);
        //            }
        //        }

        //        var ordered_list = list.OrderBy(x => x.Substring(7, 9));
        //        if (ordered_list.Count() == 0)
        //        {
        //            Console.WriteLine("[PASS]");
        //        }
        //        else
        //        {
        //            Console.WriteLine("[FAIL]");
        //            foreach (var entry in ordered_list)
        //                Console.WriteLine("* " + entry);
        //        }
        //    }

            //using (var db = new Database())
            //{
            //    Console.Write("Checking for PARENT_IDs that should be trimmed... ");
            //    bool pass = true;
            //    foreach (var app_with_parent in db.Apps.Where(x => x.PARENT_ID != null))
            //    {
            //        if (app_with_parent.PARENT_ID.Trim() != app_with_parent.PARENT_ID)
            //        {
            //            if (pass)
            //                Console.WriteLine("[FAIL]");
            //            pass = false;
            //            Console.Write($"* '{app_with_parent.PARENT_ID}'...");
            //            db.Apps.Find(app_with_parent.CONTENT_ID);
            //            app_with_parent.PARENT_ID = app_with_parent.PARENT_ID.Trim();
            //            db.SaveChanges();
            //            Console.WriteLine("[FIXED]");
            //        }
            //    }
            //    if (pass)
            //        Console.WriteLine("[PASS]");
            //}

            using (var db = new Database())
            {
                Console.Write("Checking for PARENT_IDs that have Demo type... ");
                bool pass = true;
                foreach (var app_with_parent in db.Apps.Where(x => (x.PARENT_ID != null && x.CATEGORY < 3)))
                {
                    foreach (var parent_id in app_with_parent.PARENT_ID.Split(' '))
                    {
                        if (cancel_requested)
                            return;
                        var app = db.Apps
                            .Where(x => (x.CONTENT_ID == parent_id) && (x.CATEGORY == 3))
                            .FirstOrDefault();
                        if (app != null)
                        {
                            if (pass)
                                Console.WriteLine("[FAIL]");
                            pass = false;
                            Console.Write("* '" + app_with_parent.PARENT_ID + "' -> '");
                            db.Apps.Find(app_with_parent.CONTENT_ID);
                            app_with_parent.PARENT_ID = app_with_parent.PARENT_ID.Replace(app.CONTENT_ID, "");
                            app_with_parent.PARENT_ID = app_with_parent.PARENT_ID.Replace("  ", " ");
                            app_with_parent.PARENT_ID = app_with_parent.PARENT_ID.Trim();
                            Console.WriteLine(app_with_parent.PARENT_ID + "'");
                            if (app_with_parent.PARENT_ID.Length == 0)
                                app_with_parent.PARENT_ID = null;
                            db.SaveChanges();
                        }
                    }
                }
                if (pass)
                    Console.WriteLine("[PASS]");
            }
        }

        /// <summary>
        /// Dump the database to an SQL file.
        /// </summary>
        static void SqlDump()
        {
            // This ensures that our tables are always dumped in the same order
            string[] tables = { "Apps", "Categories", "Flags", "Pkgs", "Types", "Updates" };

            // Back up and remove zRIFs if any
            ExportZRif("zrifs.txt");
            Console.Write($"Removing zRIFs... ");
            using (var db = new Database())
            {
                int i = 0;
                foreach (var app in db.Apps.Where(x => x.ZRIF != null))
                {
                    db.Apps.Find(app.CONTENT_ID);
                    app.ZRIF = null;
                    if (++i % 100 == 0)
                        db.SaveChanges();
                }
                db.SaveChanges();
            }
            Console.WriteLine("DONE");

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "sqlite3",
                Arguments = $"-bail {Settings.Instance.database_name}",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Console.Write($"Dumping {Settings.Instance.database_name} to '{Settings.Instance.local_sql}'... ");
            Process proc = new Process { StartInfo = psi };
            proc.Start();
            proc.StandardInput.WriteLine($".output {Settings.Instance.local_sql}");
            foreach (var table in tables)
                proc.StandardInput.WriteLine($".dump {table}");
            proc.StandardInput.WriteLine(".quit");
            string stderr = proc.StandardError.ReadToEnd();
            if (!string.IsNullOrEmpty(stderr))
                Console.WriteLine("error: " + stderr);
            proc.WaitForExit();
            Console.WriteLine("DONE");
            // Restore zRIFs
            ImportZRif("zrifs.txt");
        }

        /// <summary>
        /// Quick deviation analysis of the characters used in the zeus base-52 hashes.
        /// </summary>
        static void DeviationAnalysis()
        {
            int[] widths = { 64, 101 };
            int height = 52;
            foreach (int width in widths)
            {
                int[,] table = new int[width, height];
                Dictionary<char, int> dict = new Dictionary<char, int>();
                for (int i = 0; i < 26; i++)
                {
                    dict[(char)('a' + i)] = i;
                    dict[(char)('A' + i)] = 26 + i;
                }
                using (var db = new Database())
                {
                    var hashes = db.Pkgs
                        .Where(x => (x.URL.Length == 59 + width) && (x.URL.Substring(7, 4) == "zeus"))
                        .Select(x => x.URL.Substring(55, width));
                    Console.WriteLine($"Length {width}: got {hashes.Count()} hashes");
                    int mean = hashes.Count() / height;
                    foreach (var hash in hashes)
                    {
                        for (int x = 0; x < width; x++)
                            table[x, dict[hash[x]]]++;
                    }

                    int max = 0;
                    for (int x = 0; x < width; x++)
                        for (int y = 0; y < height; y++)
                        {
                            table[x, y] = (table[x, y] - mean) * (table[x, y] - mean);
                            if (table[x, y] > max)
                                max = table[x, y];
                        }

                    Console.WriteLine($"mean {mean}, max deviation = {max}");
                    Console.WriteLine("Generating output 'zeus_" + width + ".bmp'...");
                    var bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
                    ColorPalette ncp = bmp.Palette;
                    for (int i = 0; i < 256; i++)
                        ncp.Entries[i] = Color.FromArgb(255, i, i, i);
                    bmp.Palette = ncp;
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            bmp.SetPixel(x, y, ncp.Entries[table[x, y] * 255 / max]);
                    bmp.Save("zeus_" + width + ".bmp");
                }
            }
        }

        public static void ParamError(string message)
        {
            Console.Write($"{Settings.Instance.application_name} {version}: ");
            Console.WriteLine(message);
            Console.WriteLine($"Try '{Settings.Instance.application_name} --help' for more information.");
            if (wait_for_key)
                WaitForKey();
        }

        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                cancel_requested = true;
            };

            string mode = "nps";
            string input = null, output = null;

            var options = new OptionSet {
                { "m|maintenance", "perform database maintenance", x => mode = "maintenance" },
                { "i|input=", "name of the input file or URL", x => input = x },
                { "o|output=", "name of the output file", x => output = x },
                { "c|csv", "import/export CSV/TSV (Content type is deduced from filename)", x => mode = "csv" },
                { "n|nps", "import data from NoPayStation", x => mode = "nps" },
                { "chihiro", "refresh db from Chihiro", x => mode = "chihiro" },
                { "psn", "refresh db from PSN", x => mode = "psn" },
                { "region", "internal region check", x=> mode = "region" },
                { "deviation", "deviation analysis of the zeus base 52 PKG URLs", x => mode = "deviation" },
                { "d|dump", "dump database to SQL (requires sqlite3.exe)", x => mode = "dump" },
                { "p|purge", "purge/create a new PKG cache dictionary", x => purge_pkgcache = true },
                { "u|url=", "update DB from PSN Store/Pkg URL(s)", x => {mode = "url"; input = x; } },
                { "version", "display version and exit", x => mode = "version" },
                { "v", "increase verbosity", x => verbosity++ },
                { "z|zrif=", "import/export zRIFs", x => mode = "zrif" },
                { "w|wait-for-key", "wait for keypress before exiting", x => wait_for_key = true },
                { "h|help", "show this message and exit", x => mode = "help" },
            };

            try
            {
                // parse the command line
                var extra_args = options.Parse(args);
                if (extra_args.Count >= 1)
                {
                    ParamError($"Unrecognized parameter '{extra_args[0]}'");
                    return;
                }
                if (!String.IsNullOrEmpty(input) && !String.IsNullOrEmpty(output))
                {
                    ParamError($"Only one of 'input' or 'output' can be specified");
                    return;
                }
            }
            catch (OptionException e)
            {
                // output some error message
                ParamError(e.Message);
                return;
            }

            // Create the DB if needed
            if (!File.Exists(Settings.Instance.database_name))
            {
                if (!File.Exists(Settings.Instance.local_sql))
                {
                    Console.WriteLine($"Downloading SQL data from " +
                        $"'{Settings.Instance.remote_sql}' to '{Settings.Instance.local_sql}'...");
                    try
                    {
                        using (WebClient wc = new WebClient())
                            wc.DownloadFile(Settings.Instance.remote_sql, Settings.Instance.local_sql);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine($"[ERROR] Failed to download SQL data: {e.Message}");
                        return;
                    }
                }
                if (!Database.CreateDB(Settings.Instance.local_sql))
                    return;
            }

            // Create/Purge the PKG cache
            if (purge_pkgcache)
            {
                File.Delete(Settings.Instance.local_cache);
            }
            else if (!File.Exists(Settings.Instance.local_cache))
            {
                Console.WriteLine($"Downloading PKG Cache from " +
                    $"'{Settings.Instance.remote_cache}' to '{Settings.Instance.local_cache}'...");
                try
                {
                    using (WebClient wc = new WebClient())
                        wc.DownloadFile(Settings.Instance.remote_cache, Settings.Instance.local_cache);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[ERROR] Failed to download PKG Cache: {e.Message}");
                    return;
                }
            }

            switch (mode)
            {
                case "help":
                    Console.WriteLine();
                    Console.WriteLine($"Usage: {Settings.Instance.application_name} [OPTIONS]");
                    Console.WriteLine();
                    Console.WriteLine("Options:");
                    options.WriteOptionDescriptions(Console.Out);
                    if (wait_for_key)
                        WaitForKey();
                    return;
               case "csv":
                    string uri = input ?? output;
                    if (String.IsNullOrEmpty(uri))
                    {
                        ParamError("You must provide a file or URL.");
                        return;
                    }
                    int type;
                    for (type = Settings.nps_type.Length - 1; type >= 0; type--)
                    {
                        if (uri.ToLower().Contains(Settings.nps_type[type].ToLower()))
                            break;
                    }
                    if (!String.IsNullOrEmpty(input))
                        ImportCSV(input, type);
                    else
                        ExportCSV(output, type);
                    break;
                case "dump":
                    if (!File.Exists("sqlite3.exe") && !File.Exists("sqlite3"))
                    {
                        Console.Error.WriteLine("The sqlite3 executable is missing from the application directory.");
                        Console.Error.WriteLine("Please download sqlite-tools from https://www.sqlite.org and extract sqlite3 here.");
                        break;
                    }
                    SqlDump();
                    break;
                case "maintenance":
                    Maintenance();
                    break;
                case "nps":
                    for (int i = 0; i < Settings.nps_type.Length; i++)
                    {
                        if (cancel_requested)
                            break;
                        ImportCSV(Settings.Instance.nps_url[i], i);
                    }
                    break;
                case "psn":
                    RefreshDBFromPSN();
                    break;
                case "chihiro":
                    RefreshDBFromChihiro();
                    break;
                case "url":
                    if (String.IsNullOrEmpty(input))
                    {
                        ParamError("You must supply a URL or file.");
                        return;
                    }
                    UpdateFromUrl(input);
                    break;
                case "version":
                    Console.WriteLine($"{Settings.Instance.application_name} {version}");
                    break;
                case "deviation":
                    DeviationAnalysis();
                    break;
                case "zrif":
                    if (String.IsNullOrEmpty(input) && String.IsNullOrEmpty(output))
                    {
                        ParamError("You must supply the name of a file.");
                        return;
                    }
                    if (!String.IsNullOrEmpty(input))
                        ImportZRif(input);
                    else
                        ExportZRif(output);
                    break;
                case "region":
                    string[] eur_regions =
                    {
                        "en-gb", "en-ie", "en-pl", "en-se", "en-no", "en-fi", "en-dk", "en-cz",
                        "en-gr", "en-hu", "en-ro", "en-sk", "en-si", "en-tr", "en-il", "en-au",
                        "fr-fr", "it-it", "es-es", "de-de", "nl-nl", "pt-pt", "de-at", "ru-ru"
                    };
                    string[] usa_regions = { "en-us", "en-ca", "es-mx", "pt-br", "es-ar" };
                    string[] asn_regions = { "en-sg", "en-hk", "zh-hk" };
                    CheckRegion(usa_regions);
                    CheckRegion(eur_regions);
                    CheckRegion(asn_regions);
                    break;
                case "test":
                    break;
                default:
                    Console.Error.WriteLine("Unsupported mode.");
                    break;
            }

            Pkg.FlushPkgCache();
            if (wait_for_key)
                WaitForKey();
        }
    }
}
