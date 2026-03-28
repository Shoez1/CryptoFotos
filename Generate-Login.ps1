param(
    [string]$OutputPath = "login.txt",
    [string]$UserName,
    [string]$PasswordPlaintext,
    [string]$Hint,
    [int]$Iterations = 210000
)

$ErrorActionPreference = "Stop"

function Read-RequiredValue {
    param(
        [string]$Prompt,
        [string]$CurrentValue
    )

    if (-not [string]::IsNullOrWhiteSpace($CurrentValue)) {
        return $CurrentValue
    }

    $value = Read-Host $Prompt
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "$Prompt invalido."
    }

    return $value
}

function Read-PasswordValue {
    param([string]$CurrentValue)

    if (-not [string]::IsNullOrWhiteSpace($CurrentValue)) {
        return $CurrentValue
    }

    $securePassword = Read-Host "Senha do programa" -AsSecureString
    $password = [System.Net.NetworkCredential]::new('', $securePassword).Password
    if ([string]::IsNullOrWhiteSpace($password)) {
        throw "Senha invalida."
    }

    return $password
}

Add-Type -TypeDefinition @"
using System;
using System.Security.Cryptography;
using System.Text;

public static class CryptoFotosCompat
{
    public static byte[] CreateRandomBytes(int length)
    {
        byte[] bytes = new byte[length];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        return bytes;
    }

    public static byte[] DerivePbkdf2Sha256(string password, byte[] salt, int iterations, int outputLength)
    {
        if (password == null) throw new ArgumentNullException("password");
        if (salt == null) throw new ArgumentNullException("salt");
        if (iterations <= 0) throw new ArgumentOutOfRangeException("iterations");
        if (outputLength <= 0) throw new ArgumentOutOfRangeException("outputLength");

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using (HMACSHA256 hmac = new HMACSHA256(passwordBytes))
            {
                int hashLength = hmac.HashSize / 8;
                int blockCount = (int)Math.Ceiling((double)outputLength / hashLength);
                byte[] derived = new byte[blockCount * hashLength];
                byte[] saltBuffer = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, saltBuffer, 0, salt.Length);

                for (int block = 1; block <= blockCount; block++)
                {
                    saltBuffer[salt.Length] = (byte)(block >> 24);
                    saltBuffer[salt.Length + 1] = (byte)(block >> 16);
                    saltBuffer[salt.Length + 2] = (byte)(block >> 8);
                    saltBuffer[salt.Length + 3] = (byte)block;

                    byte[] u = hmac.ComputeHash(saltBuffer);
                    byte[] t = (byte[])u.Clone();

                    for (int iteration = 1; iteration < iterations; iteration++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int i = 0; i < t.Length; i++)
                        {
                            t[i] ^= u[i];
                        }
                    }

                    Buffer.BlockCopy(t, 0, derived, (block - 1) * hashLength, t.Length);
                }

                byte[] result = new byte[outputLength];
                Buffer.BlockCopy(derived, 0, result, 0, outputLength);
                return result;
            }
        }
        finally
        {
            Array.Clear(passwordBytes, 0, passwordBytes.Length);
        }
    }
}
"@

$UserName = Read-RequiredValue -Prompt "Usuario do programa" -CurrentValue $UserName
$PasswordPlaintext = Read-PasswordValue -CurrentValue $PasswordPlaintext

if ($Hint -eq $null) {
    $Hint = Read-Host "Dica de senha (opcional)"
}

$salt = [CryptoFotosCompat]::CreateRandomBytes(16)
$hash = [CryptoFotosCompat]::DerivePbkdf2Sha256($PasswordPlaintext, $salt, $Iterations, 32)

$lines = @(
    "version:1.5",
    "usuario:$UserName",
    "salt:$([Convert]::ToBase64String($salt))",
    "senhahash:$([Convert]::ToBase64String($hash))",
    "iteracoes:$Iterations"
)

if (-not [string]::IsNullOrWhiteSpace($Hint)) {
    $lines += "dicadesenha:$Hint"
}

$outputDirectory = Split-Path -Parent $OutputPath
$outputFileName = Split-Path -Leaf $OutputPath

if ([string]::IsNullOrWhiteSpace($outputDirectory)) {
    $outputDirectory = (Get-Location).Path
}
elseif (-not (Test-Path -LiteralPath $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $outputDirectory = (Resolve-Path -LiteralPath $outputDirectory).Path
}
else {
    $outputDirectory = (Resolve-Path -LiteralPath $outputDirectory).Path
}

$outputFilePath = Join-Path $outputDirectory $outputFileName
$encoding = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($outputFilePath, $lines, $encoding)

Write-Host "login.txt gerado com hash PBKDF2-SHA256 em $outputFilePath."
