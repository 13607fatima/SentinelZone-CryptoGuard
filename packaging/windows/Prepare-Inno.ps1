param([Parameter(Mandatory)][string]$Directory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Directory | Out-Null
$file=Join-Path $Directory 'innosetup-6.7.3.exe'
Invoke-WebRequest 'https://files.jrsoftware.org/is/6/innosetup-6.7.3.exe' -OutFile $file
if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732'){throw 'Compiler installer checksum mismatch'}
$sig=Get-AuthenticodeSignature -LiteralPath $file
if($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch 'Pyrsys B.V.'){throw 'Compiler installer signature invalid'}
$p=Start-Process -FilePath $file -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/PORTABLE=1',('/DIR="'+(Join-Path $Directory 'compiler')+'"')) -WindowStyle Hidden -Wait -PassThru
if($p.ExitCode){throw 'Compiler preparation failed'}
Write-Output (Join-Path $Directory 'compiler/ISCC.exe')
