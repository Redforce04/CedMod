﻿using System;
using System.Collections.Generic;
using CommandSystem;
using UnityEngine;

namespace CedMod.Commands
{
    public class PlayersCommand : ICommand
    {
        public string Command { get; } = "players";

        public string[] Aliases { get; } = new string[]
        {
        };

        public string Description { get; } = "Gives the ammount of current players";
        public bool Execute(ArraySegment<string> arguments, ICommandSender sender,
            out string response)
        {
            Dictionary<GameObject, ReferenceHub> allHubs = ReferenceHub.GetAllHubs();
            response = string.Format("List of players ({0}):", (object) (ServerStatic.IsDedicated ? allHubs.Count - 1 : allHubs.Count));
            using (Dictionary<GameObject, ReferenceHub>.ValueCollection.Enumerator enumerator = allHubs.Values.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    ReferenceHub current = enumerator.Current;
                    if (!current.isDedicatedServer)
                        sender.Respond("- " + (current.nicknameSync.CombinedName ?? "(no nickname)") + ": " + (current.characterClassManager.UserId ?? "(no User ID)") + " [" + (object) current.queryProcessor.PlayerId + "]");
                }
            }
            return true;
        }
    }
}