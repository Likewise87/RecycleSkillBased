using RimWorld;
using Verse;

namespace RecycleSkillBased
{
    [DefOf]
    public static class RecycleDefOf
    {
        public static JobDef Recycle;
        public static RecipeDef RecycleApparel;

        static RecycleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RecycleDefOf));
        }
    }
}
