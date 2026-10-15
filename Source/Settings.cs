using Verse;

namespace RecycleSkillBased
{
    public class Settings : ModSettings
    {
        // --- Yield (percent) ---
        public int ReturnAtLevel0 = 10;   // % at Crafting 0
        public int ReturnAtLevel20 = 75;   // % at Crafting 20

        // --- Tainted penalty (percent) ---
        public int TaintedPenaltyPercent = 30; // 0..100; applied only to tainted apparel

        // --- XP ---
        public float XpPerTick = 1.0f;     // per tick, before comp multipliers

        // --- Speed multipliers (as factors) ---
        public float SpeedMultLevel0 = 0.75f; // 75%
        public float SpeedMultLevel10 = 1.00f; // 100%
        public float SpeedMultLevel20 = 2.00f; // 200%

        // --- Component return toggles ---
        public bool AllowIndustrialComponents = true;
        public bool AllowAdvancedComponents = true;

        // --- Forbid items on outfit stands (workgiver/driver filter) ---
        public bool ForbidOutfitStandItems = true;

        // --- HP scaling toggle ---
        public bool UseHPScaling = true; // if true: multiply yield by HP%

        // --- UI buffers for numeric fields ---
        public string BufferReturnL0 = "";
        public string BufferReturnL20 = "";
        public string BufferTaintedPct = "";
        public string BufferXpPerTick = "";
        public string BufferSpeed0 = "";
        public string BufferSpeed10 = "";
        public string BufferSpeed20 = "";

        public override void ExposeData()
        {
            base.ExposeData();

            // Yield
            Scribe_Values.Look(ref ReturnAtLevel0, "ReturnAtLevel0", 10);
            Scribe_Values.Look(ref ReturnAtLevel20, "ReturnAtLevel20", 75);

            // Tainted penalty
            Scribe_Values.Look(ref TaintedPenaltyPercent, "TaintedPenaltyPercent", 30);

            // XP
            Scribe_Values.Look(ref XpPerTick, "XpPerTick", 1.0f);

            // Speed multipliers
            Scribe_Values.Look(ref SpeedMultLevel0, "SpeedMultLevel0", 0.75f);
            Scribe_Values.Look(ref SpeedMultLevel10, "SpeedMultLevel10", 1.00f);
            Scribe_Values.Look(ref SpeedMultLevel20, "SpeedMultLevel20", 2.00f);

            // Toggles
            Scribe_Values.Look(ref AllowIndustrialComponents, "AllowIndustrialComponents", true);
            Scribe_Values.Look(ref AllowAdvancedComponents, "AllowAdvancedComponents", true);
            Scribe_Values.Look(ref ForbidOutfitStandItems, "ForbidOutfitStandItems", true);
            Scribe_Values.Look(ref UseHPScaling, "UseHPScaling", true);
        }

        public void ResetToDefaults()
        {
            ReturnAtLevel0 = 10;
            ReturnAtLevel20 = 75;

            TaintedPenaltyPercent = 30;

            XpPerTick = 1.0f;

            SpeedMultLevel0 = 0.75f;
            SpeedMultLevel10 = 1.00f;
            SpeedMultLevel20 = 2.00f;

            AllowIndustrialComponents = true;
            AllowAdvancedComponents = true;
            ForbidOutfitStandItems = true;

            UseHPScaling = true;

            BufferReturnL0 = "";
            BufferReturnL20 = "";
            BufferTaintedPct = "";
            BufferXpPerTick = "";
            BufferSpeed0 = "";
            BufferSpeed10 = "";
            BufferSpeed20 = "";
        }
    }
}
