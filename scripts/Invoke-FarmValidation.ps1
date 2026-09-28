param(
    [ValidateSet('Solution')]
    [string]$Scope = 'Solution',
    [switch]$AuditPackages
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot

$targets = @('PiggyFarm.slnx')

foreach ($relativeTarget in $targets) {
    & dotnet test (Join-Path $repositoryRoot $relativeTarget) --no-restore
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

if ($AuditPackages) {
    foreach ($relativeTarget in $targets) {
        & dotnet list (Join-Path $repositoryRoot $relativeTarget) package --vulnerable --include-transitive --no-restore
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
}

exit 0