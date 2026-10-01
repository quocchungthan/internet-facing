[CmdletBinding()]
param(
    [string]$SubmodulePath = "",
    [string]$TargetAppDir = "",
    [string]$ServiceName = "SchoolBoardService",
    [int]$BackendPort = 5080,
    [int]$RemotePort = 9999,
    [string]$RemoteHost = "eldervibe",
    [string]$Domain = "lingobridge.eldervibe.dev",
    [switch]$SkipSubmoduleUpdate,
    [switch]$SkipBuild,
    [switch]$SkipTunnel,
    [switch]$SkipService,
    [switch]$RegisterCaddy
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
$publishStagingDir = if ($SkipBuild) {
    $TargetAppDir
} else {
    Join-Path $env:TEMP ("SchoolBoard-publish-" + [guid]::NewGuid().ToString("N"))
}
$migrationBundlePath = Join-Path $publishStagingDir "Translation.Migrations.exe"
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
Write-Host " Backend Port   : $BackendPort (local)" -ForegroundColor Gray
Write-Host " Remote Port    : $RemotePort (VPS)" -ForegroundColor Gray
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

        Write-Host "Running dotnet publish to staging directory '$publishStagingDir'..." -ForegroundColor Yellow
        & dotnet publish -c Release -o "$publishStagingDir" --source https://api.nuget.org/v3/index.json
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
    $wwwrootDir = Join-Path $publishStagingDir "wwwroot"
    if (-not (Test-Path $wwwrootDir)) {
        New-Item -ItemType Directory -Path $wwwrootDir -Force | Out-Null
    }

    $distDir = Join-Path $clientDir "dist"
    Copy-Item -Path "$distDir\*" -Destination $wwwrootDir -Recurse -Force
    Write-Host "Frontend assets copied to '$wwwrootDir'." -ForegroundColor Green

    # Ensure the staged appsettings.json configures the desired backend listening port.
    $stagedAppSettings = Join-Path $publishStagingDir "appsettings.json"
    if (Test-Path $stagedAppSettings) {
        try {
            $settingsJson = Get-Content $stagedAppSettings -Raw | ConvertFrom-Json
            if (-not $settingsJson.Urls -or $settingsJson.Urls -ne "http://127.0.0.1:$BackendPort") {
                $settingsJson | Add-Member -Name "Urls" -Value "http://127.0.0.1:$BackendPort" -MemberType NoteProperty -Force
                $settingsJson | ConvertTo-Json -Depth 10 | Set-Content $stagedAppSettings -Encoding UTF8
                Write-Host "Configured listening Urls in '$stagedAppSettings' to 'http://127.0.0.1:$BackendPort'." -ForegroundColor Green
            }
        } catch {
            throw "Could not configure the staged API listening URL: $_"
        }
    }

} else {
    Write-Host "`n[Step 2-4] Skipping Build and Bundling (-SkipBuild specified)." -ForegroundColor Yellow
}

if (-not (Test-Path $TargetAppDir)) {
    throw "Published application directory not found at '$TargetAppDir'. Remove -SkipBuild or publish the API before continuing."
}
if (-not (Test-Path $migrationBundlePath)) {
    throw "EF migration bundle not found at '$migrationBundlePath'. Remove -SkipBuild and rerun deployment to build it."
}

$productionSettingsPath = Join-Path $TargetAppDir "appsettings.Production.json"
$redisConnection = if ($secretValues.ContainsKey("ConnectionStrings:Redis")) {
    $secretValues["ConnectionStrings:Redis"]
} else {
    "localhost:6379"
}
$engineUrl = if ($secretValues.ContainsKey("Engine:Url")) {
    $secretValues["Engine:Url"]
} else {
    "http://localhost:7443"
}
if ([string]::IsNullOrWhiteSpace($redisConnection)) {
    $redisConnection = "localhost:6379"
}
if ([string]::IsNullOrWhiteSpace($engineUrl)) {
    $engineUrl = "http://localhost:7443"
}
$hasEmailHost = $secretValues.ContainsKey("Email:Host") -and -not [string]::IsNullOrWhiteSpace($secretValues["Email:Host"])
$emailPort = if ($secretValues.ContainsKey("Email:Port") -and -not [string]::IsNullOrWhiteSpace($secretValues["Email:Port"])) {
    $parsedPort = 0
    if (-not [int]::TryParse($secretValues["Email:Port"], [ref]$parsedPort) -or $parsedPort -lt 1 -or $parsedPort -gt 65535) {
        throw "Email:Port must be a valid TCP port from 1 to 65535."
    }
    $parsedPort
} elseif ($hasEmailHost) {
    587
} else {
    1025
}
$emailEnableSsl = if ($secretValues.ContainsKey("Email:EnableSsl") -and -not [string]::IsNullOrWhiteSpace($secretValues["Email:EnableSsl"])) {
    $parsedEnableSsl = $false
    if (-not [bool]::TryParse($secretValues["Email:EnableSsl"], [ref]$parsedEnableSsl)) {
        throw "Email:EnableSsl must be true or false."
    }
    $parsedEnableSsl
} else {
    $hasEmailHost
}
$emailSettings = [ordered]@{
    Host = if ($hasEmailHost) { $secretValues["Email:Host"] } else { "localhost" }
    Port = $emailPort
    EnableSsl = $emailEnableSsl
}
$hasSmtpUser = $secretValues.ContainsKey("Email:UserName") -and -not [string]::IsNullOrWhiteSpace($secretValues["Email:UserName"])
$hasSmtpPassword = $secretValues.ContainsKey("Email:Password") -and -not [string]::IsNullOrWhiteSpace($secretValues["Email:Password"])
if ($hasSmtpUser -ne $hasSmtpPassword) {
    throw "Configure both Email:UserName and Email:Password, or neither."
}
if ($hasSmtpUser) {
    $emailSettings.UserName = $secretValues["Email:UserName"]
    $emailSettings.Password = $secretValues["Email:Password"]
}
$productionSettings = [ordered]@{
    ConnectionStrings = [ordered]@{
        Default = $secretValues["ConnectionStrings:Default"]
        Redis = $redisConnection
    }
    Engine = [ordered]@{
        Url = $engineUrl
        ApiKey = $secretValues["Engine:ApiKey"]
    }
    Auth = [ordered]@{
        SigningKey = $secretValues["Auth:SigningKey"]
        ClientUrl = $secretValues["Auth:ClientUrl"]
    }
    Email = $emailSettings
}
if (-not (Test-Path $productionSettingsPath)) {
    New-Item -ItemType File -Path $productionSettingsPath | Out-Null
}
try {
    $productionSettingsAcl = Get-Acl $productionSettingsPath
    $productionSettingsAcl.SetAccessRuleProtection($true, $false)
    $allowedSids = @(
        "S-1-5-18",
        "S-1-5-32-544",
        [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    ) | Select-Object -Unique
    foreach ($sid in $allowedSids) {
        $identity = New-Object System.Security.Principal.SecurityIdentifier($sid)
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $identity,
            [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.AccessControlType]::Allow
        )
        $productionSettingsAcl.AddAccessRule($rule)
    }
    Set-Acl -Path $productionSettingsPath -AclObject $productionSettingsAcl
} catch {
    Write-Warning "Could not restrict ACL on '$productionSettingsPath': $_"
}
$productionSettings | ConvertTo-Json -Depth 5 | Set-Content -Path $productionSettingsPath -Encoding UTF8
Write-Host "Wrote production secrets to '$productionSettingsPath'." -ForegroundColor Green

if ($publishStagingDir -ne $TargetAppDir) {
    $serviceBeforeDeployment = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($serviceBeforeDeployment -and $serviceBeforeDeployment.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        if ($SkipService) {
            throw "Service '$ServiceName' is running and -SkipService was specified. Stop it manually or rerun without -SkipService before replacing deployed binaries."
        }
        if (-not (Test-IsAdministrator)) {
            throw "Service '$ServiceName' must be stopped before replacing locked binaries. Rerun this script from an elevated PowerShell prompt."
        }
    }
}

# ---------------------------------------------------------------------------
# Step 4.5: Apply Production Database Migrations
# ---------------------------------------------------------------------------
Write-Host "`n[Step 4.5] Applying Production Database Migrations..." -ForegroundColor Cyan
$previousAspNetCoreEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousDotnetEnvironment = $env:DOTNET_ENVIRONMENT
$previousConnectionString = $env:ConnectionStrings__Default
try {
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    $env:DOTNET_ENVIRONMENT = "Production"
    $env:ConnectionStrings__Default = $secretValues["ConnectionStrings:Default"]
    Push-Location $TargetAppDir
    try {
        & $migrationBundlePath
        if ($LASTEXITCODE -ne 0) {
            throw "EF migration bundle failed with exit code $LASTEXITCODE. The API service has not been restarted."
        }
    } finally {
        Pop-Location
    }
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousAspNetCoreEnvironment
    $env:DOTNET_ENVIRONMENT = $previousDotnetEnvironment
    $env:ConnectionStrings__Default = $previousConnectionString
}
Write-Host "Production database migrations applied successfully." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Step 4.6: Replace Published Files
# ---------------------------------------------------------------------------
if ($publishStagingDir -ne $TargetAppDir) {
    Write-Host "`n[Step 4.6] Stopping the service and installing staged files..." -ForegroundColor Cyan
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService -and $existingService.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        if (-not (Test-IsAdministrator)) {
            throw "Service '$ServiceName' must be stopped before replacing locked binaries. Rerun this script from an elevated PowerShell prompt."
        }

        Stop-Service -Name $ServiceName -ErrorAction Stop
        $existingService = Get-Service -Name $ServiceName
        $existingService.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(30))
    }

    Copy-Item -Path (Join-Path $publishStagingDir "*") -Destination $TargetAppDir -Recurse -Force
    Write-Host "Published API and frontend files installed in '$TargetAppDir'." -ForegroundColor Green
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
            $serviceStatus = (Get-Service -Name $ServiceName).Status
            if ($serviceStatus -eq [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
                Write-Host "Starting service '$ServiceName'..." -ForegroundColor Yellow
                Start-Service -Name $ServiceName
                Write-Host "Service '$ServiceName' started successfully." -ForegroundColor Green
            } else {
                Write-Host "Restarting service '$ServiceName'..." -ForegroundColor Yellow
                Restart-Service -Name $ServiceName
                Write-Host "Service '$ServiceName' restarted successfully." -ForegroundColor Green
            }
        }
    }
} else {
    Write-Host "`n[Step 6] Skipping Windows Service management (-SkipService specified)." -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# Step 7: SSH Tunnel to Remote (eldervibe)
# ---------------------------------------------------------------------------
if (-not $SkipTunnel) {
    Write-Host "`n[Step 7] Checking SSH Reverse Tunnel to $RemoteHost (VPS Port $RemotePort -> Local Port $BackendPort)..." -ForegroundColor Cyan

    $tunnelRunning = $false
    try {
        $processes = Get-CimInstance Win32_Process -Filter "Name = 'ssh.exe'" -ErrorAction SilentlyContinue
        foreach ($proc in $processes) {
            if ($proc.CommandLine -and $proc.CommandLine -match [regex]::Escape("$RemotePort") -and $proc.CommandLine -match [regex]::Escape($RemoteHost)) {
                $tunnelRunning = $true
                break
            }
        }
    } catch {
        $tunnelRunning = $false
    }

    $tunnelCmd = "ssh -N -v -R ${RemotePort}:127.0.0.1:${BackendPort} $RemoteHost"

    if ($tunnelRunning) {
        Write-Host "SSH reverse tunnel to $RemoteHost (port $RemotePort) is already running." -ForegroundColor Green
    } else {
        Write-Host "SSH reverse tunnel is not currently active." -ForegroundColor Yellow
        Write-Host "Tunnel command: $tunnelCmd" -ForegroundColor Yellow

        $sshCmd = Get-Command "ssh" -ErrorAction SilentlyContinue
        if ($sshCmd) {
            try {
                Start-Process -FilePath "ssh" -ArgumentList "-f", "-N", "-R", "${RemotePort}:127.0.0.1:${BackendPort}", $RemoteHost -ErrorAction Stop
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

# ---------------------------------------------------------------------------
# Step 8: Register / Validate Caddy Ingress on Remote (eldervibe)
# ---------------------------------------------------------------------------
if ($RegisterCaddy) {
    Write-Host "`n[Step 8] Registering Caddy Ingress for '$Domain' on '$RemoteHost'..." -ForegroundColor Cyan
    $sshCmd = Get-Command "ssh" -ErrorAction SilentlyContinue
    if ($sshCmd) {
        $caddyFragment = @"
$Domain {
	reverse_proxy 127.0.0.1:$RemotePort
}
"@
        try {
            $remoteScript = "cat > /tmp/$Domain.caddy && (if [ `$EUID -eq 0 ]; then cp /tmp/$Domain.caddy /etc/caddy/sites/$Domain.caddy && caddy validate --config /etc/caddy/Caddyfile && systemctl reload caddy; else sudo cp /tmp/$Domain.caddy /etc/caddy/sites/$Domain.caddy && sudo caddy validate --config /etc/caddy/Caddyfile && sudo systemctl reload caddy; fi) && rm -f /tmp/$Domain.caddy"
            $caddyFragment | & ssh -o BatchMode=yes $RemoteHost "bash -c '$remoteScript'"
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Caddy reverse proxy for '$Domain' registered and reloaded on '$RemoteHost'." -ForegroundColor Green
            } else {
                Write-Warning "Failed to register Caddy reverse proxy on '$RemoteHost' (exit code $LASTEXITCODE)."
            }
        } catch {
            Write-Warning "Error configuring Caddy on remote host '$RemoteHost': $_"
        }
    } else {
        Write-Warning "OpenSSH client not found to configure Caddy on remote host."
    }
}

if ($publishStagingDir -ne $TargetAppDir -and (Test-Path $publishStagingDir)) {
    Remove-Item -LiteralPath $publishStagingDir -Recurse -Force
}

Write-Host "`nDeployment and setup process completed." -ForegroundColor Green
