﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CedMod.Addons.QuerySystem;
using Footprinting;
using GameCore;
using InventorySystem.Items.Firearms;
using MEC;
using Newtonsoft.Json;
using PlayerStatsSystem;
using PluginAPI.Core;
using Log = PluginAPI.Core.Log;

namespace CedMod
{
    public static class API
    {
        public static string DevUri = "api.dev.cedmod.nl";
        public static string APIUrl
        {
            get
            {
                return QuerySystem.IsDev ? DevUri : "api.cedmod.nl";
            }
        }

        public static object APIRequest(string endpoint, string arguments, bool returnstring = false, string type = "GET")
        {
            string response = "";  
            try
            {
                HttpResponseMessage resp = null;
                if (type == "GET")
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("ApiKey", CedModMain.Singleton.Config.CedMod.CedModApiKey);
                        resp = client.GetAsync($"http{(QuerySystem.UseSSL ? "s" : "")}://" + APIUrl + "/" + endpoint + arguments).Result;
                        response = resp.Content.ReadAsStringAsync().Result;
                    }
                }
                
                if (type == "DELETE")
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("ApiKey", CedModMain.Singleton.Config.CedMod.CedModApiKey);
                        resp = client.DeleteAsync($"http{(QuerySystem.UseSSL ? "s" : "")}://" + APIUrl + "/" + endpoint + arguments).Result;
                        response = resp.Content.ReadAsStringAsync().Result;
                    }
                }

                if (type == "POST")
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("ApiKey", CedModMain.Singleton.Config.CedMod.CedModApiKey);
                        resp = client.PostAsync($"http{(QuerySystem.UseSSL ? "s" : "")}://" + APIUrl + "/" + endpoint, new StringContent(arguments, Encoding.UTF8, "application/json")).Result;
                        response = resp.Content.ReadAsStringAsync().Result;
                    }
                }

                if (!resp.IsSuccessStatusCode)
                {
                    Log.Error($"API request failed: {resp.StatusCode} | {response}");
                    return null;
                }
                if (!returnstring)
                {
                    var jsonObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(response);
                    return jsonObj;
                }
                return response;
            }
            catch (WebException ex)
            {
                using (StreamReader r = new StreamReader(((HttpWebResponse)ex.Response).GetResponseStream()))
                {
                    response = r.ReadToEnd();
                }
                Log.Error($"API request failed: {response} | {ex.Message}");
                return null;
            }
        }
        
        public static void Mute(Player player, string adminname, double duration, string reason, MuteType Type)
        {
            long realduration = (long)TimeSpan.FromSeconds(duration).TotalMinutes;
            string req = "{\"AdminName\": \"" + adminname + "\"," +
                         "\"Muteduration\": " + realduration + "," +
                         "\"Type\": " + (int)Type + "," +
                         "\"Mutereason\": \"" + reason + "\"}";
            Dictionary<string, string> result = (Dictionary<string, string>) APIRequest($"api/Mute/{player.UserId}", req, false, "POST");
        }
        
        public static void UnMute(Player player)
        {
            Dictionary<string, string> result = (Dictionary<string, string>) APIRequest($"api/Mute/{player.UserId}", "", false, "DELETE");
        }

        public static void Ban(CedModPlayer player, long duration, string sender, string reason, bool bc = true)
        {
            long realduration = (long)TimeSpan.FromSeconds(duration).TotalMinutes;
            if (duration >= 1)
            {
                string json = "{\"Userid\": \"" + player.UserId + "\"," +
                              "\"Ip\": \"" + player.IpAddress+"\"," +
                              "\"AdminName\": \"" + sender.Replace("\"", "'") + "\"," +
                              "\"BanDuration\": "+realduration+"," +
                              "\"BanReason\": \""+reason.Replace("\"", "'")+"\"}";
                Dictionary<string, string> result = (Dictionary<string, string>) APIRequest("Auth/Ban", json, false, "POST"); 
                ThreadDispatcher.ThreadDispatchQueue.Enqueue(() =>  Timing.RunCoroutine(StrikeBad(player, result.ContainsKey("preformattedmessage") ? result["preformattedmessage"] : $"Failed to execute api request {JsonConvert.SerializeObject(result)}")));
                if (bc)
                    Server.SendBroadcast(ConfigFile.ServerConfig.GetString("broadcast_ban_text", "%nick% has been banned from this server.").Replace("%nick%", player.Nickname), (ushort) ConfigFile.ServerConfig.GetInt("broadcast_ban_duration", 5), Broadcast.BroadcastFlags.Normal);
            }
            else
            {
                if (duration <= 0)
                {
                    ThreadDispatcher.ThreadDispatchQueue.Enqueue(() =>  Timing.RunCoroutine(StrikeBad(player, reason + "\n" + CedModMain.Singleton.Config.CedMod.AdditionalBanMessage)));
                }
            }
        }
        
        public static void BanId(string UserId, long duration, string sender, string reason, bool bc = true)
        {
            double realduration = TimeSpan.FromSeconds(duration).TotalMinutes;
            if (duration >= 1)
            {
                string json = "{\"Userid\": \"" + UserId + "\"," +
                              "\"Ip\": \"0.0.0.0\"," +
                              "\"AdminName\": \"" + sender.Replace("\"", "'") + "\"," +
                              "\"BanDuration\": "+realduration+"," +
                              "\"BanReason\": \""+reason.Replace("\"", "'")+"\"}";
                Dictionary<string, string> result = (Dictionary<string, string>) APIRequest("Auth/Ban", json, false, "POST");
                CedModPlayer player = CedModPlayer.Get(UserId);
                if (player != null)
                {
                    if (player != null)
                    {
                        ThreadDispatcher.ThreadDispatchQueue.Enqueue(() =>  Timing.RunCoroutine(StrikeBad(player, result.ContainsKey("preformattedmessage") ? result["preformattedmessage"] : $"Failed to execute api request {JsonConvert.SerializeObject(result)}")));
                    }

                    if (bc)
                        Server.SendBroadcast(ConfigFile.ServerConfig.GetString("broadcast_ban_text", "%nick% has been banned from this server.").Replace("%nick%", player.Nickname), (ushort) ConfigFile.ServerConfig.GetInt("broadcast_ban_duration", 5), Broadcast.BroadcastFlags.Normal);
                }
            }
            else
            {
                if (duration <= 0)
                {
                    CedModPlayer player = CedModPlayer.Get(UserId);
                    if (player != null)
                    {
                        ThreadDispatcher.ThreadDispatchQueue.Enqueue(() =>  Timing.RunCoroutine(StrikeBad(player, reason + "\n" + CedModMain.Singleton.Config.CedMod.AdditionalBanMessage)));
                    }
                }
            }
        }

        public static IEnumerator<float> StrikeBad(CedModPlayer player, string reason)
        {
            player.ReferenceHub.playerStats.KillPlayer(new DisruptorDamageHandler(new Footprint(player.ReferenceHub), -1));
            yield return Timing.WaitForSeconds(0.1f);
            player.Disconnect(reason);
        }
    }
}
