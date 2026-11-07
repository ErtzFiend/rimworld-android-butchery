# Android Butchery

**Package ID:** `ertzfiend.androidbutchery`  
**RimWorld Version:** 1.7  
**Dependencies:**
- `brrainz.harmony` (Harmony)
- `ChJees.Androids14` (Androids for RW 1.6 / 1.7)

---

## Behavior

In the *Androids* mod, droids (`ChjDroid`, `ChjBattleDroid`) have `hasMeat: false`. When RimWorld initializes recipes, `Verse.ThingFilter.ResolveReferences` runs on `ButcherCorpseFlesh` with `disallowDoesntProduceMeat = true`, causing droid corpses to be excluded from the butcher table. Mechanoid shredding (`ButcherCorpseMechanoid`) also rejects them because droids are categorized as humanlikes rather than mechanoids, leaving players unable to butcher or scrap droid corpses.

**Android Butchery** resolves this:
1. **Butcherability (Recipe Filter Patch):**
   - Applies a Harmony postfix on `Verse.RecipeDef.ResolveReferences`, gated strictly to `__instance.defName == "ButcherCorpseFlesh"`.
   - Calls `fixedIngredientFilter.SetAllow(corpseDef, true)` and `defaultIngredientFilter.SetAllow(corpseDef, true)` for every corpse def whose inner pawn race carries `Androids.MechanicalPawnProperties` (exact type match) or whose race fleshType is `ChJDroid`.
   - Includes a secondary `[StaticConstructorOnStartup]` pass to re-run the allow loop as a belt-and-suspenders measure for any late-registered or implied defs.
   - Mechanoid shredding (`ButcherCorpseMechanoid`) and standard organic butchering remain completely untouched.

2. **Yield Override (Pawn ButcherProducts Patch):**
   - Applies a Harmony prefix on `Verse.Pawn.ButcherProducts` when the butchered pawn is an android or mechanical droid.
   - Suppresses vanilla butcher outputs (meat, leather, and vanilla fixed butcher products) by returning `false`.
   - Yields clean material stacks representing **half the build/printer cost** (using floor division).

---

## Per-Race Yield Rules

| Race DefName | Source Cost (Androids Mod) | Butchery Yield (Half Cost, Round-Down) |
|---|---|---|
| `ChjDroid` (Worker Droid) | 225 Steel, 8 Industrial Components | **112 Steel**, **4 ComponentIndustrial** |
| `ChjBattleDroid` (Battle Droid) | 500 Steel, 18 Industrial Components | **250 Steel**, **9 ComponentIndustrial** |
| `ChjAndroid` (Printed Android) | 150 Plasteel, 8 Spacer Components (+ Nutrition) | **75 Plasteel**, **4 ComponentSpacer** |

### Calculations and Design Decisions
- **Floor Division:** All material counts divide by 2 with integer round-down:
  - `ChjDroid`: `225 // 2 = 112 Steel`, `8 // 2 = 4 Components`
  - `ChjBattleDroid`: `500 // 2 = 250 Steel`, `18 // 2 = 9 Components`
  - `ChjAndroid`: `150 // 2 = 75 Plasteel`, `8 // 2 = 4 Advanced Components`
- **ChjAndroid Replacement Decision:** In the base *Androids* mod, butchering printed androids produced humanlike meat and `Leather_ChJAndroid`. Android Butchery replaces all meat, leather, and default products with half the high-tier manufacturing cost (75 Plasteel and 4 Spacer Components). Nutrition is discarded as biological slurry expended during printing/breakdown.
- **Generic Mechanical Pawns:** Any custom race carrying the exact `Androids.MechanicalPawnProperties` extension or `ChJDroid` fleshType will salvage `count // 2` for each item in its `def.butcherProducts`.
- **Stack Splitting:** Outputs are automatically partitioned into valid stack sizes respecting `ThingDef.stackLimit` (e.g. 250 steel yields three 75-stacks and one 25-stack).

---

## Limitations

- Requires `brrainz.harmony` and `ChJees.Androids14` loaded prior to this mod.
- Android salvage occurs at the **Butcher Table** (or butcher spot) via the **Butcher creature** bill (`ButcherCorpseFlesh`), not at the Machining table (`ButcherCorpseMechanoid`).

---

## Build and Test Commands

### Build Mod Assembly
```bash
# Debug build (0 warnings, 0 errors)
dotnet build /root/AndroidButchery/AndroidButchery.csproj

# Release build
dotnet build -c Release /root/AndroidButchery/AndroidButchery.csproj
```

### Run Automated Tests
```bash
dotnet run --project /root/AndroidButchery/tests/AndroidButchery.Tests.csproj
```
