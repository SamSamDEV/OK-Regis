# Publie OK Régis dans le dossier "dist" (autonome : aucun .NET à installer sur l'autre PC).
dotnet publish "$PSScriptRoot\src\OkRegis.App" -c Release -r win-x64 --self-contained true -o "$PSScriptRoot\dist"
Write-Host "Terminé : $PSScriptRoot\dist\OkRegis.App.exe"
