# Drip Swapper

A Windows skin swapper written in C# (WPF, .NET 10). It swaps cosmetics in Fortnite, Rocket League and Fall Guys, and includes a custom model maker that builds `.dripFN` / `.dripRL` / `.dripFG` packs from FBX, GLB, OBJ and other formats. Retrac (OG server) and Project Stellar (Chapter 2 Season 2 and older) have their own windows.

Use it offline only. It does not hide anything from anti-cheat.

## Build

Needs the .NET 10 SDK on Windows.

```
dotnet publish Drip.App/DripSwapper.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -o out
```

## Layout

- `Drip.App` - the WPF app (windows, themes, icons)
- `Drip.Core` - pak/container readers and writers, swap engines, model fitting
- `libs` - third-party DLLs the projects reference (CUE4Parse, AssetsTools.NET, AssimpNet, ...)

## Notes

- Part of this code was recovered from a compiled build, so some variable names (`text6`, `flag14`) are not original.
- The bundled model packs (`Drip.App/Drips`) and build looks (`Drip.App/Looks`) are not in this repo because they contain other people's work. Put your own `.dripFN` files in those folders and they get bundled.
- `.blend` import needs Blender installed (the app asks it to export a `.glb`). If that fails, export a `.glb` or `.fbx` yourself.
