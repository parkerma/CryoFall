﻿namespace AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Doors
{
    using System;
    using System.Threading.Tasks;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Rendering;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.Helpers.Client.Walls;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Walls;
    using AtomicTorch.CBND.CoreMod.Systems.Construction;
    using AtomicTorch.CBND.CoreMod.Systems.LandClaim;
    using AtomicTorch.CBND.CoreMod.Systems.Physics;
    using AtomicTorch.CBND.CoreMod.Systems.Weapons;
    using AtomicTorch.CBND.CoreMod.Systems.WorldObjectAccessMode;
    using AtomicTorch.CBND.CoreMod.Systems.WorldObjectOwners;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.Bars;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.Data;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.GameEngine.Common.Primitives;

    public abstract class ProtoObjectDoor
        <TPrivateState,
         TPublicState,
         TClientState>
        : ProtoObjectStructure
          <TPrivateState,
              TPublicState,
              TClientState>,
          IProtoObjectDoor,
          IInteractableProtoStaticWorldObject
        where TPrivateState : ObjectDoorPrivateState, new()
        where TPublicState : ObjectDoorPublicState, new()
        where TClientState : ObjectDoorClientState, new()
    {
        private const double DrawWorldOffsetYHorizontalDoor = 0.17;

        private const double DrawWorldOffsetYVerticalDoor = 0.1;

        private readonly Lazy<ProceduralTexture> lazyHorizontalDoorBlueprintTexture;

        private readonly Lazy<ProceduralTexture> lazyVerticalDoorBlueprintTexture;

        private double doorAutoOpenRadiusSqr;

        protected ProtoObjectDoor()
        {
            this.lazyHorizontalDoorBlueprintTexture = new Lazy<ProceduralTexture>(
                () =>
                {
                    var textureResources = new[]
                    {
                        this.TextureBaseHorizontal,
                        (ITextureResource)this.AtlasTextureHorizontal.Chunk(
                            (byte)(this.AtlasTextureHorizontal.AtlasSize.ColumnsCount - 1),
                            0)
                    };
                    var proceduralTexture = new ProceduralTexture(
                        "Composed blueprint " + this.Id,
                        generateTextureCallback: request => ClientComposeIcon(request,
                                                                              textureResources),
                        isTransparent: true,
                        isUseCache: true,
                        dependsOn: textureResources);
                    return proceduralTexture;
                });

            this.lazyVerticalDoorBlueprintTexture = new Lazy<ProceduralTexture>(
                () =>
                {
                    var atlas = this.AtlasTextureVertical;
                    var columnChunk = (byte)(atlas.AtlasSize.ColumnsCount - 1);
                    return ClientProceduralTextureHelper.CreateComposedTexture(
                        "Composed vertical door blueprint " + this.Id,
                        isTransparent: true,
                        isUseCache: true,
                        textureResources: new ITextureResource[]
                        {
                            atlas.Chunk((byte)(columnChunk - 1), 0),
                            atlas.Chunk(columnChunk,             0)
                        });
                });
        }

        public virtual Vector2D DoorOpenCheckOffset { get; } = (0.5, 0.1);

        public virtual int DoorSizeTiles => 1;

        public virtual bool HasOwnersList => true;

        public override string InteractionTooltipText => InteractionTooltipTexts.Configure;

        public bool IsAutoEnterPrivateScopeOnInteraction => true;

        public bool IsClosedAccessModeAvailable => true;

        /// <summary>
        /// If set to null the door orientation is selected automatically.
        /// </summary>
        public virtual bool? IsHorizontalDoorOnly => null;

        public override double ServerUpdateIntervalSeconds => 0.2; // 5 times per second

        public virtual SoundResource SoundResourceDoorEnd { get; }
            = new SoundResource("Objects/Structures/Doors/DoorEnd");

        public virtual SoundResource SoundResourceDoorProcess { get; }
            = new SoundResource("Objects/Structures/Doors/DoorProcess");

        public virtual SoundResource SoundResourceDoorStart { get; }
            = new SoundResource("Objects/Structures/Doors/DoorStart");

        public override float StructurePointsMaxForConstructionSite
            => this.StructurePointsMax / 25;

        protected ITextureAtlasResource AtlasTextureHorizontal { get; set; }

        protected ITextureAtlasResource AtlasTextureVertical { get; set; }

        protected virtual double DoorAutoOpenRadius => 1.6; // radius at which the door will open to the owner

        protected virtual double DoorOpenCloseAnimationDuration => 0.2f;

        protected TextureResource TextureBaseHorizontal { get; set; }

        public void ClientRefreshRenderer(IStaticWorldObject door)
        {
            this.ClientSetupDoor(new ClientInitializeData(door));
        }

        public override void ClientSetupBlueprint(Tile tile, IClientBlueprint blueprint)
        {
            var renderer = blueprint.SpriteRenderer;
            var isHorizontalDoor = this.IsHorizontalDoorOnly
                                   ?? DoorHelper.IsHorizontalDoorNeeded(tile, checkExistingDoor: true);
            renderer.PositionOffset = (0,
                                       isHorizontalDoor
                                           ? DrawWorldOffsetYHorizontalDoor
                                           : DrawWorldOffsetYVerticalDoor);

            renderer.TextureResource = isHorizontalDoor
                                           ? this.lazyHorizontalDoorBlueprintTexture.Value
                                           : this.lazyVerticalDoorBlueprintTexture.Value;
        }

        public override void ServerOnBuilt(IStaticWorldObject structure, ICharacter byCharacter)
        {
            WorldObjectOwnersSystem.ServerOnBuilt(structure, byCharacter);
        }

        public override void ServerOnDestroy(IStaticWorldObject gameObject)
        {
            foreach (var occupiedTile in gameObject.OccupiedTiles)
            {
                SharedWallConstructionRefreshHelper.SharedRefreshNeighborObjects(occupiedTile,
                                                                                 isDestroy: true);
            }

            base.ServerOnDestroy(gameObject);
        }

        public bool SharedCanEditOwners(IStaticWorldObject worldObject, ICharacter byOwner)
        {
            return true;
        }

        public override bool SharedCanInteract(ICharacter character, IStaticWorldObject worldObject, bool writeToLog)
        {
            return base.SharedCanInteract(character, worldObject, writeToLog)
                   && WorldObjectOwnersSystem.SharedCanInteract(character, worldObject, writeToLog);
        }

        public override void SharedCreatePhysicsConstructionBlueprint(IPhysicsBody physicsBody)
        {
            var worldObject = (IStaticWorldObject)physicsBody.AssociatedWorldObject;
            var isHorizontalDoor = this.IsHorizontalDoorOnly
                                   ?? DoorHelper.IsHorizontalDoorNeeded(worldObject.OccupiedTile,
                                                                        checkExistingDoor: true);
            this.SharedCreateDoorPhysics(physicsBody, isHorizontalDoor, isOpened: true);
        }

        BaseUserControlWithWindow IInteractableProtoStaticWorldObject.ClientOpenUI(IStaticWorldObject worldObject)
        {
            return this.ClientOpenUI(new ClientObjectData(worldObject));
        }

        void IInteractableProtoStaticWorldObject.ServerOnClientInteract(ICharacter who, IStaticWorldObject worldObject)
        {
            // do nothing
        }

        void IInteractableProtoStaticWorldObject.ServerOnMenuClosed(ICharacter who, IStaticWorldObject worldObject)
        {
            // do nothing
        }

        ObjectDoorPrivateState IProtoObjectDoor.GetPrivateState(IStaticWorldObject door)
        {
            return GetPrivateState(door);
        }

        protected static async Task<ITextureResource> ClientComposeIcon(
            ProceduralTextureRequest request,
            params ITextureResource[] textureResources)
        {
            var rendering = Api.Client.Rendering;
            var renderingTag = request.TextureName;

            var textureSize = await rendering.GetTextureSize(textureResources[1]);
            var textureSizeBase = await rendering.GetTextureSize(textureResources[0]);
            request.ThrowIfCancelled();

            // create camera and render texture
            var renderTexture = rendering.CreateRenderTexture(renderingTag, textureSize.X, textureSize.Y);
            var cameraObject = Api.Client.Scene.CreateSceneObject(renderingTag);
            var camera = rendering.CreateCamera(cameraObject,
                                                renderingTag,
                                                drawOrder: -100);
            camera.RenderTarget = renderTexture;
            camera.SetOrthographicProjection(textureSize.X, textureSize.Y);

            // create and prepare sprite renderers
            rendering.CreateSpriteRenderer(
                cameraObject,
                textureResources[0],
                positionOffset: (0, -textureSize.Y + textureSizeBase.Y),
                // draw down
                spritePivotPoint: (0, 1),
                renderingTag: renderingTag);

            rendering.CreateSpriteRenderer(
                cameraObject,
                textureResources[1],
                positionOffset: (0, 0),
                // draw down
                spritePivotPoint: (0, 1),
                renderingTag: renderingTag);

            await camera.DrawAsync();
            cameraObject.Destroy();

            request.ThrowIfCancelled();

            var generatedTexture = await renderTexture.SaveToTexture(isTransparent: true);
            renderTexture.Dispose();
            request.ThrowIfCancelled();
            return generatedTexture;
        }

        protected override ITextureResource ClientCreateIcon()
        {
            ITextureResource[] textureResources;
            if (this.IsHorizontalDoorOnly ?? true)
            {
                textureResources = new ITextureResource[]
                {
                    // closed door
                    this.TextureBaseHorizontal,
                    // horizontal base
                    this.AtlasTextureHorizontal.Chunk(0, 0)
                };
            }
            else
            {
                // vertical door
                textureResources = new ITextureResource[]
                {
                    // closed door
                    this.AtlasTextureVertical.Chunk(0, 0),
                    // vertical base
                    this.AtlasTextureVertical.Chunk((byte)(this.AtlasTextureVertical.AtlasSize.ColumnsCount - 1), 0)
                };
            }

            return new ProceduralTexture(
                "Composed " + this.Id,
                generateTextureCallback: request => ClientComposeIcon(request, textureResources),
                isTransparent: true,
                isUseCache: true,
                dependsOn: textureResources);
        }

        protected override void ClientDeinitializeStructure(IStaticWorldObject gameObject)
        {
            base.ClientDeinitializeStructure(gameObject);
            foreach (var occupiedTile in gameObject.OccupiedTiles)
            {
                SharedWallConstructionRefreshHelper.SharedRefreshNeighborObjects(occupiedTile,
                                                                                 isDestroy: true);
            }
        }

        protected override void ClientInitialize(ClientInitializeData data)
        {
            // don't use base implementation
            //base.ClientInitialize(data);

            this.ClientAddAutoStructurePointsBar(data);

            var publicState = data.SyncPublicState;
            var clientState = data.ClientState;

            this.ClientSetupDoor(data);

            // subscribe on IsOpened change
            var staticWorldObject = data.GameObject;
            publicState.ClientSubscribe(
                _ => _.IsOpened,
                newIsOpened =>
                {
                    clientState.IsOpened = newIsOpened;
                    this.SharedCreatePhysics(staticWorldObject);
                    clientState.SpriteAnimator.Start(isPositiveDirection: newIsOpened);
                    Client.Audio.PlayOneShot(
                        this.SoundResourceDoorStart,
                        staticWorldObject);

                    // we don't use the looped "process" door sound
                    //Client.Audio.PlayOneShotLooped(
                    //    this.SoundResourceDoorProcess,
                    //    staticWorldObject,
                    //    duration: this.DoorOpenCloseAnimationDuration);

                    Client.Audio.PlayOneShot(
                        this.SoundResourceDoorEnd,
                        staticWorldObject,
                        delay: this.DoorOpenCloseAnimationDuration - this.DoorOpenCloseAnimationDuration / 5f);
                },
                subscriptionOwner: clientState);

            // subscribe on IsHorizontalDoor change
            publicState.ClientSubscribe(
                _ => _.IsHorizontalDoor,
                newIsHorizontal => this.ClientSetupDoor(data),
                subscriptionOwner: clientState);

            foreach (var occupiedTile in staticWorldObject.OccupiedTiles)
            {
                SharedWallConstructionRefreshHelper.SharedRefreshNeighborObjects(occupiedTile,
                                                                                 isDestroy: false);
            }
        }

        protected override void ClientInteractStart(ClientObjectData data)
        {
            InteractableStaticWorldObjectHelper.ClientStartInteract(data.GameObject);
        }

        protected override void ClientObserving(ClientObjectData data, bool isObserving)
        {
            base.ClientObserving(data, isObserving);
            StructureLandClaimIndicatorManager.ClientObserving(data.GameObject, isObserving);
        }

        protected BaseUserControlWithWindow ClientOpenUI(ClientObjectData data)
        {
            return WindowDoor.Open(
                new ViewModelWindowDoor(data.GameObject));
        }

        protected override void PrepareConstructionConfig(
            ConstructionTileRequirements tileRequirements,
            ConstructionStageConfig build,
            ConstructionStageConfig repair,
            ConstructionUpgradeConfig upgrade,
            out ProtoStructureCategory category)
        {
            category = GetCategory<StructureCategoryBuildings>();

            build.StagesCount = 1;
        }

        protected override ITextureResource PrepareDefaultTexture(Type thisType)
        {
            return this.AtlasTextureHorizontal;
        }

        protected virtual void PrepareProtoDoor()
        {
        }

        protected sealed override void PrepareProtoStaticWorldObject()
        {
            base.PrepareProtoStaticWorldObject();
            this.doorAutoOpenRadiusSqr = this.DoorAutoOpenRadius * this.DoorAutoOpenRadius;
            this.PrepareProtoDoor();
        }

        protected override void ServerInitialize(ServerInitializeData data)
        {
            base.ServerInitialize(data);

            var worldObject = data.GameObject;
            var privateState = data.PrivateState;
            WorldObjectOwnersSystem.ServerInitialize(worldObject);

            if (!data.IsFirstTimeInit)
            {
                if (!this.HasOwnersList)
                {
                    privateState.AccessMode = WorldObjectAccessMode.OpensToEveryone;
                }

                return;
            }

            var publicState = data.PublicState;
            privateState.AccessMode = this.HasOwnersList
                                          ? WorldObjectAccessMode.OpensToObjectOwnersOrAreaOwners
                                          : WorldObjectAccessMode.OpensToEveryone;

            // refresh door type
            publicState.IsHorizontalDoor = this.IsHorizontalDoorOnly
                                           ?? DoorHelper.IsHorizontalDoorNeeded(worldObject.OccupiedTile,
                                                                                checkExistingDoor: false);
            publicState.IsOpened = true;

            // refresh nearby door types (horizontal/vertical)
            foreach (var occupiedTile in worldObject.OccupiedTiles)
            {
                DoorHelper.RefreshNeighborDoorType(occupiedTile);
            }

            foreach (var occupiedTile in worldObject.OccupiedTiles)
            {
                SharedWallConstructionRefreshHelper.SharedRefreshNeighborObjects(occupiedTile,
                                                                                 isDestroy: false);
            }
        }

        protected override void ServerOnStaticObjectZeroStructurePoints(
            WeaponFinalCache weaponCache,
            ICharacter byCharacter,
            IWorldObject targetObject)
        {
            base.ServerOnStaticObjectZeroStructurePoints(weaponCache, byCharacter, targetObject);

            if (weaponCache != null)
            {
                // door was destroyed (and not deconstructed by a crowbar or any other means)
                LandClaimSystem.ServerOnRaid(((IStaticWorldObject)targetObject).Bounds,
                                             byCharacter);
            }
        }

        protected override void ServerUpdate(ServerUpdateData data)
        {
            var publicState = data.PublicState;
            var isOpened = publicState.IsOpened;
            var isShouldBeOpened = this.ServerCheckIsDoorShouldBeOpened(data.GameObject, data.PrivateState);
            if (isOpened == isShouldBeOpened)
            {
                return;
            }

            isOpened = isShouldBeOpened;
            publicState.IsOpened = isOpened;
            // recreate physics
            this.SharedCreatePhysics(data.GameObject);
        }

        protected override double SharedCalculateDamageByWeapon(
            WeaponFinalCache weaponCache,
            double damagePreMultiplier,
            IStaticWorldObject targetObject,
            out double obstacleBlockDamageCoef)
        {
            if (IsServer)
            {
                damagePreMultiplier = LandClaimSystem.ServerAdjustDamageToUnprotectedStrongBuilding(weaponCache,
                                                                                                    targetObject,
                                                                                                    damagePreMultiplier);
            }

            var damage = base.SharedCalculateDamageByWeapon(weaponCache,
                                                            damagePreMultiplier,
                                                            targetObject,
                                                            out obstacleBlockDamageCoef);
            return damage;
        }

        protected void SharedCreateDoorPhysics(IPhysicsBody physicsBody, bool isHorizontalDoor, bool isOpened)
        {
            // custom center offset is used for interaction zone raycasting
            physicsBody.SetCustomCenterOffset(
                isHorizontalDoor
                    ? (0.5, 0.2)
                    : (0.5, 0.5));

            double doorSize = this.DoorSizeTiles;
            if (isHorizontalDoor)
            {
                // horizontal door physics
                const double horizontalDoorHeight = 0.5;
                if (isOpened)
                {
                    const double horizontalDoorBorderWidth = 0.125;
                    physicsBody.AddShapeRectangle(
                                   size: (horizontalDoorBorderWidth, horizontalDoorHeight),
                                   offset: (0, WallPatterns.PhysicsOffset))
                               .AddShapeRectangle(
                                   size: (horizontalDoorBorderWidth, horizontalDoorHeight),
                                   offset: (doorSize - horizontalDoorBorderWidth, WallPatterns.PhysicsOffset));
                }
                else
                {
                    physicsBody.AddShapeRectangle(
                        size: (doorWidth: doorSize, horizontalDoorHeight),
                        offset: (0, WallPatterns.PhysicsOffset));
                }

                if (!isOpened)
                {
                    // horizontal door hitboxes
                    physicsBody
                        .AddShapeRectangle(
                            size: (doorWidth: doorSize, 0.6),
                            offset: (0, WallPatterns.PhysicsOffset),
                            group: CollisionGroups.HitboxMelee)
                        .AddShapeRectangle(
                            size: (doorWidth: doorSize, doorSize),
                            offset: (0, WallPatterns.PhysicsOffset),
                            group: CollisionGroups.HitboxRanged);
                }

                // click area
                physicsBody
                    .AddShapeRectangle(
                        size: (doorWidth: doorSize, doorSize),
                        offset: (0, WallPatterns.PhysicsOffset),
                        group: CollisionGroups.ClickArea);
                return;
            }

            // vertical door physics
            const double verticalDoorWidth = 0.4,
                         horizontalOffset = 0.5 - verticalDoorWidth * 0.5,
                         doorOpenedColliderHeight = 0.125;

            if (isOpened)
            {
                physicsBody.AddShapeRectangle(
                               size: (verticalDoorWidth, doorOpenedColliderHeight),
                               offset: (horizontalOffset, 0))
                           .AddShapeRectangle(
                               size: (verticalDoorWidth, doorOpenedColliderHeight),
                               offset: (horizontalOffset, doorSize - doorOpenedColliderHeight));
            }
            else
            {
                physicsBody.AddShapeRectangle(
                    size: (verticalDoorWidth, doorWidth: doorSize),
                    offset: (horizontalOffset, 0));
            }

            if (!isOpened)
            {
                // vertical door hitboxes
                physicsBody
                    .AddShapeRectangle(
                        size: (verticalDoorWidth, doorSize + 0.35),
                        offset: (horizontalOffset, 0),
                        group: CollisionGroups.HitboxMelee)
                    .AddShapeRectangle(
                        size: (verticalDoorWidth, doorSize + 0.75),
                        offset: (horizontalOffset, 0),
                        group: CollisionGroups.HitboxRanged);
            }

            // click area
            physicsBody
                .AddShapeRectangle(
                    size: (doorWidth: 1, doorSize + 0.75),
                    group: CollisionGroups.ClickArea);
        }

        protected override void SharedCreatePhysics(CreatePhysicsData data)
        {
            var publicState = data.SyncPublicState;
            var isHorizontalDoor = publicState.IsHorizontalDoor;
            var isOpened = publicState.IsOpened;

            this.SharedCreateDoorPhysics(data.PhysicsBody, isHorizontalDoor, isOpened);
        }

        private void ClientSetupDoor(ClientInitializeData data)
        {
            var sceneObject = Client.Scene.GetSceneObject(data.GameObject);
            var publicState = data.SyncPublicState;
            var clientState = data.ClientState;
            var isHorizontalDoor = publicState.IsHorizontalDoor;
            var isOpened = publicState.IsOpened;

            var atlas = isHorizontalDoor ? this.AtlasTextureHorizontal : this.AtlasTextureVertical;
            var renderer = Client.Rendering.CreateSpriteRenderer(
                sceneObject,
                atlas.Chunk(0, 0),
                drawOrder: DrawOrder.Default);

            renderer.PositionOffset = (0,
                                       isHorizontalDoor
                                           ? DrawWorldOffsetYHorizontalDoor
                                           : DrawWorldOffsetYVerticalDoor);
            renderer.DrawOrderOffsetY = isHorizontalDoor
                                            ? WallPatterns.DrawOffsetNormal - DrawWorldOffsetYHorizontalDoor
                                            : this.DoorSizeTiles - 0.01 - DrawWorldOffsetYVerticalDoor;

            var spriteSheetAnimator = sceneObject.AddComponent<ClientComponentDoorSpriteSheetAnimator>();
            var atlasColumnsCount = atlas.AtlasSize.ColumnsCount;

            clientState.Renderer?.Destroy();
            clientState.Renderer = renderer;
            clientState.SpriteAnimator?.Destroy();
            clientState.SpriteAnimator = spriteSheetAnimator;

            clientState.ExtraDoorRendererObject?.Destroy();
            clientState.ExtraDoorRendererObject = null;

            if (isHorizontalDoor)
            {
                // add extra sprite renderer for horizontal door - door base
                clientState.ExtraDoorRendererObject = Client.Rendering.CreateSpriteRenderer(
                    sceneObject,
                    this.TextureBaseHorizontal,
                    drawOrder: DrawOrder.Floor + 1,
                    positionOffset: (0, DrawWorldOffsetYHorizontalDoor),
                    spritePivotPoint: (0, 0));
            }
            else
            {
                // add extra sprite renderer for vertical door - side door front sprite
                clientState.ExtraDoorRendererObject = Client.Rendering.CreateSpriteRenderer(
                    sceneObject,
                    atlas.Chunk((byte)(atlasColumnsCount - 1), 0),
                    drawOrder: DrawOrder.Default,
                    positionOffset: (
                                        0,
                                        DrawWorldOffsetYHorizontalDoor));
            }

            var framesCount = isHorizontalDoor ? atlasColumnsCount : atlasColumnsCount - 1;
            spriteSheetAnimator.Setup(
                renderer,
                ClientComponentSpriteSheetAnimator.CreateAnimationFrames(
                    atlas,
                    columns: (byte)framesCount),
                frameDurationSeconds: this.DoorOpenCloseAnimationDuration / (double)framesCount);

            // refresh opened/closed state
            clientState.IsOpened = isOpened;
            // re-create physics
            this.SharedCreatePhysics(data.GameObject);
            spriteSheetAnimator.SetCurrentFrame(isOpened ? spriteSheetAnimator.FramesTextureResources.Length : 0);
        }

        private bool ServerCheckIsDoorShouldBeOpened(IStaticWorldObject worldObject, TPrivateState privateState)
        {
            var mode = privateState.AccessMode;
            if (mode == WorldObjectAccessMode.Closed)
            {
                return false;
            }

            var objectCenterPosition = worldObject.TilePosition.ToVector2D()
                                       + this.DoorOpenCheckOffset;

            using (var charactersNearby = Api.Shared.GetTempList<ICharacter>())
            {
                Server.World.GetScopedByPlayers(worldObject, charactersNearby);
                if (charactersNearby.Count == 0)
                {
                    // no characters nearby
                    return false;
                }

                foreach (var character in charactersNearby)
                {
                    if (!character.IsOnline
                        || character.ProtoCharacter is PlayerCharacterSpectator)
                    {
                        continue;
                    }

                    if ((character.Position - objectCenterPosition).LengthSquared
                        > this.doorAutoOpenRadiusSqr)
                    {
                        // too far from this door
                        continue;
                    }

                    if (!WorldObjectAccessModeSystem.ServerHasAccess(worldObject,
                                                                     character,
                                                                     mode,
                                                                     writeToLog: false))
                    {
                        continue;
                    }

                    // we don't do this check because it requires character to be the door owner
                    //if (!this.SharedCanInteract(character, gameObject, writeToLog: false))
                    //{
                    //    return false;
                    //}

                    // we do this check instead
                    // ensure that the character is inside the interaction area
                    // and there is a direct line of sight between the character and the door
                    if (this.SharedIsInsideCharacterInteractionArea(character, worldObject, writeToLog: false))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public abstract class ProtoObjectDoor
        : ProtoObjectDoor<ObjectDoorPrivateState, ObjectDoorPublicState, ObjectDoorClientState>
    {
    }
}