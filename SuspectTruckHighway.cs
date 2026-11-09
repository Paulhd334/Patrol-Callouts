// =====================================================================
//  Patrols Callouts - UNWARE STUDIO
//  Callout : Suspect Truck on the Highway   (FIXED VERSION)
//  Original idea : Thomas20
//
//  References : RagePluginHookSDK.dll, LSPD First Response.dll,
//               System.Drawing, System.Windows.Forms
//  Requires   : CalloutMenu.cs (PatrolsCallouts.Utils)
// =====================================================================
using System;
using System.Drawing;
using Rage;
using Rage.Native;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using PatrolsCallouts.Utils;

namespace PatrolsCallouts.Callouts
{
    // Console: StartCallout SuspectTruckHighway
    [CalloutInfo("SuspectTruckHighway", CalloutProbability.Medium)]
    public class SuspectTruckHighway : Callout
    {
        private enum Scenario { Cooperative, FakePapers, Flee }
        private enum Stage { Waiting, Driving, Stopped, Pursuit, Finished }

        private Scenario scenario;
        private Stage stage = Stage.Waiting;

        // Hidden truth of the stop
        private bool papersAreFake;
        private bool cargoIsStolen;

        // What the officer has discovered so far
        private bool askedPapers, checkedRadio, checkedCargo, talked;
        private bool radioBusy;
        private uint radioEndTime;

        // Entities
        private Vehicle truck, trailer;
        private Ped driver;
        private Blip areaBlip, truckBlip;
        private Vector3 spawnPoint;
        private LHandle pursuit;

        private GameFiber logicFiber;
        private CalloutMenu menu;
        private volatile bool running;
        private bool ended;
        private bool released;      // truck unfrozen and driving

        // Scenario data
        private static readonly Random rnd = new Random();

        private static readonly string[] TruckModels = { "HAULER", "PHANTOM", "PACKER", "MULE", "BENSON", "POUNDER" };
        private static readonly string[] Cargos = { "auto parts", "food supplies", "construction equipment", "furniture", "beverages" };
        private static readonly string[] StolenCargos = { "flat screen TVs", "power tools", "brand new phones", "engine parts", "cases of liquor" };
        private static readonly string[] Companies = { "Gruppe Sechs Logistics", "Ponsonbys Transport", "Los Santos Freight", "Blaine Haulage", "Vinewood Express" };
        private static readonly string[] HighwayKeywords = { "Fwy", "Hwy", "Freeway", "Highway", "Route 68" };

        private string declaredCargo, realCargo, company;

        // =====================================================================
        //  1) Before the callout is displayed
        // =====================================================================
        public override bool OnBeforeCalloutDisplayed()
        {
            Vector3 playerPos = Game.LocalPlayer.Character.Position;
            Vector3 found = Vector3.Zero;

            for (int i = 0; i < 40; i++)
            {
                Vector3 p = World.GetNextPositionOnStreet(playerPos.Around2D(500f, 1500f));
                string street = World.GetStreetName(p);
                found = p;
                if (IsHighway(street)) break;
            }

            if (found == Vector3.Zero) return false;
            spawnPoint = found;

            ShowCalloutAreaBlipBeforeAccepting(spawnPoint, 80f);
            AddMinimumDistanceCheck(300f, spawnPoint);

            CalloutMessage = "Suspect truck on the highway";
            CalloutPosition = spawnPoint;

            Functions.PlayScannerAudioUsingPosition(
                "ATTENTION_ALL_UNITS WE_HAVE CRIME_SUSPICIOUS_VEHICLE IN_OR_ON_POSITION", spawnPoint);

            return base.OnBeforeCalloutDisplayed();
        }

        private static bool IsHighway(string street)
        {
            if (string.IsNullOrEmpty(street)) return false;
            foreach (string k in HighwayKeywords)
                if (street.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // =====================================================================
        //  2) Callout accepted : only prepare data, the truck is spawned when
        //     the player gets close
        // =====================================================================
        public override bool OnCalloutAccepted()
        {
            int roll = rnd.Next(100);
            scenario = roll < 35 ? Scenario.Cooperative : (roll < 70 ? Scenario.FakePapers : Scenario.Flee);

            company = Companies[rnd.Next(Companies.Length)];
            declaredCargo = Cargos[rnd.Next(Cargos.Length)];

            switch (scenario)
            {
                case Scenario.Cooperative:
                    papersAreFake = false;
                    cargoIsStolen = rnd.Next(100) < 30;   // sometimes the driver is just a victim
                    break;
                case Scenario.FakePapers:
                    papersAreFake = true;
                    cargoIsStolen = true;
                    break;
                default:
                    papersAreFake = rnd.Next(100) < 70;
                    cargoIsStolen = true;
                    break;
            }
            realCargo = cargoIsStolen ? StolenCargos[rnd.Next(StolenCargos.Length)] : declaredCargo;

            areaBlip = new Blip(spawnPoint, 120f);
            areaBlip.Color = Color.Yellow;
            areaBlip.Alpha = 0.5f;
            areaBlip.IsRouteEnabled = true;

            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                "Dispatch", "~b~Suspect Truck",
                "A truck is carrying ~r~stolen cargo~w~. Head to the marked area and locate it.");

            menu = new CalloutMenu();
            menu.SetItems("TRAFFIC STOP", company,
                "Talk to the driver",
                "Ask for documents",
                "Inspect the cargo",
                "Check documents (radio)",
                "Arrest the driver",
                "Let the driver go");

            running = true;
            logicFiber = GameFiber.StartNew(LogicLoop, "SuspectTruckHighway logic");

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted()
        {
            base.OnCalloutNotAccepted();
        }

        // =====================================================================
        //  3) Spawn the truck (frozen until the road collision is loaded)
        // =====================================================================
        private bool SpawnTruck()
        {
            try
            {
                Vector3 playerPos = Game.LocalPlayer.Character.Position;

                // The truck drives away from the player along the road
                Vector3 away = spawnPoint - playerPos;
                away.Z = 0f;
                if (away.Length() < 1f) away = Vector3.RelativeFront;
                away = away / away.Length();
                Vector3 ahead = World.GetNextPositionOnStreet(spawnPoint + away * 80f);
                Vector3 dirToAhead = ahead - spawnPoint;
                dirToAhead.Z = 0f;
                float heading = dirToAhead.Length() < 5f
                    ? MathHelper.ConvertDirectionToHeading(away)
                    : MathHelper.ConvertDirectionToHeading(dirToAhead);

                string model = TruckModels[rnd.Next(TruckModels.Length)];
                truck = new Vehicle(model, spawnPoint, heading);
                if (!truck) return false;
                truck.IsPersistent = true;
                truck.IsPositionFrozen = true;     // FIX: avoids falling through the map
                NativeFunction.CallByName<uint>("REQUEST_COLLISION_AT_COORD", spawnPoint.X, spawnPoint.Y, spawnPoint.Z);
                NativeFunction.CallByName<uint>("SET_VEHICLE_DIRT_LEVEL", truck, 4f + (float)rnd.NextDouble() * 8f);

                // Semi-trailer for tractor units
                if (model == "HAULER" || model == "PHANTOM" || model == "PACKER")
                {
                    Vector3 behind = truck.GetOffsetPositionFront(-9f);
                    trailer = new Vehicle("TRAILERS", behind, heading);
                    if (trailer)
                    {
                        trailer.IsPersistent = true;
                        trailer.IsPositionFrozen = true;
                        NativeFunction.CallByName<uint>("ATTACH_VEHICLE_TO_TRAILER", truck, trailer, 5f);
                        GameFiber.Sleep(200);
                        // FIX: if the trailer did not attach, remove it (no more floating trailer)
                        if (!NativeFunction.CallByName<bool>("IS_VEHICLE_ATTACHED_TO_TRAILER", truck))
                        {
                            trailer.Delete();
                            trailer = null;
                        }
                    }
                }

                driver = new Ped(truck.GetOffsetPositionFront(1f));
                if (!driver) return false;
                driver.IsPersistent = true;
                driver.BlockPermanentEvents = true;
                driver.WarpIntoVehicle(truck, -1);
                Functions.SetPedCantBeArrestedByPlayer(driver, false);

                if (areaBlip) areaBlip.Delete();
                truckBlip = truck.AttachBlip();
                truckBlip.Color = Color.Yellow;
                truckBlip.IsRouteEnabled = true;

                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                    "Dispatch", "~b~Truck located",
                    "Catch up with the truck. Turn on your ~b~siren~w~ behind it to make it pull over.");

                return true;
            }
            catch (Exception ex)
            {
                Game.LogTrivial("[SuspectTruckHighway] SpawnTruck error: " + ex);
                return false;
            }
        }

        // Unfreeze the truck once the collision is loaded (or the player is near)
        private void TryReleaseTruck(Ped player)
        {
            bool loaded = CalloutUtils.HasCollision(truck) || player.DistanceTo(truck) < 100f;
            if (!loaded) return;

            truck.IsPositionFrozen = false;
            if (trailer) trailer.IsPositionFrozen = false;
            driver.Tasks.CruiseWithVehicle(truck, 17f, VehicleDrivingFlags.Normal);
            released = true;
        }

        // =====================================================================
        //  4) Main loop (wrapped in try/catch: an exception in a fiber would
        //     otherwise crash the plugin)
        // =====================================================================
        private void LogicLoop()
        {
            try
            {
                while (running)
                {
                    GameFiber.Yield();
                    Ped player = Game.LocalPlayer.Character;

                    // ---------- Phase 0 : player is on the way ----------
                    if (stage == Stage.Waiting)
                    {
                        if (player.DistanceTo(spawnPoint) < 300f)
                        {
                            if (SpawnTruck()) stage = Stage.Driving;
                            else { CleanFinish("~r~Could not locate the truck. Callout cancelled."); return; }
                        }
                        continue;
                    }

                    // ---------- Phase 3 : pursuit (handled by LSPDFR) ----------
                    if (stage == Stage.Pursuit)
                    {
                        if (pursuit == null || !Functions.IsPursuitStillRunning(pursuit))
                        {
                            CleanFinish("Pursuit over. Cargo will be seized if the driver was apprehended.");
                            return;
                        }
                        continue;
                    }

                    if (!driver || !truck) { CleanFinish("~r~The truck is gone."); return; }
                    if (driver.IsDead) { CleanFinish("The driver is down. Cargo secured by backup."); return; }

                    // ---------- Phase 1 : truck is driving ----------
                    if (stage == Stage.Driving)
                    {
                        if (!released) { TryReleaseTruck(player); continue; }

                        float dist = player.DistanceTo(truck);

                        if (scenario == Scenario.Flee && dist < 60f)
                        {
                            StartPursuit();
                            continue;
                        }

                        bool sirenOn = player.IsInAnyVehicle(false) && player.CurrentVehicle.IsSirenOn;
                        bool pullover = Functions.IsPlayerPerformingPullover() &&
                                        Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) == driver;

                        if (dist < 45f && (sirenOn || pullover))
                            StopTruck(pullover);
                    }

                    // ---------- Phase 2 : truck stopped ----------
                    else if (stage == Stage.Stopped)
                    {
                        if (truckBlip) truckBlip.Delete();

                        float d = player.DistanceTo(driver);
                        bool nearDriver = d < (menu.IsOpen ? 6f : 4.5f) && !player.IsInAnyVehicle(false);

                        if (!menu.IsOpen && nearDriver)
                        {
                            Game.DisplayHelp("Press ~INPUT_CONTEXT~ to interact with the driver", 50);
                            if (menu.OpenKeyPressed()) menu.Open();
                        }

                        if (menu.IsOpen)
                        {
                            if (!nearDriver) menu.Close();
                            else
                            {
                                int sel = menu.Update();
                                if (sel >= 0) ExecuteMenuItem(sel);
                            }
                        }

                        HandleRadioCheck();
                    }
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial("[SuspectTruckHighway] LogicLoop error: " + ex);
                if (!ended) CleanFinish("~r~Callout ended because of an error.");
            }
        }

        // FIX: the truck now really brakes until it is stopped, instead of
        // being sent to a parking point 25 m ahead while still rolling
        private void StopTruck(bool controlledByLspdfr)
        {
            stage = Stage.Stopped;
            Game.DisplaySubtitle("~b~The truck is pulling over.", 4000);

            if (!controlledByLspdfr)
                driver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraightBraking);

            uint timeout = Game.GameTime + 9000;
            while (truck && driver && truck.Speed > 0.6f && Game.GameTime < timeout)
                GameFiber.Yield();

            if (!truck || !driver) return;

            if (!controlledByLspdfr) driver.Tasks.Clear();
            NativeFunction.CallByName<uint>("SET_VEHICLE_HANDBRAKE", truck, true);
            CalloutUtils.SetHazards(truck, true);
        }

        // =====================================================================
        //  5) Menu actions
        // =====================================================================
        private bool IsAtRearOfTruck()
        {
            Vehicle rear = trailer ? trailer : truck;
            Vector3 rearPos = rear.GetOffsetPositionFront(-(rear.Model.Dimensions.Y * 0.5f) - 1f);
            return Game.LocalPlayer.Character.DistanceTo(rearPos) < 5.5f;
        }

        private void ExecuteMenuItem(int index)
        {
            switch (index)
            {
                // --- Talk ---
                case 0:
                    talked = true;
                    menu.MarkDone(0);
                    if (scenario == Scenario.Cooperative)
                        CalloutUtils.Say("Driver", "Good day, officer. Is there a problem? I'm delivering for " + company + ".");
                    else if (scenario == Scenario.FakePapers)
                        CalloutUtils.Say("Driver", "I'm in a hurry... I have a delivery to make. Everything is in order!");
                    else
                        CalloutUtils.Say("Driver", "...");
                    menu.SetFeedback(scenario == Scenario.FakePapers ? "The driver seems very nervous." : "The driver stays calm.");
                    break;

                // --- Documents ---
                case 1:
                    askedPapers = true;
                    menu.MarkDone(1);
                    menu.SetFeedback("Bill of lading: " + declaredCargo + " (" + company + ").");
                    CalloutUtils.Say("Driver", "Here are my papers: license, registration and bill of lading.");
                    break;

                // --- Cargo ---
                case 2:
                    if (!IsAtRearOfTruck())
                    {
                        menu.SetFeedback("Go to the rear of the truck to open the cargo.");
                        break;
                    }
                    checkedCargo = true;
                    menu.MarkDone(2);
                    string cargoText = "Cargo: " + realCargo + ".";
                    if (askedPapers && realCargo != declaredCargo)
                        cargoText += " ~r~Does not match the bill of lading!";
                    else if (askedPapers)
                        cargoText += " ~g~Matches the documents.";
                    menu.SetFeedback(cargoText);
                    break;

                // --- Radio ---
                case 3:
                    if (!askedPapers) { menu.SetFeedback("Ask the driver for documents first."); break; }
                    if (radioBusy) { menu.SetFeedback("Check already in progress..."); break; }
                    if (checkedRadio) { menu.SetFeedback("Documents already checked."); break; }
                    radioBusy = true;
                    radioEndTime = Game.GameTime + 4000;
                    menu.SetFeedback("Contacting dispatch...");
                    break;

                // --- Arrest ---
                case 4:
                    if (scenario == Scenario.FakePapers && !checkedRadio && !checkedCargo && rnd.Next(100) < 40)
                    {
                        // Guilty driver panics if arrested without evidence
                        menu.Close();
                        StartPursuit();
                        return;
                    }
                    menu.Close();
                    driver.Tasks.LeaveVehicle(truck, LeaveVehicleFlags.None).WaitForCompletion(4000);
                    if (driver) Functions.SetPedAsArrested(driver, true);
                    bool justified = papersAreFake || cargoIsStolen;
                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                        "Dispatch", justified ? "~g~Justified arrest" : "~r~Unjustified arrest",
                        justified ? "Cargo seized. Good work, officer." :
                                    "The driver was clean. A complaint may be filed.");
                    CleanFinish(null);
                    break;

                // --- Let go ---
                case 5:
                    menu.Close();
                    bool wasGuilty = papersAreFake || cargoIsStolen;
                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                        "Dispatch", wasGuilty ? "~r~Suspect released" : "~g~Stop completed",
                        wasGuilty ? "The cargo was suspicious, though..." : "The driver is on his way, everything was in order.");
                    NativeFunction.CallByName<uint>("SET_VEHICLE_HANDBRAKE", truck, false);
                    CalloutUtils.SetHazards(truck, false);
                    driver.Tasks.CruiseWithVehicle(truck, 20f, VehicleDrivingFlags.Normal);
                    CleanFinish(null);
                    break;
            }
        }

        private void HandleRadioCheck()
        {
            if (radioBusy && Game.GameTime >= radioEndTime)
            {
                radioBusy = false;
                checkedRadio = true;
                menu.MarkDone(3);
                if (papersAreFake)
                    menu.SetFeedback("Dispatch: ~r~forged documents~w~, no company registered under this name.");
                else if (cargoIsStolen)
                    menu.SetFeedback("Dispatch: documents valid. ~y~A cargo theft report is being checked.");
                else
                    menu.SetFeedback("Dispatch: ~g~documents valid~w~, nothing to report.");
            }
        }

        // =====================================================================
        //  6) Pursuit
        // =====================================================================
        private void StartPursuit()
        {
            if (stage == Stage.Pursuit) return;
            stage = Stage.Pursuit;
            menu.Close();
            if (truckBlip) truckBlip.Delete();
            if (areaBlip) areaBlip.Delete();
            NativeFunction.CallByName<uint>("SET_VEHICLE_HANDBRAKE", truck, false);
            CalloutUtils.SetHazards(truck, false);

            pursuit = Functions.CreatePursuit();
            Functions.AddPedToPursuit(pursuit, driver);
            Functions.SetPursuitIsActiveForPlayer(pursuit, true);
            Functions.RequestBackup(Game.LocalPlayer.Character.Position,
                LSPD_First_Response.EBackupResponseType.Pursuit,
                LSPD_First_Response.EBackupUnitType.LocalUnit);

            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                "Dispatch", "~r~The driver is fleeing!", "Pursuit in progress, backup is on the way.");
        }

        // =====================================================================
        //  7) End / cleanup
        // =====================================================================
        private void CleanFinish(string message)
        {
            if (stage == Stage.Finished) return;
            stage = Stage.Finished;
            running = false;
            if (menu != null) menu.Close();
            if (!string.IsNullOrEmpty(message))
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "Dispatch", "Callout finished", message);
            Functions.PlayScannerAudio("ATTENTION_ALL_UNITS_02 WE_ARE_CODE_4");
            End();
        }

        public override void Process()
        {
            base.Process();
        }

        public override void End()
        {
            running = false;
            if (!ended)
            {
                ended = true;
                if (menu != null) menu.Dispose();

                if (areaBlip) areaBlip.Delete();
                if (truckBlip) truckBlip.Delete();
                if (truck) truck.IsPositionFrozen = false;
                if (trailer) trailer.IsPositionFrozen = false;
                if (driver) driver.Dismiss();
                if (truck) truck.Dismiss();
                if (trailer) trailer.Dismiss();
            }
            base.End();
        }
    }
}