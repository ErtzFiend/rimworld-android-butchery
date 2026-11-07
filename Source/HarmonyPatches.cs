using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AndroidButchery
{
    /// <summary>
    /// Postfix on Verse.RecipeDef.ResolveReferences to ensure android and droid corpses
    /// are allowed in ButcherCorpseFlesh's ingredient filters.
    /// Vanilla ThingFilter.ResolveReferences with disallowDoesntProduceMeat=true
    /// strips races where race.hasMeat is false (ChjDroid, ChjBattleDroid).
    /// </summary>
    [HarmonyPatch(typeof(RecipeDef), nameof(RecipeDef.ResolveReferences))]
    public static class Patch_RecipeDef_ResolveReferences
    {
        public static void Postfix(RecipeDef __instance)
        {
            if (__instance == null || __instance.defName != AndroidButcheryClassifier.ButcherRecipeDefName)
                return;

            AndroidButcheryClassifier.ApplyButcherFilterAllow(__instance);
        }
    }

    /// <summary>
    /// Prefix on Verse.Pawn.ButcherProducts to intercept android and droid butchery yields.
    /// Suppresses vanilla meat, leather, and butcherProducts output, replacing them with
    /// half the manufacturing / crafting cost in steel, components, plasteel, or spacer components.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.ButcherProducts))]
    public static class Patch_Pawn_ButcherProducts
    {
        public static bool Prefix(Pawn __instance, Pawn butcher, float efficiency, ref IEnumerable<Thing> __result)
        {
            if (AndroidButcheryClassifier.IsButcherableAndroid(__instance))
            {
                __result = AndroidButcheryClassifier.MakeButcherProducts(__instance);
                return false;
            }
            return true;
        }
    }
}
