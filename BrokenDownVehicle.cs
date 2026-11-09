using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using PatrolsCallouts;
using PatrolsCallouts.Utils;
using Rage;
using Rage.Native;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SuperPoursuiteCallout
{
    // ============================================================
    // CALLOUT : Véhicule en panne sur le bas-côté de l'autoroute.   (VERSION CORRIGÉE)
    //
    // - Le véhicule est déjà à l'arrêt (pas de poursuite à l'acceptation).
    // - Le conducteur est dehors, à côté du véhicule, capot ouvert.
    // - Le véhicule fume quand le joueur s'approche.
    // - Quand le joueur est à moins de 4 m : aide "Appuyez sur E" (rappel Tow Truck).
    // - Le suspect panique et fuit si le joueur :
    //      * arrive trop près (< 2.5 m),
    //      * reste trop longtemps dans la zone (> 12 s à moins de 15 m),
    //      * ou si le temps d'attente total est dépassé (90 s).
    //   Il COURT jusqu'à son véhicule, puis la poursuite est créée.
    //
    // Requiert : CalloutMenu.cs (PatrolsCallouts.Utils)
    // ============================================================
    [CalloutInfo("VehiculeEnPanne", CalloutProbability.Medium)]
    public class BrokenDownVehicle : Callout
    {
        // ------------------------------------------------------------
        // Modèles
        // ------------------------------------------------------------
        private static readonly string[] ModelesVehicule = { "blista", "asea" };
        private const string ModeleConducteur = "a_m_y_business_01";
        private const string ModelePassager = "a_f_y_business_01";

        // ------------------------------------------------------------
        // Constantes de gameplay
        // ------------------------------------------------------------
        private const float DistanceApproche = 15f;       // zone de "pression"
        private const float DistanceTropProche = 2.5f;    // fuite immédiate
        private const float DistanceUi = 4.0f;            // aide "Appuyez sur E"
        private const uint DelaiPaniqueMs = 12000;        // temps passé dans la zone avant fuite
        private const uint DelaiGraceApprocheMs = 3000;
        private const uint DelaiPatienceSuspectMs = 90000;
        private const uint DelaiEmbarquementMs = 6000;    // max pour monter dans le véhicule
        private const float DecalageBasCote = 4.0f;

        // ------------------------------------------------------------
        // Champs d'état
        // ------------------------------------------------------------
        private Vector3 spawnPos;
        private float spawnHeading;

        private Vehicle vehicule;
        private Ped conducteur;
        private Ped passager;
        private Blip blip;

        private LHandle pursuit;
        private bool released = false;          // entités dégelées (collision chargée)
        private bool suspectEnFuite = false;    // le suspect court vers son véhicule
        private bool pursuitCree = false;       // la poursuite LSPDFR est lancée
        private uint tempsAcceptationMs;
        private uint debutFuiteMs;
        private uint tempsProcheMs = 0;

        // UI
        private bool uiAffichee = false;
        private bool toucheEPressee = false;
        private int fumeeHandle = -1;

        private static readonly Random Rng = new Random();

        public BrokenDownVehicle()
        {
            Main.Log("Constructeur VehiculeEnPanne appelé");
        }

        // ============================================================
        // 1) AVANT AFFICHAGE DU CALLOUT
        // ============================================================
        public override bool OnBeforeCalloutDisplayed()
        {
            try
            {
                Vector3 basePos = World.GetNextPositionOnStreet(
                    Game.LocalPlayer.Character.Position.Around2D(80f, 250f));

                Vector3 nodePos;
                float roadHeading;
                bool ok = NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(
                    basePos.X, basePos.Y, basePos.Z,
                    out nodePos, out roadHeading,
                    1, 3.0f, 0);

                if (!ok || nodePos == Vector3.Zero)
                {
                    nodePos = basePos;
                    roadHeading = 0f;
                }

                // FIX : vecteur "droite" du véhicule (GTA : droite = (cos h, sin h)).
                // Avant, le signe de Y était inversé -> le véhicule apparaissait côté GAUCHE
                // (côté circulation inverse / terre-plein) au lieu du bas-côté droit.
                float rad = roadHeading * (float)(Math.PI / 180.0);
                float rightX = (float)Math.Cos(rad);
                float rightY = (float)Math.Sin(rad);

                Vector3 shoulderPos = new Vector3(
                    nodePos.X + rightX * DecalageBasCote,
                    nodePos.Y + rightY * DecalageBasCote,
                    nodePos.Z);

                bool surRoute = NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(
                    shoulderPos.X, shoulderPos.Y, shoulderPos.Z, 0);

                if (surRoute)
                {
                    spawnPos = shoulderPos;
                }
                else
                {
                    float small = DecalageBasCote * 0.5f;
                    Vector3 alt = new Vector3(
                        nodePos.X + rightX * small,
                        nodePos.Y + rightY * small,
                        nodePos.Z);

                    if (NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(alt.X, alt.Y, alt.Z, 0))
                        spawnPos = alt;
                    else
                        spawnPos = nodePos;
                }

                spawnHeading = roadHeading;

                CalloutPosition = spawnPos;
                CalloutMessage = "Broken down vehicle on highway shoulder";
                CalloutAdvisory = "~y~Vehicle disabled~w~, driver waving for assistance. Approach with caution.";

                ShowCalloutAreaBlipBeforeAccepting(spawnPos, 50f);
                AddMinimumDistanceCheck(40f, spawnPos);
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans OnBeforeCalloutDisplayed (VehiculeEnPanne) : " + ex);
                return false;
            }

            return base.OnBeforeCalloutDisplayed();
        }

        // ============================================================
        // 2) ACCEPTATION DU CALLOUT
        // ============================================================
        public override bool OnCalloutAccepted()
        {
            try
            {
                string modeleChoisi = ModelesVehicule[Rng.Next(ModelesVehicule.Length)];

                // ----- Véhicule en panne -----
                // FIX : tout est GELÉ jusqu'à ce que la collision soit chargée
                // (le véhicule est créé à distance, il tombait sous la map).
                vehicule = new Vehicle(modeleChoisi, spawnPos, spawnHeading);
                if (!vehicule.Exists()) { NettoyerEntites(); return false; }
                vehicule.MakePersistent();
                vehicule.IsPositionFrozen = true;
                NativeFunction.CallByName<uint>("REQUEST_COLLISION_AT_COORD", spawnPos.X, spawnPos.Y, spawnPos.Z);

                NativeFunction.Natives.SET_VEHICLE_DOORS_LOCKED(vehicule, 0);
                NativeFunction.Natives.SET_VEHICLE_ENGINE_HEALTH(vehicule, 0f);
                NativeFunction.Natives.SET_VEHICLE_UNDRIVEABLE(vehicule, true);
                NativeFunction.Natives.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER(vehicule, false);
                vehicule.IsEngineOn = false;

                NativeFunction.Natives.SET_VEHICLE_HAZARDOUS_LIGHTS(vehicule, true);
                NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicule, 4, true, true);

                // ----- Conducteur -----
                // FIX : il est créé DEHORS, à côté du véhicule (côté bas-côté), au lieu de
                // WarpIntoVehicle + LeaveVehicle (la tâche échoue sur une entité gelée).
                conducteur = new Ped(ModeleConducteur, vehicule.GetOffsetPosition(new Vector3(2.3f, 1.5f, 0f)), spawnHeading);
                if (!conducteur.Exists()) { NettoyerEntites(); return false; }
                conducteur.MakePersistent();
                conducteur.BlockPermanentEvents = true;
                conducteur.IsPositionFrozen = true;
                conducteur.Heading = MathHelper.ConvertDirectionToHeading(vehicule.Position - conducteur.Position);

                // ----- Passager -----
                if (vehicule.PassengerCapacity >= 2)
                {
                    passager = new Ped(ModelePassager, spawnPos.Around(2f), spawnHeading);
                    if (passager.Exists())
                    {
                        passager.MakePersistent();
                        passager.BlockPermanentEvents = true;
                        passager.WarpIntoVehicle(vehicule, 0);
                    }
                }

                // ----- Blip -----
                blip = vehicule.AttachBlip();
                blip.Color = Color.Yellow;
                blip.IsFriendly = false;
                blip.Name = "Disabled vehicle";
                blip.IsRouteEnabled = true;

                // Pré-charge l'asset de particules (fumée)
                NativeFunction.CallByName<uint>("REQUEST_NAMED_PTFX_ASSET", "core");

                // ----- Audio scanner -----
                Functions.PlayScannerAudioUsingPosition(
                    "CIVILIAN_VEHICLE_DISABLED ON_HIGHWAY", spawnPos);

                tempsAcceptationMs = Game.GameTime;
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans OnCalloutAccepted (VehiculeEnPanne) : " + ex);
                NettoyerEntites();
                return false;
            }

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted()
        {
            base.OnCalloutNotAccepted();
            NettoyerEntites();
        }

        // ============================================================
        // 3) BOUCLE PRINCIPALE
        // ============================================================
        public override void Process()
        {
            base.Process();

            if (conducteur == null || !conducteur.Exists()) return;
            if (vehicule == null || !vehicule.Exists()) return;

            try
            {
                // ----- Phase 0 : on attend le chargement de la collision -----
                if (!released)
                {
                    TryRelease();
                    return;
                }

                uint ecouleMs = Game.GameTime - tempsAcceptationMs;
                if (ecouleMs < DelaiGraceApprocheMs) return;

                // ----- Phase 2 : le suspect fuit -----
                if (suspectEnFuite)
                {
                    if (!pursuitCree)
                    {
                        SuivreEmbarquement();
                        return;
                    }

                    if (pursuit != null && !Functions.IsPursuitStillRunning(pursuit))
                    {
                        Main.Log("[INFO] Poursuite VehiculeEnPanne terminée.");
                        End();
                    }
                    return;
                }

                // ----- Conducteur mort avant la fuite -----
                if (conducteur.IsDead)
                {
                    Main.Log("[INFO] Conducteur mort, fin du callout VehiculeEnPanne.");
                    End();
                    return;
                }

                // ----- UI + touche E -----
                GererUiEtToucheE();

                // ----- Le joueur s'approche -----
                // FIX : avant, le suspect fuyait dès 15 m, donc l'UI (4 m) n'apparaissait jamais.
                float distanceJoueur = Game.LocalPlayer.Character.DistanceTo(conducteur.Position);

                if (distanceJoueur <= DistanceTropProche)
                {
                    DeclencherFuite("joueur trop proche");
                    return;
                }

                if (distanceJoueur <= DistanceApproche)
                {
                    if (tempsProcheMs == 0)
                        tempsProcheMs = Game.GameTime;
                    else if (Game.GameTime - tempsProcheMs >= DelaiPaniqueMs)
                    {
                        DeclencherFuite("joueur reste trop longtemps");
                        return;
                    }
                }
                else
                {
                    tempsProcheMs = 0;
                }

                // ----- Le suspect perd patience -----
                if (DelaiPatienceSuspectMs > 0 && ecouleMs >= DelaiPatienceSuspectMs)
                {
                    DeclencherFuite("suspect impatient");
                    return;
                }
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans Process (VehiculeEnPanne) : " + ex);
            }
        }

        // Dégèle les entités quand la collision est chargée (ou joueur proche)
        private void TryRelease()
        {
            float dist = Game.LocalPlayer.Character.DistanceTo(vehicule.Position);
            if (!(CalloutUtils.HasCollision(vehicule) || dist < 80f)) return;

            vehicule.IsPositionFrozen = false;
            conducteur.IsPositionFrozen = false;
            if (passager != null && passager.Exists()) passager.IsPositionFrozen = false;

            NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(vehicule);
            NativeFunction.Natives.SET_VEHICLE_HAZARDOUS_LIGHTS(vehicule, true);
            NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicule, 4, true, true);

            CalloutUtils.StartScenario(conducteur, "WORLD_HUMAN_STAND_IMPATIENT");

            released = true;
            tempsAcceptationMs = Game.GameTime;   // les délais partent du moment où la scène est active
        }

        // ============================================================
        // 4) UI + TOUCHE E
        // ============================================================
        private void GererUiEtToucheE()
        {
            if (vehicule == null || !vehicule.Exists()) return;

            float distVehicule = Game.LocalPlayer.Character.DistanceTo(vehicule.Position);

            // ----- Fumée quand le joueur est proche -----
            // FIX : l'asset de particules doit être chargé et sélectionné avant le lancement,
            // sinon aucune fumée n'apparaît.
            if (distVehicule <= 12f && fumeeHandle == -1)
            {
                if (NativeFunction.CallByName<bool>("HAS_NAMED_PTFX_ASSET_LOADED", "core"))
                {
                    NativeFunction.CallByName<uint>("USE_PARTICLE_FX_ASSET", "core");
                    fumeeHandle = NativeFunction.Natives.START_PARTICLE_FX_LOOPED_ON_ENTITY<int>(
                        "ent_amb_smoke_vehicle", vehicule,
                        0f, 1.2f, 0f,
                        0f, 0f, 0f,
                        0.6f,
                        false, false, false);
                }
                else
                {
                    NativeFunction.CallByName<uint>("REQUEST_NAMED_PTFX_ASSET", "core");
                }
            }
            else if (distVehicule > 15f && fumeeHandle != -1)
            {
                NativeFunction.Natives.STOP_PARTICLE_FX_LOOPED(fumeeHandle, false);
                fumeeHandle = -1;
            }

            // ----- Aide "Appuyez sur E" si assez proche -----
            uiAffichee = distVehicule <= DistanceUi && !Game.LocalPlayer.Character.IsInAnyVehicle(false);

            if (uiAffichee)
            {
                NativeFunction.Natives.BEGIN_TEXT_COMMAND_DISPLAY_HELP("STRING");
                NativeFunction.Natives.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(
                    "Press ~INPUT_CONTEXT~ to call Ultimate Backup (Tow Truck)");
                NativeFunction.Natives.END_TEXT_COMMAND_DISPLAY_HELP(
                    0, false, true, -1);

                if (Game.IsKeyDown(Keys.E) && !toucheEPressee)
                {
                    toucheEPressee = true;
                    AfficherPanneauUb();
                }
                else if (Game.GetKeyboardState().IsUp(Keys.E))
                {
                    toucheEPressee = false;
                }
            }
        }

        private void AfficherPanneauUb()
        {
            Game.DisplayNotification(
                "~y~Ultimate Backup~w~ opened by your service. " +
                "Please call a ~b~Tow Truck~w~ to remove the disabled vehicle.");
        }

        // ============================================================
        // 5) FUITE
        // ============================================================
        private void DeclencherFuite(string raison)
        {
            if (suspectEnFuite) return;
            suspectEnFuite = true;
            debutFuiteMs = Game.GameTime;

            uiAffichee = false;
            if (fumeeHandle != -1)
            {
                NativeFunction.Natives.STOP_PARTICLE_FX_LOOPED(fumeeHandle, false);
                fumeeHandle = -1;
            }

            try
            {
                Main.Log("[INFO] Fuite déclenchée (" + raison + ").");

                // Le véhicule "repart" comme par magie : réparation + moteur
                NativeFunction.Natives.SET_VEHICLE_UNDRIVEABLE(vehicule, false);
                NativeFunction.Natives.SET_VEHICLE_ENGINE_HEALTH(vehicule, 1000f);
                NativeFunction.Natives.SET_VEHICLE_HAZARDOUS_LIGHTS(vehicule, false);
                NativeFunction.Natives.SET_VEHICLE_DOOR_CLOSE(vehicule, 4);
                vehicule.IsEngineOn = true;

                // FIX : le conducteur COURT jusqu'à son véhicule (avant : téléportation instantanée).
                // La poursuite est créée dès qu'il est au volant (voir SuivreEmbarquement).
                conducteur.Tasks.Clear();
                conducteur.Tasks.EnterVehicle(vehicule, -1);

                if (blip != null && blip.Exists())
                {
                    blip.Color = Color.Red;
                    blip.Name = "Fleeing suspect";
                }
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans DeclencherFuite (VehiculeEnPanne) : " + ex);
            }
        }

        // Attend que le conducteur soit au volant (ou timeout -> téléportation), puis lance la poursuite
        private void SuivreEmbarquement()
        {
            if (conducteur.IsDead) { End(); return; }

            bool dedans = conducteur.IsInVehicle(vehicule, false);
            bool timeout = Game.GameTime - debutFuiteMs > DelaiEmbarquementMs;
            if (!dedans && !timeout) return;

            if (!dedans) conducteur.WarpIntoVehicle(vehicule, -1);
            CreerPoursuite();
        }

        private void CreerPoursuite()
        {
            pursuitCree = true;

            pursuit = Functions.CreatePursuit();
            Functions.AddPedToPursuit(pursuit, conducteur);
            if (passager != null && passager.Exists())
                Functions.AddPedToPursuit(pursuit, passager);

            Functions.SetPursuitCopsCanJoin(pursuit, false);
            Functions.SetPursuitIsActiveForPlayer(pursuit, true);

            Functions.PlayScannerAudioUsingPosition(
                "SUSPECT_IS_FLEEING IN_OR_ON_POSITION", vehicule.Position);
        }

        // ============================================================
        // 6) FIN / NETTOYAGE
        // ============================================================
        public override void End()
        {
            base.End();
            NettoyerEntites();
        }

        private void NettoyerEntites()
        {
            if (fumeeHandle != -1)
            {
                NativeFunction.Natives.STOP_PARTICLE_FX_LOOPED(fumeeHandle, false);
                fumeeHandle = -1;
            }

            uiAffichee = false;
            toucheEPressee = false;

            if (blip != null && blip.Exists()) blip.Delete();

            // FIX : Dismiss au lieu de Delete, pour ne pas faire disparaître
            // un suspect arrêté / un véhicule sous les yeux du joueur.
            if (vehicule != null && vehicule.Exists()) { vehicule.IsPositionFrozen = false; vehicule.Dismiss(); }
            if (conducteur != null && conducteur.Exists()) { conducteur.IsPositionFrozen = false; conducteur.Dismiss(); }
            if (passager != null && passager.Exists()) { passager.IsPositionFrozen = false; passager.Dismiss(); }
        }
    }
}