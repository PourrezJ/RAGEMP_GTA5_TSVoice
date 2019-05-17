﻿using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamSpeak3QueryApi.Net;
using TeamSpeak3QueryApi.Net.Specialized;

namespace RAGEMP_TsVoice
{
    public class Teamspeak : Script
    {
        private static string TeamspeakQueryAddress { get; set; }
        private static short TeamspeakQueryPort { get; set; }
        private static string TeamspeakPort { get; set; }
        private static string TeamspeakLogin { get; set; }
        private static string TeamspeakPassword { get; set; }
        private static string TeamspeakChannel { get; set; }

        public Teamspeak()
        {
            Console.WriteLine("Teamspeak Wrapper Initialization...");
        }

        [ServerEvent(Event.PlayerConnected)]
        public void OnPlayerConnected(Client client)
        {
            Teamspeak.Connect(client, client.SocialClubName);
        }


        [Command("tsstop")]
        public static void tsstop(Client client)
        {
            client.TriggerEvent("DisconnectTeamspeak");
        }

        [ServerEvent(Event.ResourceStart)]
        public void OnResourceStart()
        {
            Task.Run(async () =>
            {
                try
                {
                    TeamspeakQueryAddress = NAPI.Resource.GetSetting<string>(this, "teamspeak_query_address");
                    TeamspeakQueryPort = NAPI.Resource.GetSetting<short>(this, "teamspeak_query_port");
                    TeamspeakPort = NAPI.Resource.GetSetting<string>(this, "teamspeak_port");
                    TeamspeakLogin = NAPI.Resource.GetSetting<string>(this, "teamspeak_login");
                    TeamspeakPassword = NAPI.Resource.GetSetting<string>(this, "teamspeak_password");
                    TeamspeakChannel = NAPI.Resource.GetSetting<string>(this, "teamspeak_channel");

                    await CheckSpeakingClients();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                
                Console.WriteLine("Teamspeak Wrapper Initialised!");
            });          
        }

        [ServerEvent(Event.ResourceStop)]
        public static void OnResourceStop()
        {
            NAPI.ClientEvent.TriggerClientEventForAll("DisconnectTeamspeak");
        }

        [RemoteEvent("ChangeVoiceRange")]
        public void ChangeVoiceRange(Client client)
        {
            string voiceRange = "Normal";
            if (client.HasSharedData("VOICE_RANGE"))
                voiceRange = client.GetSharedData("VOICE_RANGE");


            switch (voiceRange)
            {
                case "Normal":
                    voiceRange = "Weit";
                    break;
                case "Weit":
                    voiceRange = "Kurz";
                    break;
                case "Kurz":
                    voiceRange = "Normal";
                    break;
            }
            client.SetSharedData("VOICE_RANGE", voiceRange);
            client.SendNotification("Voice Range: " + voiceRange);
        }

        public static void Connect(Client client, string characterName)
        {
            client.SetSharedData("VOICE_RANGE", "Normal");
            client.SetSharedData("TsName", characterName);
            client.TriggerEvent("ConnectTeamspeak", characterName);
        }

        private async Task CheckSpeakingClients()
        {
            var rc = new TeamSpeakClient(TeamspeakQueryAddress, TeamspeakQueryPort); // Create rich client instance

            try
            {              
                await rc.Connect(); // connect to the server
                await rc.Login(TeamspeakLogin, TeamspeakPassword); // login to do some stuff that requires permission
                await rc.UseServer(1); // Use the server with id '1'
                var me = await rc.WhoAmI(); // Get information about yourself!
            }
            catch(QueryException ex)
            {
                Console.WriteLine(ex.ToString());
            }

            var channel = (await rc.FindChannel(TeamspeakChannel)).FirstOrDefault();

            while (rc.Client.IsConnected)
            {
                var clients = await rc.GetClients(GetClientOptions.Voice);
                var clientschannel = clients.ToList().FindAll(c => c.ChannelId == channel.Id);

                var players = NAPI.Pools.GetAllPlayers().FindAll(p=>p.Exists && p.HasSharedData("TsName"));

                for(int i = 0; i < players.Count; i++)
                {
                    if (players[i] == null)
                        continue;

                    var name = players[i].GetSharedData("TsName");
                    var tsplayer = clientschannel.Find(p => p.NickName == name);
                    var player = players[i];

                    if (!player.Exists)
                        continue;

                    if (tsplayer != null)
                    {
                        if (tsplayer.Talk && !player.HasData("IS_SPEAKING"))
                        {
                            players.FindAll(p => p.Exists && p.Position.DistanceTo2D(player.Position) < 5f)
                                .ForEach((client) => client.TriggerEvent("Teamspeak_LipSync", player.Handle.Value, true));

                            player.SetData("IS_SPEAKING", true);
                        }
                        else if (!tsplayer.Talk && player.HasData("IS_SPEAKING"))
                        {
                            players.FindAll(p => p.Exists && p.Position.DistanceTo2D(player.Position) < 5f)
                                .ForEach((client) => client.TriggerEvent("Teamspeak_LipSync", player.Handle.Value, false));

                            player.ResetData("IS_SPEAKING");
                        }
                    }
                    await Task.Delay(10);
                }
                await Task.Delay(50);
            }
        }

        public static string ReplaceStr(string str)
        {
            str = str.Replace("\\\\", "\\");
            str = str.Replace("\\/", "/");
            str = str.Replace("\\s", " ");
            str = str.Replace("\\p", "|");
            str = str.Replace("\\a", "\a");
            str = str.Replace("\\b", "\b");
            str = str.Replace("\\f", "\f");
            str = str.Replace("\\n", "\n");
            str = str.Replace("\\r", "\r");
            str = str.Replace("\\t", "\t");
            str = str.Replace("\\v", "\v");
            return str;
        }
    }
}
