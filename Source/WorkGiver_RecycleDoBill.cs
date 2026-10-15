using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RecycleSkillBased
{
    public class WorkGiver_RecycleDoBill : WorkGiver_DoBill
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            var job = base.JobOnThing(pawn, t, forced);
            if (job?.bill?.recipe == null)
                return job;

            // Only redirect our specific recycling recipe to our custom JobDef
            if (job.bill.recipe.defName != "RecycleApparel")
                return job;

            job.def = RecycleDefOf.Recycle;

            // Clean the target queue: remove unspawned items or items on stands
            // If the queue becomes empty, return null to prevent job-start spam
            if (job.targetQueueB != null && job.targetQueueB.Count > 0)
            {
                PruneInvalidTargets(pawn, job, job.targetQueueB);
                if (job.targetQueueB.Count == 0)
                    return null;

                // Update targetB to the first valid item in the pruned queue
                job.targetB = job.targetQueueB[0];
            }
            else
            {
                // If there is no queue, check the single targetB directly
                var thing = job.targetB.Thing;
                if (!IsValidRecyclableTarget(pawn, thing))
                    return null;
            }

            return job;
        }

        private static void PruneInvalidTargets(Pawn pawn, Job job, List<LocalTargetInfo> q)
        {
            for (int i = q.Count - 1; i >= 0; i--)
            {
                var th = q[i].Thing;
                if (!IsValidRecyclableTarget(pawn, th))
                {
                    q.RemoveAt(i);
                }
            }
        }

        private static bool IsValidRecyclableTarget(Pawn pawn, Thing th)
        {
            if (th == null) return false;

            bool forbidStands = RecycleSkillBasedMod.Settings?.ForbidOutfitStandItems ?? true;
            if (forbidStands)
            {
                // Container check (Odyssey outfit stand).
                IThingHolder holder = th.ParentHolder;
                while (holder != null)
                {
                    if (holder is Building_OutfitStand)
                        return false;
                    holder = holder.ParentHolder;
                }
            }

            // Map Presence: Item must be spawned to be physically picked up.
            if (!th.Spawned) return false;

            if (forbidStands)
            {
                // Cell/Def check for stands that place items on the building cell.
                var buildingAtPos = th.Position.GetFirstBuilding(pawn.Map);
                if (buildingAtPos != null)
                {
                    if (buildingAtPos is Building_OutfitStand || IsOutfitStandDefName(buildingAtPos.def?.defName))
                        return false;
                }
            }

            if (th.IsForbidden(pawn)) return false;
            if (!pawn.CanReserve(th)) return false;

            return true;
        }

        private static bool IsOutfitStandDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;

            // This string check acts as a backup for non-DLC mods.
            // It is worth keeping to ensure maximum compatibility.
            string lowName = defName.ToLowerInvariant();
            return lowName.Contains("outfit")
                || lowName.Contains("wardrobe")
                || (lowName.Contains("apparel") && (lowName.Contains("stand") || lowName.Contains("rack")))
                || lowName.Contains("clothesrack")
                || lowName.Contains("displaystand");
        }
    }
}