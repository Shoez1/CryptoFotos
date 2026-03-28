using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CryptoFotos.Utils
{
    internal sealed class LoginConfiguration
    {
        public const int DefaultHashIterations = 210000;

        public string? Hint { get; init; }
        public bool UseDynamicPassword { get; init; }
        public string? UserName { get; init; }
        public string? LegacyPassword { get; init; }
        public byte[]? PasswordSalt { get; init; }
        public byte[]? PasswordHash { get; init; }
        public int HashIterations { get; init; } = DefaultHashIterations;

        public bool ValidateCredentials(string userName, string password)
        {
            if (UseDynamicPassword)
            {
                return false;
            }

            if (!string.Equals(userName, UserName, StringComparison.Ordinal))
            {
                return false;
            }

            if (PasswordSalt != null && PasswordHash != null)
            {
                byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    PasswordSalt,
                    HashIterations,
                    HashAlgorithmName.SHA256,
                    PasswordHash.Length);

                return CryptographicOperations.FixedTimeEquals(computedHash, PasswordHash);
            }

            return !string.IsNullOrEmpty(LegacyPassword)
                && string.Equals(password, LegacyPassword, StringComparison.Ordinal);
        }

        public string GetExpectedDynamicPassword(DateTime currentTime)
        {
            int nextDay = currentTime.Day == DateTime.DaysInMonth(currentTime.Year, currentTime.Month)
                ? 1
                : currentTime.Day + 1;
            int previousHour = (currentTime.Hour + 23) % 24;
            return $"{nextDay}{previousHour}";
        }
    }

    internal static class LoginConfigurationLoader
    {
        public static LoginConfiguration LoadFromEmbeddedResource()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string? resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("login.txt", StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
            {
                throw new InvalidOperationException("login.txt não foi encontrado como recurso embutido.");
            }

            using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                throw new InvalidOperationException("Não foi possível abrir o recurso embutido login.txt.");
            }

            using var reader = new StreamReader(resourceStream, Encoding.UTF8);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                lines.Add(reader.ReadLine() ?? string.Empty);
            }

            return Parse(lines);
        }

        private static LoginConfiguration Parse(IEnumerable<string> lines)
        {
            string? hint = null;
            bool useDynamicPassword = false;
            string? userName = null;
            string? legacyPassword = null;
            byte[]? passwordSalt = null;
            byte[]? passwordHash = null;
            int hashIterations = LoginConfiguration.DefaultHashIterations;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();

                switch (key.ToLowerInvariant())
                {
                    case "senhapadrao":
                        useDynamicPassword = value.Equals("sim", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "dicadesenha":
                        hint = value;
                        break;
                    case "usuario":
                    case "user":
                        userName = value;
                        break;
                    case "senhahash":
                    case "passwordhash":
                        passwordHash = Convert.FromBase64String(value);
                        break;
                    case "salt":
                        passwordSalt = Convert.FromBase64String(value);
                        break;
                    case "iteracoes":
                    case "iterations":
                        if (!int.TryParse(value, out hashIterations) || hashIterations < 10000)
                        {
                            throw new InvalidOperationException("O valor de iterações do login.txt é inválido.");
                        }
                        break;
                    case "version":
                    case "debug":
                        break;
                    default:
                        if (userName == null && legacyPassword == null)
                        {
                            userName = key;
                            legacyPassword = value;
                        }
                        break;
                }
            }

            if (!useDynamicPassword)
            {
                bool hasHashedCredential = !string.IsNullOrWhiteSpace(userName) && passwordSalt != null && passwordHash != null;
                bool hasLegacyCredential = !string.IsNullOrWhiteSpace(userName) && !string.IsNullOrWhiteSpace(legacyPassword);

                if (!hasHashedCredential && !hasLegacyCredential)
                {
                    throw new InvalidOperationException("login.txt embutido está vazio ou mal formatado.");
                }
            }

            return new LoginConfiguration
            {
                Hint = hint,
                UseDynamicPassword = useDynamicPassword,
                UserName = userName,
                LegacyPassword = legacyPassword,
                PasswordSalt = passwordSalt,
                PasswordHash = passwordHash,
                HashIterations = hashIterations
            };
        }
    }
}
