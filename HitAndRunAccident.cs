// =====================================================================
//  Patrols Callouts - UNWARE STUDIO
//  Callout : Hit-and-Run Accident
//
//  Two vehicles are involved in an accident, but one driver has fled.
//  Interview the victim and witnesses, collect clues, broadcast a
//  description, then search for the fleeing vehicle.
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
    // Console: StartCallout HitAndRunAccident
    [CalloutInfo("HitAndRunAccident", CalloutProbability.Medium)]
    public class HitAndRunAccident : Callout
    {
        // ------------------------------------------------------------------
        //  States
        // ------------------------------------------------------------------
        private enum Behavior { Surrender, FleeInVehicle, AbandonedVehicle }
        private enum Stage { Responding, OnScene, Search, Stopped, Pursuit, Finished }

        private Behavior behavior;
        private Stage stage = Stage.Responding;

        // ------------------------------------------------------------------
        //  Scenario data
        // ------------------------------------------------------------------
        private struct CarColor
        {
            public string Name; public Color Value;
            public CarColor(string n, Color v) { Name = n; Value = v; }
        }

        private struct CarModel
        {
            public string Name; public string Model;
            public CarModel(string n, string m) { Name = n; Model = m; }
        }

        private static readonly Random rnd = new Random();

        private static readonly CarColor[] Colors =
        {
            new CarColor("black", Color.Black),
            new CarColor("white", Color.WhiteSmoke),
            new CarColor("red", Color.Red),
            new CarColor("blue", Color.RoyalBlue),
            new CarColor("silver", Color.Silver),
            new CarColor("green", Color.DarkGreen),
            new CarColor("yellow", Color.Gold),
            new CarColor("gray", Color.Gray)
        };

        private static readonly CarModel[] SuspectModels =
        {
            new CarModel("Bison pickup", "BISON"),
            new CarModel("Sentinel sedan", "SENTINEL"),
            new CarModel("Asea sedan", "ASEA"),
            new CarModel("Premier sedan", "PREMIER"),
            new CarModel("Stanier sedan", "STANIER"),
            new CarModel("Emperor sedan", "EMPEROR"),
            new CarModel("Sadler pickup", "SADLER"),
            new CarModel("Fugitive sedan", "FUGITIVE"),
            new CarModel("Tailgater sedan", "TAILGATER"),
            new CarModel("Granger SUV", "GRANGER")
        };

        private static readonly string[] VictimModels = { "PRIMO", "BLISTA", "INGOT", "MINIVAN", "WASHINGTON", "ORACLE" };

        private CarColor suspectColor;
        private CarModel suspectModel, wrongModel;
        private string plate, plate3;
        private bool drunk;

        // Geometry
        private Vector3 accidentPoint, suspectPoint, searchCenter, roadDir;
        private float roadHeading, searchRadius;
        private string fleeText;

        // Entities
        private Vehicle victimCar, suspectCar;
        private Ped victim, witness1, witness2, suspectPed;
        private Blip areaBlip, searchBlip, suspectBlip;
        private LHandle pursuit;

        private GameFiber logicFiber;
        private CalloutMenu menu;
        private volatile bool running;
        private bool ended;

        // Spawn flags
        private bool sceneReleased, suspectSpawned, suspectReleased, abandonedNoticeShown;

        // What the officer has learned
        private bool knowDirection, knowColor, knowModel, knowPlate;
        private bool victimDone, w1Done, w2Done, paintDone, ambulanceCalled;

        // Suspect stop
        private bool talked, damageChecked, radioChecked, radioBusy;
        private uint radioEndTime;

        // =====================================================================
        //  1) Before the callout is displayed
        // =====================================================================
        public override bool OnBeforeCalloutDisplayed()
        {
            Vector3 playerPos = Game.LocalPlayer.Character.Position;
            accidentPoint = World.GetNextPositionOnStreet(playerPos.Around2D(400f, 1000f));
            if (accidentPoint == Vector3.Zero) return false;

            ShowCalloutAreaBlipBeforeAccepting(accidentPoint, 80f);
            AddMinimumDistanceCheck(250f, accidentPoint);

            CalloutMessage = "Hit-and-run accident";
            CalloutPosition = accidentPoint;

            Functions.PlayScannerAudioUsingPosition(
                "ATTENTION_ALL_UNITS WE_HAVE CRIME_HIT_AND_RUN IN_OR_ON_POSITION", accidentPoint);

            return base.OnBeforeCalloutDisplayed();
        }

        // =====================================================================
        //  2) Callout accepted : prepare the hidden truth of the scenario
        // =====================================================================
        public override bool OnCalloutAccepted()
        {
            int roll = rnd.Next(100);
            behavior = roll < 40 ? Behavior.Surrender : (roll < 75 ? Behavior.FleeInVehicle : Behavior.AbandonedVehicle);

            suspectColor = Colors[rnd.Next(Colors.Length)];
            suspectModel = SuspectModels[rnd.Next(SuspectModels.Length)];
            do { wrongModel = SuspectModels[rnd.Next(SuspectModels.Length)]; }
            while (wrongModel.Model == suspectModel.Model);
            plate = RandomPlate();
            plate3 = plate.Substring(0, 3);
            drunk = rnd.Next(100) < 50;

            // --- Road direction at the accident, and where the suspect fled ---
            float a = (float)(rnd.NextDouble() * 360.0);
            Vector3 dir = MathHelper.ConvertHeadingToDirection(a);
            dir.Z = 0f;
            Vector3 ahead = World.GetNextPositionOnStreet(accidentPoint + dir * 60f);
            Vector3 d = ahead - accidentPoint;
            d.Z = 0f;
            if (d.Length() < 10f) d = dir;
            d = d / d.Length();
            roadDir = d;
            roadHeading = MathHelper.ConvertDirectionToHeading(d);

            float fleeDist = 450f + (float)rnd.NextDouble() * 250f;
            suspectPoint = World.GetNextPositionOnStreet(accidentPoint + d * fleeDist);
            if ((suspectPoint - accidentPoint).Length() < 250f)
                suspectPoint = World.GetNextPositionOnStreet(accidentPoint + d * (fleeDist + 300f));

            Vector3 toSuspect = suspectPoint - accidentPoint;
            toSuspect.Z = 0f;
            fleeText = CalloutUtils.Cardinal(MathHelper.ConvertDirectionToHeading(toSuspect));

            // --- Blip to the scene ---
            areaBlip = new Blip(accidentPoint, 100f);
            areaBlip.Color = Color.Yellow;
            areaBlip.Alpha = 0.5f;
            areaBlip.IsRouteEnabled = true;

            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                "Dispatch", "~b~Hit-and-Run",
                "A driver hit another vehicle and ~r~fled the scene~w~. Interview the witnesses and find the suspect.");

            menu = new CalloutMenu();

            running = true;
            logicFiber = GameFiber.StartNew(LogicLoop, "HitAndRunAccident logic");

            return base.OnCalloutAccepted();
        }

        public override void OnCalloutNotAccepted()
        {
            base.OnCalloutNotAccepted();
        }

        private static string RandomPlate()
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            return rnd.Next(10, 100).ToString()
                 + letters[rnd.Next(letters.Length)]
                 + letters[rnd.Next(letters.Length)]
                 + letters[rnd.Next(letters.Length)]
                 + rnd.Next(100, 1000).ToString();
        }

        // =====================================================================
        //  3) Main loop
        // =====================================================================
        private void LogicLoop()
        {
            try
            {
                while (running)
                {
                    GameFiber.Yield();
                    Ped player = Game.LocalPlayer.Character;

                    // ---------- Phase 0 : on the way to the scene ----------
                    if (stage == Stage.Responding)
                    {
                        if (player.DistanceTo(accidentPoint) < 250f)
                        {
                            if (SpawnScene()) stage = Stage.OnScene;
                            else { CleanFinish("~r~Could not set up the accident scene. Callout cancelled."); return; }
                        }
                        continue;
                    }

                    // ---------- Pursuit (handled by LSPDFR) ----------
                    if (stage == Stage.Pursuit)
                    {
                        if (pursuit == null || !Functions.IsPursuitStillRunning(pursuit))
                        {
                            CleanFinish("Pursuit over. The suspect will be processed if apprehended.");
                            return;
                        }
                        continue;
                    }

                    // ---------- Phase 1 : investigating the scene ----------
                    if (stage == Stage.OnScene)
                    {
                        if (!sceneReleased) TryReleaseScene(player);

                        bool nearScene = player.DistanceTo(accidentPoint) < (menu.IsOpen ? 25f : 20f)
                                         && !player.IsInAnyVehicle(false);

                        if (!menu.IsOpen && nearScene)
                        {
                            Game.DisplayHelp("Press ~INPUT_CONTEXT~ to investigate the accident", 50);
                            if (menu.OpenKeyPressed()) menu.Open();
                        }

                        if (menu.IsOpen)
                        {
                            if (!nearScene) menu.Close();
                            else
                            {
                                int sel = menu.Update();
                                if (sel >= 0) ExecuteSceneItem(sel);
                            }
                        }
                        continue;
                    }

                    // ---------- Phase 2 : searching for the suspect ----------
                    if (stage == Stage.Search)
                    {
                        if (!suspectSpawned)
                        {
                            if (player.DistanceTo(suspectPoint) < 250f)
                            {
                                if (SpawnSuspect()) suspectSpawned = true;
                                else { CleanFinish("~r~Could not locate the suspect vehicle. Callout cancelled."); return; }
                            }
                            continue;
                        }

                        if (!suspectCar || !suspectPed) { CleanFinish("~r~The suspect vehicle is gone."); return; }
                        if (suspectPed.IsDead) { CleanFinish("The suspect is down. Backup will secure the scene."); return; }
                        if (!suspectReleased) { TryReleaseSuspect(player); continue; }

                        HandleSearch(player);
                        continue;
                    }

                    // ---------- Phase 3 : suspect stopped ----------
                    if (stage == Stage.Stopped)
                    {
                        if (!suspectCar || !suspectPed) { CleanFinish("~r~The suspect vehicle is gone."); return; }
                        if (suspectPed.IsDead) { CleanFinish("The suspect is down. Backup will secure the scene."); return; }

                        float d = player.DistanceTo(suspectPed);
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
                                if (sel >= 0) ExecuteSuspectItem(sel);
                            }
                        }

                        HandleRadioCheck();
                    }
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial("[HitAndRunAccident] LogicLoop error: " + ex);
                if (!ended) CleanFinish("~r~Callout ended because of an error.");
            }
        }

        // =====================================================================
        //  4) Accident scene
        // =====================================================================
        private bool SpawnScene()
        {
            try
            {
                victimCar = new Vehicle(VictimModels[rnd.Next(VictimModels.Length)], accidentPoint, roadHeading);
                if (!victimCar) return false;
                victimCar.IsPersistent = true;
                victimCar.IsPositionFrozen = true;      // unfrozen once collision is loaded
                Color vc = Colors[rnd.Next(Colors.Length)].Value;
                victimCar.PrimaryColor = vc;
                victimCar.SecondaryColor = vc;
                NativeFunction.CallByName<uint>("REQUEST_COLLISION_AT_COORD", accidentPoint.X, accidentPoint.Y, accidentPoint.Z);
                // Rear damage + hazard lights
                NativeFunction.CallByName<uint>("SET_VEHICLE_DAMAGE", victimCar, 0.5f, -2.0f, 0f, 250f, 150f, true);
                CalloutUtils.SetHazards(victimCar, true);

                victim = new Ped(victimCar.GetOffsetPosition(new Vector3(-2.4f, 0.8f, 0f)));
                witness1 = new Ped(victimCar.GetOffsetPosition(new Vector3(3.8f, 6f, 0f)));
                witness2 = new Ped(victimCar.GetOffsetPosition(new Vector3(-4.5f, -7f, 0f)));

                foreach (Ped p in new[] { victim, witness1, witness2 })
                {
                    if (!p) return false;
                    p.IsPersistent = true;
                    p.BlockPermanentEvents = true;
                    p.IsPositionFrozen = true;
                    p.Heading = MathHelper.ConvertDirectionToHeading(victimCar.Position - p.Position);
                }

                if (areaBlip) areaBlip.Delete();

                menu.SetItems("ACCIDENT SCENE", "Hit-and-run",
                    "Interview the victim",
                    "Interview witness #1",
                    "Interview witness #2",
                    "Inspect the victim's vehicle",
                    "Request an ambulance",
                    "Broadcast suspect description");

                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                    "Dispatch", "~b~Scene reached",
                    "Interview the victim and the witnesses, then ~y~broadcast a description~w~ to start the search.");
                return true;
            }
            catch (Exception ex)
            {
                Game.LogTrivial("[HitAndRunAccident] SpawnScene error: " + ex);
                return false;
            }
        }

        private void TryReleaseScene(Ped player)
        {
            bool loaded = CalloutUtils.HasCollision(victimCar) || player.DistanceTo(accidentPoint) < 90f;
            if (!loaded) return;

            victimCar.IsPositionFrozen = false;
            victim.IsPositionFrozen = false;
            witness1.IsPositionFrozen = false;
            witness2.IsPositionFrozen = false;

            CalloutUtils.StartScenario(victim, "WORLD_HUMAN_STAND_IMPATIENT");
            CalloutUtils.StartScenario(witness1, "WORLD_HUMAN_STAND_MOBILE");
            CalloutUtils.StartScenario(witness2, "WORLD_HUMAN_STAND_IMPATIENT");
            sceneReleased = true;
        }

        private static bool IsNear(Entity e, float distance)
        {
            return e.Exists() && Game.LocalPlayer.Character.DistanceTo(e) < distance;
        }

        private int ClueCount()
        {
            return (knowDirection ? 1 : 0) + (knowColor ? 1 : 0) + (knowModel ? 1 : 0) + (knowPlate ? 1 : 0);
        }

        private string BuildDescription()
        {
            string vehicle = (knowColor ? suspectColor.Name + " " : "") + (knowModel ? suspectModel.Name : "vehicle");
            string desc = vehicle;
            if (knowPlate) desc += ", plate starting with " + plate3;
            if (knowDirection) desc += ", last seen heading " + fleeText;
            if (drunk && (victimDone)) desc += ", driver possibly drunk";
            return desc;
        }

        private void ExecuteSceneItem(int index)
        {
            switch (index)
            {
                // --- Victim ---
                case 0:
                    if (!IsNear(victim, 6f)) { menu.SetFeedback("Get closer to the victim."); break; }
                    victimDone = true;
                    knowDirection = true;
                    menu.MarkDone(0);
                    CalloutUtils.Say("Victim", "He hit me and sped off toward the " + fleeText + "!" + (drunk ? " He looked drunk!" : ""));
                    menu.SetFeedback("The suspect fled toward the " + fleeText + "." + (drunk ? " ~y~Driver possibly drunk." : ""));
                    break;

                // --- Witness #1 : reliable ---
                case 1:
                    if (!IsNear(witness1, 6f)) { menu.SetFeedback("Get closer to witness #1."); break; }
                    w1Done = true;
                    knowDirection = true;
                    knowColor = true;
                    knowModel = true;
                    menu.MarkDone(1);
                    CalloutUtils.Say("Witness #1", "It was a " + suspectColor.Name + " " + suspectModel.Name + ". It left toward the " + fleeText + ".");
                    menu.SetFeedback("Vehicle: " + suspectColor.Name + " " + suspectModel.Name + ", heading " + fleeText + ".");
                    break;

                // --- Witness #2 : saw the plate, unsure about the model ---
                case 2:
                    if (!IsNear(witness2, 6f)) { menu.SetFeedback("Get closer to witness #2."); break; }
                    w2Done = true;
                    knowPlate = true;
                    menu.MarkDone(2);
                    CalloutUtils.Say("Witness #2", "I'm not sure, maybe a " + wrongModel.Name + "... but the plate started with " + plate3 + ".");
                    string fb = "Partial plate: " + plate3 + "... Model uncertain (" + wrongModel.Name + "?).";
                    if (w1Done && wrongModel.Model != suspectModel.Model)
                        fb += " ~y~Contradicts witness #1 on the model.";
                    menu.SetFeedback(fb);
                    break;

                // --- Paint transfer on the victim's car ---
                case 3:
                    if (!IsNear(victimCar, 6f)) { menu.SetFeedback("Get closer to the victim's vehicle."); break; }
                    paintDone = true;
                    knowColor = true;
                    menu.MarkDone(3);
                    menu.SetFeedback("Paint transfer on the rear bumper: ~y~" + suspectColor.Name + "~w~.");
                    break;

                // --- Ambulance ---
                case 4:
                    if (ambulanceCalled) { menu.SetFeedback("An ambulance is already on its way."); break; }
                    ambulanceCalled = true;
                    menu.MarkDone(4);
                    Functions.RequestBackup(accidentPoint,
                        LSPD_First_Response.EBackupResponseType.Code3,
                        LSPD_First_Response.EBackupUnitType.Ambulance);
                    menu.SetFeedback("Ambulance requested.");
                    break;

                // --- BOLO / start the search ---
                case 5:
                    if (ClueCount() == 0)
                    {
                        menu.SetFeedback("Not enough information yet. Interview the victim or a witness first.");
                        break;
                    }
                    StartSearch();
                    break;
            }
        }

        // =====================================================================
        //  5) Search phase
        // =====================================================================
        private void StartSearch()
        {
            int clues = ClueCount();
            searchRadius = Math.Max(120f, 380f - 70f * clues);   // more clues = smaller area
            searchCenter = suspectPoint.Around2D(10f, searchRadius * 0.5f);

            searchBlip = new Blip(searchCenter, searchRadius);
            searchBlip.Color = Color.Yellow;
            searchBlip.Alpha = 0.5f;
            searchBlip.IsRouteEnabled = true;

            menu.Close();
            stage = Stage.Search;

            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                "Dispatch", "~y~BOLO issued",
                "Looking for: " + BuildDescription() + ". Search area marked on the map.");
            Functions.PlayScannerAudio("ATTENTION_ALL_UNITS");
        }

        private bool SpawnSuspect()
        {
            try
            {
                suspectCar = new Vehicle(suspectModel.Model, suspectPoint, roadHeading);
                if (!suspectCar) return false;
                suspectCar.IsPersistent = true;
                suspectCar.IsPositionFrozen = true;
                suspectCar.PrimaryColor = suspectColor.Value;
                suspectCar.SecondaryColor = suspectColor.Value;
                suspectCar.LicensePlate = plate;
                NativeFunction.CallByName<uint>("REQUEST_COLLISION_AT_COORD", suspectPoint.X, suspectPoint.Y, suspectPoint.Z);
                // Front damage matching the accident
                NativeFunction.CallByName<uint>("SET_VEHICLE_DAMAGE", suspectCar, 0f, 2.2f, 0f, 250f, 150f, true);

                if (behavior == Behavior.AbandonedVehicle)
                {
                    suspectCar.IsEngineOn = false;
                    CalloutUtils.SetHazards(suspectCar, true);
                    suspectPed = new Ped(suspectCar.GetOffsetPosition(new Vector3(6f, -22f, 0f)));
                    if (!suspectPed) return false;
                    suspectPed.IsPositionFrozen = true;
                }
                else
                {
                    suspectPed = new Ped(suspectCar.GetOffsetPosition(new Vector3(0f, 1f, 0f)));
                    if (!suspectPed) return false;
                    suspectPed.WarpIntoVehicle(suspectCar, -1);
                }

                suspectPed.IsPersistent = true;
                suspectPed.BlockPermanentEvents = true;
                Functions.SetPedCantBeArrestedByPlayer(suspectPed, false);
                if (drunk) NativeFunction.CallByName<uint>("SET_PED_IS_DRUNK", suspectPed, true);

                return true;
            }
            catch (Exception ex)
            {
                Game.LogTrivial("[HitAndRunAccident] SpawnSuspect error: " + ex);
                return false;
            }
        }

        private void TryReleaseSuspect(Ped player)
        {
            bool loaded = CalloutUtils.HasCollision(suspectCar) || player.DistanceTo(suspectCar) < 100f;
            if (!loaded) return;

            suspectCar.IsPositionFrozen = false;
            if (behavior == Behavior.AbandonedVehicle)
            {
                suspectPed.IsPositionFrozen = false;
                CalloutUtils.StartScenario(suspectPed, "WORLD_HUMAN_STAND_IMPATIENT");
            }
            else
            {
                suspectPed.Tasks.CruiseWithVehicle(suspectCar, 14f, VehicleDrivingFlags.Normal);
            }
            suspectReleased = true;
        }

        private void HandleSearch(Ped player)
        {
            float distCar = player.DistanceTo(suspectCar);

            // Close enough : the vehicle shows up on the map
            if (!suspectBlip && distCar < 150f)
            {
                if (searchBlip) searchBlip.Delete();
                suspectBlip = suspectCar.AttachBlip();
                suspectBlip.Color = Color.Yellow;
                suspectBlip.IsRouteEnabled = true;
                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                    "Dispatch", "~b~Possible suspect vehicle",
                    behavior == Behavior.AbandonedVehicle
                        ? "A vehicle matching the description has been spotted."
                        : "A vehicle matching the description is driving nearby. Use your ~b~siren~w~ to stop it.");
            }

            switch (behavior)
            {
                case Behavior.Surrender:
                    {
                        bool sirenOn = player.IsInAnyVehicle(false) && player.CurrentVehicle.IsSirenOn;
                        bool pullover = Functions.IsPlayerPerformingPullover() &&
                                        Functions.GetPulloverSuspect(Functions.GetCurrentPullover()) == suspectPed;
                        if (distCar < 45f && (sirenOn || pullover)) StopSuspect(pullover);
                        break;
                    }

                case Behavior.FleeInVehicle:
                    if (distCar < 60f) StartPursuit();
                    break;

                case Behavior.AbandonedVehicle:
                    if (!abandonedNoticeShown && distCar < 30f)
                    {
                        abandonedNoticeShown = true;
                        Game.DisplaySubtitle("~y~The vehicle is abandoned and has front damage. The driver must be nearby!", 5000);
                    }
                    // The driver runs when the player gets too close
                    if (player.DistanceTo(suspectPed) < 14f) StartPursuit();
                    break;
            }
        }

        // The vehicle brakes until it is really stopped (no moving target)
        private void StopSuspect(bool controlledByLspdfr)
        {
            stage = Stage.Stopped;
            Game.DisplaySubtitle("~b~The vehicle is pulling over.", 4000);

            if (!controlledByLspdfr)
                suspectPed.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraightBraking);

            uint timeout = Game.GameTime + 9000;
            while (suspectCar && suspectPed && suspectCar.Speed > 0.6f && Game.GameTime < timeout)
                GameFiber.Yield();

            if (!suspectCar || !suspectPed) return;

            if (!controlledByLspdfr) suspectPed.Tasks.Clear();
            NativeFunction.CallByName<uint>("SET_VEHICLE_HANDBRAKE", suspectCar, true);
            if (suspectBlip) suspectBlip.Delete();

            menu.SetItems("TRAFFIC STOP", suspectModel.Name,
                "Talk to the driver",
                "Inspect the vehicle damage",
                "Check license plate (radio)",
                "Arrest the driver",
                "Let the driver go");
        }

        // =====================================================================
        //  6) Suspect interaction
        // =====================================================================
        private void ExecuteSuspectItem(int index)
        {
            switch (index)
            {
                // --- Talk ---
                case 0:
                    talked = true;
                    menu.MarkDone(0);
                    if (drunk)
                    {
                        CalloutUtils.Say("Driver", "Whaaat? I... didn't see nothing, officer... must've been somebody else.");
                        menu.SetFeedback("~y~Strong smell of alcohol. Slurred speech.");
                    }
                    else
                    {
                        CalloutUtils.Say("Driver", "I panicked... I'm so sorry, I didn't mean to hit him.");
                        menu.SetFeedback("The driver is shaking and avoids eye contact.");
                    }
                    break;

                // --- Damage ---
                case 1:
                    if (!IsNear(suspectCar, 7f)) { menu.SetFeedback("Get closer to the vehicle front."); break; }
                    damageChecked = true;
                    menu.MarkDone(1);
                    menu.SetFeedback("Fresh front-end damage. ~r~Paint transfer matches the victim's vehicle.");
                    break;

                // --- Radio ---
                case 2:
                    if (radioBusy) { menu.SetFeedback("Check already in progress..."); break; }
                    if (radioChecked) { menu.SetFeedback("Plate already checked."); break; }
                    radioBusy = true;
                    radioEndTime = Game.GameTime + 4000;
                    menu.SetFeedback("Contacting dispatch...");
                    break;

                // --- Arrest ---
                case 3:
                    menu.Close();
                    suspectPed.Tasks.LeaveVehicle(suspectCar, LeaveVehicleFlags.None).WaitForCompletion(4000);
                    if (suspectPed) Functions.SetPedAsArrested(suspectPed, true);
                    int evidence = (talked ? 1 : 0) + (damageChecked ? 1 : 0) + (radioChecked ? 1 : 0)
                                   + (knowPlate ? 1 : 0);
                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                        "Dispatch", "~g~Hit-and-run suspect arrested",
                        evidence >= 3 ? "Solid case: the evidence is strong. Good work, officer."
                                      : "Arrest made, but the evidence is thin. A prosecutor may struggle.");
                    CleanFinish(null);
                    break;

                // --- Let go ---
                case 4:
                    menu.Close();
                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                        "Dispatch", "~r~Suspect released",
                        "The victim was injured by this driver, though...");
                    NativeFunction.CallByName<uint>("SET_VEHICLE_HANDBRAKE", suspectCar, false);
                    suspectPed.Tasks.CruiseWithVehicle(suspectCar, 20f, VehicleDrivingFlags.Normal);
                    CleanFinish(null);
                    break;
            }
        }

        private void HandleRadioCheck()
        {
            if (radioBusy && Game.GameTime >= radioEndTime)
            {
                radioBusy = false;
                radioChecked = true;
                menu.MarkDone(2);
                if (knowPlate)
                    menu.SetFeedback("Dispatch: ~r~plate matches the partial plate~w~ given by witness #2 (" + plate3 + "...).");
                else
                    menu.SetFeedback("Dispatch: vehicle registered to the driver, ~y~no prior report~w~.");
            }
        }

        // =====================================================================
        //  7) Pursuit
        // =====================================================================
        private void StartPursuit()
        {
            if (stage == Stage.Pursuit) return;
            stage = Stage.Pursuit;
            menu.Close();
            if (suspectBlip) suspectBlip.Delete();
            if (searchBlip) searchBlip.Delete();
            if (suspectCar) NativeFunction.CallByName<uint>("SET_VEHICLE_HANDBRAKE", suspectCar, false);

            pursuit = Functions.CreatePursuit();
            Functions.AddPedToPursuit(pursuit, suspectPed);
            Functions.SetPursuitIsActiveForPlayer(pursuit, true);
            Functions.RequestBackup(Game.LocalPlayer.Character.Position,
                LSPD_First_Response.EBackupResponseType.Pursuit,
                LSPD_First_Response.EBackupUnitType.LocalUnit);

            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                "Dispatch", "~r~The suspect is fleeing!", "Pursuit in progress, backup is on the way.");
        }

        // =====================================================================
        //  8) End / cleanup
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
                if (searchBlip) searchBlip.Delete();
                if (suspectBlip) suspectBlip.Delete();

                if (victimCar) victimCar.IsPositionFrozen = false;
                if (suspectCar) suspectCar.IsPositionFrozen = false;
                if (victim) victim.Dismiss();
                if (witness1) witness1.Dismiss();
                if (witness2) witness2.Dismiss();
                if (suspectPed) suspectPed.Dismiss();
                if (victimCar) victimCar.Dismiss();
                if (suspectCar) suspectCar.Dismiss();
            }
            base.End();
        }
    }
}