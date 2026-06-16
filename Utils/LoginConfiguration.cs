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
        public const int DefaultHashIterations = 600000;
        public const int MinimumHashIterations = 210000;
        public const int MinimumSaltSize = 16;
        public const int MinimumHashSize = 32;

        public string? Hint { get; init; }
        public string? UserName { get; init; }
        public byte[]? PasswordSalt { get; init; }
        public byte[]? PasswordHash { get; init; }
        public int HashIterations { get; init; } = DefaultHashIterations;

        public bool ValidateCredentials(string userName, string password)
        {
            if (!string.Equals(userName, UserName, StringComparison.Ordinal))
            {
                return false;
            }

            if (PasswordSalt == null || PasswordHash == null)
            {
                return false;
            }

            byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                PasswordSalt,
                HashIterations,
                HashAlgorithmName.SHA256,
                PasswordHash.Length);

            try
            {
                return CryptographicOperations.FixedTimeEquals(computedHash, PasswordHash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(computedHash);
            }
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
            string? userName = null;
            byte[]? passwordSalt = null;
            byte[]? passwordHash = null;
            int hashIterations = LoginConfiguration.DefaultHashIterations;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) ||
                    line.StartsWith("#", StringComparison.Ordinal) ||
                    line.StartsWith(";", StringComparison.Ordinal))
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
                        throw new InvalidOperationException("O modo senhapadrao foi removido por segurança.");
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
                        if (!int.TryParse(value, out hashIterations) ||
                            hashIterations < LoginConfiguration.MinimumHashIterations)
                        {
                            throw new InvalidOperationException("O valor de iterações do login.txt e inválido.");
                        }
                        break;
                    case "version":
                    case "debug":
                        break;
                    default:
                        throw new InvalidOperationException("Formato legado de login em texto puro não é mais aceito.");
                }
            }

            bool hasHashedCredential = !string.IsNullOrWhiteSpace(userName) &&
                passwordSalt != null &&
                passwordHash != null;

            if (!hasHashedCredential)
            {
                throw new InvalidOperationException("login.txt embutido está vazio ou mal formatado.");
            }

            if (passwordSalt!.Length < LoginConfiguration.MinimumSaltSize)
            {
                throw new InvalidOperationException("O salt do login.txt e muito curto.");
            }

            if (passwordHash!.Length < LoginConfiguration.MinimumHashSize)
            {
                throw new InvalidOperationException("O hash do login.txt e muito curto.");
            }

            return new LoginConfiguration
            {
                Hint = hint,
                UserName = userName,
                PasswordSalt = passwordSalt,
                PasswordHash = passwordHash,
                HashIterations = hashIterations
            };
        }
    }
}
