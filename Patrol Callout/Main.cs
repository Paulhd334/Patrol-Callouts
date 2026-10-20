using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;
using SuperPoursuiteCallout;
using System;
using System.Collections.Generic;
using System.IO;

namespace PatrolsCallouts
{
    public class Main : Plugin
    {
        private const string NomPlugin = "Patrols Callouts";
        private const string Version = "1.0.0";
        private const string Auteur = "UNWARE STUDIO";

        private const string BadgeTexture = "WEB_LOSSANTOSPOLICEDEPT";

        private static readonly string LogPath = @"C:\Temp\patrols_callouts_debug.log";
        private const long TailleMaxLog = 1024 * 1024; // 1 Mo, au-dela le log est remis a zero
        private static readonly object LogLock = new object();

        // Evite d'enregistrer les callouts deux fois si le joueur passe plusieurs fois en service
        private static bool calloutsEnregistres = false;

        public override void Initialize()
        {
            Log("=== Initialize() DEBUT ===");
            try
            {
                Game.LogTrivial($"[{NomPlugin}] Initialisation du plugin...");

                // IMPORTANT : on n'enregistre PAS les callouts ici.
                // LSPDFR n'est pas pret a ce moment-la (RegisterCallout -> NullReferenceException).
                // On attend que le joueur passe en service.
                Functions.OnOnDutyStateChanged += OnOnDutyStateChanged;
                Log("Abonnement a OnOnDutyStateChanged OK");

                Game.LogTrivial($"[{NomPlugin}] Plugin initialise, en attente de la prise de service.");
            }
            catch (Exception ex)
            {
                Log("EXCEPTION dans Initialize(): " + ex);
                Game.LogTrivial($"[{NomPlugin}] ERREUR: {ex.Message}");
            }
            Log("=== Initialize() FIN ===");
        }

        private static void OnOnDutyStateChanged(bool onDuty)
        {
            try
            {
                Log("OnOnDutyStateChanged(" + onDuty + ")");
                if (!onDuty || calloutsEnregistres)
                    return;

                int nb = EnregistrerCallouts();
                if (nb > 0)
                    calloutsEnregistres = true;

                // Notification dans son propre fiber : elle attend le chargement de la texture (Sleep)
                GameFiber.StartNew(() => NotifierChargement(NomPlugin, Version, Auteur), "PatrolsCallouts-Notif");
            }
            catch (Exception ex)
            {
                Log("EXCEPTION dans OnOnDutyStateChanged(): " + ex);
            }
        }

        private static int EnregistrerCallouts()
        {
            // Pour l'instant : un seul callout. Les autres seront ajoutes ici au fur et a mesure.
            var callouts = new List<Type>
            {
                typeof(BrokenDownVehicle)
            };

            int ok = 0;
            foreach (var type in callouts)
            {
                try
                {
                    Functions.RegisterCallout(type);
                    Log($"RegisterCallout({type.Name}) OK, joueur en service");
                    ok++;
                }
                catch (Exception ex)
                {
                    Log($"EXCEPTION lors de l'enregistrement de {type.Name}: " + ex);
                }
            }
            return ok;
        }

        public override void Finally()
        {
            try { Functions.OnOnDutyStateChanged -= OnOnDutyStateChanged; } catch { /* ignore */ }
            calloutsEnregistres = false;
            Log("Plugin decharge (Finally appele)");
        }

        // A appeler depuis un GameFiber (utilise GameFiber.Sleep)
        private static bool ChargerBadgePolice(int timeoutMs = 3000)
        {
            try
            {
                if (NativeFunction.Natives.HAS_STREAMED_TEXTURE_DICT_LOADED<bool>(BadgeTexture))
                    return true;

                NativeFunction.Natives.REQUEST_STREAMED_TEXTURE_DICT(BadgeTexture, false);

                int attente = 0;
                while (!NativeFunction.Natives.HAS_STREAMED_TEXTURE_DICT_LOADED<bool>(BadgeTexture)
                       && attente < timeoutMs)
                {
                    GameFiber.Sleep(50);
                    attente += 50;
                }

                bool chargee = NativeFunction.Natives.HAS_STREAMED_TEXTURE_DICT_LOADED<bool>(BadgeTexture);
                if (!chargee)
                    Log($"ATTENTION : badge texture non chargee apres {timeoutMs}ms (timeout)");
                return chargee;
            }
            catch (Exception ex)
            {
                Log("EXCEPTION dans ChargerBadgePolice(): " + ex);
                return false;
            }
        }

        internal static void NotifierChargement(string nomPlugin, string version, string auteur)
        {
            try
            {
                bool badge = ChargerBadgePolice();
                string texte = $"~b~{nomPlugin}~w~ ~o~v{version}~w~\nby ~y~{auteur}~w~\nhas been loaded. ~g~Enjoy!";

                if (badge)
                    Game.DisplayNotification(BadgeTexture, BadgeTexture, "UNWARE STUDIO", "Patrols Callouts", texte);
                else
                    Game.DisplayNotification(texte);

                Log("NotifierChargement OK");
            }
            catch (Exception ex)
            {
                Log("EXCEPTION dans NotifierChargement(): " + ex);
            }
        }

        internal static void NotifierDebug(string message, string couleur = "~y~")
        {
            try
            {
                Game.DisplayNotification($"{couleur}{message}~w~");
            }
            catch (Exception ex)
            {
                Log("EXCEPTION dans NotifierDebug(): " + ex);
            }
        }

        internal static void Log(string message)
        {
            try
            {
                lock (LogLock)
                {
                    string directory = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    // Rotation simple : on repart de zero si le fichier depasse 1 Mo
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > TailleMaxLog)
                        File.Delete(LogPath);

                    File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n");
                }
            }
            catch { /* ignore */ }
        }
    }
}