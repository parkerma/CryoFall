﻿namespace AtomicTorch.CBND.CoreMod.CraftRecipes
{
    using System;
    using AtomicTorch.CBND.CoreMod.Items.Generic;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.CraftingStations;
    using AtomicTorch.CBND.CoreMod.Systems;
    using AtomicTorch.CBND.CoreMod.Systems.Crafting;

    public class RecipeVialEmpty : Recipe.RecipeForStationCrafting
    {
        protected override void SetupRecipe(
            StationsList stations,
            out TimeSpan duration,
            InputItems inputItems,
            OutputItems outputItems)
        {
            stations.Add<ObjectMedicalLab>();

            duration = CraftingDuration.VeryShort;

            inputItems.Add<ItemGlassRaw>(count: 100);
            inputItems.Add<ItemIngotCopper>(count: 10);
            inputItems.Add<ItemOrePragmium>(count: 5);
            inputItems.Add<ItemIngotLithium>(count: 1);

            outputItems.Add<ItemVialEmpty>(count: 25);
        }
    }
}