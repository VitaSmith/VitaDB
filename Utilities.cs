/*
 * VitaDB - Vita DataBase Updater © 2017 VitaSmith
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace VitaDB
{
    static class Utilities
    {
        public static UInt16 SwapBytes(UInt16 x)
        {
            return (UInt16)((UInt16)((x & 0xff) << 8) | ((x >> 8) & 0xff));
        }

        public static UInt32 SwapBytes(UInt32 x)
        {
            return ((x & 0x000000ff) << 24) +
                   ((x & 0x0000ff00) << 8) +
                   ((x & 0x00ff0000) >> 8) +
                   ((x & 0xff000000) >> 24);
        }

        public static UInt64 SwapBytes(UInt64 value)
        {
            return ((0x00000000000000FF) & (value >> 56)
                   | (0x000000000000FF00) & (value >> 40)
                   | (0x0000000000FF0000) & (value >> 24)
                   | (0x00000000FF000000) & (value >> 8)
                   | (0x000000FF00000000) & (value << 8)
                   | (0x0000FF0000000000) & (value << 24)
                   | (0x00FF000000000000) & (value << 40)
                   | (0xFF00000000000000) & (value << 56));
        }

        public static UInt64 GetBe64(byte[] array, int pos = 0)
        {
            if (BitConverter.IsLittleEndian)
                return SwapBytes(BitConverter.ToUInt64(array, pos));
            else
                return BitConverter.ToUInt64(array, pos);
        }

        public static UInt32 GetBe32(byte[] array, int pos = 0)
        {
            if (BitConverter.IsLittleEndian)
                return SwapBytes(BitConverter.ToUInt32(array, pos));
            else
                return BitConverter.ToUInt32(array, pos);
        }

        public static UInt16 GetBe16(byte[] array, int pos = 0)
        {
            if (BitConverter.IsLittleEndian)
                return SwapBytes(BitConverter.ToUInt16(array, pos));
            else
                return BitConverter.ToUInt16(array, pos);
        }

        public static UInt64 GetLe64(byte[] array, int pos = 0)
        {
            if (!BitConverter.IsLittleEndian)
                return SwapBytes(BitConverter.ToUInt64(array, pos));
            else
                return BitConverter.ToUInt64(array, pos);
        }

        public static UInt32 GetLe32(byte[] array, int pos = 0)
        {
            if (!BitConverter.IsLittleEndian)
                return SwapBytes(BitConverter.ToUInt32(array, pos));
            else
                return BitConverter.ToUInt32(array, pos);
        }

        public static UInt16 GetLe16(byte[] array, int pos = 0)
        {
            if (!BitConverter.IsLittleEndian)
                return SwapBytes(BitConverter.ToUInt16(array, pos));
            else
                return BitConverter.ToUInt16(array, pos);
        }

        /// <summary>
        /// Returns a copy of a region of a byte array.
        /// </summary>
        /// <param name="array">The source byte array.</param>
        /// <param name="pos">The position at which to start the copy.</param>
        /// <param name="length">The length to copy.</param>
        /// <returns>A copy of the requested section, or null on error.</returns>
        public static byte[] MemCpy(byte[] array, int pos, int length)
        {
            if (pos + length > array.Length)
                return null;
            byte[] ret = new byte[length];
            Buffer.BlockCopy(array, pos, ret, 0, length);
            return ret;
        }

        /// <summary>
        /// Returns a copy of a region of a byte array, as a string.
        /// </summary>
        /// <param name="array">The source byte array.</param>
        /// <param name="pos">The position at which to start the copy.</param>
        /// <param name="length">(Optional) The number of character to copy for non NUL-terminated strings.
        /// If this parameter is omitted or zero, the string is assumed to be NUL-terminated.</param>
        /// <returns>The string content.</returns>
        public static string MemCpyStr(byte[] array, int pos, int length = 0)
        {
            string str = "";
            for (int count = 0;
                ((length <= 0) && (array[pos + count] != 0)) || ((length > 0) && (count < length));
                count++)
            {
                str += (char)array[pos + count];
            }
            return str;
        }

        /// <summary>
        /// Compares 2 byte arrays, starting at an optional offset.
        /// </summary>
        /// <param name="array">The array to perform the lookup on.</param>
        /// <param name="constant">The array containing the values to look for.</param>
        /// <param name="start">(Optional) The position at which to start the lookup.</param>
        /// <returns>true if a match is found, false otherwise.</returns>
        public static bool MemCmp(byte[] array, byte[] constant, int start = 0)
        {
            if ((array == null) || (constant == null))
                return false;
            if (start + constant.Length >= array.Length)
                return false;
            for (int i = 0; i < constant.Length; i++)
            {
                if (array[start + i] != constant[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Convert a BCD encoded version to a base 10 integer one (major * 100 + minor).
        /// </summary>
        /// <param name="version">The BCD encoded version.</param>
        /// <returns>The base 10 version.</returns>
        public static UInt32 GetVersionFromBcd(UInt32 version)
        {
            version /= 0x10000;
            version = (version / 0x1000 * 1000)
                + ((version & 0x0F00) / 0x100 * 100)
                + ((version & 0x00F0) / 0x10 * 10)
                + (version & 0x000F);
            return version;
        }

        /// <summary>
        /// Convert a string version to a base 10 integer one (major * 100 + minor).
        /// </summary>
        /// <param name="version">The version string.</param>
        /// <returns>The base 10 version.</returns>
        public static UInt32 GetVersionFromString(string version)
        {
            if (version == null)
                return 0;
            if ((version.Length < 5) || (version[2] != '.'))
            {
                Console.Error.WriteLine($"[WARNING] Unexpected version format for '{version}'");
                return 0;
            }
            return UInt32.Parse(version.Substring(0, 2)) * 100 + UInt32.Parse(version.Substring(3, 2));
        }

        public static int HexVal(char hex)
        {
            int val = (int)hex;
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        /// <summary>
        /// Convert an hex string to a byte array.
        /// </summary>
        /// <param name="hex_string">The string to convert.</param>
        /// <returns>The cmnverted byet array.</returns>
        public static byte[] HexStringToByteArray(string hex_string)
        {
            if (hex_string == null)
                return new byte[0];

            if (hex_string.Length % 2 == 1)
                hex_string = "0" + hex_string;

            byte[] array = new byte[hex_string.Length >> 1];
            for (int i = 0; i < hex_string.Length >> 1; ++i)
                array[i] = (byte)((HexVal(hex_string[i << 1]) << 4) + (HexVal(hex_string[(i << 1) + 1])));
            return array;
        }

        /// <summary>
        /// Convert a byte array to an hex string.
        /// </summary>
        /// <param name="array">The byte array.</param>
        /// <returns>The hex string.</returns>
        public static string ByteArrayToHexString(byte[] array)
        {
            if (Nullable(array).Length == 0)
                return null;
            return BitConverter.ToString(array).Replace("-", "");
        }

        /// <summary>
        /// Hex dump a byte array as a string.
        /// </summary>
        /// <param name="array">The array to dump.</param>
        /// <param name="bytes_per_line">The number of bytes that should be displayed per line.</param>
        /// <returns>A string containing the hex dump.</returns>
        public static string HexDump(byte[] array, int bytes_per_line = 16)
        {
            if (array == null) return "<null>";
            int bytesLength = array.Length;

            char[] HexChars = "0123456789ABCDEF".ToCharArray();

            int firstHexColumn = 8 + 3;

            int firstCharColumn = firstHexColumn + bytes_per_line * 3 + (bytes_per_line - 1) / 8 + 2;

            int lineLength = firstCharColumn + bytes_per_line + Environment.NewLine.Length;

            char[] line = (new String(' ', lineLength - Environment.NewLine.Length) + Environment.NewLine).ToCharArray();
            int expectedLines = (bytesLength + bytes_per_line - 1) / bytes_per_line;
            StringBuilder result = new StringBuilder(expectedLines * lineLength);

            for (int i = 0; i < bytesLength; i += bytes_per_line)
            {
                line[0] = HexChars[(i >> 28) & 0xF];
                line[1] = HexChars[(i >> 24) & 0xF];
                line[2] = HexChars[(i >> 20) & 0xF];
                line[3] = HexChars[(i >> 16) & 0xF];
                line[4] = HexChars[(i >> 12) & 0xF];
                line[5] = HexChars[(i >> 8) & 0xF];
                line[6] = HexChars[(i >> 4) & 0xF];
                line[7] = HexChars[(i >> 0) & 0xF];

                int hexColumn = firstHexColumn;
                int charColumn = firstCharColumn;

                for (int j = 0; j < bytes_per_line; j++)
                {
                    if (j > 0 && (j & 7) == 0) hexColumn++;
                    if (i + j >= bytesLength)
                    {
                        line[hexColumn] = ' ';
                        line[hexColumn + 1] = ' ';
                        line[charColumn] = ' ';
                    }
                    else
                    {
                        byte b = array[i + j];
                        line[hexColumn] = HexChars[(b >> 4) & 0xF];
                        line[hexColumn + 1] = HexChars[b & 0xF];
                        line[charColumn] = (b < 32 ? '·' : (char)b);
                    }
                    hexColumn += 3;
                    charColumn++;
                }
                result.Append(line);
            }
            return result.ToString();
        }

        /// <summary>
        /// An alternative to Count() that returns 0 if the collection is null.
        /// </summary>
        /// <typeparam name="T">The type of the collection to count elements on.</typeparam>
        /// <param name="collection">The collection</param>
        /// <returns>The number or elements.</returns>
        public static int MyCount<T>(this IEnumerable<T> collection)
        {
            return (collection == null) ? 0 : collection.Count();
        }

        /// <summary>
        /// Deserialize an XML Document into an object instance.
        /// </summary>
        /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
        /// <param name="xml_doc">The XML document.</param>
        /// <returns>An instance of the deserialized object.</returns>
        public static T DeserializeFromXML<T>(XDocument xml_doc)
        {
            XmlSerializer xml_serializer = new XmlSerializer(typeof(T));
            return (T)xml_serializer.Deserialize(xml_doc.CreateReader());
        }

        /// <summary>
        /// Serialize an object instance to an XML Document.
        /// </summary>
        /// <typeparam name="T">The type of the object to serialize from.</typeparam>
        /// <param name="instance">The instance of the object to serialize.</param>
        /// <returns>An XML document with the object data.</returns>
        public static XDocument SerializeToXML<T>(T instance)
        {
            XmlSerializer xml_serializer = new XmlSerializer(typeof(T));

            XDocument xml_doc = new XDocument();
            using (var writer = xml_doc.CreateWriter())
            {
                xml_serializer.Serialize(writer, instance);
            }

            return xml_doc;
        }

        /// <summary>
        /// Identity method for an array, that accepts null as parameters.
        /// </summary>
        /// <typeparam name="T">The type of the array.</typeparam>
        /// <param name="array">The array.</param>
        /// <returns>The source array or the empty array if the source array was null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Nullable<T>(T[] array)
        {
            return array ?? Enumerable.Empty<T>().ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<T> Nullable<T>(List<T> list)
        {
            return list ?? new List<T>();
        }

        /// <summary>
        /// Encrypt data through AES using a specific key and optional IV.
        /// </summary>
        /// <param name="data">The data to encrypt.</param>
        /// <param name="key">The AES key to encrypt the data with.</param>
        /// <param name="iv">(Optional) The IV to use. If not provided EBC mode will be used.</param>
        /// <returns>The encrypted data.</returns>
        public static byte[] AesEncrypt(byte[] data, byte[] key, byte[] iv = null)
        {
            using (MemoryStream mem_stream = new MemoryStream())
            using (AesCryptoServiceProvider aes_provider = new AesCryptoServiceProvider())
            {
                aes_provider.Padding = PaddingMode.Zeros;
                aes_provider.Mode = (iv == null) ? CipherMode.ECB : CipherMode.CBC;
                using (CryptoStream crypt_stream = new CryptoStream(mem_stream,
                    aes_provider.CreateEncryptor(key, iv ?? new byte[key.Length]),
                    CryptoStreamMode.Write))
                {
                    crypt_stream.Write(data, 0, data.Length);
                }
                return mem_stream.ToArray();
            }
        }

        /// <summary>
        /// Decrypt data through AES using a specific key and optional IV.
        /// </summary>
        /// <param name="data">The data to encrypt.</param>
        /// <param name="key">The AES key to encrypt the data with.</param>
        /// <param name="iv">(Optional) The IV to use. If not provided EBC mode is used.</param>
        /// <returns>The decrypted data.</returns>
        public static byte[] AesDecrypt(byte[] data, byte[] key, byte[] iv = null)
        {
            using (MemoryStream mem_stream = new MemoryStream())
            using (AesCryptoServiceProvider aes_provider = new AesCryptoServiceProvider())
            {
                aes_provider.Padding = PaddingMode.Zeros;
                aes_provider.Mode = (iv == null) ? CipherMode.ECB : CipherMode.CBC;
                using (CryptoStream crypt_stream = new CryptoStream(mem_stream,
                    aes_provider.CreateDecryptor(key, iv ?? new byte[key.Length]),
                    CryptoStreamMode.Write))
                {
                    crypt_stream.Write(data, 0, data.Length);
                }
                return mem_stream.ToArray();
            }
        }

        /// <summary>
        /// Wait for a key to be pressed.
        /// </summary>
        public static void WaitForKey()
        {
            // Flush the input buffer
            while (Console.KeyAvailable)
                Console.ReadKey(true);
            Console.WriteLine("");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey(true);
        }
    }
}
