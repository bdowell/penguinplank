<#
.SYNOPSIS
Applies EF Core migrations as an explicit deployment step, using the SEPARATE migration account.

.DESCRIPTION
The API never migrates on startup and its runtime login cannot change schema. This script generates
an idempotent SQL script from the migrations and applies it with sqlcmd under the migration login
(deploy/sql/01-create-migration-principal.sql). The password is read from environment variable
PP_MIGRATION_PASSWORD (sqlcmd -P is avoided so it never appears on a command line or in logs).

Requires the .NET SDK and the dotnet-ef tool on the build/deploy machine. Always review the generated
script before the first production run. Do not point this at a database you did not intend to change.

.PARAMETER SqlServerInstance  e.g. ".\SQLEXPRESS" (env PP_SQL_INSTANCE)
.PARAMETER Database           Target database (env PP_SQL_DATABASE)
.PARAMETER MigrationLogin     Migration login (env PP_MIGRATION_LOGIN)
.PARAMETER ScriptOnly         Generate and print the script path; do not apply it.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$SqlServerInstance = $env:PP_SQL_INSTANCE,
    [string]$Database = $env:PP_SQL_DATABASE,
    [string]$MigrationLogin = $env:PP_MIGRATION_LOGIN,
    [switch]$ScriptOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$keepScript = $false
$scriptPath = Join-Path ([System.IO.Path]::GetTempPath()) ("pp-migrate-" + [guid]::NewGuid().ToString('N') + '.sql')

try {
    & dotnet ef migrations script --idempotent `
        --project (Join-Path $repoRoot 'src\PenguinPlank.Infrastructure') `
        --startup-project (Join-Path $repoRoot 'src\PenguinPlank.Infrastructure') `
        --output $scriptPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet ef failed (exit $LASTEXITCODE)." }
    Write-Output "Generated $scriptPath"
    if ($ScriptOnly) { $keepScript = $true; return }

    foreach ($required in @('SqlServerInstance', 'Database', 'MigrationLogin')) {
        if ([string]::IsNullOrWhiteSpace((Get-Variable $required).Value)) { throw "$required is required (parameter or PP_* environment variable)." }
    }
    if ([string]::IsNullOrWhiteSpace($env:PP_MIGRATION_PASSWORD)) { throw 'Set PP_MIGRATION_PASSWORD from your secret store.' }

    if ($PSCmdlet.ShouldProcess("$SqlServerInstance/$Database", 'Apply migrations')) {
        $env:SQLCMDPASSWORD = $env:PP_MIGRATION_PASSWORD
        try {
            & sqlcmd -S $SqlServerInstance -d $Database -U $MigrationLogin -b -i $scriptPath
            if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed applying migrations (exit $LASTEXITCODE)." }
        }
        finally { Remove-Item Env:\SQLCMDPASSWORD -ErrorAction SilentlyContinue }
        Write-Output 'Migrations applied.'
    }
}
finally {
    if (-not $keepScript -and (Test-Path -LiteralPath $scriptPath)) { Remove-Item -LiteralPath $scriptPath -Force }
}
