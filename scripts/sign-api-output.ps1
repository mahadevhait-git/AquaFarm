param(
  [Parameter(Mandatory = $true)]
  [string]$OutputDir
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $OutputDir)) {
  throw "Output directory not found: $OutputDir"
}

$certSubject = "CN=AquaFarm Local Dev Signing"
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -eq $certSubject } | Select-Object -First 1

if (-not $cert) {
  $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $certSubject -CertStoreLocation "Cert:\CurrentUser\My"
  $certPath = Join-Path $env:TEMP "aquafarm-dev-signing.cer"
  Export-Certificate -Cert $cert -FilePath $certPath | Out-Null
  Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
  Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null
  Remove-Item -LiteralPath $certPath -Force -ErrorAction SilentlyContinue
}

$targets = Get-ChildItem -LiteralPath $OutputDir -File | Where-Object { $_.Extension -in ".dll", ".exe" }
foreach ($target in $targets) {
  Set-AuthenticodeSignature -FilePath $target.FullName -Certificate $cert | Out-Null
}
