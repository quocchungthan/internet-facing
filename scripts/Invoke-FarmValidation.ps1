param(
    [ValidateSet('Core', 'Solution')]
    [string]$Scope = 'Solution'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

$target = if ($Scope -eq 'Core') {
    Join-Path $repositoryRoot 'Farm.Core.Tests/Farm.Core.Tests.csproj'
}
else {
    Join-Path $repositoryRoot 'PiggyFarm.slnx'
}

& dotnet test $target --no-restore
exit $LASTEXITCODE