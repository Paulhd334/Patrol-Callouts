using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using PatrolsCallouts;
using Rage;
using Rage.Native;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SuperPoursuiteCallout
{
    // ============================================================
    // CALLOUT : Véhicule en panne sur le bas-côté de l'autoroute.
    //
    // - Le véhicule est déjà à l'arrêt (pas de poursuite à l'acceptation).
    // - Le conducteur est dehors, capot ouvert, et fait des signes.
    // - Le véhicule fume quand le joueur s'approche.
    // - Si le joueur s'approche trop près / reste trop longtemps,
    //   le suspect panique et prend la fuite -> poursuite créée À CE MOMENT.
    // - Si le joueur est assez proche, une jolie UI apparaît.
    //   Appuyer sur E affiche le rappel : ouvrir Ultimate Backup
    //   et appeler un Tow Truck.
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
        private const float DistanceApproche = 15f;
        private const float DistanceUi = 4.0f;
        private const uint DelaiGraceApprocheMs = 3000;
        private const uint DelaiPatienceSuspectMs = 90000;
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
        private bool suspectEnFuite = false;
        private bool joueurSurPlace = false;
        private uint tempsAcceptationMs;

        // UI
        private bool uiAffichee = false;
        private bool panneauAffiche = false;
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
                    Game.LocalPlayer.Character.Position.Around(180f));

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

                float rad = roadHeading * (float)(Math.PI / 180.0);
                float rightX = (float)Math.Cos(rad);
                float rightY = -(float)Math.Sin(rad);

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
                vehicule = new Vehicle(modeleChoisi, spawnPos, spawnHeading);
                if (!vehicule.Exists()) return false;
                vehicule.MakePersistent();

                NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(vehicule);
                NativeFunction.Natives.SET_VEHICLE_DOORS_LOCKED(vehicule, 0);
                NativeFunction.Natives.SET_VEHICLE_ENGINE_HEALTH(vehicule, 0f);
                NativeFunction.Natives.SET_VEHICLE_UNDRIVEABLE(vehicule, true);
                NativeFunction.Natives.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER(vehicule, false);
                vehicule.IsEngineOn = false;

                NativeFunction.Natives.SET_VEHICLE_HAZARDOUS_LIGHTS(vehicule, true);
                NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicule, 4, true, true);

                // ----- Conducteur -----
                conducteur = new Ped(ModeleConducteur, spawnPos.Around(2f), spawnHeading);
                if (!conducteur.Exists()) return false;
                conducteur.MakePersistent();
                conducteur.BlockPermanentEvents = true;

                conducteur.WarpIntoVehicle(vehicule, -1);
                conducteur.Tasks.LeaveVehicle(LeaveVehicleFlags.None);

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

                // ----- Audio scanner -----
                Functions.PlayScannerAudioUsingPosition(
                    "CIVILIAN_VEHICLE_DISABLED ON_HIGHWAY", spawnPos);

                tempsAcceptationMs = Game.GameTime;
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans OnCalloutAccepted (VehiculeEnPanne) : " + ex);
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

            try
            {
                uint ecouleMs = Game.GameTime - tempsAcceptationMs;

                if (ecouleMs < DelaiGraceApprocheMs) return;

                // ----- Cas A : poursuite déjà déclenchée -----
                if (suspectEnFuite)
                {
                    if (pursuit != null && !Functions.IsPursuitStillRunning(pursuit))
                    {
                        Main.Log("[INFO] Poursuite VehiculeEnPanne terminée.");
                        End();
                    }
                    return;
                }

                // ----- UI + touche E -----
                GererUiEtToucheE();

                // ----- Cas B : le joueur s'approche -> le suspect panique -----
                float distanceJoueur = Game.LocalPlayer.Character.DistanceTo(conducteur.Position);
                if (!joueurSurPlace && distanceJoueur <= DistanceApproche)
                {
                    joueurSurPlace = true;
                    DeclencherFuite("joueur proche");
                    return;
                }

                // ----- Cas C : le suspect perd patience -----
                if (!joueurSurPlace && DelaiPatienceSuspectMs > 0 && ecouleMs >= DelaiPatienceSuspectMs)
                {
                    DeclencherFuite("suspect impatient");
                    return;
                }

                // ----- Cas D : conducteur mort -----
                if (conducteur.IsDead)
                {
                    Main.Log("[INFO] Conducteur mort, fin du callout VehiculeEnPanne.");
                    End();
                    return;
                }
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans Process (VehiculeEnPanne) : " + ex);
            }
        }

        // ============================================================
        // 4) UI + TOUCHE E
        // ============================================================
        private void GererUiEtToucheE()
        {
            if (vehicule == null || !vehicule.Exists()) return;

            float distVehicule = Game.LocalPlayer.Character.DistanceTo(vehicule.Position);

            // ----- Fumée quand le joueur est proche -----
            if (distVehicule <= 12f && fumeeHandle == -1)
            {
                fumeeHandle = NativeFunction.Natives.START_PARTICLE_FX_LOOPED_ON_ENTITY<int>(
                    "ent_amb_smoke_vehicle", vehicule,
                    0f, 1.2f, 0f,
                    0f, 0f, 0f,
                    0.6f,
                    false, false, false);
            }
            else if (distVehicule > 15f && fumeeHandle != -1)
            {
                NativeFunction.Natives.STOP_PARTICLE_FX_LOOPED(fumeeHandle, false);
                fumeeHandle = -1;
            }

            // ----- Affichage du bouton "Appuyez sur E" si assez proche -----
            bool assezProche = distVehicule <= DistanceUi;

            if (assezProche && !uiAffichee)
            {
                uiAffichee = true;
            }
            else if (!assezProche && uiAffichee)
            {
                uiAffichee = false;
                panneauAffiche = false;
            }

            // ----- Dessin du bouton E -----
            if (uiAffichee)
            {
                // Joli panneau natif "Appuyez sur E"
                NativeFunction.Natives.BEGIN_TEXT_COMMAND_DISPLAY_HELP("STRING");
                NativeFunction.Natives.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(
                    "Press ~INPUT_CONTEXT~ to call Ultimate Backup (Tow Truck)");
                NativeFunction.Natives.END_TEXT_COMMAND_DISPLAY_HELP(
                    0, false, true, -1);
            }

            // ----- Détection de la touche E -----
            if (uiAffichee)
            {
                if (Game.IsKeyDown(Keys.E) && !toucheEPressee)
                {
                    toucheEPressee = true;
                    panneauAffiche = true;
                    AfficherPanneauUb();
                }
                else if (Game.GetKeyboardState().IsUp(Keys.E))
                {
                    toucheEPressee = false;
                }
            }
        }

        // ------------------------------------------------------------
        // Joli panneau UI : invite à ouvrir Ultimate Backup + Tow Truck
        // ------------------------------------------------------------
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

            // On cache l'UI et on coupe la fumée.
            uiAffichee = false;
            panneauAffiche = false;
            if (fumeeHandle != -1)
            {
                NativeFunction.Natives.STOP_PARTICLE_FX_LOOPED(fumeeHandle, false);
                fumeeHandle = -1;
            }

            try
            {
                Main.Log("[INFO] Fuite déclenchée (" + raison + ").");

                if (conducteur != null && conducteur.Exists() && !conducteur.IsDead)
                {
                    NativeFunction.Natives.SET_VEHICLE_UNDRIVEABLE(vehicule, false);
                    NativeFunction.Natives.SET_VEHICLE_ENGINE_HEALTH(vehicule, 1000f);
                    NativeFunction.Natives.SET_VEHICLE_HAZARDOUS_LIGHTS(vehicule, false);
                    NativeFunction.Natives.SET_VEHICLE_DOOR_CLOSE(vehicule, 4);
                    vehicule.IsEngineOn = true;

                    conducteur.WarpIntoVehicle(vehicule, -1);
                }

                pursuit = Functions.CreatePursuit();
                if (conducteur != null && conducteur.Exists())
                    Functions.AddPedToPursuit(pursuit, conducteur);
                if (passager != null && passager.Exists())
                    Functions.AddPedToPursuit(pursuit, passager);

                Functions.SetPursuitCopsCanJoin(pursuit, false);
                Functions.SetPursuitIsActiveForPlayer(pursuit, true);

                if (blip != null && blip.Exists())
                {
                    blip.Color = Color.Red;
                    blip.Name = "Fleeing suspect";
                }

                Functions.PlayScannerAudioUsingPosition(
                    "SUSPECT_IS_FLEEING IN_OR_ON_POSITION", vehicule.Position);
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans DeclencherFuite (VehiculeEnPanne) : " + ex);
            }
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
            panneauAffiche = false;
            toucheEPressee = false;

            if (blip != null && blip.Exists()) blip.Delete();
            if (passager != null && passager.Exists()) passager.Delete();
            if (conducteur != null && conducteur.Exists()) conducteur.Delete();
            if (vehicule != null && vehicule.Exists()) vehicule.Delete();
        }
    }
}