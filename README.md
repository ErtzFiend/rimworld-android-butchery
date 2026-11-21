# Android Butchery

Butcher android and droid corpses from Androids for materials instead of meat and leather. Uses the **Butcher creature** bill, not mechanoid shredding.

| Race | Materials |
| --- | --- |
| Worker droid | 112 steel, 4 components |
| Battle droid | 250 steel, 9 components |
| Printed android | 75 plasteel, 4 advanced components |

Other recognized mechanical races yield half their configured butcher products, rounded down.

## Requirements and installation

- RimWorld 1.7
- [Harmony](https://github.com/pardeike/HarmonyRimWorld) (`brrainz.harmony`)
- [Androids](https://steamcommunity.com/sharedfiles/filedetails/?id=2889326293) by ChJees (`ChJees.Androids14`)

Copy the mod folder containing `About/` and `1.7/` into RimWorld's `Mods` folder. Enable it after Harmony and Androids. Package ID: `ertzfiend.androidbutchery`.

## Compatibility

The mod changes butcher recipe filters and replaces butcher products for recognized android races. Other mods changing those filters or products may interact with it. Live-game compatibility is not established here.

## Build and test

Requires the .NET 10 SDK and local RimWorld 1.7 and Harmony assemblies. Set `GameManagedDir` to the directory containing the referenced DLLs:

```sh
dotnet build AndroidButchery.csproj -c Release -p:GameManagedDir=/path/to/assemblies
dotnet run --project tests/AndroidButchery.Tests.csproj -c Release -p:GameManagedDir=/path/to/assemblies
```
