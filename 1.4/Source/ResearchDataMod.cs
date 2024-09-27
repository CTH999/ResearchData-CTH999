using HarmonyLib;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ResearchData
{
    [HarmonyPatch]
    public static class JobDriver_Research_Patch
    {
        public static MethodBase TargetMethod()
        {
            foreach (var type in typeof(JobDriver_Research).GetNestedTypes(AccessTools.all))
            {
                foreach (var method in type.GetMethods(AccessTools.all))
                {
                    if (method.Name.Contains("<MakeNewToils>b__0"))
                    {
                        return method;
                    }
                }
            }
            throw new Exception("[Research Data] Failed to patch JobDriver_Research");
        }
    
        public static void Postfix(object __instance, Toil ___research) 
        {
            var driver = ___research.actor.jobs.curDriver as JobDriver_Research;
            var bench = driver.ResearchBench;
            var comp = bench.GetComp<CompRefuelable>();
            if (comp != null && comp.Props.consumeFuelOnlyWhenUsed)
            {
                comp.ConsumeFuel(comp.ConsumptionRatePerTick);
            }
        }
    }


    [HarmonyPatch(typeof(WorkGiver_Researcher), "HasJobOnThing")]
    public static class WorkGiver_Researcher_HasJobOnThing_Patch
    {
        public static bool Prefix(ref bool __result, Pawn pawn, Thing t, bool forced)
        {
            CompRefuelable compRefuelable = t.TryGetComp<CompRefuelable>();
            if (compRefuelable != null && !compRefuelable.HasFuel)
            {
                if (!RefuelWorkGiverUtility.CanRefuel(pawn, t, forced))
                {
                    __result = false;
                }
                else
                {
                    __result = RefuelWorkGiverUtility.RefuelJob(pawn, t, forced) != null;
                }
                if (__result is false)
                {
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(WorkGiver_Researcher), "JobOnThing")]
    public static class WorkGiver_Researcher_JobOnThing_Patch
    {
        public static bool Prefix(ref Job __result, Pawn pawn, Thing t, bool forced)
        {
            CompRefuelable compRefuelable = t.TryGetComp<CompRefuelable>();
            if (compRefuelable != null && !compRefuelable.HasFuel)
            {
                if (!RefuelWorkGiverUtility.CanRefuel(pawn, t, forced))
                {
                    __result = null;
                }
                else
                {
                    __result = RefuelWorkGiverUtility.RefuelJob(pawn, t, forced);
                }
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(JobDriver_Research), "MakeNewToils")]
    public static class JobDriver_Research_MakeNewToils_Patch
    {
        public static IEnumerable<Toil> Postfix(IEnumerable<Toil> __result, JobDriver_Research __instance)
        {
            foreach (Toil toil in __result)
            {
                if (toil.debugName == "MakeNewToils")
                {
                    toil.FailOn(() => __instance.ResearchBench.TryGetComp<CompRefuelable>() is CompRefuelable comp && comp.HasFuel is false);
                }
                yield return toil;
            }
        }
    }

    public class FuelExtension : DefModExtension
    {
        public bool stopWhenPowerIsOff;
        public bool facilityNotActiveWhenFuelEmpty;
    }

    [HarmonyPatch(typeof(CompFacility), "CanBeActive", MethodType.Getter)]
    public static class CompFacility_CanBeActive_Patch
    {
        public static void Postfix(CompFacility __instance, ref bool __result)
        {
            var extension = __instance.parent.def.GetModExtension<FuelExtension>();
            if (extension != null)
            {
                if (extension.facilityNotActiveWhenFuelEmpty)
                {
                    var comp = __instance.parent.GetComp<CompRefuelable>();
                    if (comp != null && comp.HasFuel is false)
                    {
                        __result = false;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(CompRefuelable), "ConsumeFuel")]
    public static class CompRefuelable_ConsumeFuel_Patch
    {
        public static bool Prefix(CompRefuelable __instance)
        {
            var extension = __instance.parent.def.GetModExtension<FuelExtension>();
            if (extension != null)
            {
                if (extension.stopWhenPowerIsOff)
                {
                    var compPower = __instance.parent.GetComp<CompPowerTrader>();
                    if (compPower != null && compPower.PowerOn is false)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
    public class ResearchDataMod : Mod
    {
        public ResearchDataMod(ModContentPack pack) : base(pack)
        {
			new Harmony("ResearchDataMod").PatchAll();
        }
    }
}
