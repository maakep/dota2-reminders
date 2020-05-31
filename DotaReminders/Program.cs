using Dota2GSI;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Speech.Synthesis;
using Dota2GSI.Nodes;
using System.Collections.Generic;

namespace DotaReminders
{
    class Program
    {
        static GameStateListener _gsl;
        static SpeechSynthesizer synth = new SpeechSynthesizer();

        static int time = 0;
        static int sayCooldown = 5;
        static int nextReminder = 0;

        static void Main(string[] args)
        {
            if (args == null) Console.WriteLine();

            CreateGsifile();

            Process[] pname = Process.GetProcessesByName("Dota2");

            if (pname.Length == 0)
            {
                Console.WriteLine("Dota 2 is not running. Please start Dota 2.");
                Console.ReadLine();
                Environment.Exit(0);
            }

            _gsl = new GameStateListener(4000);
            _gsl.NewGameState += OnNewGameState;


            if (!_gsl.Start())
            {
                Console.WriteLine("GameStateListener could not start. Try running this program as Administrator. Exiting.");
                Console.ReadLine();
                Environment.Exit(0);
            }
            Console.WriteLine("Listening for game integration calls...");

            Console.WriteLine("Press ESC to quit");
            do
            {
                while (!Console.KeyAvailable)
                {
                    Thread.Sleep(1000);
                }
            } while (Console.ReadKey(true).Key != ConsoleKey.Escape);
        }

        static void Say(string msg, bool priority = false)
        {
            if (priority || time > nextReminder)
            {
                nextReminder = time + sayCooldown;
                synth.SpeakAsync(msg);
            }
        }

        static void OnNewGameState(GameState gs)
        {
            try
            {
                if (gs.Player.Activity != PlayerActivity.Playing) return;

                time = gs.Map.ClockTime;

                if (HealthAlert(gs) || BountyRuneAlert(gs) || StackCampsAlert(gs) || MidasAlert(gs))
                    return;
            } catch (Exception e)
            {
                Console.WriteLine(e);
                // Sometimes json can't be parsed, lul
            }

        }

        private static bool MidasAlert(GameState gs)
        {
            if (gs.Items.InventoryContains("item_hand_of_midas"))
            {
                var midas = gs.Items.GetInventoryAt(gs.Items.InventoryIndexOf("item_hand_of_midas"));
                if (midas.Cooldown <= 5)
                {
                    Say("Midas off cooldown");
                    return true;
                }
            }

            return false;
        }

        private static bool StackCampsAlert(GameState gs)
        {
            if ((time + 20) % 60 == 0)
            {
                Say("Stack camps");
                return true;
            }
            return false;
        }

        private static bool BountyRuneAlert(GameState gs)
        {
            if ((time + 30) % 300 == 0)
            {
                Say("Bounty runes");
                return true;
            }
            return false;
        }

        private static bool HealthAlert(GameState gs)
        {
            var healthPercent = gs.Hero.HealthPercent;
            var prevHealthPercent = gs.Previously.Hero.HealthPercent;
            if (healthPercent < 20 && prevHealthPercent > healthPercent) // HP declining under 20%
            {
                string[] itemNames = {
                    "item_guardian_greaves",
                    "item_mekansm",
                    "item_magic_wand",
                    "item_magic_stick",
                };
                foreach (var itemName in itemNames)
                {
                    var itemPos = gs.Items.InventoryIndexOf(itemName);
                    if (itemPos > -1)
                    {
                        var item = gs.Items.GetInventoryAt(itemPos);

                        if ((item.Charges == -1 || item.Charges > 0) && item.Cooldown <= 1)
                        {
                            var name = item.Name.Replace("item", "").Replace("_", "");
                            Say(String.Format("{0} at {1}", item.Name, itemPos + 1));
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void CreateGsifile()
        {
            RegistryKey regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");

            if (regKey != null)
            {
                string gsifolder = regKey.GetValue("SteamPath") +
                                    @"\steamapps\common\dota 2 beta\game\dota\cfg\gamestate_integration";
                Directory.CreateDirectory(gsifolder);
                string gsifile = gsifolder + @"\gamestate_integration_testGSI.cfg";
                if (File.Exists(gsifile))
                    return;

                string[] contentofgsifile =
                {
                "\"Dota 2 Integration Configuration\"",
                "{",
                "    \"uri\"           \"http://localhost:4000\"",
                "    \"timeout\"       \"5.0\"",
                "    \"buffer\"        \"0.1\"",
                "    \"throttle\"      \"0.1\"",
                "    \"heartbeat\"     \"30.0\"",
                "    \"data\"",
                "    {",
                "        \"provider\"      \"1\"",
                "        \"map\"           \"1\"",
                "        \"player\"        \"1\"",
                "        \"hero\"          \"1\"",
                "        \"abilities\"     \"1\"",
                "        \"items\"         \"1\"",
                "    }",
                "}",

            };

                File.WriteAllLines(gsifile, contentofgsifile);
            }
            else
            {
                Console.WriteLine("Registry key for steam not found, cannot create Gamestate Integration file");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }
    }
}
