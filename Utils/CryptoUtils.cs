using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoFotos.Utils
{
    public static class CryptoUtils
    {
        private static readonly byte[] key = new byte[32]
        {
            0xD2, 0x7A, 0xE4, 0x1B, 0xC5, 0xF8, 0x3C, 0xA9,
            0x5E, 0xB0, 0x6D, 0xF3, 0x21, 0x8C, 0x47, 0x9F,
            0x13, 0x6B, 0xC8, 0x2E, 0xA4, 0x7D, 0xF1, 0x35,
            0xB8, 0x0F, 0x92, 0xE7, 0x54, 0x3A, 0xC1, 0x68
        };

        // Mantido apenas para compatibilidade com arquivos antigos em AES-CBC.
        private static readonly byte[] iv = new byte[16]
        {
            0xA1, 0x3F, 0xC6, 0x8B, 0xD7, 0x24, 0xE9, 0x5C,
            0x7E, 0xB2, 0x0D, 0xF4, 0x69, 0x13, 0x8A, 0x57
        };

        private static readonly byte[] FormatMagic = Encoding.ASCII.GetBytes("CFG1");
        private const int NonceSize = 12;
        private const int TagSize = 16;

        public static string Encrypt(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(EncryptBytes(plainBytes));
        }

        public static string Decrypt(string cipherText)
        {
            byte[] encryptedBytes = Convert.FromBase64String(cipherText);
            return Encoding.UTF8.GetString(DecryptBytes(encryptedBytes));
        }

        public static byte[] EncryptBytes(byte[] data) => EncryptBytesWithKey(data, key, iv);

        public static byte[] DecryptBytes(byte[] data) => DecryptBytesWithKey(data, key, iv);

        public static byte[] GetKey() => (byte[])key.Clone();

        public static byte[] GetIV() => (byte[])iv.Clone();

        public static byte[] DecryptBytesWithKey(byte[] data, byte[] customKey, byte[] customIV)
        {
            ValidateKey(customKey);

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (IsCurrentFormat(data))
            {
                return DecryptCurrentFormat(data, customKey);
            }

            ValidateIv(customIV);
            return DecryptLegacyFormat(data, customKey, customIV);
        }

        public static byte[] EncryptBytesWithKey(byte[] data, byte[] customKey, byte[] customIV)
        {
            ValidateKey(customKey);

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] ciphertext = new byte[data.Length];
            byte[] tag = new byte[TagSize];

            using (var aes = new AesGcm(customKey, TagSize))
            {
                aes.Encrypt(nonce, data, ciphertext, tag);
            }

            using var output = new MemoryStream(FormatMagic.Length + NonceSize + TagSize + ciphertext.Length);
            output.Write(FormatMagic, 0, FormatMagic.Length);
            output.Write(nonce, 0, nonce.Length);
            output.Write(tag, 0, tag.Length);
            output.Write(ciphertext, 0, ciphertext.Length);
            return output.ToArray();
        }

        private static byte[] DecryptCurrentFormat(byte[] data, byte[] customKey)
        {
            int minimumSize = FormatMagic.Length + NonceSize + TagSize;
            if (data.Length < minimumSize)
            {
                throw new CryptographicException("Arquivo criptografado inválido ou truncado.");
            }

            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[data.Length - minimumSize];

            Buffer.BlockCopy(data, FormatMagic.Length, nonce, 0, nonce.Length);
            Buffer.BlockCopy(data, FormatMagic.Length + nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(data, minimumSize, ciphertext, 0, ciphertext.Length);

            byte[] plaintext = new byte[ciphertext.Length];

            try
            {
                using var aes = new AesGcm(customKey, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext);
                return plaintext;
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException(
                    "Falha ao validar os dados criptografados. A chave pode estar incorreta ou o arquivo pode ter sido alterado.",
                    ex);
            }
        }

        private static byte[] DecryptLegacyFormat(byte[] data, byte[] customKey, byte[] customIV)
        {
            using Aes aes = Aes.Create();
            aes.Key = customKey;
            aes.IV = customIV;

            using var input = new MemoryStream(data);
            using var cryptoStream = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var output = new MemoryStream();
            cryptoStream.CopyTo(output);
            return output.ToArray();
        }

        private static bool IsCurrentFormat(byte[] data)
        {
            if (data.Length < FormatMagic.Length)
            {
                return false;
            }

            for (int index = 0; index < FormatMagic.Length; index++)
            {
                if (data[index] != FormatMagic[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateKey(byte[] customKey)
        {
            if (customKey == null)
            {
                throw new ArgumentNullException(nameof(customKey));
            }

            if (customKey.Length != 32)
            {
                throw new ArgumentException("A chave precisa ter 32 bytes para AES-256.", nameof(customKey));
            }
        }

        private static void ValidateIv(byte[] customIV)
        {
            if (customIV == null)
            {
                throw new ArgumentNullException(nameof(customIV));
            }

            if (customIV.Length != 16)
            {
                throw new ArgumentException("O IV precisa ter 16 bytes para compatibilidade com o formato antigo.", nameof(customIV));
            }
        }
    }
}
