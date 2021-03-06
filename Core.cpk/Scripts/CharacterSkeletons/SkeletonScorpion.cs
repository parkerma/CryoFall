﻿namespace AtomicTorch.CBND.CoreMod.CharacterSkeletons
{
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;

    public class SkeletonScorpion : ProtoCharacterSkeletonAnimal
    {
        public override double DefaultMoveSpeed => 1.5;

        public override SkeletonResource SkeletonResourceBack { get; }
            = new SkeletonResource("Scorpion/ScorpionBack");

        public override SkeletonResource SkeletonResourceFront { get; }
            = new SkeletonResource("Scorpion/ScorpionFront");

        public override double WorldScale => 0.4;

        protected override string SoundsFolderPath => "Skeletons/Scorpion";

        public override void ClientSetupShadowRenderer(IComponentSpriteRenderer shadowRenderer, double scaleMultiplier)
        {
            shadowRenderer.PositionOffset = (0, -0.15 * scaleMultiplier);
            shadowRenderer.Scale = 1.75 * scaleMultiplier;
        }

        public override void CreatePhysics(IPhysicsBody physicsBody)
        {
            physicsBody.AddShapeCircle(
                           radius: 0.35,
                           center: (0 - 0.125, 0.1),
                           group: CollisionGroups.Default)
                       .AddShapeCircle(
                           radius: 0.35,
                           center: (0 + 0.125, 0.1),
                           group: CollisionGroups.Default)
                       .AddShapeCircle(
                           radius: 0.7,
                           center: (0, 0.15),
                           group: CollisionGroups.HitboxMelee)
                       .AddShapeCircle(
                           radius: 0.7,
                           center: (0, 0.15),
                           group: CollisionGroups.HitboxRanged);
        }
    }
}