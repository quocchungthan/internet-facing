[CmdletBinding()]
param(
    [string]$SubmodulePath = "",
    [switch]$IncludeUntracked = $true
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($SubmodulePath)) {
    $SubmodulePath = Join-Path $PSScriptRoot "SchoolBoard"
}
$SubmodulePath = [System.IO.Path]::GetFullPath($SubmodulePath)

Write-Host "=== Submodule Update: $SubmodulePath ===" -ForegroundColor Cyan

if (-not (Test-Path $SubmodulePath)) {
    Write-Host "Submodule path '$SubmodulePath' does not exist. Initializing submodule..." -ForegroundColor Yellow
    $rootDir = Join-Path $PSScriptRoot ".."
    & git -C "$rootDir" submodule update --init --recursive "Streams/SchoolBoard"
}

if (Test-Path $SubmodulePath) {
    Write-Host "Checking for uncommitted changes in '$SubmodulePath'..." -ForegroundColor Cyan
    $status = & git -C "$SubmodulePath" status --porcelain 2>&1
    if ($status -and ($status | Out-String).Trim().Length -gt 0) {
        Write-Host "Uncommitted changes detected. Stashing changes..." -ForegroundColor Yellow
        $timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        & git -C "$SubmodulePath" stash save --include-untracked "auto-stash before update $timestamp"
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "git stash exited with code $LASTEXITCODE"
        } else {
            Write-Host "Successfully stashed local changes." -ForegroundColor Green
        }
    } else {
        Write-Host "Working tree in '$SubmodulePath' is clean." -ForegroundColor Green
    }

    Write-Host "Pulling latest revision in '$SubmodulePath'..." -ForegroundColor Cyan
    $currentBranch = (& git -C "$SubmodulePath" rev-parse --abbrev-ref HEAD 2>&1 | Out-String).Trim()
    if ($currentBranch -and $currentBranch -ne "HEAD") {
        Write-Host "Pulling latest commits on branch '$currentBranch'..." -ForegroundColor Yellow
        & git -C "$SubmodulePath" pull --autostash origin $currentBranch
    } else {
        Write-Host "Submodule is in detached HEAD. Running git submodule update --init --recursive --remote..." -ForegroundColor Yellow
        $rootDir = Join-Path $PSScriptRoot ".."
        & git -C "$rootDir" submodule update --init --recursive --remote "Streams/SchoolBoard"
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to pull latest revision in submodule '$SubmodulePath'."
    }
    Write-Host "Submodule '$SubmodulePath' updated to latest revision." -ForegroundColor Green
}

Write-Host "=== Submodule update finished ===" -ForegroundColor Cyan
