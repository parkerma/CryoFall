﻿namespace AtomicTorch.CBND.CoreMod.Items.Equipment
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Controls;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Stats;
    using AtomicTorch.CBND.CoreMod.Systems.ItemDurability;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Items.Controls.SlotOverlays;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.GameEngine.Common.DataStructures;

    /// <summary>
    /// Item prototype for equipment.
    /// </summary>
    public abstract class ProtoItemEquipment
        <TPrivateState,
         TPublicState,
         TClientState>
        : ProtoItem
          <TPrivateState,
              TPublicState,
              TClientState>,
          IProtoItemEquipment
        where TPrivateState : BasePrivateState, IItemWithDurabilityPrivateState, new()
        where TPublicState : BasePublicState, new()
        where TClientState : BaseClientState, new()
    {
        protected ProtoItemEquipment()
        {
            this.Icon = new TextureResource("Items/Equipment/" + this.GetType().Name);
        }

        public byte[] CompatibleContainerSlotsIds { get; private set; }

        public abstract ushort DurabilityMax { get; }

        public abstract EquipmentType EquipmentType { get; }

        public override double GroundIconScale => 1.5;

        /// <summary>
        /// Gets the item icon (it will be acquired and cached during script prepare).
        /// </summary>
        public override ITextureResource Icon { get; }

        /// <summary>
        /// Equipment items cannot be stacked.
        /// </summary>
        public sealed override ushort MaxItemsPerStack => ItemStackSize.Single;

        public IReadOnlyStatsDictionary ProtoEffects { get; private set; }

        public abstract bool RequireEquipmentTextures { get; }

        public IReadOnlyList<SkeletonSlotAttachment> SlotAttachmentsFemale { get; private set; }

        public IReadOnlyList<SkeletonSlotAttachment> SlotAttachmentsMale { get; private set; }

        protected abstract double DefenseMultiplier { get; }

        public virtual void ClientCreateItemSlotOverlayControls(IItem item, List<Control> controls)
        {
            if (this.DurabilityMax > 0)
            {
                controls.Add(ItemSlotDurabilityOverlayControl.Create(item));
            }
        }

        public virtual void ClientSetupSkeleton(
            IItem item,
            ICharacter character,
            IComponentSkeleton skeletonRenderer,
            List<IClientComponent> skeletonComponents)
        {
            var isMale = PlayerCharacter.GetPublicState(character).IsMale;
            var slotAttachments = isMale
                                      ? this.SlotAttachmentsMale
                                      : this.SlotAttachmentsFemale;

            ClientSkeletonAttachmentsLoader.SetAttachments(skeletonRenderer, slotAttachments);
        }

        public virtual void ServerOnItemBrokeAndDestroyed(IItem item, IItemsContainer container, byte slotId)
        {
        }

        public virtual void ServerOnItemDamaged(IItem item, double damageApplied)
        {
            ItemDurabilitySystem.ServerModifyDurability(item, delta: -(int)damageApplied);
        }

        protected virtual void ClientFillSlotAttachmentSources(ITempList<string> folders)
        {
            folders.Add("Characters/Equipment/" + this.ShortId);
        }

        protected abstract void PrepareDefense(DefenseDescription defense);

        protected virtual void PrepareEffects(Effects effects)
        {
        }

        protected sealed override void PrepareProtoItem()
        {
            base.PrepareProtoItem();
            this.CompatibleContainerSlotsIds = this.SharedGetCompatibleContainerSlotsIds();

            if (IsClient)
            {
                using (var tempSourcePaths = Api.Shared.GetTempList<string>())
                {
                    this.ClientFillSlotAttachmentSources(tempSourcePaths);
                    using (var tempSpritePaths = ClientEquipmentSpriteHelper.CollectSpriteFilePaths(
                        tempSourcePaths.ToList()))
                    {
                        this.SlotAttachmentsMale = ClientEquipmentSpriteHelper.CollectSlotAttachments(
                            tempSpritePaths.AsList(),
                            this.Id,
                            isMale: true,
                            requireEquipmentTextures: this.RequireEquipmentTextures);

                        // we have not completed female sprites yet
                        this.SlotAttachmentsFemale = this.SlotAttachmentsMale;

                        foreach (var file in tempSpritePaths)
                        {
                            file.FilesInFolder.Dispose();
                        }
                    }
                }
            }

            var defenseDescription = new DefenseDescription();
            if (this.DefenseMultiplier < 0
                || this.DefenseMultiplier > 1)
            {
                throw new Exception(
                    $"{nameof(this.DefenseMultiplier)} must be in range from 0 to 1 (inclusive) for {this}. Current value is: {this.DefenseMultiplier:F2}");
            }

            defenseDescription.SetMultiplier(this.DefenseMultiplier);
            this.PrepareDefense(defenseDescription);
            var defense = defenseDescription.ToReadOnly();

            var effects = new Effects();
            defense.FillEffects(this, effects);
            this.PrepareEffects(effects);
            this.ProtoEffects = effects.ToReadOnly();

            this.PrepareProtoItemEquipment();
        }

        protected virtual void PrepareProtoItemEquipment()
        {
        }

        protected override void ServerInitialize(ServerInitializeData data)
        {
            base.ServerInitialize(data);
            ItemDurabilitySystem.ServerInitializeItem(data.PrivateState, data.IsFirstTimeInit);
        }

        /// <summary>
        /// Gets the array of allowed slots to allow the item to be equipped into. Cached during <see cref="PrepareProtoItem" />.
        /// </summary>
        protected virtual byte[] SharedGetCompatibleContainerSlotsIds()
        {
            return new[]
            {
                (byte)this.EquipmentType
            };
        }
    }

    /// <summary>
    /// Item prototype for equipment.
    /// </summary>
    public abstract class ProtoItemEquipment
        : ProtoItemEquipment
            <ItemWithDurabilityPrivateState,
                EmptyPublicState,
                EmptyClientState>
    {
    }
}