﻿namespace AtomicTorch.CBND.CoreMod.Systems.ServerWelcomeMessage
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using AtomicTorch.CBND.CoreMod.Systems.PvE;
    using AtomicTorch.CBND.CoreMod.Systems.ServerOperator;
    using AtomicTorch.CBND.CoreMod.UI;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.CBND.GameApi.ServicesClient;
    using AtomicTorch.CBND.GameApi.ServicesClient.Servers;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class WelcomeMessageSystem : ProtoSystem<WelcomeMessageSystem>
    {
        public const string OfficialServerWelcomeMessagePvE =
            @"Welcome to the official CryoFall server!
              [br]
              [br]
              [b]Important:[/b]
              This is a PvE (player versus environment) server. On this server, players cannot attack each other either directly or indirectly. This server is dedicated to peaceful exploration of the game without hostile player engagement. You cannot kill, destroy, steal or otherwise attack other players. Aside from that, the server offers you a complete CryoFall experience with its dangerous creatures, technologies to unlock and world to explore. See where this journey will take you.
              [br]
              [br]
              If you'd rather have a more thrilling and unrestricted experience—please consider joining a PvP server.
              [br]
              [br]
              [b]Server rules:[/b]
              [*]Be respectful and courteous to others. We do not tolerate any form of abuse, discrimination, etc.
              [*]Comply with all requests by server moderators.
              [*]Do not spam or flood the chat or use excessive profanity.
              [*]Do not try to circumvent or exploit any of the PvE restrictions.
              [*]Do not abuse game rules or mechanics in order to grief or otherwise inconvenience other players.
              [br]
              [br]
              Good luck! :)";

        public const string OfficialServerWelcomeMessagePvP =
            @"Welcome to the official CryoFall server!
              [br]
              [br]
              [b]Important:[/b]
              This is a free-for-all PvP server. Please remember that on PvP-centered servers, you will encounter aggression from other players. You are expected to fight and defend yourself. You will have to properly protect yourself and exercise vigilance at all times. You might also consider joining forces with other survivors to increase your chances.
              [br]
              [br]
              If you'd rather have a more relaxed experience, especially if this is your first time playing—please consider joining a PvE server.
              [br]
              [br]
              [b]Server rules:[/b]
              [*]Be respectful and courteous to others. We do not tolerate any form of abuse, discrimination, etc.
              [*]Comply with all requests by server moderators.
              [*]Do not spam or flood the chat or use excessive profanity.
              [br]
              [br]
              Good luck! :)";

        public const string WelcomeToServerTitleFormat = "Welcome to {0}";

        private static readonly IClientStorage ClientStorageLastServerMessage;

        private static Task<string> lastGetWelcomeMessageTask;

        static WelcomeMessageSystem()
        {
            if (Api.IsClient)
            {
                ClientStorageLastServerMessage = Api.Client.Storage.GetStorage("Servers/LastWelcomeMessages");
                ClientStorageLastServerMessage.RegisterType(typeof(ServerAddress));
                ClientStorageLastServerMessage.RegisterType(typeof(AtomicGuid));
            }
        }

        public override string Name => "Welcome message system";

        [SuppressMessage("ReSharper", "CanExtractXamlLocalizableStringCSharp")]
        public static async void ClientEditWelcomeMessage()
        {
            var originalText = await lastGetWelcomeMessageTask;
            originalText = originalText != null
                               ? TrimSpacesOnEachLine(originalText)
                               : string.Empty;

            var textBox = new TextBox
            {
                Text = originalText,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                FontSize = 13,
                Width = 350,
                Height = 350,
                TextAlignment = TextAlignment.Left,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Top,
                AcceptsReturn = true,
                Margin = new Thickness(-20, -15, -20, 0),
                MaxLength = 4096
            };

            DialogWindow.ShowDialog(
                title: null,
                content: textBox,
                okAction: () =>
                          {
                              var text = textBox.Text;
                              Instance.CallServer(_ => _.ServerRemote_SetWelcomeMessage(text));
                              lastGetWelcomeMessageTask = Task.FromResult(text);
                          },
                cancelAction: () => { },
                okText: CoreStrings.Button_Save,
                focusOnCancelButton: true);
        }

        public static async void ClientShowWelcomeMessage()
        {
            if (lastGetWelcomeMessageTask == null)
            {
                return;
            }

            var welcomeMessage = await lastGetWelcomeMessageTask;
            ClientShowWelcomeMessageInternal(welcomeMessage);
        }

        protected override void PrepareSystem()
        {
            if (IsServer)
            {
                return;
            }

            Client.Characters.CurrentPlayerCharacterChanged += Refresh;

            void Refresh()
            {
                if (Api.Client.Characters.CurrentPlayerCharacter != null)
                {
                    RefreshWelcomeMessage();
                }
            }
        }

        private static async void ClientShowWelcomeMessageInternal(string welcomeMessage)
        {
            if (string.IsNullOrWhiteSpace(welcomeMessage))
            {
                if (!Client.CurrentGame.IsConnectedToOfficialServer)
                {
                    return;
                }

                welcomeMessage = GetOfficialServerWelcomeMessage();
            }

            var game = Client.CurrentGame;
            var serverInfo = game.ServerInfo;

            await LoadingSplashScreenManager.WaitHiddenAsync();

            if (game.ConnectionState != ConnectionState.Connected
                || serverInfo != Client.CurrentGame.ServerInfo)
            {
                return;
            }

            var dialogWindow = DialogWindow.ShowDialog(
                string.Format(WelcomeToServerTitleFormat, serverInfo.ServerName),
                new ScrollViewer()
                {
                    MaxHeight = 380,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = new FormattedTextBlock()
                    {
                        Content = welcomeMessage,
                        TextWrapping = TextWrapping.Wrap,
                        TextTrimming = TextTrimming.None,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                },
                closeByEscapeKey: false);

            dialogWindow.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            dialogWindow.GameWindow.FocusOnControl = null;
            dialogWindow.GameWindow.Width = 530;
            dialogWindow.GameWindow.UpdateLayout();
        }

        private static string GetOfficialServerWelcomeMessage()
        {
            var message = PveSystem.ClientIsPve(logErrorIfDataIsNotYetAvailable: true)
                              ? OfficialServerWelcomeMessagePvE
                              : OfficialServerWelcomeMessagePvP;

            return message.Trim();
        }

        private static async void RefreshWelcomeMessage()
        {
            if (Api.IsEditor)
            {
                return;
            }

            var gameServerInfo = Client.CurrentGame.ServerInfo;

            var serverAddress = gameServerInfo.ServerAddress;
            lastGetWelcomeMessageTask = await Instance.CallServer(_ => _.ServerRemote_GetWelcomeMessage())
                                                      .ContinueWith(async t =>
                                                                    {
                                                                        await PveSystem.ClientAwaitPvEModeFromServer();
                                                                        return t.Result;
                                                                    },
                                                                    TaskContinuationOptions.ExecuteSynchronously);

            var welcomeMessage = await lastGetWelcomeMessageTask;
            if (string.IsNullOrWhiteSpace(welcomeMessage))
            {
                if (!Client.CurrentGame.IsConnectedToOfficialServer)
                {
                    return;
                }

                welcomeMessage = GetOfficialServerWelcomeMessage();
            }

            // load the last messages from storage
            if (!ClientStorageLastServerMessage.TryLoad(out Dictionary<ServerAddress, string> dictLastMessages))
            {
                dictLastMessages = new Dictionary<ServerAddress, string>();
            }

            if (dictLastMessages.TryGetValue(serverAddress, out var lastMessage)
                && lastMessage == welcomeMessage)
            {
                // the player already saw this welcome message
                return;
            }

            dictLastMessages[serverAddress] = welcomeMessage;
            ClientStorageLastServerMessage.Save(dictLastMessages);
            ClientShowWelcomeMessageInternal(welcomeMessage);
        }

        private static string TrimSpacesOnEachLine(string text)
        {
            text = text.Replace("\r", "");
            var split = text.Split('\n');
            for (var index = 0; index < split.Length; index++)
            {
                var line = split[index];
                line = line.Trim();
                split[index] = line;
            }

            text = string.Join(Environment.NewLine, split);
            return text;
        }

        private string ServerRemote_GetWelcomeMessage()
        {
            return Server.Core.WelcomeMessageText;
        }

        private void ServerRemote_SetWelcomeMessage(string text)
        {
            var character = ServerRemoteContext.Character;
            if (!ServerOperatorSystem.SharedIsOperator(character))
            {
                throw new Exception("Not a server operator: " + character);
            }

            text = text?.Trim() ?? string.Empty;
            Api.Server.Core.WelcomeMessageText = text;

            Logger.Important("Server welcome message changed to:"
                             + Environment.NewLine
                             + text);
        }
    }
}