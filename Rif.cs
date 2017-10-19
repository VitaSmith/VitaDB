/*
 * VitaDB - Vita DataBase Updater © 2017 VitaSmith
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using ICSharpCode.SharpZipLib.Zip.Compression;
using System;
using System.Linq;

using static VitaDB.Utilities;

namespace VitaDB
{
    class RIF
    {
        public static readonly UInt32 ZLIB_DICTIONARY_ID_ZRIF = 0x627d1d5d;
        public static readonly byte[] zrif_dict = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 48, 48, 48, 48, 57, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 48, 48,
            48, 48, 54, 48, 48, 48, 48, 55, 48, 48, 48, 48, 56, 0, 48, 48, 48, 48, 51, 48, 48, 48, 48, 52, 48, 48, 48, 48,
            53, 48, 95, 48, 48, 45, 65, 68, 68, 67, 79, 78, 84, 48, 48, 48, 48, 50, 45, 80, 67, 83, 71, 48, 48, 48, 48,
            48, 48, 48, 48, 48, 48, 49, 45, 80, 67, 83, 69, 48, 48, 48, 45, 80, 67, 83, 70, 48, 48, 48, 45, 80, 67, 83,
            67, 48, 48, 48, 45, 80, 67, 83, 68, 48, 48, 48, 45, 80, 67, 83, 65, 48, 48, 48, 45, 80, 67, 83, 66, 48, 48,
            48, 0, 1, 0, 1, 0, 1, 0, 2, 239, 205, 171, 137, 103, 69, 35, 1
        };
        public static readonly byte[] rif_name_key = {
            0x19, 0xDD, 0x4F, 0xB9, 0x89, 0x48, 0x2B, 0xD4,
            0xCB, 0x9E, 0xC9, 0xC7, 0x9A, 0x2E, 0xFB, 0xD0
        };

        // https://gist.github.com/TheOfficialFloW/30c90b95c35623dda8a367996b4c08b8
        /// <summary>
        /// Get the RIF name associated with a specific AID.
        /// </summary>
        /// <param name="mode">The mode to use (0 = bounded, 1 = fixed).</param>
        /// <param name="aid">The user's 64-bit PSN Account ID.</param>
        /// <returns>The RIF name.</returns>
        public static string GetRifName(UInt64 mode, UInt64 aid)
        {
            if (!BitConverter.IsLittleEndian)
            {
                // Values must be copied in little endian mode
                aid = SwapBytes(aid);
                mode = SwapBytes(mode);
            }
            byte[] data = new byte[0x10];
            Buffer.BlockCopy(BitConverter.GetBytes(mode), 0, data, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(aid), 0, data, 8, 8);

            return BitConverter.ToString(AesEncrypt(data, rif_name_key), 0, 0x10)
                .Replace("-", "").ToLower() + ".rif";
        }

        /// <summary>
        /// Get the AID from a RIF name.
        /// </summary>
        /// <param name="rif_name">The RIF name.</param>
        /// <returns>The 64-bit AID value (as per ux0's 'id.dat')</returns>
        public static UInt64 GetAidFromRifName(string rif_name)
        {
            byte[] data = AesDecrypt(HexStringToByteArray(rif_name.Split('.').First()), rif_name_key);
            if (!MemCmp(data, new byte[7], 1))
                throw new ApplicationException($"Decoded RIF mode 0x'{GetLe64(data):X8}' does not match 0 or 1");
            return GetLe64(data, 8);
        }

        /// <summary>
        /// Decode a zRIF encoded string to a RIF byte array.
        /// </summary>
        /// <param name="zrif">The zRIF string</param>
        /// <returns>The 512-byte RIF byte array, or null on error</returns>
        public static byte[] DecodeRif(string zrif)
        {
            byte[] input, output = new byte[512];

            try
            {
                // Pad string if not a multiple of 4
                if (zrif.Length % 4 != 0)
                    zrif += new string('=', 4 - (zrif.Length % 4));
                input = Convert.FromBase64String(zrif);
            }
            catch (Exception e) when (e is System.FormatException || e is System.NullReferenceException)
            {
                Console.Error.WriteLine($"[ERROR] {e.Message}");
                return null;
            }

            if (input.Length < 6)
            {
                Console.Error.WriteLine("[ERROR] zRIF length too short");
                return null;
            }
            if (((input[0] << 8) + input[1]) % 31 != 0)
            {
                Console.Error.WriteLine("[ERROR] zRIF header is corrupted");
                return null;
            }
            var inflater = new Inflater();
            inflater.SetInput(input);
            inflater.Inflate(output);
            if (inflater.IsNeedingDictionary)
                inflater.SetDictionary(zrif_dict);
            // TODO: Validate CONTENT_ID format
            return (inflater.Inflate(output, 0, 512) == 512) ? output : null;
        }

        /// <summary>
        /// Encode a RIF byte array to zRIF string.
        /// </summary>
        /// <param name="zrif">The zRIF string.</param>
        /// <returns>The zRIF string or null on error.</returns>
        public static string EncodeRif(byte[] rif)
        {
            byte[] output = new byte[128 + 2];

            if (rif.Length != 512)
            {
                Console.Error.WriteLine("[ERROR] invalid RIF length");
                return null;
            }
            var deflater = new Deflater();
            deflater.SetDictionary(zrif_dict);
            deflater.SetInput(rif);
            deflater.SetLevel(Deflater.BEST_COMPRESSION);
            deflater.SetStrategy(DeflateStrategy.Default);
            deflater.Finish();
            int size = deflater.Deflate(output, 0, output.Length - 2);
            if (!deflater.IsFinished)
            {
                Console.Error.WriteLine("[ERROR] Deflate error");
                return null;
            }
            // Don't have that much control over Window size so our header needs to be adjusted.
            if ((output[0] == 0x78) && (output[1] == 0xF9))
            {
                output[0] = 0x28;
                output[1] = 0xEE;
            }
            // Adjust the size to be a multiple of 3 so that we don't get padding
            return Convert.ToBase64String(output, 0, ((size + 2) / 3) * 3);
        }

        /// <summary>
        /// Retrieve the CONTENT_ID field from a zRIF encoded key.
        /// </summary>
        /// <param name="rif">The 512-byte RIF byte array.</param>
        /// <returns>The CONTENT_ID string or null on error.</returns>
        public static string GetContentIdFromZRif(string zrif)
        {
            if (zrif == null)
                return null;
            var rif = RIF.DecodeRif(zrif);
            if (rif == null)
                return null;
            byte[] content_id = new byte[0x24];
            Buffer.BlockCopy(rif, 0x10, content_id, 0, 0x24);
            try
            {
                return System.Text.ASCIIEncoding.Default.GetString(content_id);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
