using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using AndroidButchery;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Androids
{
    public class MechanicalPawnProperties : DefModExtension
    {
    }
}

namespace CustomOther
{
    public class MechanicalPawnProperties : DefModExtension
    {
    }
}

internal static class Program
{
    private static int passes;
    private static int failures;

    private static void Check(bool ok, string name)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (ok) passes++;
        else failures++;
    }

    private static ThingDef MakeThingDef(string defName, bool hasMeat = false)
    {
        var def = (ThingDef)RuntimeHelpers.GetUninitializedObject(typeof(ThingDef));
        def.defName = defName;
        def.virtualDefs = new List<ThingDef>();
        var race = (RaceProperties)RuntimeHelpers.GetUninitializedObject(typeof(RaceProperties));
        race.hasMeat = hasMeat;
        def.race = race;
        return def;
    }

    private static ThingDef MakeCorpseDef(string defName, ThingDef raceDef)
    {
        var corpse = (ThingDef)RuntimeHelpers.GetUninitializedObject(typeof(ThingDef));
        corpse.defName = defName;
        corpse.category = ThingCategory.Item;
        corpse.virtualDefs = new List<ThingDef>();
        corpse.ingestible = new IngestibleProperties { sourceDef = raceDef };
        if (raceDef?.race != null)
        {
            raceDef.race.corpseDef = corpse;
        }
        return corpse;
    }

    private static Pawn MakePawn(ThingDef raceDef)
    {
        var pawn = (Pawn)RuntimeHelpers.GetUninitializedObject(typeof(Pawn));
        pawn.def = raceDef;
        return pawn;
    }

    private static RecipeDef MakeRecipeDef(string defName)
    {
        var recipe = (RecipeDef)RuntimeHelpers.GetUninitializedObject(typeof(RecipeDef));
        recipe.defName = defName;
        recipe.fixedIngredientFilter = new ThingFilter();
        recipe.defaultIngredientFilter = new ThingFilter();
        return recipe;
    }

    private static int Main()
    {
        // 1. Runtime assembly resolving hook for Microsoft.Extensions.*
        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            var path = "/root/.nuget/packages/microsoft.aspnetcore.app.runtime.linux-x64/10.0.14/runtimes/linux-x64/lib/net10.0/" + name.Name + ".dll";
            return File.Exists(path) ? ctx.LoadFromAssemblyPath(path) : null;
        };

        // 2. Initialize the mod (runs Harmony.PatchAll and belt-and-suspenders startup pass)
        RuntimeHelpers.RunClassConstructor(typeof(AndroidButcheryMain).TypeHandle);

        // --- HARMONY PATCH REGISTRATION CHECKS ---

        // Check 1: RecipeDef.ResolveReferences postfix is registered
        var resolveMethod = AccessTools.Method(typeof(RecipeDef), nameof(RecipeDef.ResolveReferences));
        Check(resolveMethod != null, "RecipeDef.ResolveReferences method resolves");

        var resolvePatchInfo = Harmony.GetPatchInfo(resolveMethod);
        bool resolvePostfixRegistered = resolvePatchInfo != null && resolvePatchInfo.Postfixes.Any(p =>
            p.PatchMethod.DeclaringType == typeof(Patch_RecipeDef_ResolveReferences) && p.PatchMethod.Name == "Postfix");
        Check(resolvePostfixRegistered, "Harmony postfix registered on RecipeDef.ResolveReferences");

        // Check 2: Pawn.ButcherProducts prefix is registered
        var butcherMethod = AccessTools.Method(typeof(Pawn), nameof(Pawn.ButcherProducts),
            new[] { typeof(Pawn), typeof(float) });
        Check(butcherMethod != null, "Pawn.ButcherProducts method resolves");

        var butcherPatchInfo = Harmony.GetPatchInfo(butcherMethod);
        bool butcherPrefixRegistered = butcherPatchInfo != null && butcherPatchInfo.Prefixes.Any(p =>
            p.PatchMethod.DeclaringType == typeof(Patch_Pawn_ButcherProducts) && p.PatchMethod.Name == "Prefix");
        Check(butcherPrefixRegistered, "Harmony prefix registered on Pawn.ButcherProducts");

        // --- CLASSIFIER LOGIC CHECKS ---

        // Check 3: null safety
        Check(!AndroidButcheryClassifier.IsButcherableAndroid(null), "IsButcherableAndroid(null) is false");
        Check(!AndroidButcheryClassifier.IsButcherableAndroidRace(null), "IsButcherableAndroidRace(null) is false");
        Check(!AndroidButcheryClassifier.IsAndroidOrDroidCorpseDef(null), "IsAndroidOrDroidCorpseDef(null) is false");
        Check(AndroidButcheryClassifier.GetInnerPawnRaceDef(null) == null, "GetInnerPawnRaceDef(null) is null");

        // Check 4: Organic pawn race is not android
        var humanRace = MakeThingDef("Human", hasMeat: true);
        var humanPawn = MakePawn(humanRace);
        Check(!AndroidButcheryClassifier.IsButcherableAndroid(humanPawn), "Normal human pawn is not android");
        Check(!AndroidButcheryClassifier.IsMechanicalOrDroidRace(humanRace), "Normal human race is not mechanical");

        // Check 5: Known defNames (ChjDroid, ChjBattleDroid, ChjAndroid)
        var droidDef = MakeThingDef("ChjDroid", hasMeat: false);
        var battleDroidDef = MakeThingDef("ChjBattleDroid", hasMeat: false);
        var androidDef = MakeThingDef("ChjAndroid", hasMeat: true);

        Check(AndroidButcheryClassifier.IsButcherableAndroidRace(droidDef), "ChjDroid race is recognized");
        Check(AndroidButcheryClassifier.IsButcherableAndroidRace(battleDroidDef), "ChjBattleDroid race is recognized");
        Check(AndroidButcheryClassifier.IsButcherableAndroidRace(androidDef), "ChjAndroid race is recognized");

        // Check 6: MechanicalPawnProperties modExtension detection (exact type match)
        var customDroidDef = MakeThingDef("CustomWorkerDroid", hasMeat: false);
        customDroidDef.modExtensions = new List<DefModExtension> { new Androids.MechanicalPawnProperties() };
        Check(AndroidButcheryClassifier.HasMechanicalPawnProperties(customDroidDef),
            "HasMechanicalPawnProperties detects Androids.MechanicalPawnProperties");
        Check(AndroidButcheryClassifier.IsMechanicalOrDroidRace(customDroidDef),
            "Custom race with MechanicalPawnProperties is recognized as mechanical");

        // Check 7: Different namespace with same class name is ignored (exact type match)
        var fakeExtDef = MakeThingDef("FakeExtPawn", hasMeat: false);
        fakeExtDef.modExtensions = new List<DefModExtension> { new CustomOther.MechanicalPawnProperties() };
        Check(!AndroidButcheryClassifier.HasMechanicalPawnProperties(fakeExtDef),
            "HasMechanicalPawnProperties rejects non-Androids namespace");

        // Check 8: FleshType ChJDroid detection
        var droidFlesh = (FleshTypeDef)RuntimeHelpers.GetUninitializedObject(typeof(FleshTypeDef));
        droidFlesh.defName = "ChJDroid";
        var fleshDroidDef = MakeThingDef("CustomFleshDroid", hasMeat: false);
        var fleshField = typeof(RaceProperties).GetField("fleshType", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        fleshField.SetValue(fleshDroidDef.race, droidFlesh);

        Check(AndroidButcheryClassifier.HasDroidFleshType(fleshDroidDef),
            "HasDroidFleshType detects ChJDroid fleshType");
        Check(AndroidButcheryClassifier.IsMechanicalOrDroidRace(fleshDroidDef),
            "Race with ChJDroid fleshType is recognized as mechanical");

        // Check 9: Corpse detection via ingestible.sourceDef and race.corpseDef
        var corpseDef = MakeCorpseDef("Corpse_ChjDroid", droidDef);
        Check(AndroidButcheryClassifier.GetInnerPawnRaceDef(corpseDef) == droidDef,
            "GetInnerPawnRaceDef resolves sourceDef for corpse");
        Check(AndroidButcheryClassifier.IsAndroidOrDroidCorpseDef(corpseDef),
            "IsAndroidOrDroidCorpseDef identifies droid corpse");

        var humanCorpse = MakeCorpseDef("Corpse_Human", humanRace);
        Check(!AndroidButcheryClassifier.IsAndroidOrDroidCorpseDef(humanCorpse),
            "IsAndroidOrDroidCorpseDef ignores organic human corpse");

        // --- YIELD CALCULATION CHECKS ---

        // Check 10: ChjDroid yields Steel 112, ComponentIndustrial 4
        var droidPawn = MakePawn(droidDef);
        var droidYield = AndroidButcheryClassifier.GetYieldCounts(droidPawn);
        Check(droidYield.Count == 2, "ChjDroid yield count is 2 items");
        Check(droidYield.Any(y => y.DefName == "Steel" && y.Count == 112),
            "ChjDroid yields exactly 112 Steel (half of 225, floor division)");
        Check(droidYield.Any(y => y.DefName == "ComponentIndustrial" && y.Count == 4),
            "ChjDroid yields exactly 4 ComponentIndustrial (half of 8, floor division)");

        // Check 11: ChjBattleDroid yields Steel 250, ComponentIndustrial 9
        var battleDroidPawn = MakePawn(battleDroidDef);
        var battleYield = AndroidButcheryClassifier.GetYieldCounts(battleDroidPawn);
        Check(battleYield.Count == 2, "ChjBattleDroid yield count is 2 items");
        Check(battleYield.Any(y => y.DefName == "Steel" && y.Count == 250),
            "ChjBattleDroid yields exactly 250 Steel (half of 500)");
        Check(battleYield.Any(y => y.DefName == "ComponentIndustrial" && y.Count == 9),
            "ChjBattleDroid yields exactly 9 ComponentIndustrial (half of 18)");

        // Check 12: ChjAndroid yields Plasteel 75, ComponentSpacer 4
        var androidPawn = MakePawn(androidDef);
        var androidYield = AndroidButcheryClassifier.GetYieldCounts(androidPawn);
        Check(androidYield.Count == 2, "ChjAndroid yield count is 2 items");
        Check(androidYield.Any(y => y.DefName == "Plasteel" && y.Count == 75),
            "ChjAndroid yields exactly 75 Plasteel (half of 150 printer cost)");
        Check(androidYield.Any(y => y.DefName == "ComponentSpacer" && y.Count == 4),
            "ChjAndroid yields exactly 4 ComponentSpacer (half of 8 printer cost)");

        // Check 13: Generic mechanical pawn with custom butcherProducts
        var genericSteelDef = MakeThingDef("Steel");
        var genericPawnDef = MakeThingDef("CustomMechUnit", hasMeat: false);
        genericPawnDef.modExtensions = new List<DefModExtension> { new Androids.MechanicalPawnProperties() };
        genericPawnDef.butcherProducts = new List<ThingDefCountClass>
        {
            new ThingDefCountClass(genericSteelDef, 55)
        };
        var genericPawn = MakePawn(genericPawnDef);
        var genericYield = AndroidButcheryClassifier.GetYieldCounts(genericPawn);
        Check(genericYield.Count == 1 && genericYield[0].Count == 27,
            "Generic mechanical pawn yields floor(55 / 2) = 27 Steel");

        // Check 14: Organic pawn yield is empty
        var humanYield = AndroidButcheryClassifier.GetYieldCounts(humanPawn);
        Check(humanYield.Count == 0, "Organic pawn has 0 android yield items");

        // --- RECIPEDEF FILTER SETALLOW CHECKS ---

        // Check 15: ApplyButcherFilterAllow gates on ButcherCorpseFlesh
        var otherRecipe = MakeRecipeDef("ButcherCorpseMechanoid");
        AndroidButcheryClassifier.ApplyButcherFilterAllow(otherRecipe);
        Check(!otherRecipe.fixedIngredientFilter.Allows(corpseDef),
            "ApplyButcherFilterAllow ignores non-ButcherCorpseFlesh recipe");

        // Check 16: ButcherCorpseFlesh filter allow directly
        var butcherRecipe = MakeRecipeDef("ButcherCorpseFlesh");
        butcherRecipe.fixedIngredientFilter.SetAllow(corpseDef, true);
        butcherRecipe.defaultIngredientFilter.SetAllow(corpseDef, true);
        Check(butcherRecipe.fixedIngredientFilter.Allows(corpseDef),
            "ButcherCorpseFlesh fixedIngredientFilter allows droid corpse");
        Check(butcherRecipe.defaultIngredientFilter.Allows(corpseDef),
            "ButcherCorpseFlesh defaultIngredientFilter allows droid corpse");

        // --- HARMONY PREFIX GATE CHECKS ---

        // Check 17: Prefix returns true (allows original) for normal human pawn
        IEnumerable<Thing> dummyResult = null;
        bool prefixNormalRun = Patch_Pawn_ButcherProducts.Prefix(humanPawn, null, 1f, ref dummyResult);
        Check(prefixNormalRun, "Pawn.ButcherProducts Prefix allows vanilla for organic pawn");

        // Check 18: Prefix returns false (suppresses original) for droid pawn
        IEnumerable<Thing> dummyResult2 = null;
        bool prefixAndroidRun = Patch_Pawn_ButcherProducts.Prefix(droidPawn, null, 1f, ref dummyResult2);
        Check(!prefixAndroidRun, "Pawn.ButcherProducts Prefix suppresses vanilla for droid pawn");

        // Check 19: Prefix returns false (suppresses original) for android pawn
        IEnumerable<Thing> dummyResult3 = null;
        bool prefixChjAndroidRun = Patch_Pawn_ButcherProducts.Prefix(androidPawn, null, 1f, ref dummyResult3);
        Check(!prefixChjAndroidRun, "Pawn.ButcherProducts Prefix suppresses vanilla for ChjAndroid pawn");

        Console.WriteLine($"\nTOTAL: {passes} PASSED, {failures} FAILED");
        return failures == 0 ? 0 : 1;
    }
}
