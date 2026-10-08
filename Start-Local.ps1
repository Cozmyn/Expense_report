$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Instalează Docker Desktop / Install Docker Desktop first.' }
if (-not (Test-Path '.env')) {
  $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
  $databaseBytes = New-Object byte[] 32
  $administratorBytes = New-Object byte[] 30
  $rng.GetBytes($databaseBytes)
  $rng.GetBytes($administratorBytes)
  $rng.Dispose()
  $databaseSecret = [Convert]::ToBase64String($databaseBytes)
  $administratorSecret = 'Aa1!' + [Convert]::ToBase64String($administratorBytes)
  $settings = "DATABASE_PASSWORD=$databaseSecret`nADMIN_USERNAME=administrator`nADMIN_EMAIL=admin@localhost.invalid`nADMIN_PASSWORD=$administratorSecret"
  [System.IO.File]::WriteAllText((Join-Path $PSScriptRoot '.env'), $settings, (New-Object System.Text.UTF8Encoding $false))
}
docker compose up -d db scanner
if ($LASTEXITCODE -ne 0) { throw 'Pornirea serviciilor a eșuat / Service startup failed.' }
docker compose --profile setup run --rm init
if ($LASTEXITCODE -ne 0) { throw 'Inițializarea a eșuat / Initialisation failed. Preserve the build error.' }
docker compose up -d web
if ($LASTEXITCODE -ne 0) { throw 'Pornirea aplicației a eșuat / Application startup failed.' }
Write-Host 'Deschide / Open http://localhost:5080. Datele contului sunt în / Credentials are in .env.'
