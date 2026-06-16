using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoCommon
{
    public static class SharedCrypto
    {
        private static readonly byte[] DataMagic = Encoding.ASCII.GetBytes("CSG3");
        private static readonly byte[] KeyFileMagic = Encoding.ASCII.GetBytes("CSK3");
        private static readonly byte[] FileKeyInfo = Encoding.ASCII.GetBytes("Crypto Suite shared file encryption v3");
        private static readonly byte[] EmbeddedDefaultKey = new byte[32]
        {
            0xD8, 0xE8, 0x93, 0x6D, 0x51, 0x67, 0x34, 0x72,
            0xB0, 0x92, 0x81, 0xCA, 0x90, 0x96, 0x33, 0xA8,
            0xDE, 0xBA, 0xAB, 0xA0, 0xA7, 0xE6, 0x38, 0x4F,
            0xB2, 0x90, 0x9D, 0xFE, 0xBF, 0xE5, 0x26, 0x50
        };

        private static readonly byte[] EmbeddedDefaultIV = new byte[16]
        {
            0xE3, 0xB8, 0xA9, 0x73, 0x3B, 0x7C, 0x6F, 0x4D,
            0x50, 0x62, 0x05, 0xAB, 0x14, 0x67, 0x38, 0xFE
        };

        private const int KeySize = 32;
        private const int IvSize = 16;
        private const int SaltSize = 16;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        public static (byte[] Key, byte[] IV) GetOrCreateUserKeyMaterial()
        {
            return ((byte[])EmbeddedDefaultKey.Clone(), (byte[])EmbeddedDefaultIV.Clone());
        }

        public static (byte[] Key, byte[] IV) GenerateNewKeyMaterial()
        {
            return (RandomNumberGenerator.GetBytes(KeySize), RandomNumberGenerator.GetBytes(IvSize));
        }

        public static byte[] EncryptBytesWithKey(byte[] data, byte[] keyBytes, byte[] ivBytes)
        {
            ValidateKey(keyBytes);
            ValidateIv(ivBytes);

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] encryptionKey = DeriveFileKey(keyBytes, salt);
            byte[] ciphertext = new byte[data.Length];
            byte[] tag = new byte[TagSize];
            byte[] header = new byte[DataMagic.Length + SaltSize + NonceSize];

            try
            {
                Buffer.BlockCopy(DataMagic, 0, header, 0, DataMagic.Length);
                Buffer.BlockCopy(salt, 0, header, DataMagic.Length, SaltSize);
                Buffer.BlockCopy(nonce, 0, header, DataMagic.Length + SaltSize, NonceSize);

                using (var aes = new AesGcm(encryptionKey, TagSize))
                {
                    aes.Encrypt(nonce, data, ciphertext, tag, header);
                }

                using var output = new MemoryStream(header.Length + tag.Length + ciphertext.Length);
                output.Write(header, 0, header.Length);
                output.Write(tag, 0, tag.Length);
                output.Write(ciphertext, 0, ciphertext.Length);
                return output.ToArray();
            }
            finally
            {
                ClearSensitiveBytes(salt);
                ClearSensitiveBytes(nonce);
                ClearSensitiveBytes(encryptionKey);
                ClearSensitiveBytes(ciphertext);
                ClearSensitiveBytes(tag);
                ClearSensitiveBytes(header);
            }
        }

        public static byte[] DecryptBytesWithKey(byte[] data, byte[] keyBytes, byte[] ivBytes)
        {
            ValidateKey(keyBytes);
            ValidateIv(ivBytes);

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            int headerSize = DataMagic.Length + SaltSize + NonceSize;
            int minimumSize = headerSize + TagSize;
            if (data.Length < minimumSize || !IsFormat(data, DataMagic))
            {
                throw new CryptographicException("Arquivo fora do padrão CSG3 ou truncado.");
            }

            byte[] header = new byte[headerSize];
            byte[] salt = new byte[SaltSize];
            byte[] nonce = new byte[NonceSize];
            byte[] tag = new byte[TagSize];
            byte[] ciphertext = new byte[data.Length - minimumSize];
            byte[] encryptionKey = Array.Empty<byte>();
            byte[] plaintext = new byte[ciphertext.Length];

            Buffer.BlockCopy(data, 0, header, 0, header.Length);
            Buffer.BlockCopy(data, DataMagic.Length, salt, 0, salt.Length);
            Buffer.BlockCopy(data, DataMagic.Length + salt.Length, nonce, 0, nonce.Length);
            Buffer.BlockCopy(data, headerSize, tag, 0, tag.Length);
            Buffer.BlockCopy(data, minimumSize, ciphertext, 0, ciphertext.Length);

            try
            {
                encryptionKey = DeriveFileKey(keyBytes, salt);
                using var aes = new AesGcm(encryptionKey, TagSize);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, header);
                return plaintext;
            }
            catch (CryptographicException ex)
            {
                throw new CryptographicException(
                    "Falha ao validar os dados criptografados. A chave pode estar incorreta ou o arquivo pode ter sido alterado.",
                    ex);
            }
            finally
            {
                ClearSensitiveBytes(header);
                ClearSensitiveBytes(salt);
                ClearSensitiveBytes(nonce);
                ClearSensitiveBytes(tag);
                ClearSensitiveBytes(ciphertext);
                ClearSensitiveBytes(encryptionKey);
            }
        }

        public static bool HasAuthenticatedFormat(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return IsFormat(data, DataMagic);
        }

        public static byte[] CreateProtectedKeyFileBytes(byte[] keyBytes, byte[] ivBytes)
        {
            ValidateKey(keyBytes);
            ValidateIv(ivBytes);

            byte[] output = new byte[KeyFileMagic.Length + KeySize + IvSize];
            Buffer.BlockCopy(KeyFileMagic, 0, output, 0, KeyFileMagic.Length);
            Buffer.BlockCopy(keyBytes, 0, output, KeyFileMagic.Length, KeySize);
            Buffer.BlockCopy(ivBytes, 0, output, KeyFileMagic.Length + KeySize, IvSize);
            return output;
        }

        public static bool IsProtectedKeyFile(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return data.Length == KeyFileMagic.Length + KeySize + IvSize
                && IsFormat(data, KeyFileMagic);
        }

        public static (byte[] Key, byte[] IV) ParseProtectedKeyFile(byte[] keyFileBytes)
        {
            if (keyFileBytes == null)
            {
                throw new ArgumentNullException(nameof(keyFileBytes));
            }

            int expectedSize = KeyFileMagic.Length + KeySize + IvSize;
            if (keyFileBytes.Length != expectedSize || !IsFormat(keyFileBytes, KeyFileMagic))
            {
                throw new InvalidOperationException("Arquivo de chave fora do padrão CSK3.");
            }

            byte[] keyBytes = new byte[KeySize];
            byte[] ivBytes = new byte[IvSize];
            Buffer.BlockCopy(keyFileBytes, KeyFileMagic.Length, keyBytes, 0, keyBytes.Length);
            Buffer.BlockCopy(keyFileBytes, KeyFileMagic.Length + KeySize, ivBytes, 0, ivBytes.Length);
            return (keyBytes, ivBytes);
        }

        public static void ClearSensitiveBytes(byte[]? data)
        {
            if (data != null && data.Length > 0)
            {
                CryptographicOperations.ZeroMemory(data);
            }
        }

        private static byte[] DeriveFileKey(byte[] rootKey, byte[] salt)
        {
            return HkdfSha256(rootKey, salt, FileKeyInfo, KeySize);
        }

        private static byte[] HkdfSha256(byte[] inputKeyMaterial, byte[] salt, byte[] info, int outputLength)
        {
            using var extract = new HMACSHA256(salt);
            byte[] pseudoRandomKey = extract.ComputeHash(inputKeyMaterial);

            try
            {
                using var expand = new HMACSHA256(pseudoRandomKey);
                byte[] output = new byte[outputLength];
                byte[] previousBlock = Array.Empty<byte>();
                int bytesWritten = 0;
                byte counter = 1;

                while (bytesWritten < outputLength)
                {
                    byte[] blockInput = new byte[previousBlock.Length + info.Length + 1];
                    Buffer.BlockCopy(previousBlock, 0, blockInput, 0, previousBlock.Length);
                    Buffer.BlockCopy(info, 0, blockInput, previousBlock.Length, info.Length);
                    blockInput[blockInput.Length - 1] = counter;

                    byte[] currentBlock = expand.ComputeHash(blockInput);
                    int bytesToCopy = Math.Min(currentBlock.Length, outputLength - bytesWritten);
                    Buffer.BlockCopy(currentBlock, 0, output, bytesWritten, bytesToCopy);

                    ClearSensitiveBytes(previousBlock);
                    ClearSensitiveBytes(blockInput);
                    previousBlock = currentBlock;
                    bytesWritten += bytesToCopy;
                    counter++;
                }

                ClearSensitiveBytes(previousBlock);
                return output;
            }
            finally
            {
                ClearSensitiveBytes(pseudoRandomKey);
            }
        }

        private static bool IsFormat(byte[] data, byte[] magic)
        {
            if (data.Length < magic.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < magic.Length; index++)
            {
                difference |= data[index] ^ magic[index];
            }

            return difference == 0;
        }

        private static void ValidateKey(byte[] keyBytes)
        {
            if (keyBytes == null)
            {
                throw new ArgumentNullException(nameof(keyBytes));
            }

            if (keyBytes.Length != KeySize)
            {
                throw new ArgumentException("A chave precisa ter 32 bytes para AES-256.", nameof(keyBytes));
            }
        }

        private static void ValidateIv(byte[] ivBytes)
        {
            if (ivBytes == null)
            {
                throw new ArgumentNullException(nameof(ivBytes));
            }

            if (ivBytes.Length != IvSize)
            {
                throw new ArgumentException("O IV precisa ter 16 bytes.", nameof(ivBytes));
            }
        }
    }
}
