$ErrorActionPreference = "Stop"

$project = "src/SmBlazor/SmBlazor.csproj"
$out = "docs"

# publish
dotnet publish $project -c Release -o $out

Write-Host "Blazor WASM published for GitHub Pages"
