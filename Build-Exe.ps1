param(
    [string]$Project = "CryptoFotos.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$ForceLogin,
    [string]$UserName,
    [string]$PasswordPlaintext,
    [string]$Hint,
    [switch]$KeepExistingKey,
    [string]$ExportKeyPath,
    [alias("no-pause")]
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"
$hintParameterProvided = $PSBoundParameters.ContainsKey("Hint")

$projectRoot = $PSScriptRoot
$projectPath = Join-Path $projectRoot $Project
$loginPath = Join-Path $projectRoot "login.txt"
$generateLoginScript = Join-Path $projectRoot "Generate-Login.ps1"
$logFile = Join-Path $projectRoot "build-exe.log"

[System.IO.File]::WriteAllText($logFile, "")

function Write-Log {
    param(
        [string]$Level,
        [string]$Message
    )

    $line = "[{0}] {1}" -f $Level, $Message
    Write-Host $line
    Add-Content -Path $logFile -Value $line
}

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
        throw ("{0} inválido." -f $Prompt)
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
        throw "Senha inválida."
    }

    return $password
}

function Invoke-And-Log {
    param(
        [scriptblock]$Action
    )

    & $Action 2>&1 | ForEach-Object {
        $text = $_.ToString()
        Write-Host $text
        Add-Content -Path $logFile -Value $text
    }
}

function Test-LoginFileHasSecureHash {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    $lines = Get-Content -LiteralPath $Path
    $hasUser = $false
    $hasSalt = $false
    $hasHash = $false
    $hasIterations = $false

    foreach ($line in $lines) {
        if ($line -match '^\s*(debug|senhapadrao)\s*:') {
            return $false
        }
        if ($line -match '^\s*(usuario|user)\s*:') {
            $hasUser = $true
        }
        elseif ($line -match '^\s*salt\s*:') {
            $hasSalt = $true
        }
        elseif ($line -match '^\s*(senhahash|passwordhash)\s*:') {
            $hasHash = $true
        }
        elseif ($line -match '^\s*(iteracoes|iterations)\s*:') {
            $hasIterations = $true
        }
    }

    return $hasUser -and $hasSalt -and $hasHash -and $hasIterations
}

function Test-LoginFileHasHint {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $false
    }

    return [bool](Select-String -LiteralPath $Path -Pattern '^\s*dicadesenha\s*:\s*\S+' -Quiet)
}

function Set-LoginFileHint {
    param(
        [string]$Path,
        [string]$HintValue
    )

    if ([string]::IsNullOrWhiteSpace($HintValue)) {
        return $false
    }

    $encoding = New-Object System.Text.UTF8Encoding($false)
    $newLines = New-Object System.Collections.Generic.List[string]
    $hintWritten = $false

    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ($line -match '^\s*dicadesenha\s*:') {
            if (-not $hintWritten) {
                $newLines.Add(("dicadesenha:{0}" -f $HintValue))
                $hintWritten = $true
            }
        }
        else {
            $newLines.Add($line)
        }
    }

    if (-not $hintWritten) {
        $newLines.Add(("dicadesenha:{0}" -f $HintValue))
    }

    [System.IO.File]::WriteAllLines($Path, $newLines, $encoding)
    return $true
}

function Get-ProjectProperty {
    param(
        [xml]$ProjectXml,
        [string]$Name
    )

    $values = @($ProjectXml.Project.PropertyGroup | ForEach-Object { $_.$Name } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($values.Count -eq 0) {
        return $null
    }

    return [string]$values[0]
}

function Format-ByteArrayToCSharp {
    param(
        [byte[]]$Bytes,
        [int]$BytesPerLine = 8
    )

    $rows = New-Object System.Collections.Generic.List[string]
    for ($i = 0; $i -lt $Bytes.Length; $i += $BytesPerLine) {
        $count = [Math]::Min($BytesPerLine, $Bytes.Length - $i)
        $slice = $Bytes[$i..($i + $count - 1)]
        $hexItems = ($slice | ForEach-Object { "0x{0:X2}" -f $_ }) -join ", "
        $rows.Add("            " + $hexItems)
    }
    return ($rows -join ",`r`n")
}

function Export-ProtectedKeyFile {
    param(
        [byte[]]$Key,
        [byte[]]$Iv,
        [string]$OutputPath
    )

    $keyFileMagic = [System.Text.Encoding]::ASCII.GetBytes("CSK3")
    $buffer = New-Object byte[] ($keyFileMagic.Length + 32 + 16)
    [Buffer]::BlockCopy($keyFileMagic, 0, $buffer, 0, $keyFileMagic.Length)
    [Buffer]::BlockCopy($Key, 0, $buffer, $keyFileMagic.Length, 32)
    [Buffer]::BlockCopy($Iv, 0, $buffer, $keyFileMagic.Length + 32, 16)

    $base64 = [Convert]::ToBase64String($buffer)
    $parent = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($parent) -and -not (Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($OutputPath, $base64, $encoding)
    [System.Array]::Clear($buffer, 0, $buffer.Length)
}

function Update-SharedCryptoKey {
    param(
        [string]$SharedCryptoFilePath,
        [byte[]]$NewKey = $null,
        [byte[]]$NewIv = $null
    )

    if (-not (Test-Path -LiteralPath $SharedCryptoFilePath)) {
        throw ("Arquivo '{0}' não encontrado para rotação da chave embutida." -f $SharedCryptoFilePath)
    }

    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    if ($null -eq $NewKey) {
        $NewKey = New-Object byte[] 32
        $rng.GetBytes($NewKey)
    }
    if ($null -eq $NewIv) {
        $NewIv = New-Object byte[] 16
        $rng.GetBytes($NewIv)
    }

    if ($NewKey.Length -ne 32) {
        throw "A chave AES-256 precisa ter 32 bytes."
    }
    if ($NewIv.Length -ne 16) {
        throw "O IV precisa ter 16 bytes."
    }

    $content = [System.IO.File]::ReadAllText($SharedCryptoFilePath)

    $keyPattern = '(?s)[ \t]*private\s+static\s+readonly\s+byte\[\]\s+EmbeddedDefaultKey\s*=\s*new\s+byte\[32\]\s*\{[^}]*\};'
    $ivPattern = '(?s)[ \t]*private\s+static\s+readonly\s+byte\[\]\s+EmbeddedDefaultIV\s*=\s*new\s+byte\[16\]\s*\{[^}]*\};'

    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($content, $keyPattern)) {
        throw "Não foi possível localizar o campo 'EmbeddedDefaultKey' em SharedCrypto.cs."
    }
    if (-not [System.Text.RegularExpressions.Regex]::IsMatch($content, $ivPattern)) {
        throw "Não foi possível localizar o campo 'EmbeddedDefaultIV' em SharedCrypto.cs."
    }

    $formattedKey = Format-ByteArrayToCSharp -Bytes $NewKey
    $formattedIv = Format-ByteArrayToCSharp -Bytes $NewIv

    $keyReplacement = "        private static readonly byte[] EmbeddedDefaultKey = new byte[32]`r`n        {`r`n" + $formattedKey + "`r`n        };"
    $ivReplacement = "        private static readonly byte[] EmbeddedDefaultIV = new byte[16]`r`n        {`r`n" + $formattedIv + "`r`n        };"

    $content = [System.Text.RegularExpressions.Regex]::Replace($content, $keyPattern, $keyReplacement)
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, $ivPattern, $ivReplacement)

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($SharedCryptoFilePath, $content, $encoding)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    $keyHash = [BitConverter]::ToString($sha256.ComputeHash($NewKey)).Replace("-", "")

    return [PSCustomObject]@{
        KeyHash = $keyHash
        Key = $NewKey
        Iv = $NewIv
    }
}

function Write-IntegrityTokenFile {
    param(
        [string]$Path,
        [string]$Modulus,
        [string]$Exponent
    )

    $content = @"
namespace CryptoFotos.Security
{
    // <auto-generated>
    // Escrito por Build-Exe.ps1 com a chave pública da assinatura deste build.
    internal static class IntegrityToken
    {
        public const string ModulusBase64 = "$Modulus";
        public const string ExponentBase64 = "$Exponent";

        public static bool HasToken => ModulusBase64.Length > 0 && ExponentBase64.Length > 0;
    }
}
"@
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $content, $encoding)
}

function Add-IntegrityOverlay {
    param(
        [string]$ExePath,
        [System.Security.Cryptography.RSA]$Rsa
    )

    $exeBytes = [System.IO.File]::ReadAllBytes($ExePath)
    $signature = $Rsa.SignData(
        $exeBytes,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

    # Layout do overlay: [magic 8][assinatura N][tamanho 4] — o campo de tamanho
    # fica no FINAL do arquivo para o IntegrityGuard localizar a assinatura.
    $magic = [System.Text.Encoding]::ASCII.GetBytes("CTXSIGN1")
    $lengthField = [BitConverter]::GetBytes([int]$signature.Length)

    $stream = [System.IO.File]::Open($ExePath, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write)
    try {
        $stream.Write($magic, 0, $magic.Length)
        $stream.Write($signature, 0, $signature.Length)
        $stream.Write($lengthField, 0, $lengthField.Length)
    }
    finally {
        $stream.Dispose()
    }

    return $signature.Length
}

try {
    Write-Log "INFO" ("Iniciando build em {0}" -f $projectRoot)

    if ($Configuration -ne "Release") {
        throw "Build de distribuicao deve usar Configuration=Release."
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "O .NET SDK não foi encontrado no PATH. Instale o .NET SDK usado pelo projeto."
    }

    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw ("Projeto '{0}' não encontrado." -f $Project)
    }

    [xml]$projectXml = Get-Content -LiteralPath $projectPath -Raw
    $targetFramework = Get-ProjectProperty -ProjectXml $projectXml -Name "TargetFramework"
    if ([string]::IsNullOrWhiteSpace($targetFramework)) {
        throw "TargetFramework não encontrado no projeto."
    }

    $assemblyName = Get-ProjectProperty -ProjectXml $projectXml -Name "AssemblyName"
    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    }

    $publishDir = Join-Path $projectRoot ("bin\{0}\{1}\{2}\publish" -f $Configuration, $targetFramework, $RuntimeIdentifier)
    $outputExe = Join-Path $publishDir ("{0}.exe" -f $assemblyName)

    $loginHasSecureHash = Test-LoginFileHasSecureHash -Path $loginPath
    $loginHasHint = Test-LoginFileHasHint -Path $loginPath
    $generateLogin = $ForceLogin -or -not $loginHasSecureHash

    if ($generateLogin) {
        if (-not (Test-Path -LiteralPath $generateLoginScript)) {
            throw "Generate-Login.ps1 não foi encontrado."
        }

        if (Test-Path -LiteralPath $loginPath) {
            Write-Log "AVISO" "login.txt atual não possui credenciais seguras completas ou contem opções legadas; ele será regenerado."
        }

        $userNameValue = Read-RequiredValue -Prompt "Usuario do programa" -CurrentValue $UserName
        $passwordPlaintextValue = Read-PasswordValue -CurrentValue $PasswordPlaintext

        if (-not $hintParameterProvided) {
            $Hint = Read-Host "Dica de senha (opcional)"
            $hintParameterProvided = $true
        }

        Write-Log "INFO" "Gerando login seguro com PBKDF2-SHA256."
        & $generateLoginScript -OutputPath $loginPath -UserName $userNameValue -PasswordPlaintext $passwordPlaintextValue -Hint $Hint
        if (-not $?) {
            throw "Falha ao gerar login.txt."
        }

        $passwordPlaintextValue = $null
        Write-Log "INFO" ("login.txt gerado em {0}" -f $loginPath)
    }
    else {
        if ($loginHasHint) {
            Write-Log "INFO" "login.txt seguro encontrado com dica de senha."
        }
        else {
            Write-Log "AVISO" "login.txt seguro encontrado, mas sem dica de senha."
        }

        $overwriteLogin = Read-Host "login.txt seguro encontrado. Deseja gerar um novo? [S/N]"
        if ($overwriteLogin -match '^(s|sim)$') {
            $userNameValue = Read-RequiredValue -Prompt "Usuario do programa" -CurrentValue $UserName
            $passwordPlaintextValue = Read-PasswordValue -CurrentValue $PasswordPlaintext
            if (-not $hintParameterProvided) {
                $Hint = Read-Host "Dica de senha (opcional)"
                $hintParameterProvided = $true
            }

            Write-Log "INFO" "Gerando novo login seguro com PBKDF2-SHA256."
            & $generateLoginScript -OutputPath $loginPath -UserName $userNameValue -PasswordPlaintext $passwordPlaintextValue -Hint $Hint
            if (-not $?) {
                throw "Falha ao gerar login.txt."
            }

            $passwordPlaintextValue = $null
        }
        else {
            if (-not $loginHasHint) {
                if (-not $hintParameterProvided) {
                    $Hint = Read-Host "Dica de senha (opcional)"
                    $hintParameterProvided = $true
                }

                if (Set-LoginFileHint -Path $loginPath -HintValue $Hint) {
                    Write-Log "INFO" "Dica de senha adicionada ao login.txt existente."
                }
                else {
                    Write-Log "AVISO" "Nenhuma dica de senha foi informada."
                }
            }

            Write-Log "INFO" "Usando login.txt existente."
        }
    }

    Write-Log "INFO" "Executando dotnet restore..."
    Invoke-And-Log { & dotnet restore $projectPath }
    if ($LASTEXITCODE -ne 0) {
        throw ("Falha no dotnet restore. Veja o log em '{0}'." -f $logFile)
    }

    $tokenFilePath = Join-Path $projectRoot "Security\IntegrityToken.g.cs"
    $sharedCryptoPath = Join-Path $projectRoot "Utils\SharedCrypto.cs"
    $previousToken = $null
    $previousSharedCryptoContent = $null
    $signatureBytes = 0
    $signingCompleted = $false

    try {
        if (Test-Path -LiteralPath $tokenFilePath) {
            $previousToken = [System.IO.File]::ReadAllText($tokenFilePath)
        }

        if (Test-Path -LiteralPath $sharedCryptoPath) {
            $previousSharedCryptoContent = [System.IO.File]::ReadAllText($sharedCryptoPath)
        }

        if (-not $KeepExistingKey) {
            Write-Log "INFO" "Gerando nova chave criptográfica compartilhada embutida para tornar este build único..."
            $keyResult = Update-SharedCryptoKey -SharedCryptoFilePath $sharedCryptoPath
            Write-Log "INFO" ("Chave criptográfica compartilhada embutida atualizada em {0}" -f $sharedCryptoPath)
            Write-Log "INFO" ("Fingerprint SHA-256 da nova chave AES-256: {0}" -f $keyResult.KeyHash)

            if (-not [string]::IsNullOrWhiteSpace($ExportKeyPath)) {
                Export-ProtectedKeyFile -Key $keyResult.Key -Iv $keyResult.Iv -OutputPath $ExportKeyPath
                Write-Log "INFO" ("Chave embutida exportada (formato CSK3) para '{0}'." -f $ExportKeyPath)
            }
        }
        else {
            Write-Log "INFO" "Manutenção da chave embutida existente solicitada (-KeepExistingKey)."
        }

        $signingRsa = [System.Security.Cryptography.RSACryptoServiceProvider]::new(2048)
        $publicParams = $signingRsa.ExportParameters($false)
        $modulus = [Convert]::ToBase64String($publicParams.Modulus)
        $exponent = [Convert]::ToBase64String($publicParams.Exponent)
        Write-IntegrityTokenFile -Path $tokenFilePath -Modulus $modulus -Exponent $exponent
        Write-Log "INFO" "Chave de assinatura gerada; token de integridade escrito antes do publish."

        Write-Log "INFO" "Executando dotnet publish..."
        Invoke-And-Log {
            & dotnet publish $projectPath `
                -c $Configuration `
                -r $RuntimeIdentifier `
                --self-contained true `
                /p:PublishSingleFile=true `
                /p:EnableCompressionInSingleFile=true `
                /p:DebugType=None `
                /p:DebugSymbols=false
        }
        if ($LASTEXITCODE -ne 0) {
            throw ("Falha no dotnet publish. Veja o log em '{0}'." -f $logFile)
        }

        if (Test-Path -LiteralPath $outputExe) {
            $signatureBytes = Add-IntegrityOverlay -ExePath $outputExe -Rsa $signingRsa
            $signingCompleted = $true
            Write-Log "INFO" ("Assinatura de integridade aplicada ao EXE ({0} bytes)." -f $signatureBytes)
        }
    }
    finally {
        if ($null -ne $previousToken) {
            [System.IO.File]::WriteAllText($tokenFilePath, $previousToken)
            Write-Log "INFO" "Token de integridade restaurado para o stub de desenvolvimento."
        }
        elseif (Test-Path -LiteralPath $tokenFilePath) {
            Remove-Item -LiteralPath $tokenFilePath -Force
        }
    }

    if (Test-Path -LiteralPath $outputExe) {
        if ($signingCompleted) {
            Write-Log "INFO" "Build concluido com sucesso (EXE assinado com token de integridade)."
        }
        else {
            Write-Log "INFO" "Build concluido com sucesso (sem assinatura de integridade; o EXE usara auto-pin)."
        }
        Write-Host ""
        Write-Host "EXE gerado em:"
        Write-Host $outputExe
    }
    else {
        Write-Log "AVISO" "O build terminou, mas o EXE não foi localizado no caminho esperado."
        Write-Host $outputExe
    }

    exit 0
}
catch {
    Write-Log "ERRO" $_.Exception.Message
    if ($null -ne $previousToken -and (Test-Path -LiteralPath $tokenFilePath)) {
        [System.IO.File]::WriteAllText($tokenFilePath, $previousToken)
    }
    if ($null -ne $previousSharedCryptoContent -and (Test-Path -LiteralPath $sharedCryptoPath)) {
        $encoding = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($sharedCryptoPath, $previousSharedCryptoContent, $encoding)
        Write-Log "AVISO" "Utils\SharedCrypto.cs restaurado para o estado anterior devido à falha no build."
    }
    exit 1
}
