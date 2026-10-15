using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace RecycleSkillBased
{
    public class JobDriver_Recycle : JobDriver_DoBill
    {
        private const TargetIndex BenchInd = TargetIndex.A;   // tailoring bench
        private const TargetIndex ItemInd = TargetIndex.B;   // item to recycle
        private const TargetIndex PlaceInd = TargetIndex.C;

        private const string OutfitStandDefName = "Building_OutfitStand";

        private float workLeft;
        private float totalWork;
        private float compTimeMult;
        private float compXpMult;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnBurningImmobile(BenchInd);
            this.FailOn(() => job.bill == null || job.bill.DeletedOrDereferenced);

            yield return Toils_Reserve.Reserve(BenchInd, 1);
            yield return Toils_Reserve.ReserveQueue(ItemInd, 1);

            yield return Toils_JobTransforms.ExtractNextTargetFromQueue(ItemInd);

            var skipInvalid = new Toil
            {
                initAction = () =>
                {
                    while (true)
                    {
                        var current = job.GetTarget(ItemInd).Thing;

                        if (current != null && current.Spawned && !IsBlockedOutfitStandTarget(current))
                            break;

                        var q = job.targetQueueB;
                        if (q != null && q.Count > 0)
                        {
                            job.SetTarget(ItemInd, q[0]);
                            q.RemoveAt(0);
                            continue;
                        }

                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                }
            };
            skipInvalid.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return skipInvalid;

            yield return Toils_Goto.GotoThing(ItemInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(ItemInd);
            yield return Toils_Haul.StartCarryThing(ItemInd);

            yield return Toils_Goto.GotoThing(BenchInd, PathEndMode.InteractionCell);
            yield return Toils_JobTransforms.SetTargetToIngredientPlaceCell(BenchInd, ItemInd, PlaceInd);
            yield return Toils_Haul.PlaceHauledThingInCell(PlaceInd, null, false);
            yield return Toils_Reserve.Reserve(ItemInd, 1);

            var work = new Toil();
            work.initAction = () =>
            {
                var it = job.GetTarget(ItemInd).Thing;
                compTimeMult = 1f;
                compXpMult = 1f;

                bool hasIndustrialComp = it?.def?.costList != null &&
                                         it.def.costList.Any(e => e.thingDef == ThingDefOf.ComponentIndustrial);
                bool hasAdvancedComp = it?.def?.costList != null &&
                                         it.def.costList.Any(e => e.thingDef == ThingDefOf.ComponentSpacer);

                if (hasAdvancedComp) { compTimeMult = 4f; compXpMult = 2.5f; }
                else if (hasIndustrialComp) { compTimeMult = 2f; compXpMult = 1.5f; }

                var st = RecycleSkillBasedMod.Settings;

                int lvl = Mathf.Clamp(pawn.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0, 0, 20);
                float speedMult;
                if (lvl <= 10)
                {
                    float t = lvl / 10f;
                    speedMult = Mathf.Lerp(st.SpeedMultLevel0, st.SpeedMultLevel10, t);
                }
                else
                {
                    float t = (lvl - 10f) / 10f;
                    speedMult = Mathf.Lerp(st.SpeedMultLevel10, st.SpeedMultLevel20, t);
                }

                float recipeWork = Mathf.Max(1f, job.bill.recipe.workAmount);
                totalWork = (recipeWork * compTimeMult) / Mathf.Max(0.01f, speedMult);
                workLeft = totalWork;

                job.bill.Notify_DoBillStarted(pawn);
            };

            work.tickAction = () =>
            {
                workLeft -= pawn.GetStatValue(StatDefOf.GeneralLaborSpeed);

                if (pawn.skills != null)
                {
                    float perTick = Mathf.Clamp(RecycleSkillBasedMod.Settings.XpPerTick, 0f, 3f);
                    pawn.skills.Learn(SkillDefOf.Crafting, perTick * compXpMult);
                }

                if (workLeft <= 0f)
                    ReadyForNextToil();
            };

            work.defaultCompleteMode = ToilCompleteMode.Never;
            work.WithProgressBar(BenchInd, () => 1f - (workLeft / (totalWork <= 0f ? 1f : totalWork)), false, 0.5f);
            work.WithEffect(() => job.bill.recipe.effectWorking, BenchInd);
            work.PlaySustainerOrSound(() => job.bill.recipe.soundWorking);
            yield return work;

            var finalize = new Toil();
            finalize.initAction = () =>
            {
                var item = job.GetTarget(ItemInd).Thing;
                if (item != null && !item.Destroyed)
                {
                    var st = RecycleSkillBasedMod.Settings;

                    int lvl = Mathf.Clamp(pawn.skills?.GetSkill(SkillDefOf.Crafting)?.Level ?? 0, 0, 20);
                    float minYield = Mathf.Clamp01(st.ReturnAtLevel0 / 100f);
                    float maxYield = Mathf.Clamp01(st.ReturnAtLevel20 / 100f);
                    float yieldFactor = Mathf.Lerp(minYield, maxYield, lvl / 20f);

                    // Tainted penalty (apparel only)
                    bool isTaintedApparel = (item is Apparel ap) && ap.WornByCorpse;
                    if (isTaintedApparel && st.TaintedPenaltyPercent > 0)
                    {
                        float penalty = Mathf.Clamp01(st.TaintedPenaltyPercent / 100f);
                        yieldFactor *= (1f - penalty);
                    }

                    // HP scaling (after tainted penalty)
                    if (st.UseHPScaling && item.HitPoints > 0 && item.MaxHitPoints > 0)
                    {
                        float hpFactor = (float)item.HitPoints / item.MaxHitPoints;
                        yieldFactor *= Mathf.Clamp01(hpFactor);
                    }

                    yieldFactor = Mathf.Clamp01(yieldFactor);

                    var products = new List<Thing>();

                    // A) Stuff
                    if (item.def.MadeFromStuff && item.Stuff != null && item.def.costStuffCount > 0)
                    {
                        int cnt = Mathf.FloorToInt(item.def.costStuffCount * yieldFactor);
                        if (cnt > 0)
                        {
                            var t = ThingMaker.MakeThing(item.Stuff);
                            t.stackCount = cnt;
                            products.Add(t);
                        }
                    }

                    // B) Fixed cost list
                    bool anyCost = false;
                    if (item.def.costList != null && item.def.costList.Count > 0)
                    {
                        anyCost = true;
                        foreach (var e in item.def.costList)
                        {
                            if (!st.AllowIndustrialComponents && e.thingDef == ThingDefOf.ComponentIndustrial)
                                continue;
                            if (!st.AllowAdvancedComponents && e.thingDef == ThingDefOf.ComponentSpacer)
                                continue;

                            int cnt = Mathf.FloorToInt(e.count * yieldFactor);
                            if (cnt > 0)
                            {
                                var t = ThingMaker.MakeThing(e.thingDef);
                                t.stackCount = cnt;
                                products.Add(t);
                            }
                        }
                    }

                    // C) Fallback: smelt products if no costList
                    if (!anyCost && (item.def.smeltProducts != null && item.def.smeltProducts.Count > 0))
                    {
                        foreach (var e in item.def.smeltProducts)
                        {
                            if (!st.AllowIndustrialComponents && e.thingDef == ThingDefOf.ComponentIndustrial)
                                continue;
                            if (!st.AllowAdvancedComponents && e.thingDef == ThingDefOf.ComponentSpacer)
                                continue;

                            int cnt = Mathf.FloorToInt(e.count * yieldFactor);
                            if (cnt > 0)
                            {
                                var t = ThingMaker.MakeThing(e.thingDef);
                                t.stackCount = cnt;
                                products.Add(t);
                            }
                        }
                    }

                    item.Destroy(DestroyMode.Vanish);

                    foreach (var p in products)
                        GenPlace.TryPlaceThing(p, pawn.Position, pawn.Map, ThingPlaceMode.Near);

                    job.bill.Notify_IterationCompleted(pawn, products);
                }

                EndJobWith(JobCondition.Succeeded);
            };
            finalize.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finalize;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "workLeft", 0f);
            Scribe_Values.Look(ref totalWork, "totalWork", 0f);
            Scribe_Values.Look(ref compTimeMult, "compTimeMult", 1f);
            Scribe_Values.Look(ref compXpMult, "compXpMult", 1f);
        }

        private static bool IsBlockedOutfitStandTarget(Thing t)
        {
            if (!(RecycleSkillBasedMod.Settings?.ForbidOutfitStandItems ?? true))
                return false;
            return IsOnOutfitStandOrSimilar(t);
        }

        // -------- outfit stand detection (container + display-on-cell) --------
        private static bool IsOutfitStandDefName(string? dn)
        {
            if (string.IsNullOrEmpty(dn)) return false;
            if (dn.Equals(OutfitStandDefName, StringComparison.OrdinalIgnoreCase)) return true;

            dn = dn.ToLowerInvariant();
            return dn.Contains("outfitstand")
                || dn.Contains("wardrobe")
                || (dn.Contains("apparel") && (dn.Contains("stand") || dn.Contains("rack") || dn.Contains("display")))
                || dn.Contains("clothesrack")
                || dn.Contains("clothingstand")
                || dn.Contains("displaystand");
        }

        private static bool IsOnOutfitStandOrSimilar(Thing t)
        {
            if (t == null) return false;

            IThingHolder? h = t.ParentHolder;
            while (h != null)
            {
                if (h is Building_OutfitStand)
                    return true;
                if (h is Building b && IsOutfitStandDefName(b.def?.defName))
                    return true;
                h = h.ParentHolder;
            }

            if (!t.Spawned || t.Map == null)
                return false;

            var building = t.Position.GetFirstBuilding(t.Map);
            if (building is Building_OutfitStand)
                return true;
            if (building != null && IsOutfitStandDefName(building.def?.defName))
                return true;

            return false;
        }
    }
}
