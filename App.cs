/*
 * VitaDB - Vita DataBase Updater © 2017 VitaSmith
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.RegularExpressions;

using static VitaDB.Utilities;

namespace VitaDB
{
    public class App
    {
        public string TITLE_ID { get; set; }
        public string NAME { get; set; }
        public string ALT_NAME { get; set; }
        [Key]
        public string CONTENT_ID { get; set; }
        public string PARENT_ID { get; set; }
        public int? CATEGORY { get; set; }
        public UInt32? PKG_ID { get; set; }
        public string ZRIF { get; set; }
        public string COMMENTS { get; set; }
        public UInt16 FLAGS { get; set; }

        [NotMapped]
        public string PKG_URL { get; set; }

        static private UInt32 count = 0;

        /// <summary>
        /// Set a flag or a set of flags.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="flag_names">One or more names of flags to set.</param>
        public void SetFlag(Database db, params string[] flag_names)
        {
            for (int i = 0; i < flag_names.Length; i++)
                this.FLAGS |= (UInt16)db.Flag[flag_names[i]];
        }

        /// <summary>
        /// Set an attribute to read-only.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="attr_names">The names of one or more attrinbutes to set to read-only.</param>
        public void SetReadOnly(Database db, params string[] attr_names)
        {
            for (int i = 0; i < attr_names.Length; i++)
                this.FLAGS |= (UInt16)db.Flag[attr_names[i] + "_RO"];
        }

        /// <summary>
        /// Add a new PARENT_ID entry.
        /// </summary>
        /// <param name="parent_id">The ID to add.</param>
        public void AddParent(string parent_id)
        {
            if (String.IsNullOrEmpty(parent_id))
                return;
            if (String.IsNullOrEmpty(this.PARENT_ID))
                this.PARENT_ID = parent_id;
            else if (!this.PARENT_ID.Contains(parent_id))
                this.PARENT_ID += " " + parent_id;
        }

        /// <summary>
        /// Insert or update a new application entry into the Apps database.
        /// This method preserves attributes that have been flagged to read-only.
        /// Note: This method only saves changes to the databse every 100 records.
        /// </summary>
        /// <param name="db">The database context.</param>
        public void Upsert(Database db)
        {
            var app = db.Apps.Find(CONTENT_ID);
            if (app == null)
            {
                db.Apps.Add(this);
            }
            else
            {
                var org_app = db.Apps
                    .AsNoTracking()
                    .FirstOrDefault(x => x.CONTENT_ID == this.CONTENT_ID);
                if (org_app == null)
                {
                    // Changes need to be applied
                    db.SaveChanges();
                    org_app = db.Apps
                        .AsNoTracking()
                        .FirstOrDefault(x => x.CONTENT_ID == this.CONTENT_ID);
                    if (org_app == null)
                        throw new ApplicationException("Tracked App found, but database changes were not applied.");
                }
                var entry = db.Entry(this);

                foreach (var attr in typeof(App).GetProperties())
                {
                    if ((attr.Name == nameof(PKG_URL)) || (attr.Name == nameof(CONTENT_ID)))
                        continue;

                    // Manually Check for values that have been modified
                    var new_value = attr.GetValue(this, null);
                    var org_value = attr.GetValue(org_app, null);
                    if ((new_value == null) || new_value.Equals(org_value))
                    {
                        entry.Property(attr.Name).IsModified = false;
                        continue;
                    }

                    // Set modified attribute according to the read-only flags
                    if (db.Flag.ContainsKey(attr.Name + "_RO"))
                    {
                        entry.Property(attr.Name).IsModified =
                            ((org_app.FLAGS & db.Flag[attr.Name + "_RO"]) == 0);
                    }
                    else
                    {
                        entry.Property(attr.Name).IsModified = true;
                    }
                }

                // Flags can only be added in this method, never removed
                if (this.FLAGS != org_app.FLAGS)
                {
                    this.FLAGS |= org_app.FLAGS;
                    entry.Property(nameof(FLAGS)).IsModified = true;
                }
            }
            if (++count % 100 == 0)
                db.SaveChanges();
        }

        /// <summary>
        /// Validate that TITLE_ID matches the expected format.
        /// </summary>
        /// <param name="title_id">The TITLE_ID string.</param>
        /// <returns>true if TITLE_ID is valid, false otherwise.</returns>
        public static bool ValidateTitleID(string title_id)
        {
            Regex regexp = new Regex(@"^[A-Z]{4}\d{5}$");
            if (title_id == null)
                return false;
            return regexp.IsMatch(title_id);
        }

        /// <summary>
        /// Check if a CONTENT_ID is a Vita title.
        /// </summary>
        /// <param name="content_id">The CONTENT_ID.</param>
        /// <returns>True if Vita CONTENT_ID, false otherwise.</returns>
        public static bool IsVitaContentID(string content_id)
        {
            if (!ValidateContentID(content_id))
                return false;
            return (content_id[7] == 'P') || (content_id[7] == 'V');
        }

        /// <summary>
        /// Check if a CONTENT_ID is a Bundle.
        /// </summary>
        /// <param name="content_id">The CONTENT_ID.</param>
        /// <returns>True if bundle, false otherwise.</returns>
        public static bool IsBundleContentID(string content_id)
        {
            if (!ValidateContentID(content_id))
                return false;
            return content_id.Substring(7, 4) == "CUSA";
        }

        /// <summary>
        /// Validate that CONTENT_ID matches the expected format.
        /// </summary>
        /// <param name="title_id">The CONTENT_ID string.</param>
        /// <returns>true if CONTENT_ID is valid, false otherwise.</returns>
        public static bool ValidateContentID(string content_id)
        {
            Regex regexp = new Regex(@"^[A-Z\?]{2}[\d\?]{4}-[A-Z]{4}\d{5}_[\d\?]{2}-[A-Z0-9_\?]{16}$");
            if (content_id == null)
                return false;
            return regexp.IsMatch(content_id);
        }

        /// <summary>
        /// Update or add an App to the DB.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="name">The name the App.</param>
        /// <param name="content_id">The content ID.</param>
        /// <param name="parent_id">The content ID of the parent.</param>
        /// <param name="category">The category.</param>
        /// <param name="comments">(Optional) A comment string.</param>
        static void AddApp(Database db, string name, string content_id, string parent_id, int? category, string comments = null)
        {
            if (parent_id == content_id)
                parent_id = null;
            var app = db.Apps.Find(content_id);
            if (app == null)
            {
                app = new App
                {
                    NAME = name,
                    TITLE_ID = content_id.Substring(7, 9),
                    CONTENT_ID = content_id,
                    PARENT_ID = parent_id,
                    CATEGORY = category,
                    COMMENTS = comments
                };
            }
            else
            {
                app.NAME = name;
                app.TITLE_ID = content_id.Substring(7, 9);
                app.AddParent(parent_id);
                app.CATEGORY = category;
                app.COMMENTS = comments;
            }
            app.SetReadOnly(db, nameof(App.NAME), nameof(App.CATEGORY));
            if (comments != null)
                app.SetReadOnly(db, nameof(App.COMMENTS));
            app.Upsert(db);
        }

        /// <summary>
        /// Update an App entry by querying Chihiro.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="lang">(Optional) The language settings to use when querying Chihiro.</param>
        /// <param name="add_lang">(Optional) If true, add <param name="lang"/> to the COMMENTS field.</param>
        public void UpdateFromChihiro(Database db, string lang = null, bool add_lang = false)
        {
            var data = Chihiro.GetData(CONTENT_ID, lang);
            if (data == null)
                return;

            // Process root data
            NAME = data.name;
            CATEGORY = db.Category[data.top_category];
            if (IsVitaContentID(CONTENT_ID))
            {
                Console.WriteLine($"{TITLE_ID}: {data.name}");
                AddApp(db, NAME, CONTENT_ID, PARENT_ID, CATEGORY, add_lang ? lang : null);
            }

            // Process entitlements
            if (data.default_sku != null)
            {
                foreach (var ent in Nullable(data.default_sku.entitlements))
                {
                    if (!IsVitaContentID(ent.id) || (ent.id == CONTENT_ID))
                        continue;
                    Console.WriteLine($"* {ent.id}: {ent.name}");
                    AddApp(db, ent.name, ent.id, CONTENT_ID, CATEGORY, add_lang ? lang : null);
                }
            }

            // Process links
            foreach (var link in Nullable(data.links))
            {
                // May get mixed DLC content
                if (!IsVitaContentID(link.id))
                    continue;
                Console.WriteLine($"* {link.id}: {link.top_category}");
                AddApp(db, link.name, link.id, CONTENT_ID, db.Category[link.top_category], add_lang ? lang : null);

                // Process sub-entitlements
                if (link.default_sku != null)
                {
                    foreach (var ent in Nullable(link.default_sku.entitlements))
                    {
                        if (!IsVitaContentID(ent.id) || (ent.id == link.id))
                            continue;
                        Console.WriteLine($"  * {ent.id}: {ent.name}");
                        AddApp(db, ent.name, ent.id, CONTENT_ID, db.Category[link.top_category], add_lang ? lang : null);
                    }
                }
            }
        }
    }

    // DB entries for App.CATEGORY
    public class Category
    {
        public string NAME { get; set; }
        [Key]
        public int VALUE { get; set; }
    }

    // DB entries for App.FLAGS
    public class Flag
    {
        public string NAME { get; set; }
        [Key]
        public int VALUE { get; set; }
    }
}
