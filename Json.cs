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
using System.Net;

namespace VitaDB
{
    // This is the JSON descriptor for the data we get from Chihiro
    public class Chihiro
    {
        public class SubCategory
        {
            public string name { get; set; }
            public int count { get; set; }
            public string key { get; set; }
        }

        public class Price
        {
            public string name { get; set; }
            public int count { get; set; }
            public string key { get; set; }
        }

        public class TopCategory
        {
            public string name { get; set; }
            public int count { get; set; }
            public string key { get; set; }
        }

        public class Relationship
        {
            public string name { get; set; }
            public int count { get; set; }
            public string key { get; set; }
        }

        public class Facets
        {
            public List<SubCategory> sub_category { get; set; }
            public List<Price> price { get; set; }
            public List<TopCategory> top_category { get; set; }
            public List<Relationship> relationship { get; set; }
        }

        public class Attributes
        {
            public Facets facets { get; set; }
            public List<object> next { get; set; }
        }

        public class ContentDescriptor
        {
            public string description { get; set; }
            public string url { get; set; }
            public string name { get; set; }
        }

        public class ContentRating
        {
            public string description { get; set; }
            public string rating_system { get; set; }
            public string url { get; set; }
        }

        public class MediaProp
        {
        }

        public class Drm
        {
            public int drm_category_type { get; set; }
            public string id { get; set; }
            public int is_streamable { get; set; }
            public MediaProp media_prop { get; set; }
            public long size { get; set; }
            public int type { get; set; }
        }

        public class Entitlement
        {
            public string description { get; set; }
            public List<Drm> drms { get; set; }
            public int duration { get; set; }
            public string durationOverrideTypeId { get; set; }
            public int exp_after_first_use { get; set; }
            public int feature_type_id { get; set; }
            public string id { get; set; }
            public int license_type { get; set; }
            public object metadata { get; set; }
            public string name { get; set; }
            public string packageType { get; set; }
            public List<object> packages { get; set; }
            public bool preorder_placeholder_flag { get; set; }
            public int size { get; set; }
            public int subType { get; set; }
            public List<string> subtitle_language_codes { get; set; }
            public int type { get; set; }
            public int use_count { get; set; }
            public List<string> voice_language_codes { get; set; }
        }

        public class Sku
        {
            public bool amortizeFlag { get; set; }
            public bool bundleExclusiveFlag { get; set; }
            public bool chargeImmediatelyFlag { get; set; }
            public int charge_type_id { get; set; }
            public int credit_card_required_flag { get; set; }
            public bool defaultSku { get; set; }
            public string display_price { get; set; }
            public List<object> eligibilities { get; set; }
            public List<Entitlement> entitlements { get; set; }
            public string id { get; set; }
            public bool is_original { get; set; }
            public string name { get; set; }
            public List<int> platforms { get; set; }
            public int price { get; set; }
            public List<object> rewards { get; set; }
            public bool seasonPassExclusiveFlag { get; set; }
            public bool skuAvailabilityOverrideFlag { get; set; }
            public int sku_type { get; set; }
            public string type { get; set; }
        }

        public class GameContentTypesList
        {
            public string name { get; set; }
            public string key { get; set; }
        }

        public class Image
        {
            public int type { get; set; }
            public string url { get; set; }
        }

        public class Metadata
        {
            public List<string> packageSubType { get; set; }
        }

        public class Link
        {
            public string bucket { get; set; }
            public List<string> cloud_only_platform { get; set; }
            public string container_type { get; set; }
            public string content_type { get; set; }
            public Sku default_sku { get; set; }
            public string id { get; set; }
            public List<Image> images { get; set; }
            public string name { get; set; }
            public string parent_name { get; set; }
            public List<string> playable_platform { get; set; }
            public string provider_name { get; set; }
            public DateTime release_date { get; set; }
            public bool restricted { get; set; }
            public int revision { get; set; }
            public string short_name { get; set; }
            public UInt64 timestamp { get; set; }
            public string top_category { get; set; }
            public string url { get; set; }
            public string sub_category { get; set; }
        }

        public class Screenshot
        {
            public string type { get; set; }
            public int typeId { get; set; }
            public string source { get; set; }
            public string url { get; set; }
            public int order { get; set; }
        }

        public class MediaList
        {
            public List<Screenshot> screenshots { get; set; }
        }

        public class MediaLayout
        {
            public string type { get; set; }
            public int height { get; set; }
            public int width { get; set; }
        }

        public class CnRemotePlay
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class CloudOnlyPlatform
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class CnVrEnabled
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class SecondaryClassification
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class CnVrRequired
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class GameGenre
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class PlayablePlatform
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class CnDualshockVibration
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class TertiaryClassification
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class Genre
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class CnPsVrAimRequired
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class CnCrossPlatformPSVita
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class CnPsVrAimEnabled
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class PrimaryClassification
        {
            public string name { get; set; }
            public List<string> values { get; set; }
        }

        public class Country
        {
            public int agelimit { get; set; }
            public int uagelimit { get; set; }
            public string country { get; set; }
        }

        public class Url
        {
            public int type { get; set; }
            public string url { get; set; }
        }

        public class Material
        {
            public string anno { get; set; }
            public List<Country> countries { get; set; }
            public DateTime from { get; set; }
            public int id { get; set; }
            public List<string> lang { get; set; }
            public DateTime lastm { get; set; }
            public DateTime until { get; set; }
            public List<Url> urls { get; set; }
        }

        public class Promomedia
        {
            public string anno { get; set; }
            public int id { get; set; }
            public string key { get; set; }
            public List<Material> materials { get; set; }
            public string multi { get; set; }
            public string rep { get; set; }
            public int type { get; set; }
        }

        public class Count
        {
            public int star { get; set; }
            public int count { get; set; }
        }

        public class StarRating
        {
            public string total { get; set; }
            public string score { get; set; }
            public List<Count> count { get; set; }
        }

        public class Data
        {
            public int age_limit { get; set; }
            public Attributes attributes { get; set; }
            public string bucket { get; set; }
            public List<string> cloud_only_platform { get; set; }
            public string container_type { get; set; }
            public List<ContentDescriptor> content_descriptors { get; set; }
            public int content_origin { get; set; }
            public ContentRating content_rating { get; set; }
            public string content_type { get; set; }
            public Sku default_sku { get; set; }
            public bool dob_required { get; set; }
            public List<GameContentTypesList> gameContentTypesList { get; set; }
            public string game_contentType { get; set; }
            public string id { get; set; }
            public List<Image> images { get; set; }
            public List<Link> links { get; set; }
            public string long_desc { get; set; }
            public MediaList mediaList { get; set; }
            public List<MediaLayout> media_layouts { get; set; }
            public Metadata metadata { get; set; }
            public string name { get; set; }
            public int pageTypeId { get; set; }
            public List<Link> parent_links { get; set; }
            public List<string> playable_platform { get; set; }
            public List<Promomedia> promomedia { get; set; }
            public string provider_name { get; set; }
            public List<Relationship> relationships { get; set; }
            public DateTime release_date { get; set; }
            public bool restricted { get; set; }
            public int revision { get; set; }
            public string short_name { get; set; }
            public int size { get; set; }
            public List<string> sku_links { get; set; }
            public List<Sku> skus { get; set; }
            public string sort { get; set; }
            public StarRating star_rating { get; set; }
            public int start { get; set; }
            public UInt64 timestamp { get; set; }
            public string title_name { get; set; }
            public string top_category { get; set; }
            public int total_results { get; set; }
        }

        /// <summary>
        /// Fetches JSON data from Sony's Chihiro servers.
        /// </summary>
        /// <param name="content_id">The CONTENT_ID to look up.</param>
        /// <param name="region">An optional region string, to use with the Chihiro server.
        /// If this parameter is not provided, the language to use is deduced from content_id</param>
        /// <returns>A Chihiro.Data JSON object.</returns>
        public static Chihiro.Data GetData(string content_id, string region = null)
        {
            if (!App.ValidateContentID(content_id))
            {
                Console.Error.WriteLine($"[ERROR] '{content_id}' is not valid");
                return null;
            }
            if (String.IsNullOrEmpty(region))
            {
                // Asian content is a mess...
                if (content_id.Substring(20).Contains("ASIA"))
                    region = "en-hk";
                else
                    switch (content_id[0])
                    {
                        case 'E':
                            region = "en-ie";
                            break;
                        case 'H':
                            region = "en-hk";
                            break;
                        case 'J':
                            region = "ja-jp";
                            break;
                        default:
                            region = "en-us";
                            break;
                    }
            }
            if (String.IsNullOrEmpty(region))
            {
                Console.Error.WriteLine($"[ERROR] Could not get language for '{content_id}'");
                return null;
            }
            string chihiro_region = region.Substring(3, 2).ToUpper() + "/" + region.Substring(0, 2);
            var url = "https://store.playstation.com/store/api/chihiro/00_09_000/container/" +
                chihiro_region + "/999/" + content_id;
            using (WebClient wc = new WebClient())
            {
                try
                {
                    var json = wc.DownloadString(url);
                    return String.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Chihiro.Data>(json);
                }
                catch (System.Net.WebException) { }
            }
            return null;
        }

        /// <summary>
        /// Fetches all Vita titles for a specific region.
        /// </summary>
        /// <param name="region">The region to perform the lookup for.
        /// <param name="root_name">The optional root string to search from.</param>
        /// <returns>A Chihiro.Data JSON object.</returns>
        public static Chihiro.Data GetAllTitles(string region, string root_name = null)
        {
            string[] root_names = 
            {
                "STORE-MSF77008-PSVITAALLGAMES",
                "STORE-MSF75508-PLATFORMPSVITA",
                "STORE-MSF86012-PSVITAGAMES",
                "PN.CH.CN-PN.CH.MIXED.CN-PSVITAGAMES",
            };
            List<string>[] root = new List<string>[root_names.Length];
            root[0] = new List<string>() { "us", "ca", "mx", "br", "ar" };
            root[1] = new List<string>() { "ie", "gb", "cz", "dk", "fi",
                "no", "se", "gr", "hu", "ro", "sk", "si", "tr", "fr", "de",
                "it", "es", "nl", "pt", "pl", "il", "at", "au", "ua", "ru" };
            root[2] = new List<string>() { "hk" };
            root[3] = new List<string>() { "cn" };

            if (root_name == null)
            {
                var country = region.Substring(3, 2);
                int i;
                for (i = 0; i < root_names.Length; i++)
                {
                    if (root[i].Contains(country))
                    {
                        root_name = root_names[i];
                        break;
                    }
                }
                if (i > root_names.Length)
                {
                    Console.Error.WriteLine($"[ERROR] No root defined for '{region}'");
                    return null;
                }
            }

            string chihiro_region = region.Substring(3, 2).ToUpper() + "/" + region.Substring(0, 2);
            var url = "https://store.playstation.com/store/api/chihiro/00_09_000/container/" +
                chihiro_region + "/999/" + root_name + "?sort=name&direction=asc&size=10000";
            using (WebClient wc = new WebClient())
            {
                try
                {
                    var json = wc.DownloadString(url);
                    return String.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Chihiro.Data>(json);
                }
                catch (System.Net.WebException) { }
            }
            return null;
        }
    }
}
