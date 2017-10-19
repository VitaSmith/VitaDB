/*
 * VitaDB - Vita DataBase Updater © 2017 VitaSmith
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System;

using static VitaDB.Utilities;

namespace VitaDB
{
    public class Sfo
    {
        public string Category;
        public UInt32? AppVer;
        public UInt32? SysVer;
        public UInt32? CDate;

        static readonly byte[] sfo_magic = { 0x00, (byte)'P', (byte)'S', (byte)'F' };

        /// <summary>
        /// Generic method to get the value associated with a key from SFO data.
        /// </summary>
        /// <typeparam name="T">The type of the value to be returned.</typeparam>
        /// <param name="sfo">The SFO data.</param>
        /// <param name="key">The key.</param>
        /// <returns>The value associated with they key.</returns>
        public static T GetValue<T>(byte[] sfo, string key)
        {
            UInt32 keys = GetLe32(sfo, 0x08);
            UInt32 values = GetLe32(sfo, 0x0c);
            UInt32 count = GetLe32(sfo, 0x10);
            UInt32 value_length = 0;
            UInt16 value_type = 0;

            int i;
            for (i = 0; i < count; i++)
            {
                if (MemCpyStr(sfo, (int)keys + GetLe16(sfo, i * 16 + 20)) == key)
                {
                    value_type = GetLe16(sfo, 0x14 + (i * 0x10) + 2);
                    value_length = GetLe32(sfo, 0x14 + (i * 0x10) + 4);
                    break;
                }
            }
            if (i >= count)
                return default(T);

            int pos = (int)(values + GetLe32(sfo, i * 16 + 20 + 12));
            if (typeof(T) == typeof(string))
            {
                string value;
                switch (value_type)
                {
                    case 0x0204:
                        // NUL terminated string
                        value = MemCpyStr(sfo, pos);
                        if (value.Length != value_length - 1)
                            Console.Error.WriteLine($"[WARNING] SFO: Unexpected length {value.Length}, while looking for NUL-terminated key '{key}'");
                        break;
                    case 0x0004:
                        // non-NUL terminated
                        value = MemCpyStr(sfo, pos, (int)value_length);
                        break;
                    default:
                        throw new ApplicationException($"SFO: Unexpected type 0x{value_type:X4}, while looking for string key '{key}'.");
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            else if (typeof(T) == typeof(UInt32))
            {
                if (value_type != 0x0404)
                    throw new ApplicationException($"SFO: Unexpected type 0x{value_type:X4}, while looking for integer key '{key}'.");
                if (value_length != 4)
                    throw new ApplicationException($"SFO: Unexpected length {value_length}, while looking for for integer key '{key}'.");
                var value = GetLe32(sfo, pos);
                return (T)Convert.ChangeType(value, typeof(T));
            }
            else
            {
                throw new ApplicationException($"SFO: Unhandled type '{typeof(T)}' requested for key {key}");
            }
        }

        public override string ToString()
        {
            string str = "(Sfo Object)\n";
            foreach (var attr in typeof(Sfo).GetFields())
                str += ($"* {attr.Name} = '{this.GetType().GetField(attr.Name).GetValue(this)}'\n");
            return str;
        }

        /// <summary>
        /// Constructs a new Sfo object, using the SFO data passed as parameter.
        /// </summary>
        /// <param name="sfo">The SFO data.</param>
        public Sfo(byte[] sfo)
        {
            if (!MemCmp(sfo, sfo_magic, 0))
            {
                Console.Error.WriteLine("[ERROR] Not an SFO file");
                return;
            }
            if (GetLe32(sfo, 4) != 0x00000101)
            {
                Console.Error.WriteLine("[ERROR] Only SFO version 1.1 is supported");
                return;
            }
            Category = GetValue<string>(sfo, "CATEGORY");
            // Convert App and Sys versions to (major * 100 + minor)
            AppVer = GetVersionFromString(GetValue<string>(sfo, "APP_VER"));
            if (AppVer == 0)
                AppVer = null;
            SysVer = GetVersionFromBcd(GetValue<UInt32>(sfo, "PSP2_SYSTEM_VER"));
            if (SysVer == 0)
                SysVer = null;
            string pubtoolinfo = GetValue<string>(sfo, "PUBTOOLINFO");
            if ((pubtoolinfo != null) && pubtoolinfo.Contains("c_date="))
            {
                var date = pubtoolinfo.Substring(pubtoolinfo.IndexOf("c_date=") + "c_date=".Length, 8);
                CDate = UInt32.Parse(date);
            }
        }
    }
}
