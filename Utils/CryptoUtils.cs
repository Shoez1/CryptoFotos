using System;
using System.IO;
using System.Text;

namespace CryptoFotos.Utils
{
    public static class CryptoUtils
    {
        private static byte[]? importedKey;
        private static byte[]? importedIV;

        public static bool IsCustomKeyActive => importedKey != null && importedIV != null;

        public static string ActiveKeyDescription => IsCustomKeyActive
            ? "Chave Personalizada (CSK3)"
            : "Chave Padrão (Compartilhada)";

        public static string Encrypt(string plainText)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            try
            {
                return Convert.ToBase64String(EncryptBytes(plainBytes));
            }
            finally
            {
                ClearSensitiveBytes(plainBytes);
            }
        }

        public static string Decrypt(string cipherText)
        {
            byte[] encryptedBytes = Convert.FromBase64String(cipherText);
            byte[] plainBytes = DecryptBytes(encryptedBytes);

            try
            {
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally
            {
                ClearSensitiveBytes(encryptedBytes);
                ClearSensitiveBytes(plainBytes);
            }
        }

        public static byte[] EncryptBytes(byte[] data)
        {
            (byte[] keyBytes, byte[] ivBytes) = GetActiveKeyMaterial();

            try
            {
                return EncryptBytesWithKey(data, keyBytes, ivBytes);
            }
            finally
            {
                ClearSensitiveBytes(keyBytes);
                ClearSensitiveBytes(ivBytes);
            }
        }

        public static byte[] DecryptBytes(byte[] data)
        {
            (byte[] keyBytes, byte[] ivBytes) = GetActiveKeyMaterial();

            try
            {
                return DecryptBytesWithKey(data, keyBytes, ivBytes);
            }
            finally
            {
                ClearSensitiveBytes(keyBytes);
                ClearSensitiveBytes(ivBytes);
            }
        }

        public static byte[] EncryptBytesWithKey(byte[] data, byte[] customKey, byte[] customIV)
        {
            return CryptoCommon.SharedCrypto.EncryptBytesWithKey(data, customKey, customIV);
        }

        public static byte[] DecryptBytesWithKey(byte[] data, byte[] customKey, byte[] customIV)
        {
            return CryptoCommon.SharedCrypto.DecryptBytesWithKey(data, customKey, customIV);
        }

        public static byte[] GetKey()
        {
            (byte[] keyBytes, byte[] ivBytes) = GetOrCreateUserKeyMaterial();
            ClearSensitiveBytes(ivBytes);
            return keyBytes;
        }

        public static byte[] GetIV()
        {
            (byte[] keyBytes, byte[] ivBytes) = GetOrCreateUserKeyMaterial();
            ClearSensitiveBytes(keyBytes);
            return ivBytes;
        }

        public static (byte[] Key, byte[] IV) GetOrCreateUserKeyMaterial()
        {
            return CryptoCommon.SharedCrypto.GetOrCreateUserKeyMaterial();
        }

        public static (byte[] Key, byte[] IV) GenerateNewKeyMaterial()
        {
            return CryptoCommon.SharedCrypto.GenerateNewKeyMaterial();
        }

        public static bool HasAuthenticatedFormat(byte[] data)
        {
            return CryptoCommon.SharedCrypto.HasAuthenticatedFormat(data);
        }

        public static byte[] CreateProtectedKeyFileBytes(byte[] keyBytes, byte[] ivBytes)
        {
            return CryptoCommon.SharedCrypto.CreateProtectedKeyFileBytes(keyBytes, ivBytes);
        }

        public static bool IsProtectedKeyFile(byte[] data)
        {
            return CryptoCommon.SharedCrypto.IsProtectedKeyFile(data);
        }

        public static (byte[] Key, byte[] IV) ParseProtectedKeyFile(byte[] keyFileBytes)
        {
            return CryptoCommon.SharedCrypto.ParseProtectedKeyFile(keyFileBytes);
        }

        public static void ImportKeyAndIV(byte[] customKey, byte[] customIV)
        {
            ClearImportedKeyAndIV();
            importedKey = (byte[])customKey.Clone();
            importedIV = (byte[])customIV.Clone();
        }

        public static void ClearImportedKeyAndIV()
        {
            ClearSensitiveBytes(importedKey);
            ClearSensitiveBytes(importedIV);
            importedKey = null;
            importedIV = null;
        }

        public static void ExportKeyAndIV(string filePath)
        {
            (byte[] keyBytes, byte[] ivBytes) = GetActiveKeyMaterial();
            byte[]? keyFileBytes = null;

            try
            {
                keyFileBytes = CreateProtectedKeyFileBytes(keyBytes, ivBytes);
                File.WriteAllText(filePath, Convert.ToBase64String(keyFileBytes), new UTF8Encoding(false));
            }
            finally
            {
                ClearSensitiveBytes(keyBytes);
                ClearSensitiveBytes(ivBytes);
                ClearSensitiveBytes(keyFileBytes);
            }
        }

        public static bool ImportKeyAndIVFromFile(string filePath)
        {
            byte[]? importedBytes = null;

            try
            {
                importedBytes = ReadKeyFileBytes(filePath);
                if (!IsProtectedKeyFile(importedBytes))
                {
                    return false;
                }

                (byte[] keyBytes, byte[] ivBytes) = ParseProtectedKeyFile(importedBytes);
                ImportKeyAndIV(keyBytes, ivBytes);
                ClearSensitiveBytes(keyBytes);
                ClearSensitiveBytes(ivBytes);
                return true;
            }
            finally
            {
                ClearSensitiveBytes(importedBytes);
            }
        }

        public static void ClearSensitiveBytes(byte[]? data)
        {
            CryptoCommon.SharedCrypto.ClearSensitiveBytes(data);
        }

        private static (byte[] Key, byte[] IV) GetActiveKeyMaterial()
        {
            if (importedKey != null && importedIV != null)
            {
                return ((byte[])importedKey.Clone(), (byte[])importedIV.Clone());
            }

            return GetOrCreateUserKeyMaterial();
        }

        private static byte[] ReadKeyFileBytes(string filePath)
        {
            string content = File.ReadAllText(filePath).Trim();

            try
            {
                return Convert.FromBase64String(content);
            }
            catch (FormatException)
            {
                return File.ReadAllBytes(filePath);
            }
        }
    }
}
