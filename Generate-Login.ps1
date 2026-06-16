param(
    [string]$OutputPath = "login.txt",
    [string]$UserName,
    [string]$Hint,
    [int]$Iterations = 600000
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
        throw "$Prompt inválido."
    }

    return $value
}

function Read-PasswordValue {
    $securePassword = Read-Host "Senha do programa" -AsSecureString
    $password = [System.Net.NetworkCredential]::new('', $securePassword).Password
    if ([string]::IsNullOrWhiteSpace($password)) {
        throw "Senha inválida."
    }

    return $password
}

function Protect-LoginFileAcl {
    param([string]$Path)

    $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    $system = New-Object System.Security.Principal.SecurityIdentifier("S-1-5-18")
    $administrators = New-Object System.Security.Principal.SecurityIdentifier("S-1-5-32-544")

    $acl = New-Object System.Security.AccessControl.FileSecurity
    $rights = [System.Security.AccessControl.FileSystemRights]::FullControl
    $allow = [System.Security.AccessControl.AccessControlType]::Allow

    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($currentUser, $rights, $allow)))
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($system, $rights, $allow)))
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($administrators, $rights, $allow)))
    $acl.SetAccessRuleProtection($true, $false)
    Set-Acl -LiteralPath $Path -AclObject $acl
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
$PasswordPlaintext = Read-PasswordValue

if ($Iterations -lt 210000) {
    throw "Iteracoes precisa ser 210000 ou maior."
}

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
Protect-LoginFileAcl -Path $outputFilePath

[System.GC]::Collect()

Write-Host "login.txt gerado com hash PBKDF2-SHA256 em $outputFilePath."
