# Package assets

## icon.png

`icon.png` (256×256 PNG) is embedded in every NuGet package via `src/Directory.Build.props`.

The current file is a **generated placeholder** (indigo rounded square with a "K" glyph whose lower arm carries a green success accent). To replace it with official Koras Technologies branding:

1. Export a square PNG, 256×256 or 512×512, ≤ 1 MB (NuGet limit), with transparency preserved.
2. Overwrite `assets/icon.png` (keep the file name — all projects reference this path).
3. Run `dotnet pack -c Release` and verify the icon appears in the package via `unzip -l artifacts/*.nupkg | grep icon.png`.

Do not add per-package icons unless a package genuinely needs distinct branding; if so, override `<PackageIcon>` in that project and add the file to its project directory.
