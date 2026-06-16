param(
    [string]$Project = "CryptoFotos.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$ForceRegenerateLogin,
    [switch]$UseExistingLogin,
    [string]$LoginUserName,
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
        throw ("{0} inválido." -f $Prompt)
    }

    return $value
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
        throw "Não foi possível identificar o TargetFramework no arquivo do projeto."
    }

    return $framework
}

function Clear-PublishDirectory {
    param(
        [string]$ProjectRootPath,
        [string]$PublishDirectory
    )

    if (-not (Test-Path -LiteralPath $PublishDirectory)) {
        return
    }

    $resolvedProjectRoot = [System.IO.Path]::GetFullPath($ProjectRootPath.TrimEnd('\') + '\')
    $resolvedPublishDirectory = [System.IO.Path]::GetFullPath($PublishDirectory)

    if (-not $resolvedPublishDirectory.StartsWith($resolvedProjectRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw ("Caminho de publish inválido para limpeza: {0}" -f $resolvedPublishDirectory)
    }

    Write-Log "INFO" ("Limpando publish anterior em {0}" -f $PublishDirectory)
    Remove-Item -LiteralPath $PublishDirectory -Recurse -Force
}

function Assert-PortableSingleExe {
    param(
        [string]$PublishDirectory,
        [string]$ExpectedExePath
    )

    if (-not (Test-Path -LiteralPath $ExpectedExePath)) {
        Write-Log "AVISO" "O build terminou, mas o EXE não foi localizado no caminho esperado."
        Write-Host $ExpectedExePath
        return $false
    }

    $extraFiles = Get-ChildItem -LiteralPath $PublishDirectory -File |
        Where-Object { -not [string]::Equals($_.FullName, $ExpectedExePath, [System.StringComparison]::OrdinalIgnoreCase) }

    if ($extraFiles) {
        $extraNames = ($extraFiles | ForEach-Object { $_.Name }) -join ", "
        throw ("O build gerou arquivos extras ao lado do EXE: {0}. O executável final não está portátil sozinho." -f $extraNames)
    }

    return $true
}

function Assert-SafeLoginFile {
    param(
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "login.txt não encontrado."
    }

    $content = Get-Content -LiteralPath $Path
    if ($content -match '^\s*senhapadrao\s*:\s*sim\s*$') {
        throw "login.txt usa senhapadrao, que foi removido por segurança."
    }

    if ($content -match '^\s*[^#;][^:]+:[^:]+$' -and -not ($content -match '^\s*senhahash\s*:')) {
        throw "login.txt parece estar em formato legado com senha em texto puro."
    }

    $hasUser = $content -match '^\s*(usuario|user)\s*:'
    $hasSalt = $content -match '^\s*salt\s*:'
    $hasHash = $content -match '^\s*(senhahash|passwordhash)\s*:'
    $iterationLine = $content | Where-Object { $_ -match '^\s*(iteracoes|iterations)\s*:' } | Select-Object -First 1

    if (-not ($hasUser -and $hasSalt -and $hasHash -and $iterationLine)) {
        throw "login.txt precisa conter os campos usuario, salt, senhahash e iteracoes."
    }

    $iterationsText = ($iterationLine -split ':', 2)[1].Trim()
    [int]$iterations = 0
    if (-not [int]::TryParse($iterationsText, [ref]$iterations) -or $iterations -lt 210000) {
        throw "login.txt usa iterações PBKDF2 insuficientes."
    }
}

try {
    Write-Log "INFO" ("Iniciando build em {0}" -f $projectRoot)

    if ($ForceRegenerateLogin -and $UseExistingLogin) {
        throw "Use apenas uma opcao entre ForceRegenerateLogin e UseExistingLogin."
    }

    if (-not (Test-Path -LiteralPath $projectPath)) {
        throw ("Projeto '{0}' não encontrado." -f $Project)
    }

    if (-not [string]::Equals($Configuration, "Release", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "O build portátil de segurança deve ser feito em Release."
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "O .NET SDK não foi encontrado no PATH. Instale o .NET 8 SDK ou superior."
    }

    $targetFramework = Get-TargetFramework -CsprojPath $projectPath
    $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    $publishDir = Join-Path $projectRoot ("bin\{0}\{1}\{2}\publish" -f $Configuration, $targetFramework, $RuntimeIdentifier)
    $outputExe = Join-Path $publishDir ("{0}.exe" -f $assemblyName)

    $generateLogin = $false

    if ($UseExistingLogin) {
        if (-not (Test-Path -LiteralPath $loginPath)) {
            throw "Não existe login.txt para reutilizar."
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
            Write-Log "AVISO" "O login.txt atual não possui dica de senha."
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
            throw "Generate-Login.ps1 não foi encontrado."
        }

        $userName = if (-not [string]::IsNullOrWhiteSpace($LoginUserName)) {
            $LoginUserName
        }
        else {
            Read-RequiredValue "Usuario do programa"
        }

        $hint = if ($PSBoundParameters.ContainsKey("LoginHint")) {
            $LoginHint
        }
        else {
            Read-Host "Dica de senha (opcional)"
        }

        Write-Log "INFO" "Gerando login seguro com hash."
        & $generateLoginScript -OutputPath $loginPath -UserName $userName -Hint $hint
        Write-Log "INFO" ("login.txt gerado em {0}" -f $loginPath)
    }

    Assert-SafeLoginFile -Path $loginPath

    Write-Log "INFO" "Executando dotnet restore..."
    Invoke-And-Log { & dotnet restore $projectPath -r $RuntimeIdentifier }
    if ($LASTEXITCODE -ne 0) {
        throw ("Falha no dotnet restore. Veja o log em '{0}'." -f $logFile)
    }

    Clear-PublishDirectory -ProjectRootPath $projectRoot -PublishDirectory $publishDir

    Write-Log "INFO" "Executando dotnet publish..."
    Invoke-And-Log {
        & dotnet publish $projectPath `
            -c $Configuration `
            -r $RuntimeIdentifier `
            -o $publishDir `
            --self-contained true `
            --no-restore `
            /p:PublishSingleFile=true `
            /p:IncludeNativeLibrariesForSelfExtract=true `
            /p:EnableCompressionInSingleFile=true `
            /p:DebugType=None `
            /p:DebugSymbols=false
    }
    if ($LASTEXITCODE -ne 0) {
        throw ("Falha no dotnet publish. Veja o log em '{0}'." -f $logFile)
    }

    if (Assert-PortableSingleExe -PublishDirectory $publishDir -ExpectedExePath $outputExe) {
        Write-Log "INFO" "Build concluido com sucesso."
        Write-Log "INFO" "Pacote portátil válidado: somente o EXE final foi gerado."
        Write-Host ""
        Write-Host "EXE gerado em:"
        Write-Host $outputExe
    }

    exit 0
}
catch {
    Write-Log "ERRO" $_.Exception.Message
    exit 1
}
