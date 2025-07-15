using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoFotos.Utils
{
    public static class CryptoUtils
    {
        // Chave AES-256 fixa (32 bytes)
        private static readonly byte[] key = new byte[32] {
            0xD2, 0x7A, 0xE4, 0x1B, 0xC5, 0xF8, 0x3C, 0xA9,
            0x5E, 0xB0, 0x6D, 0xF3, 0x21, 0x8C, 0x47, 0x9F,
            0x13, 0x6B, 0xC8, 0x2E, 0xA4, 0x7D, 0xF1, 0x35,
            0xB8, 0x0F, 0x92, 0xE7, 0x54, 0x3A, 0xC1, 0x68
        };
        // IV fixo (16 bytes)
        private static readonly byte[] iv = new byte[16] {
            0xA1, 0x3F, 0xC6, 0x8B, 0xD7, 0x24, 0xE9, 0x5C,
            0x7E, 0xB2, 0x0D, 0xF4, 0x69, 0x13, 0x8A, 0x57
        };


        public static string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    var bytes = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(bytes, 0, bytes.Length);
                    cs.FlushFinalBlock();
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var ms = new MemoryStream(Convert.FromBase64String(cipherText)))
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public static byte[] EncryptBytes(byte[] data)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var ms = new MemoryStream())
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        public static byte[] DecryptBytes(byte[] data)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                using (var ms = new MemoryStream(data))
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var outMs = new MemoryStream())
                {
                    cs.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }
        }
    }
}
