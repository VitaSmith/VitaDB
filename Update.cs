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
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

using static VitaDB.Utilities;

namespace VitaDB
{
    // XML Elements for PCS######-ver.xml deserialization
    [Serializable, XmlType("titlepatch")]
    public class TitlePatch
    {
        [XmlAttribute(AttributeName = "status")]
        public string Status { get; set; }

        [XmlAttribute(AttributeName = "titleid")]
        public string TitleId { get; set; }

        [XmlElement(typeof(Tag), ElementName = "tag")]
        public Tag Tag { get; set; }

        [XmlAnyAttribute]
        public XmlAttribute[] AnyAttributes;

        [XmlAnyElement]
        public XmlElement[] AnyElements;
    }

    public class Tag
    {
        [XmlAttribute(AttributeName = "name")]
        public string Name { get; set; }

        [XmlAttribute(AttributeName = "signoff")]
        public bool Signoff { get; set; }

        [XmlElement(typeof(Package), ElementName = "package")]
        public Package[] Packages { get; set; }

        [XmlAnyAttribute]
        public XmlAttribute[] AnyAttributes;

        [XmlAnyElement]
        public XmlElement[] AnyElements;
    }

    public class Package
    {
        [XmlAttribute(AttributeName = "version")]
        public string Version { get; set; }

        [XmlAttribute(AttributeName = "type")]
        public string Type { get; set; }

        [XmlAttribute(AttributeName = "size")]
        public UInt64 Size { get; set; }

        [XmlAttribute(AttributeName = "sha1sum")]
        public string Sha1Sum { get; set; }

        [XmlAttribute(AttributeName = "url")]
        public string Url { get; set; }

        [XmlAttribute(AttributeName = "psp2_system_ver")]
        public UInt32 SysVer { get; set; }

        [XmlAttribute(AttributeName = "content_id")]
        public string ContentId { get; set; }

        [XmlElement(ElementName = "paramsfo")]
        public ParamSfo Sfo { get; set; }

        [XmlElement(ElementName = "changeinfo")]
        public ChangeInfo Info { get; set; }

        [XmlElement(typeof(Package), ElementName = "hybrid_package")]
        public Package HybridPackage { get; set; }

        [XmlAnyAttribute]
        public XmlAttribute[] AnyAttributes;

        [XmlAnyElement]
        public XmlElement[] AnyElements;
    }

    public class ParamSfo
    {
        [XmlElement(ElementName = "title")]
        public string Title { get; set; }

        [XmlAnyAttribute]
        public XmlAttribute[] AnyAttributes;

        [XmlAnyElement]
        public XmlElement[] AnyElements;
    }

    public class ChangeInfo
    {
        [XmlAttribute(AttributeName = "url")]
        public string Url { get; set; }

        [XmlAnyAttribute]
        public XmlAttribute[] AnyAttributes;

        [XmlAnyElement]
        public XmlElement[] AnyElements;
    }

    public class Update
    {
        public string CONTENT_ID { get; set; }
        public UInt32 VERSION { get; set; }
        public int TYPE { get; set; }
        [Key]
        public UInt32 PKG_ID { get; set; }

        [NotMapped]
        public string URL { get; set; }
        [NotMapped]
        public UInt64 Size;
        [NotMapped]
        public byte[] Sha1Sum;
        [NotMapped]
        public UInt32 SysVer;

        // The SHA-256 key used to convert a TITLE_ID to an update hash
        private static readonly byte[] hmac_sha256_key =
        {
            0xE5, 0xE2, 0x78, 0xAA, 0x1E, 0xE3, 0x40, 0x82, 0xA0, 0x88, 0x27, 0x9C, 0x83, 0xF9, 0xBB, 0xC8,
            0x06, 0x82, 0x1C, 0x52, 0xF2, 0xAB, 0x5D, 0x2B, 0x4A, 0xBD, 0x99, 0x54, 0x50, 0x35, 0x51, 0x14
        };
        private static HMACSHA256 hmac = new HMACSHA256(hmac_sha256_key);
        private static readonly string base_url = "https://gs-sec.ww.np.dl.playstation.net/pl/np/";

        /// <summary>
        /// Upsert an entry into the DB.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="detach">(Optional) Detach the entry after upsert.</param>
        public void Upsert(Database db, bool detach = false)
        {
            if (db.Updates.Any(x => x.PKG_ID == PKG_ID))
                db.Entry(this).State = EntityState.Modified;
            else
                db.Updates.Add(this);
            db.SaveChanges();
            if (detach)
                db.Entry(this).State = EntityState.Detached;
        }

        /// <summary>
        /// Fetch XML update data from the PSN update servers.
        /// </summary>
        /// <param name="title_id">The TITLE_ID to look for update data.</param>
        /// <returns>An XML document with the update data, or null if no update is avaialble.</returns>
        private static XDocument GetUpdateData(string title_id)
        {
            byte[] hash = hmac.ComputeHash(new ASCIIEncoding().GetBytes("np_" + title_id));
            var url = base_url + title_id + "/" +
                BitConverter.ToString(hash).ToLower().Replace("-", "") + "/" + title_id + "-ver.xml";

            // Required to ignore Sony's self-signed certificate errors
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            try
            {
                return XDocument.Load(url);
            }
            catch (System.Net.WebException)
            {
                // URL does not exist
                return null;
            }
            catch (System.Xml.XmlException)
            {
                // XML is empty
                return null;
            }
        }

        /// <summary>
        /// Check the updates PSN servers and insert/update the relevant DB records.
        /// </summary>
        /// <param name="db">The database context.</param>
        /// <param name="title_id">The TITLE_ID to search updates for.</param>
        public static void Check(Database db, string title_id)
        {
            var document = Update.GetUpdateData(title_id);
            if (document == null)
                return;

            TitlePatch patch = DeserializeFromXML<TitlePatch>(document);
            if (patch.Tag != null)
            {
                foreach (var update_pkg in Nullable(patch.Tag.Packages))
                {
                    Update update = new Update
                    {
                        CONTENT_ID = update_pkg.ContentId,
                        VERSION = GetVersionFromString(update_pkg.Version),
                        TYPE = db.Type[update_pkg.Type ?? "cumulative"],
                        URL = update_pkg.Url,
                        Size = update_pkg.Size,
                        Sha1Sum = HexStringToByteArray(update_pkg.Sha1Sum),
                        SysVer = GetVersionFromBcd(update_pkg.SysVer),
                    };

                    var app = db.Apps.Where(x => x.CONTENT_ID == update.CONTENT_ID).FirstOrDefault();
                    if (app == null)
                    {
                        app = new App
                        {
                            CONTENT_ID = update.CONTENT_ID,
                            TITLE_ID = update.CONTENT_ID.Substring(7, 9),
                            NAME = document.Descendants("title").FirstOrDefault().Value
                        };
                        Console.WriteLine($"[NOTE] Adding new App {app.CONTENT_ID}: {app.NAME}");
                        db.Apps.Add(app);
                        db.SaveChanges();
                    }

                    var pkg = db.Pkgs.Where(x => x.URL == update.URL).FirstOrDefault();
                    if (pkg == null)
                    {
                        pkg = Pkg.CreatePkg(update.URL);
                        if (pkg == null)
                            continue;
                        db.Pkgs.Add(pkg);
                        db.SaveChanges();
                    }
                    update.PKG_ID = pkg.ID;
                    update.Upsert(db, true);

                    if (update_pkg.HybridPackage != null)
                    {
                        // Hybrid packages are derived from parent
                        update.CONTENT_ID = update_pkg.HybridPackage.ContentId;
                        update.TYPE = db.Type["hybrid"];
                        update.URL = update_pkg.HybridPackage.Url;
                        update.Size = update_pkg.HybridPackage.Size;
                        update.Sha1Sum = HexStringToByteArray(update_pkg.HybridPackage.Sha1Sum);

                        pkg = db.Pkgs.Where(x => x.URL == update.URL).FirstOrDefault();
                        if (pkg == null)
                        {
                            pkg = Pkg.CreatePkg(update.URL);
                            db.Pkgs.Add(pkg);
                            db.SaveChanges();
                        }
                        update.PKG_ID = pkg.ID;
                        update.Upsert(db, true);
                    }
                }
            }
        }
    }

    // DB entries for Update.TYPE
    public class Type
    {
        public string NAME { get; set; }
        [Key]
        public int VALUE { get; set; }
    }
}
