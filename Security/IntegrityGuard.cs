using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace CryptoFotos.Security
{
    internal static class IntegrityGuard
    {
        private const string TamperMessage = "O executável foi adulterado. O aplicativo será encerrado.";
        private const string SignatureMagic = "CTXSIGN1";
        private const int MagicLength = 8;
        private const int LengthFieldSize = 4;
        private const int MaximumSignatureLength = 512;

        private static readonly string AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CryptoFotos");

        private static readonly string PinPath = Path.Combine(AppDataDirectory, "integrity.dat");
        private static readonly string PinBackupPath = Path.Combine(AppDataDirectory, "integrity.bak");

        private static long _lastLength;
        private static DateTime _lastWriteUtc;

        public static string ExecutablePath => Environment.ProcessPath ?? Application.ExecutablePath;

        public static void EnforceAtStartup()
        {
            if (IsTampered())
            {
                MessageBox.Show(TamperMessage, "Integridade", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

        public static bool IsTampered()
        {
            string path = ExecutablePath;
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return true;
            }

            if (_lastLength != 0 && _lastLength == info.Length && _lastWriteUtc == info.LastWriteTimeUtc)
            {
                return false;
            }

            byte[] executable = ReadExecutable(path);
            _lastLength = info.Length;
            _lastWriteUtc = info.LastWriteTimeUtc;

            try
            {
                if (IntegrityToken.HasToken)
                {
                    return !VerifySignature(executable);
                }

                return !VerifySelfPin(executable);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(executable);
            }
        }

        private static byte[] ReadExecutable(string path)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes, 0, bytes.Length);
            return bytes;
        }

        private static bool VerifySignature(byte[] executable)
        {
            if (!TryGetOverlay(executable, out byte[] signature, out int overlayStart))
            {
                return false;
            }

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = Convert.FromBase64String(IntegrityToken.ModulusBase64),
                    Exponent = Convert.FromBase64String(IntegrityToken.ExponentBase64)
                });

                byte[] data = new byte[overlayStart];
                Buffer.BlockCopy(executable, 0, data, 0, overlayStart);

                try
                {
                    return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(data);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetOverlay(byte[] executable, out byte[] signature, out int overlayStart)
        {
            signature = Array.Empty<byte>();
            overlayStart = 0;

            int minimumSize = MagicLength + LengthFieldSize + 1;
            if (executable.Length < minimumSize)
            {
                return false;
            }

            int lengthFieldOffset = executable.Length - LengthFieldSize;
            int signatureLength = BitConverter.ToInt32(executable, lengthFieldOffset);
            if (signatureLength <= 0 || signatureLength > MaximumSignatureLength)
            {
                return false;
            }

            overlayStart = lengthFieldOffset - signatureLength - MagicLength;
            if (overlayStart < 0)
            {
                return false;
            }

            string magic = Encoding.ASCII.GetString(executable, overlayStart, MagicLength);
            if (!string.Equals(magic, SignatureMagic, StringComparison.Ordinal))
            {
                return false;
            }

            signature = new byte[signatureLength];
            Buffer.BlockCopy(executable, overlayStart + MagicLength, signature, 0, signatureLength);
            return true;
        }

        private static bool VerifySelfPin(byte[] executable)
        {
            byte[] hash = SHA256.HashData(executable);

            try
            {
                byte[]? pin = ReadPin(PinPath);
                if (pin != null)
                {
                    if (FixedTimeEquals(hash, pin))
                    {
                        return true;
                    }

                    byte[]? backup = ReadPin(PinBackupPath);
                    if (backup != null && FixedTimeEquals(hash, backup))
                    {
                        WritePin(PinPath, hash);
                        return true;
                    }

                    return false;
                }

                byte[]? existingBackup = ReadPin(PinBackupPath);
                if (existingBackup != null && FixedTimeEquals(hash, existingBackup))
                {
                    WritePin(PinPath, hash);
                    return true;
                }

                WritePin(PinPath, hash);
                WritePin(PinBackupPath, hash);
                return true;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(hash);
            }
        }

        private static byte[]? ReadPin(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                using (FileStream stream = File.OpenRead(path))
                {
                    var bytes = new byte[32];
                    if (stream.Length != bytes.Length)
                    {
                        return null;
                    }

                    stream.ReadExactly(bytes, 0, bytes.Length);
                    return bytes;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void WritePin(string path, byte[] hash)
        {
            try
            {
                Directory.CreateDirectory(AppDataDirectory);
                string tempPath = path + ".tmp";
                File.WriteAllBytes(tempPath, hash);
                File.Move(tempPath, path, true);
            }
            catch
            {
                // melhor esforço: falha de gravação não encerra o app
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
