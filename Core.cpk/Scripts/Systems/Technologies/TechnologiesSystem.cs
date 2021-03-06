﻿namespace AtomicTorch.CBND.CoreMod.Systems.Technologies
{
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.CoreMod.Technologies;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Scripting.Network;

    public class TechnologiesSystem : ProtoSystem<TechnologiesSystem>
    {
        public const string NotificationCannotUnlockTech = "Cannot unlock tech";

        public override string Name => "Technologies system";

        public static void ClientUnlockGroup(TechGroup techGroup)
        {
            if (!techGroup.SharedCanUnlock(Client.Characters.CurrentPlayerCharacter, out var error))
            {
                NotificationSystem.ClientShowNotification(NotificationCannotUnlockTech,
                                                          error,
                                                          NotificationColor.Bad);
                return;
            }

            Instance.CallServer(_ => _.ServerRemote_UnlockGroup(techGroup));
        }

        public static void ClientUnlockNode(TechNode techNode)
        {
            if (!techNode.SharedCanUnlock(Client.Characters.CurrentPlayerCharacter, out var error))
            {
                NotificationSystem.ClientShowNotification(NotificationCannotUnlockTech,
                                                          error,
                                                          NotificationColor.Bad);
                return;
            }

            Instance.CallServer(_ => _.ServerRemote_UnlockNode(techNode));
        }

        public static void ServerInitCharacterTechnologies(PlayerCharacterTechnologies technologies)
        {
            foreach (var techGroup in FindProtoEntities<TechGroup>())
            {
                if (techGroup.GroupRequirements.Count > 0)
                {
                    continue;
                }

                // add free group
                technologies.ServerAddGroup(techGroup);

                foreach (var techNode in techGroup.RootNodes)
                {
                    ProcessNode(techNode);
                }
            }

            void ProcessNode(TechNode techNode)
            {
                if (techNode.LearningPointsPrice > 0)
                {
                    return;
                }

                // add free node
                technologies.ServerAddNode(techNode);

                foreach (var dependentNode in techNode.DependentNodes)
                {
                    ProcessNode(dependentNode);
                }
            }
        }

        public static void ServerUnlockGroup(ICharacter character, TechGroup techGroup)
        {
            techGroup.SharedValidateCanUnlock(character);
            var technologies = character.SharedGetTechnologies();
            technologies.ServerRemoveLearningPoints(techGroup.LearningPointsPrice);
            technologies.ServerAddGroup(techGroup);
        }

        public static void ServerUnlockNode(ICharacter character, TechNode techNode)
        {
            techNode.SharedValidateCanUnlock(character);
            var technologies = character.SharedGetTechnologies();
            technologies.ServerRemoveLearningPoints(techNode.LearningPointsPrice);
            technologies.ServerAddNode(techNode);
        }

        private void ServerRemote_UnlockGroup(TechGroup techGroup)
        {
            ServerUnlockGroup(ServerRemoteContext.Character, techGroup);
        }

        private void ServerRemote_UnlockNode(TechNode techNode)
        {
            ServerUnlockNode(ServerRemoteContext.Character, techNode);
        }
    }
}