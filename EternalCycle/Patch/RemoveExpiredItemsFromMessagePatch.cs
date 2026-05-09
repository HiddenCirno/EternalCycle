using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Loaders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Dialog;
using SPTarkov.Server.Core.Models.Spt.Launcher;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;
using VulcanCore;

namespace VulcanCore
{
    public class RemoveExpiredItemsFromMessagePatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(DialogueController).GetMethod("RemoveExpiredItemsFromMessage", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        [PatchPrefix]
        public static bool Prefix(DialogueController __instance, MongoId sessionId, MongoId dialogueId)
        {
            var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
            var dialogueHelper = ServiceLocator.ServiceProvider.GetService<DialogueHelper>();
            var randomUtil = ServiceLocator.ServiceProvider.GetService<RandomUtil>();
            var mailSendService = ServiceLocator.ServiceProvider.GetService<MailSendService>();
            var logger = ServiceLocator.ServiceProvider.GetService<ISptLogger<VulcanCore>>();

            var dialogs = dialogueHelper.GetDialogsForProfile(sessionId);
            if (!dialogs.TryGetValue(dialogueId, out var dialog))
            {
                //VulcanLog.Debug("dialogs is null", logger);
                return false;
            }

            if (dialog.Messages is null)
            {
                //VulcanLog.Debug("dialog.Messages is null", logger);
                return false;
            }
            var messageHasExpiredMethod = typeof(DialogueController)
    .GetMethod("MessageHasExpired", BindingFlags.NonPublic | BindingFlags.Instance);

            HashSet<SendMessageDetails> detailsList = new HashSet<SendMessageDetails>();

            // 在Patch中调用
            foreach (var message in dialog.Messages.Where(m =>
                (bool)messageHasExpiredMethod.Invoke(__instance, new object[] { m })))
            {
                // Reset expired message items data
                message.Items = new();
                //VulcanLog.Debug("expired start", logger);
                //VulcanLog.Debug($"TraderId: {dialogueId}", logger);
                var traderDialogMessages = databaseService.GetTrader(dialogueId)?.Dialogue;
                if (message?.Items?.Data?.Count <= 0 || message.TemplateId == null)
                {
                    continue;  // 早退出，不满足条件时直接返回
                }

                if (traderDialogMessages?.TryGetValue("insuranceFound", out var successMessageIds) != true ||
                    successMessageIds == null || !successMessageIds.Contains(message.TemplateId))
                {
                    continue;  // 早退出，条件不满足时直接返回
                }

                if (traderDialogMessages.TryGetValue("insuranceExpired", out var responseMessageIds) && responseMessageIds != null)
                {
                    var responseMessageId = randomUtil.GetArrayValue(responseMessageIds);
                    detailsList.Add(new SendMessageDetails
                    {
                        RecipientId = sessionId,
                        Sender = MessageType.NpcTraderMessage,
                        DialogType = MessageType.NpcTraderMessage,
                        Trader = dialogueId,
                        TemplateId = responseMessageId,
                        Items = []
                    });
                }
            }
            if (detailsList.Count > 0)
            {
                foreach (var insuranceExpiredDialog in detailsList)
                {
                    mailSendService.SendMessageToPlayer(insuranceExpiredDialog);
                }
            }

            return false; // 跳过原始方法执行
        }

    }
}