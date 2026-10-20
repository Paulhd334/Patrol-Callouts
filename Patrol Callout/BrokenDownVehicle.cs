using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using PatrolsCallouts;
using Rage;
using Rage.Native;
using System;
using System.Drawing;

namespace SuperPoursuiteCallout
{
    // Vehicule en panne sur le bas-cote de l'autoroute.
    // Le vehicule est deja a l'arret (pas de poursuite a l'acceptation).
    // Le conducteur est dehors, capot ouvert, et fait des signes.
    // Si le joueur s'approche trop pres / reste trop longtemps, le suspect
    // panique et prend la fuite -> la poursuite est creee A CE MOMENT-LA.
    [CalloutInfo("VehiculeEnPanne", CalloutProbability.Medium)]
    public class BrokenDownVehicle : Callout
    {
        // Vehicules "civils" plausibles en panne. On prend un des deux au hasard
        // pour varier d'une apparition a l'autre.
        private static readonly string[] ModelesVehicule =
        {
            "blista",
            "asea"
        };

        // Ped civil standard (pas de mp_freemode : tenue deja generee par le jeu).
        private const string ModeleConducteur = "a_m_y_business_01";
        private const string ModelePassager = "a_f_y_business_01";

        // Distance (m) sous laquelle le joueur est considere "arrive sur place".
        private const float DistanceApproche = 15f;

        // Distance (m) a partir de laquelle on force le warp du vehicule sur le
        // bas-cote (evite qu'il spawn au milieu de la voie, cf. plus bas).
        private const float DecalageBasCote = 6f;

        // Delai (ms) apres acceptation avant de commencer a verifier la proximite.
        // Laisse le temps au blip/scanner de jouer et evite un declenchement au
        // premier tick si le spawn est proche du joueur.
        private const uint DelaiGraceApprocheMs = 3000;

        // Apres ce delai (ms) sans que le joueur approche, on autorise la fuite
        // automatique (le suspect en a marre d'attendre). 0 = jamais.
        private const uint DelaiPatienceSuspectMs = 90000;

        private Vector3 spawnPos;
        private Vehicle vehicule;
        private Ped conducteur;
        private Ped passager;
        private Blip blip;

        private LHandle pursuit;
        private bool poursuiteScolarisee = false;
        private bool suspectEnFuite = false;
        private bool joueurSurPlace = false;
        private uint tempsAcceptationMs;

        public BrokenDownVehicle()
        {
            Main.Log("Constructeur VehiculeEnPanne appele");
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            try
            {
                // On cherche une position SUR autoroute (highway) si possible.
                // World.GetNextPositionOnStreet ne distingue pas autoroute/route
                // classique : on demande une position sur route, puis on tente
                // d'affiner. Si rien de mieux n'est trouve, on garde la position
                // generique (le callout reste jouable sur route normale).
                spawnPos = World.GetNextPositionOnStreet(
                    Game.LocalPlayer.Character.Position.Around(180f));

                CalloutPosition = spawnPos;
                CalloutMessage = "Broken down vehicle on highway shoulder";
                CalloutAdvisory = "~y~Vehicle disabled~w~, driver waving for assistance. Approach with caution.";

                ShowCalloutAreaBlipBeforeAccepting(spawnPos, 50f);
                AddMinimumDistanceCheck(40f, spawnPos);
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans OnBeforeCalloutDisplayed (VehiculeEnPanne): " + ex);
                return false;
            }

            return base.OnBeforeCalloutDisplayed();
        }

        public override bool OnCalloutAccepted()
        {
            try
            {
                // 1) Creation du vehicule a l'arret, sur le cote.
                string modeleChoisi = ModelesVehicule[
                    new Random(Game.GameTime.GetHashCode()).Next(ModelesVehicule.Length)];

                vehicule = new Vehicle(modeleChoisi, spawnPos, 0f);
                if (!vehicule.Exists()) return false;
                vehicule.MakePersistent();

                // Decalage lateral : on pousse le vehicule sur le bas-cote pour
                // eviter qu'il bloque la voie de circulation (le joueur doit
                // pouvoir se garer a cote).
                DecalerSurBasCote(vehicule);

                // Etat "en panne" : capot ouvert, phares de detresse, moteur coupe.
                NativeFunction.Natives.SET_VEHICLE_DOORS_LOCKED(vehicule, 1); // 1 = unlocked
                NativeFunction.Natives.SET_VEHICLE_ENGINE_HEALTH(vehicule, 0f);
                NativeFunction.Natives.SET_VEHICLE_UNDRIVEABLE(vehicule, true);
                NativeFunction.Natives.SET_VEHICLE_HAS_BEEN_OWNED_BY_PLAYER(vehicule, false);
                vehicule.IsEngineOn = false;

                // Phares de detresse (feux warning). La native attend un bool.
                NativeFunction.Natives.SET_VEHICLE_HAZARDOUS_LIGHTS(vehicule, true);

                // Capot ouvert (index 4 = capot sur la plupart des vehicules civils).
                NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicule, 4, false, true);

                // 2) Conducteur dehors, sur le bas-cote, en train de faire signe.
                conducteur = new Ped(ModeleConducteur, vehicule.Position.Around(2f), 0f);
                if (!conducteur.Exists()) return false;
                conducteur.MakePersistent();
                conducteur.BlockPermanentEvents = true;
                conducteur.WarpIntoVehicle(vehicule, -1); // conducteur
                // Il sort immediatement du vehicule.
                conducteur.Tasks.LeaveVehicle(LeaveVehicleFlags.None);

                // 3) Passager eventuel, reste dans le vehicule.
                //    (Uniquement si le modele a au moins 2 sieges.)
                if (vehicule.PassengerCapacity >= 2)
                {
                    passager = new Ped(ModelePassager, vehicule.Position.Around(2f), 0f);
                    if (passager.Exists())
                    {
                        passager.MakePersistent();
                        passager.BlockPermanentEvents = true;
                        passager.WarpIntoVehicle(vehicule, 0); // passager avant
                    }
                }

                // 4) Blip sur le vehicule.
                blip = vehicule.AttachBlip();
                blip.Color = Color.Yellow;
                blip.IsFriendly = false;
                blip.Name = "Disabled vehicle";
                blip.IsRouteEnabled = true;

                // 5) Audio scanner.
                Functions.PlayScannerAudioUsingPosition(
                    "CIVILIAN_VEHICLE_DISABLED ON_HIGHWAY", spawnPos);

                tempsAcceptationMs = Game.GameTime;
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans OnCalloutAccepted (VehiculeEnPanne): " + ex);
                return false;
            }

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted()
        {
            base.OnCalloutNotAccepted();
            NettoyerEntites();
        }

        public override void Process()
        {
            base.Process();

            // Pas encore initialise ou deja termine -> rien a faire.
            if (conducteur == null || !conducteur.Exists()) return;

            try
            {
                uint ecouleMs = Game.GameTime - tempsAcceptationMs;

                // Delai de grace : on ne teste rien avant.
                if (ecouleMs < DelaiGraceApprocheMs) return;

                // --- Cas A : poursuite deja declenchee -> on delegue a LSPDFR ---
                if (suspectEnFuite)
                {
                    if (pursuit != null && !Functions.IsPursuitStillRunning(pursuit))
                    {
                        Main.Log("[INFO] Poursuite VehiculeEnPanne terminee.");
                        End();
                    }
                    return;
                }

                // --- Cas B : le joueur s'est approche -> le suspect panique ---
                float distanceJoueur = Game.LocalPlayer.Character.DistanceTo(conducteur.Position);
                if (!joueurSurPlace && distanceJoueur <= DistanceApproche)
                {
                    joueurSurPlace = true;
                    DeclencherFuite(raison: "joueur proche");
                    return;
                }

                // --- Cas C : le suspect perd patience et part de lui-meme ---
                if (!joueurSurPlace && DelaiPatienceSuspectMs > 0 && ecouleMs >= DelaiPatienceSuspectMs)
                {
                    DeclencherFuite(raison: "suspect impatient");
                    return;
                }

                // --- Cas D : le conducteur est mort -> on nettoie ---
                if (conducteur.IsDead)
                {
                    Main.Log("[INFO] Conducteur mort, fin du callout VehiculeEnPanne.");
                    End();
                    return;
                }
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans Process (VehiculeEnPanne): " + ex);
            }
        }

        // Declenche la fuite du suspect : il remonte dans le vehicule, demarre
        // le moteur, et une poursuite est creee. A appeler une seule fois.
        private void DeclencherFuite(string raison)
        {
            if (suspectEnFuite) return;
            suspectEnFuite = true;

            try
            {
                Main.Log("[INFO] Fuite declenchee (" + raison + ").");

                // Le conducteur remonte.
                if (conducteur != null && conducteur.Exists() && !conducteur.IsDead)
                {
                    conducteur.BlockPermanentEvents = true;
                    NativeFunction.Natives.SET_VEHICLE_UNDRIVEABLE(vehicule, false);
                    NativeFunction.Natives.SET_VEHICLE_ENGINE_HEALTH(vehicule, 1000f);
                    NativeFunction.Natives.SET_VEHICLE_HAZARDOUS_LIGHTS(vehicule, false);
                    NativeFunction.Natives.SET_VEHICLE_DOOR_CLOSE(vehicule, 4); // capot
                    vehicule.IsEngineOn = true;
                    conducteur.WarpIntoVehicle(vehicule, -1);
                }

                // Le passager reste passager (rien a faire).

                // Poursuite cote LSPDFR.
                pursuit = Functions.CreatePursuit();
                if (conducteur != null && conducteur.Exists())
                    Functions.AddPedToPursuit(pursuit, conducteur);
                if (passager != null && passager.Exists())
                    Functions.AddPedToPursuit(pursuit, passager);

                Functions.SetPursuitCopsCanJoin(pursuit, false);
                Functions.SetPursuitIsActiveForPlayer(pursuit, true);

                // Le blip devient rouge (suspect en fuite) au lieu de jaune.
                if (blip != null && blip.Exists())
                {
                    blip.Color = Color.Red;
                    blip.Name = "Fleeing suspect";
                }

                Functions.PlayScannerAudioUsingPosition(
                    "SUSPECT_IS_FLEEING IN_OR_ON_POSITION", vehicule.Position);

                poursuiteScolarisee = true;
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans DeclencherFuite (VehiculeEnPanne): " + ex);
            }
        }

        public override void End()
        {
            base.End();
            if (blip != null && blip.Exists()) blip.Delete();
        }

        private void NettoyerEntites()
        {
            if (blip != null && blip.Exists()) blip.Delete();
            if (passager != null && passager.Exists()) passager.Delete();
            if (conducteur != null && conducteur.Exists()) conducteur.Delete();
            if (vehicule != null && vehicule.Exists()) vehicule.Delete();
        }

        // --------------------------------------------------------------
        // Helpers
        // --------------------------------------------------------------

        // Pousse le vehicule lateralement par rapport a son axe pour le coller
        // sur le bas-cote. On utilise le vecteur "droite" du vehicule (RightVector)
        // et non RightVector sur un Vehicle inexistant : Rage expose bien
        // Entity.RightVector (Vector3 perpendiculaire a Heading).
        private void DecalerSurBasCote(Vehicle v)
        {
            if (v == null || !v.Exists()) return;
            try
            {
                Vector3 droite = v.RightVector;
                Vector3 nouvellePos = v.Position + droite * DecalageBasCote;

                // On verifie que la position decalee est bien sur une route/berge
                // praticable ; sinon on garde la position d'origine (mieux vaut
                // un vehicule au milieu qu'un vehicule dans un mur).
                if (World.GetNextPositionOnStreet(nouvellePos).DistanceTo(nouvellePos) < 5f)
                {
                    v.Position = nouvellePos;
                }
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION dans DecalerSurBasCote (VehiculeEnPanne): " + ex);
            }
        }
    }
}