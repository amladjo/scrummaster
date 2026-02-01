$ErrorActionPreference = "Stop"

$project = "src/SmBlazor/SmBlazor.csproj"
$out = "docs"

# clean old output
if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

# publish
dotnet publish $project -c Release -o $out

# flatten wwwroot -> docs
if (Test-Path "$out\wwwroot") {
    robocopy "$out\wwwroot" $out /E
    Remove-Item "$out\wwwroot" -Recurse -Force
}

# disable jekyll
New-Item "$out\.nojekyll" -ItemType File -Force | Out-Null

# SPA refresh fix
Copy-Item "$out\index.html" "$out\404.html" -Force

Write-Host "Blazor WASM published for GitHub Pages"
