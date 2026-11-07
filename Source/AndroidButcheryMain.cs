using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AndroidButchery
{
    [StaticConstructorOnStartup]
    public static class AndroidButcheryMain
    {
        public const string HarmonyId = "ertzfiend.androidbutchery";

        static AndroidButcheryMain()
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Belt-and-suspenders pass: re-apply filter allow in case any implied or dynamically
            // registered corpse defs resolved after RecipeDef.ResolveReferences.
            var butcherRecipe = DefDatabase<RecipeDef>.GetNamedSilentFail(AndroidButcheryClassifier.ButcherRecipeDefName);
            if (butcherRecipe != null)
            {
                AndroidButcheryClassifier.ApplyButcherFilterAllow(butcherRecipe);
            }
        }
    }
}
