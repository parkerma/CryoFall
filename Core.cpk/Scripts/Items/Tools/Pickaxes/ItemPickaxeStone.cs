﻿namespace AtomicTorch.CBND.CoreMod.Items.Tools.Pickaxes
{
    public class ItemPickaxeStone : ProtoItemToolPickaxe
    {
        public override double DamageToMinerals => 50;

        public override double DamageToNonMinerals => 12;

        public override string Description => "Stone pickaxe can be used to mine mineral deposits.";

        public override ushort DurabilityMax => 600;

        public override string Name => "Stone pickaxe";
    }
}