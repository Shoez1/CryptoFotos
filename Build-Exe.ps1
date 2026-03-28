param(
    [string]$Project = "CryptoFotos.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$ForceRegenerateLogin,
    [switch]$UseExistingLogin,
    [string]$LoginUserName,
    [string]$LoginPasswordPlaintext,
    [string]$LoginHint
)

$ErrorActionPreference = "Stop"

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
        [string]$Prompt
    )

    $value = Read-Host $Prompt
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw ("{0} invalido." -f $Prompt)
    }

    return $value
}

function Read-PasswordValue {
    $securePassword = Read-Host "Senha do programa" -AsSecureString
    $password = [System.Net.NetworkCredential]::new('', $securePassword).Password
    if ([string]::IsNullOrWhiteSpace($password)) {
        throw "Senha invalida."
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

function Get-TargetFramework {
    param(
        [string]$CsprojPath
    )

    [xml]$projectXml = Get-Content -LiteralPath $CsprojPath
    $framework = $projectXml.Project.PropertyGroup.TargetFramework | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($framework)) {
        throw "Nao foi possivel identificar o TargetFramework no arquivo do projeto."
    }

    return $framework
}

try {
    Write-Log "INFO" ("Iniciando build em {0}" -f $projectRoot)

    if ($ForceRegenerateLogin -and $UseExistingLogin) {
        throw "Use apenas uma opcao entre ForceRegenerateLogin e UseExistingLogin."
    }

    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw ("Projeto '{0}' nao encontrado." -f $Project)
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "O .NET SDK nao foi encontrado no PATH. Instale o .NET 8 SDK ou superior."
    }

    $targetFramework = Get-TargetFramework -CsprojPath $projectPath
    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    $publishDir = Join-Path $projectRoot ("bin\{0}\{1}\{2}\publish" -f $Configuration, $targetFramework, $RuntimeIdentifier)
    $outputExe = Join-Path $publishDir ("{0}.exe" -f $assemblyName)

    $generateLogin = $false

    if ($UseExistingLogin) {
        if (-not (Test-Path -LiteralPath $loginPath)) {
            throw "Nao existe login.txt para reutilizar."
        }

        Write-Log "INFO" "Usando login.txt existente por parametro."
    }
    elseif ($ForceRegenerateLogin) {
        $generateLogin = $true
        Write-Log "INFO" "Regenerando login.txt por parametro."
    }
    elseif (Test-Path -LiteralPath $loginPath) {
        $hasHint = Select-String -Path $loginPath -Pattern '^\s*dicadesenha:' -Quiet
        if ($hasHint) {
            Write-Log "INFO" "login.txt atual encontrado com dica de senha."
        }
        else {
            Write-Log "AVISO" "O login.txt atual nao possui dica de senha."
        }

        $overwriteLogin = Read-Host "Deseja sobrescrever o login.txt atual e gerar um novo? [S/N]"
        if ($overwriteLogin -match '^(s|sim)$') {
            $generateLogin = $true
        }
        else {
            Write-Log "INFO" "Usando login.txt existente."
        }
    }
    else {
        $generateLogin = $true
    }

    if ($generateLogin) {
        if (-not (Test-Path -LiteralPath $generateLoginScript)) {
            throw "Generate-Login.ps1 nao foi encontrado."
        }

        $userName = if (-not [string]::IsNullOrWhiteSpace($LoginUserName)) {
            $LoginUserName
        }
        else {
            Read-RequiredValue "Usuario do programa"
        }

        $passwordPlaintext = if (-not [string]::IsNullOrWhiteSpace($LoginPasswordPlaintext)) {
            $LoginPasswordPlaintext
        }
        else {
            Read-PasswordValue
        }

        $hint = if ($PSBoundParameters.ContainsKey("LoginHint")) {
            $LoginHint
        }
        else {
            Read-Host "Dica de senha (opcional)"
        }

        Write-Log "INFO" "Gerando login seguro com hash."
        & $generateLoginScript -OutputPath $loginPath -UserName $userName -PasswordPlaintext $passwordPlaintext -Hint $hint
        Write-Log "INFO" ("login.txt gerado em {0}" -f $loginPath)
    }

    Write-Log "INFO" "Executando dotnet restore..."
    Invoke-And-Log { & dotnet restore $projectPath -r $RuntimeIdentifier }
    if ($LASTEXITCODE -ne 0) {
        throw ("Falha no dotnet restore. Veja o log em '{0}'." -f $logFile)
    }

    Write-Log "INFO" "Executando dotnet publish..."
    Invoke-And-Log {
        & dotnet publish $projectPath `
            -c $Configuration `
            -r $RuntimeIdentifier `
            --self-contained true `
            --no-restore `
            /p:PublishSingleFile=true `
            /p:EnableCompressionInSingleFile=true `
            /p:DebugType=None `
            /p:DebugSymbols=false
    }
    if ($LASTEXITCODE -ne 0) {
        throw ("Falha no dotnet publish. Veja o log em '{0}'." -f $logFile)
    }

    if (Test-Path -LiteralPath $outputExe) {
        Write-Log "INFO" "Build concluido com sucesso."
        Write-Host ""
        Write-Host "EXE gerado em:"
        Write-Host $outputExe
    }
    else {
        Write-Log "AVISO" "O build terminou, mas o EXE nao foi localizado no caminho esperado."
        Write-Host $outputExe
    }

    exit 0
}
catch {
    Write-Log "ERRO" $_.Exception.Message
    exit 1
}
