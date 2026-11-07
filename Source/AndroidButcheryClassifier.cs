using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace AndroidButchery
{
    public struct YieldCount
    {
        public string DefName;
        public int Count;

        public YieldCount(string defName, int count)
        {
            DefName = defName;
            Count = count;
        }
    }

    public static class AndroidButcheryClassifier
    {
        public const string MechanicalPropertiesType = "Androids.MechanicalPawnProperties";
        public const string DroidFleshTypeDef = "ChJDroid";
        public const string ButcherRecipeDefName = "ButcherCorpseFlesh";

        /// <summary>
        /// Checks if a pawn race carries the Androids.MechanicalPawnProperties modExtension (exact type name).
        /// </summary>
        public static bool HasMechanicalPawnProperties(ThingDef raceDef)
        {
            if (raceDef?.modExtensions == null) return false;
            for (int i = 0; i < raceDef.modExtensions.Count; i++)
            {
                var ext = raceDef.modExtensions[i];
                if (ext != null && ext.GetType().FullName == MechanicalPropertiesType)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if a pawn race has fleshType defName "ChJDroid".
        /// </summary>
        public static bool HasDroidFleshType(ThingDef raceDef)
        {
            return raceDef?.race?.FleshType != null && raceDef.race.FleshType.defName == DroidFleshTypeDef;
        }

        /// <summary>
        /// True if the given race definition represents a mechanical droid (ChjDroid, ChjBattleDroid, or carrying
        /// MechanicalPawnProperties / ChJDroid fleshType).
        /// </summary>
        public static bool IsMechanicalOrDroidRace(ThingDef raceDef)
        {
            if (raceDef == null) return false;
            if (raceDef.defName == "ChjDroid" || raceDef.defName == "ChjBattleDroid")
                return true;
            return HasMechanicalPawnProperties(raceDef) || HasDroidFleshType(raceDef);
        }

        /// <summary>
        /// Resolves the inner pawn race definition for a corpse or pawn race ThingDef.
        /// </summary>
        public static ThingDef GetInnerPawnRaceDef(ThingDef def)
        {
            if (def == null) return null;
            if (def.race != null) return def;
            if (def.ingestible?.sourceDef != null && def.ingestible.sourceDef.race != null)
                return def.ingestible.sourceDef;
            return null;
        }

        /// <summary>
        /// True if the corpse definition corresponds to an android or mechanical droid pawn.
        /// </summary>
        public static bool IsAndroidOrDroidCorpseDef(ThingDef corpseDef)
        {
            if (corpseDef == null) return false;
            var raceDef = GetInnerPawnRaceDef(corpseDef);
            if (raceDef == null || raceDef == corpseDef) return false;
            return IsButcherableAndroidRace(raceDef);
        }

        /// <summary>
        /// True if the race is butcherable under our android/droid salvage rules.
        /// </summary>
        public static bool IsButcherableAndroidRace(ThingDef raceDef)
        {
            if (raceDef == null) return false;
            if (raceDef.defName == "ChjAndroid" || raceDef.defName == "ChjDroid" || raceDef.defName == "ChjBattleDroid")
                return true;
            return HasMechanicalPawnProperties(raceDef) || HasDroidFleshType(raceDef);
        }

        /// <summary>
        /// True if the pawn is an android or mechanical droid.
        /// </summary>
        public static bool IsButcherableAndroid(Pawn pawn)
        {
            if (pawn?.def == null) return false;
            return IsButcherableAndroidRace(pawn.def);
        }

        /// <summary>
        /// Scans all registered ThingDefs and returns every corpse def associated with a mechanical droid or android.
        /// </summary>
        public static List<ThingDef> GetAndroidCorpseDefs()
        {
            var results = new List<ThingDef>();
            var allDefs = DefDatabase<ThingDef>.AllDefsListForReading;
            if (allDefs == null) return results;

            for (int i = 0; i < allDefs.Count; i++)
            {
                var def = allDefs[i];
                if (def == null) continue;

                // 1. Race def with a linked corpseDef
                if (def.race?.corpseDef != null && IsButcherableAndroidRace(def))
                {
                    if (!results.Contains(def.race.corpseDef))
                        results.Add(def.race.corpseDef);
                }

                // 2. Corpse def pointing back to inner race
                if (def.ingestible?.sourceDef != null && def.ingestible.sourceDef.race != null)
                {
                    if (IsButcherableAndroidRace(def.ingestible.sourceDef))
                    {
                        if (!results.Contains(def))
                            results.Add(def);
                    }
                }
            }
            return results;
        }

        /// <summary>
        /// Enables android and droid corpse definitions in both fixedIngredientFilter and defaultIngredientFilter
        /// for the ButcherCorpseFlesh recipe.
        /// </summary>
        public static void ApplyButcherFilterAllow(RecipeDef recipe)
        {
            if (recipe == null || recipe.defName != ButcherRecipeDefName) return;

            var corpses = GetAndroidCorpseDefs();
            for (int i = 0; i < corpses.Count; i++)
            {
                var corpseDef = corpses[i];
                if (corpseDef == null) continue;
                recipe.fixedIngredientFilter?.SetAllow(corpseDef, true);
                recipe.defaultIngredientFilter?.SetAllow(corpseDef, true);
            }
        }

        /// <summary>
        /// Calculates the exact yield counts for the given pawn:
        /// - ChjDroid: Steel 112, ComponentIndustrial 4 (half of 225 steel, 8 components; floor division)
        /// - ChjBattleDroid: Steel 250, ComponentIndustrial 9 (half of 500 steel, 18 components; floor division)
        /// - ChjAndroid: Plasteel 75, ComponentSpacer 4 (half of 150 plasteel, 8 spacer components; floor division)
        /// - Fallback for other mechanical pawns: half of pawn.def.butcherProducts
        /// </summary>
        public static List<YieldCount> GetYieldCounts(Pawn pawn)
        {
            var list = new List<YieldCount>();
            if (pawn?.def == null) return list;

            string defName = pawn.def.defName;
            if (defName == "ChjDroid")
            {
                list.Add(new YieldCount("Steel", 112));
                list.Add(new YieldCount("ComponentIndustrial", 4));
                return list;
            }
            if (defName == "ChjBattleDroid")
            {
                list.Add(new YieldCount("Steel", 250));
                list.Add(new YieldCount("ComponentIndustrial", 9));
                return list;
            }
            if (defName == "ChjAndroid")
            {
                list.Add(new YieldCount("Plasteel", 75));
                list.Add(new YieldCount("ComponentSpacer", 4));
                return list;
            }

            if (IsMechanicalOrDroidRace(pawn.def) && pawn.def.butcherProducts != null)
            {
                for (int i = 0; i < pawn.def.butcherProducts.Count; i++)
                {
                    var bp = pawn.def.butcherProducts[i];
                    if (bp?.thingDef != null && bp.count > 0)
                    {
                        int half = bp.count / 2;
                        if (half > 0)
                            list.Add(new YieldCount(bp.thingDef.defName, half));
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Instantiates Things for the yielded items, splitting into valid stacks respecting ThingDef.stackLimit.
        /// </summary>
        public static IEnumerable<Thing> MakeButcherProducts(Pawn pawn)
        {
            var yields = GetYieldCounts(pawn);
            for (int i = 0; i < yields.Count; i++)
            {
                var y = yields[i];
                var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(y.DefName);
                if (thingDef == null) continue;

                int remaining = y.Count;
                int maxStack = thingDef.stackLimit > 0 ? thingDef.stackLimit : remaining;
                while (remaining > 0)
                {
                    Thing thing = ThingMaker.MakeThing(thingDef);
                    int count = Math.Min(remaining, maxStack);
                    thing.stackCount = count;
                    remaining -= count;
                    yield return thing;
                }
            }
        }
    }
}
