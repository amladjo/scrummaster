$ErrorActionPreference = "Stop"

$project = "src/SmBlazor/SmBlazor.csproj"
$out = "docs"

# clean output folder
if (Test-Path $out) {
    Write-Host "Removing existing output folder: $out"
    Remove-Item -Path $out -Recurse -Force
}

# publish
dotnet publish $project -c Release -o $out

Write-Host "Blazor WASM published for GitHub Pages"

