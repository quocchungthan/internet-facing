[CmdletBinding()]
param(
    [string]$SubmodulePath = "",
    [string]$TargetAppDir = "",
    [string]$ServiceName = "SchoolBoardService",
    [int]$BackendPort = 5080,
    [string]$RemoteHost = "eldervibe",
    [switch]$SkipSubmoduleUpdate,
    [switch]$SkipBuild,
    [switch]$SkipTunnel,
    [switch]$SkipService
)

$ErrorActionPreference = "Stop"

# On Windows PowerShell 5.1 $PSScriptRoot can be empty inside param defaults
if ([string]::IsNullOrWhiteSpace($SubmodulePath)) {
    $SubmodulePath = Join-Path $PSScriptRoot "..\Streams\SchoolBoard"
}
if ([string]::IsNullOrWhiteSpace($TargetAppDir)) {
    $TargetAppDir = Join-Path $PSScriptRoot "app"
}

$SubmodulePath = [System.IO.Path]::GetFullPath($SubmodulePath)
$TargetAppDir = [System.IO.Path]::GetFullPath($TargetAppDir)
$apiDir = Join-Path $SubmodulePath "backend\src\Translation.Api"
$infrastructureProject = Join-Path $SubmodulePath "backend\src\Translation.Infrastructure\Translation.Infrastructure.csproj"
$apiProject = Join-Path $apiDir "Translation.Api.csproj"
$migrationBundlePath = Join-Path $TargetAppDir "Translation.Migrations.exe"
$clientDir = Join-Path $SubmodulePath "client"

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " SchoolBoard Deployment & Windows Service Pipeline" -ForegroundColor Cyan
Write-Host " Submodule Path : $SubmodulePath" -ForegroundColor Gray
Write-Host " Target App Dir : $TargetAppDir" -ForegroundColor Gray
Write-Host " Service Name   : $ServiceName" -ForegroundColor Gray
Write-Host " Backend Port   : $BackendPort" -ForegroundColor Gray
Write-Host " Remote Host    : $RemoteHost" -ForegroundColor Gray
Write-Host "==========================================================" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# Step 0: Stash Changes & Pull Latest Submodule Revision
# ---------------------------------------------------------------------------
if (-not $SkipSubmoduleUpdate) {
    Write-Host "`n[Step 0] Stashing Submodule Changes and Pulling Latest Revision..." -ForegroundColor Cyan
    $streamsDir = Join-Path $PSScriptRoot "..\Streams"
    $updateScriptPs1 = Join-Path $streamsDir "Update-Submodules.ps1"
    $updateScriptSh  = Join-Path $streamsDir "update-submodules.sh"

    if (Test-Path $updateScriptPs1) {
        Write-Host "Executing '$updateScriptPs1'..." -ForegroundColor Yellow
        & "$updateScriptPs1" -SubmodulePath "$SubmodulePath"
    } elseif (Test-Path $updateScriptSh) {
        Write-Host "Executing '$updateScriptSh'..." -ForegroundColor Yellow
        $bashCmd = Get-Command "bash" -ErrorAction SilentlyContinue
        if ($bashCmd) {
            & bash "$updateScriptSh" "Streams/SchoolBoard"
        } else {
            & sh "$updateScriptSh" "Streams/SchoolBoard"
        }
    } else {
        Write-Warning "No submodule update script found in Streams folder."
    }
} else {
    Write-Host "`n[Step 0] Skipping Submodule update (-SkipSubmoduleUpdate specified)." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Step 1: Check Required Environment / User Secrets & Docker
# ---------------------------------------------------------------------------
Write-Host "`n[Step 1] Checking Environment, User Secrets, and Docker Containers..." -ForegroundColor Cyan

if (-not (Test-Path $apiDir)) {
    throw "Backend API directory not found at: '$apiDir'"
}

$secretsOutput = & dotnet user-secrets list --project "$apiDir" 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Could not read user-secrets for Translation.Api. Run the deployment from the account that owns the configured secrets."
}

$secretValues = @{}
foreach ($line in $secretsOutput) {
    if ("$line" -match '^\s*(?<key>[^=]+?)\s*=\s*(?<value>.*)$') {
        $secretValues[$Matches.key.Trim()] = $Matches.value
    }
}

$requiredKeys = @(
    "ConnectionStrings:Default",
    "Engine:ApiKey",
    "Auth:SigningKey",
    "Auth:ClientUrl"
)

$missingSecrets = @()
foreach ($key in $requiredKeys) {
    if (-not $secretValues.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($secretValues[$key])) {
        $missingSecrets += $key
    }
}

if ($missingSecrets.Count -gt 0) {
    Write-Host "Missing required user secret(s) in Translation.Api:" -ForegroundColor Red
    foreach ($mKey in $missingSecrets) {
        Write-Host "  dotnet user-secrets set `"$mKey`" `"<value>`" --project src/Translation.Api" -ForegroundColor Yellow
    }
    throw "Configure the required secrets, then rerun deployment."
} else {
    Write-Host "User secrets verified." -ForegroundColor Green
}

$dockerCmd = Get-Command "docker" -ErrorAction SilentlyContinue
if ($dockerCmd) {
    $containers = & docker ps --format "{{.Names}}" 2>$null
    $requiredContainers = @("lingobridge-postgres", "lingobridge-redis", "lingobridge-mailpit")
    $missingContainers = @()
    foreach ($c in $requiredContainers) {
        $found = $false
        foreach ($name in $containers) {
            if ($name -match [regex]::Escape($c)) {
                $found = $true
                break
            }
        }
        if (-not $found) {
            $missingContainers += $c
        }
    }
    if ($missingContainers.Count -gt 0) {
        Write-Warning "Docker containers missing or not running: $($missingContainers -join ', ')"
        Write-Host "Run the following in '$SubmodulePath':" -ForegroundColor Yellow
        Write-Host "  docker compose up -d --wait" -ForegroundColor Yellow
    } else {
        Write-Host "Required Docker containers are running." -ForegroundColor Green
    }
} else {
    Write-Warning "Docker CLI not detected in PATH. Ensure PostgreSQL (5432), Redis (6379), and Mailpit (1025/8025) are running."
}

# ---------------------------------------------------------------------------
# Step 2 & 3: Build & Bundle (Frontend & Backend)
# ---------------------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Host "`n[Step 2] Building Frontend Client..." -ForegroundColor Cyan
    if (-not (Test-Path $clientDir)) {
        throw "Client directory not found at: '$clientDir'"
    }

    Push-Location $clientDir
    try {
        if (-not (Test-Path "node_modules")) {
            Write-Host "node_modules not found. Running npm install..." -ForegroundColor Yellow
            & npm install
            if ($LASTEXITCODE -ne 0) {
                throw "npm install failed with exit code $LASTEXITCODE"
            }
        }

        Write-Host "Building client bundle with npm run build..." -ForegroundColor Yellow
        & npm run build
        if ($LASTEXITCODE -ne 0) {
            throw "npm run build failed with exit code $LASTEXITCODE"
        }

        $distDir = Join-Path $clientDir "dist"
        if (-not (Test-Path $distDir)) {
            throw "Client build succeeded but dist directory was not produced at '$distDir'"
        }
        Write-Host "Frontend build completed successfully." -ForegroundColor Green
    } finally {
        Pop-Location
    }

    Write-Host "`n[Step 3] Publishing Backend API..." -ForegroundColor Cyan
    Push-Location $apiDir
    try {
        if (-not (Test-Path $TargetAppDir)) {
            New-Item -ItemType Directory -Path $TargetAppDir -Force | Out-Null
        }

        Write-Host "Restoring pinned .NET tools..." -ForegroundColor Yellow
        & dotnet tool restore --add-source https://api.nuget.org/v3/index.json
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet tool restore failed with exit code $LASTEXITCODE"
        }

        Write-Host "Running dotnet publish to '$TargetAppDir'..." -ForegroundColor Yellow
        & dotnet publish -c Release -o "$TargetAppDir" --source https://api.nuget.org/v3/index.json
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }
        Write-Host "Backend publish completed successfully." -ForegroundColor Green

        Write-Host "Creating Windows EF migration bundle..." -ForegroundColor Yellow
        $previousRestoreSources = $env:RestoreSources
        try {
            $env:RestoreSources = "https://api.nuget.org/v3/index.json"
            & dotnet ef migrations bundle --no-build --configuration Release --project "$infrastructureProject" --startup-project "$apiProject" --self-contained --target-runtime win-x64 --output "$migrationBundlePath" --force
            if ($LASTEXITCODE -ne 0) {
                throw "EF migration bundle creation failed with exit code $LASTEXITCODE"
            }
        } finally {
            $env:RestoreSources = $previousRestoreSources
        }
        Write-Host "EF migration bundle created at '$migrationBundlePath'." -ForegroundColor Green
    } finally {
        Pop-Location
    }

    # ---------------------------------------------------------------------------
    # Step 4: Bundle Frontend into Backend (wwwroot)
    # ---------------------------------------------------------------------------
    Write-Host "`n[Step 4] Bundling Frontend into Backend wwwroot..." -ForegroundColor Cyan
    $wwwrootDir = Join-Path $TargetAppDir "wwwroot"
    if (-not (Test-Path $wwwrootDir)) {
        New-Item -ItemType Directory -Path $wwwrootDir -Force | Out-Null
    }

    $distDir = Join-Path $clientDir "dist"
    Copy-Item -Path "$distDir\*" -Destination $wwwrootDir -Recurse -Force
    Write-Host "Frontend assets copied to '$wwwrootDir'." -ForegroundColor Green

    # Ensure appsettings.json in target directory configures the desired backend listening port
    $targetAppSettings = Join-Path $TargetAppDir "appsettings.json"
    if (Test-Path $targetAppSettings) {
        try {
            $settingsJson = Get-Content $targetAppSettings -Raw | ConvertFrom-Json
            if (-not $settingsJson.Urls -or $settingsJson.Urls -ne "http://127.0.0.1:$BackendPort") {
                $settingsJson | Add-Member -Name "Urls" -Value "http://127.0.0.1:$BackendPort" -MemberType NoteProperty -Force
                $settingsJson | ConvertTo-Json -Depth 10 | Set-Content $targetAppSettings -Encoding UTF8
                Write-Host "Configured listening Urls in '$targetAppSettings' to 'http://127.0.0.1:$BackendPort'." -ForegroundColor Green
            }
        } catch {
            Write-Warning "Could not update Urls in '$targetAppSettings': $_"
        }
    }

} else {
    Write-Host "`n[Step 2-4] Skipping Build and Bundling (-SkipBuild specified)." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Step 5: Route & Navigation Check (Static Files & SPA Fallback)
# ---------------------------------------------------------------------------
Write-Host "`n[Step 5] Checking Static Files and SPA Fallback Middleware..." -ForegroundColor Cyan
$programCsPath = Join-Path $apiDir "Program.cs"
if (Test-Path $programCsPath) {
    $programContent = Get-Content $programCsPath -Raw
    $hasDefaultFiles = $programContent -match "UseDefaultFiles\s*\("
    $hasStaticFiles  = $programContent -match "UseStaticFiles\s*\("
    $hasFallback     = $programContent -match "MapFallbackToFile\s*\("

    if (-not ($hasDefaultFiles -and $hasStaticFiles -and $hasFallback)) {
        Write-Warning "Translation.Api Program.cs currently only maps /api and does not configure UseDefaultFiles(), UseStaticFiles(), or MapFallbackToFile('index.html'). Direct navigation or reloads on SPA client routes (like /login, /upload) will return 404 until Program.cs adds static files and SPA fallback middleware."
    } else {
        Write-Host "Static files and SPA fallback middleware configuration verified in Program.cs." -ForegroundColor Green
    }
} else {
    Write-Warning "Program.cs not found at '$programCsPath' to verify static files middleware."
}

# ---------------------------------------------------------------------------
# Step 6: Register / Update Windows Service
# ---------------------------------------------------------------------------
if (-not $SkipService) {
    Write-Host "`n[Step 6] Registering or Updating Windows Service '$ServiceName'..." -ForegroundColor Cyan

    $programCsPath = Join-Path $apiDir "Program.cs"
    $hasWindowsService = $false
    if (Test-Path $programCsPath) {
        $hasWindowsService = (Get-Content $programCsPath -Raw) -match "UseWindowsService\s*\("
    }
    if (-not $hasWindowsService) {
        Write-Warning "Translation.Api does not currently call builder.Host.UseWindowsService() from Microsoft.Extensions.Hosting.WindowsServices. Native Windows SCM will time out (Error 1053) unless UseWindowsService() is integrated or a service wrapper like NSSM / WinSW is used."
    }

    $exePath = Join-Path $TargetAppDir "Translation.Api.exe"
    $dllPath = Join-Path $TargetAppDir "Translation.Api.dll"
    if (Test-Path $exePath) {
        $binPath = "`"$exePath`""
    } elseif (Test-Path $dllPath) {
        $binPath = "`"dotnet.exe`" `"$dllPath`""
    } else {
        $binPath = "`"$exePath`""
    }

    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    $isAdmin = Test-IsAdministrator

    if (-not $existingService) {
        if (-not $isAdmin) {
            Write-Warning "Service '$ServiceName' does not exist. Creating a Windows Service requires Administrator privileges."
            Write-Host "Run the following command from an elevated PowerShell prompt:" -ForegroundColor Yellow
            Write-Host "  New-Service -Name `"$ServiceName`" -BinaryPathName `"$binPath`" -DisplayName `"$ServiceName`" -StartupType Automatic" -ForegroundColor Yellow
            Write-Host "  Start-Service -Name `"$ServiceName`"" -ForegroundColor Yellow
        } else {
            Write-Host "Creating service '$ServiceName' pointing to $binPath..." -ForegroundColor Yellow
            New-Service -Name $ServiceName -BinaryPathName $binPath -DisplayName $ServiceName -StartupType Automatic
            Start-Service -Name $ServiceName
            Write-Host "Service '$ServiceName' created and started." -ForegroundColor Green
        }
    } else {
        if (-not $isAdmin) {
            Write-Warning "Service '$ServiceName' exists. Restarting requires Administrator privileges."
            Write-Host "Run the following command from an elevated PowerShell prompt:" -ForegroundColor Yellow
            Write-Host "  Restart-Service -Name `"$ServiceName`"" -ForegroundColor Yellow
        } else {
            Write-Host "Restarting service '$ServiceName'..." -ForegroundColor Yellow
            Restart-Service -Name $ServiceName
            Write-Host "Service '$ServiceName' restarted successfully." -ForegroundColor Green
        }
    }
} else {
    Write-Host "`n[Step 6] Skipping Windows Service management (-SkipService specified)." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Step 7: SSH Tunnel to Remote (eldervibe)
# ---------------------------------------------------------------------------
if (-not $SkipTunnel) {
    Write-Host "`n[Step 7] Checking SSH Reverse Tunnel to $RemoteHost (Port $BackendPort)..." -ForegroundColor Cyan

    $tunnelRunning = $false
    try {
        $processes = Get-CimInstance Win32_Process -Filter "Name = 'ssh.exe'" -ErrorAction SilentlyContinue
        foreach ($proc in $processes) {
            if ($proc.CommandLine -and $proc.CommandLine -match [regex]::Escape("$BackendPort") -and $proc.CommandLine -match [regex]::Escape($RemoteHost)) {
                $tunnelRunning = $true
                break
            }
        }
    } catch {
        $tunnelRunning = $false
    }

    $tunnelCmd = "ssh -f -N -R ${BackendPort}:localhost:${BackendPort} $RemoteHost"

    if ($tunnelRunning) {
        Write-Host "SSH reverse tunnel to $RemoteHost on port $BackendPort is already running." -ForegroundColor Green
    } else {
        Write-Host "SSH reverse tunnel is not currently active." -ForegroundColor Yellow
        Write-Host "Starting SSH tunnel with command: $tunnelCmd" -ForegroundColor Yellow

        $sshCmd = Get-Command "ssh" -ErrorAction SilentlyContinue
        if ($sshCmd) {
            try {
                Start-Process -FilePath "ssh" -ArgumentList "-f", "-N", "-R", "${BackendPort}:localhost:${BackendPort}", $RemoteHost -ErrorAction Stop
                Write-Host "SSH reverse tunnel process initiated." -ForegroundColor Green
            } catch {
                Write-Warning "Failed to start SSH tunnel automatically: $_"
                Write-Host "Run the following command manually:" -ForegroundColor Yellow
                Write-Host "  $tunnelCmd" -ForegroundColor Yellow
            }
        } else {
            Write-Warning "OpenSSH client (ssh.exe) not found in PATH."
            Write-Host "Run the following command once SSH is available:" -ForegroundColor Yellow
            Write-Host "  $tunnelCmd" -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "`n[Step 7] Skipping SSH Tunnel check (-SkipTunnel specified)." -ForegroundColor Yellow
}

Write-Host "`nDeployment and setup process completed." -ForegroundColor Green
