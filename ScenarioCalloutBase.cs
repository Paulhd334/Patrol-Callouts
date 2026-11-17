using System;
using System.Drawing;
using System.Windows.Forms;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using PatrolsCallouts.Utils;
using Rage;
using Rage.Native;

namespace PatrolsCallouts.Callouts
{
    public enum SceneKind { Abandoned, GasStation, Barricaded, Theft, PoliceAccident, Bus }

    // All world operations run on LSPDFR's Process fiber. No entity is required
    // before acceptance, and nothing spawns until nearby collision is loaded.
    public abstract class ScenarioCalloutBase : Callout
    {
        protected abstract SceneKind Kind { get; }
        protected abstract string DispatchName { get; }
        protected abstract string DispatchAdvisory { get; }
        protected abstract string SceneTitle { get; }
        protected abstract string InitialBriefing { get; }
        protected abstract string InterviewText { get; }
        protected abstract string EvidenceText { get; }
        protected abstract string VehicleModel { get; }
        protected virtual bool HasWitness { get { return false; } }
        protected virtual bool EscalatesToPursuit { get { return false; } }

        private readonly ScenarioProgress progress = new ScenarioProgress();
        private Vector3 scenePosition;
        private float heading;
        private Vehicle vehicle, otherVehicle;
        private Ped subject, witness;
        private Blip destination, targetBlip;
        private LHandle pursuit;
        private bool ended, accepted, ready, pursuitStarted, backupRequested;
        private bool interactionWasDown, backupWasDown, endWasDown, ownsHelp;
        private uint spawnStart, retryAt, promptAt, dialogueUntil, busExitStarted;
        private string lastPrompt;
        private Vector3 subjectPoint, witnessPoint, secondVehiclePoint;

        private bool DriverSuspect { get { return Kind == SceneKind.Theft || Kind == SceneKind.Barricaded; } }

        public override bool OnBeforeCalloutDisplayed()
        {
            try
            {
                if (!SelectLocation()) return false;
                CalloutMessage = DispatchName;
                CalloutAdvisory = DispatchAdvisory;
                CalloutPosition = scenePosition;
                ShowCalloutAreaBlipBeforeAccepting(scenePosition, 35f);
                AddMinimumDistanceCheck(80f, scenePosition);
                return base.OnBeforeCalloutDisplayed();
            }
            catch (Exception ex) { Fail("offer", ex); return false; }
        }

        private bool SelectLocation()
        {
            Vector3 player = Game.LocalPlayer.Character.Position;
            // Fixed forecourts: do not project a gas-station call onto a random street.
            if (Kind == SceneKind.GasStation)
            {
                Vector3[] stations = {
                    new Vector3(265.2f, -1261.3f, 29.2f),
                    new Vector3(-723.1f, -935.5f, 19.2f),
                    new Vector3(1181.0f, -330.5f, 69.3f),
                    new Vector3(1698.0f, 4924.0f, 42.1f)
                };
                float best = float.MaxValue;
                foreach (Vector3 station in stations)
                {
                    float distance = player.DistanceTo(station);
                    if (distance > 100f && distance < best) { best = distance; scenePosition = station; }
                }
                heading = 90f;
                return scenePosition != Vector3.Zero;
            }

            Vector3 anchor = player;
            if (Kind == SceneKind.Abandoned)
            {
                Vector3[] ruralRoads = {
                    new Vector3(1095f, 2662f, 38f),
                    new Vector3(1985f, 4600f, 41f),
                    new Vector3(-1118f, 4908f, 218f)
                };
                float best = float.MaxValue;
                foreach (Vector3 rural in ruralRoads)
                    if (player.DistanceTo(rural) < best) { anchor = rural; best = player.DistanceTo(rural); }
            }
            for (int attempt = 0; attempt < 20; attempt++)
            {
                Vector3 candidate = Kind == SceneKind.Abandoned
                    ? anchor.Around2D(40f, 160f) : player.Around2D(350f, 700f);
                Vector3 node;
                float roadHeading;
                if (!NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(
                    candidate.X, candidate.Y, candidate.Z, out node, out roadHeading, 1, 3f, 0)) continue;
                if (node == Vector3.Zero || player.DistanceTo(node) < 100f) continue;
                scenePosition = node;
                heading = roadHeading;
                return true;
            }
            Main.Log(DispatchName + ": no road node found.");
            return false;
        }

        public override bool OnCalloutAccepted()
        {
            try
            {
                accepted = true;
                // Use a point, not a filled yellow search circle, after acceptance.
                destination = new Blip(scenePosition);
                destination.Color = Color.Yellow;
                destination.IsRouteEnabled = true;
                Notify(InitialBriefing + " Follow the GPS. [B]: one backup unit. [End]: cancel.");
                base.OnCalloutAccepted();
                return true;
            }
            catch (Exception ex) { Fail("accept", ex); return false; }
        }

        public override void Process()
        {
            base.Process();
            if (!accepted || ended) return;
            try
            {
                bool interact = Pressed(Keys.Y, ref interactionWasDown);
                bool backup = Pressed(Keys.B, ref backupWasDown);
                bool cancel = Pressed(Keys.End, ref endWasDown);
                if (cancel) { Complete("Callout canceled by the officer."); return; }
                if (backup) RequestBackup();
                Ped player = Game.LocalPlayer.Character;
                if (!player.Exists() || player.IsDead) { End(); return; }

                if (!ready)
                {
                    if (player.DistanceTo(scenePosition) > 150f)
                    { Prompt("Follow the GPS to the scene. [B] Backup | [End] Cancel"); return; }
                    // Delete guidance on arrival even while waiting for collision.
                    DeleteBlip(ref destination);
                    if (spawnStart == 0) spawnStart = Game.GameTime;
                    if (Game.GameTime - spawnStart > 15000)
                    { Complete("Scene unavailable: no safe ground or clear space. Please try again."); return; }
                    Prompt("Preparing the scene. [End] Cancel");
                    if (Game.GameTime < retryAt) return;
                    retryAt = Game.GameTime + 500;
                    if (!PrepareGround()) return;
                    SpawnScene();
                    ready = true;
                    Main.Log(DispatchName + ": scene ready at " + scenePosition);
                }

                if (pursuitStarted)
                {
                    if (!Functions.IsPursuitStillRunning(pursuit))
                        Complete("Pursuit concluded. Code four.");
                    else Prompt("Follow the fleeing suspect. [B] Backup | [End] End callout");
                    return;
                }
                if (subject == null || !subject.Exists() || subject.IsDead)
                { Complete("The involved person is unavailable. Dispatch has been notified."); return; }
                if (DriverSuspect && Functions.IsPedArrested(subject))
                { Complete("Suspect in custody. Code four."); return; }
                if (HasWitness && (witness == null || !witness.Exists() || witness.IsDead))
                { Complete("The witness is unavailable. Dispatch has been notified."); return; }
                if (Kind == SceneKind.Bus && busExitStarted != 0 && subject.IsInAnyVehicle(false)
                    && Game.GameTime - busExitStarted > 12000)
                {
                    // Some replacement bus models have unusable door paths.
                    // Recover only onto a freshly validated outdoor position.
                    Vector3 exitPoint;
                    if (!SafePedPoint(Offset(4f, 3f), out exitPoint))
                    { Complete("The bus exit is obstructed. Dispatch will arrange assistance."); return; }
                    NativeFunction.Natives.CLEAR_PED_TASKS_IMMEDIATELY(subject);
                    subject.Position = exitPoint;
                }

                // Theft is an encounter, not a hidden menu choice. Can trigger in a patrol car.
                if (Kind == SceneKind.Theft && player.DistanceTo(vehicle) <= 60f)
                { StartPursuit(); return; }

                Entity target = GetTarget();
                SetTarget(target);
                float range = target == vehicle || target == otherVehicle ? 8f : 4f;
                if (player.DistanceTo(target) > range || player.IsInAnyVehicle(false))
                {
                    Prompt("Approach " + TargetName() + " on foot. [B] Backup | [End] Cancel");
                    return;
                }
                if (Game.GameTime < dialogueUntil) return;
                Prompt("[Y] " + ActionText() + " | [B] Backup | [End] Cancel");
                if (interact) Advance();
            }
            catch (Exception ex) { Fail("process", ex); }
        }

        private bool PrepareGround()
        {
            NativeFunction.Natives.REQUEST_COLLISION_AT_COORD(scenePosition.X, scenePosition.Y, scenePosition.Z);
            float? ground = World.GetGroundZ(scenePosition + new Vector3(0f, 0f, 8f), false, false);
            if (!ground.HasValue || Math.Abs(ground.Value - scenePosition.Z) > 8f) return false;
            scenePosition = new Vector3(scenePosition.X, scenePosition.Y, ground.Value + 0.15f);
            if (NativeFunction.Natives.IS_ANY_VEHICLE_NEAR_POINT<bool>(
                scenePosition.X, scenePosition.Y, scenePosition.Z, Kind == SceneKind.Bus ? 9f : 5f)) return false;

            // Ped positions are navmesh-validated and ground-checked before creating anything.
            if (!SafePedPoint(Offset(3.5f, 2f), out subjectPoint)) return false;
            if (HasWitness)
            {
                if (!SafePedPoint(Offset(4f, -5f), out witnessPoint)) return false;
                if (subjectPoint.DistanceTo(witnessPoint) < 2f) return false;
            }
            if (Kind == SceneKind.PoliceAccident)
            {
                secondVehiclePoint = Offset(0f, 7f);
                float? otherGround = World.GetGroundZ(secondVehiclePoint + new Vector3(0f, 0f, 6f), false, false);
                if (!otherGround.HasValue) return false;
                secondVehiclePoint.Z = otherGround.Value + 0.15f;
                if (NativeFunction.Natives.IS_ANY_VEHICLE_NEAR_POINT<bool>(
                    secondVehiclePoint.X, secondVehiclePoint.Y, secondVehiclePoint.Z, 4f)) return false;
            }
            return true;
        }

        private bool SafePedPoint(Vector3 desired, out Vector3 point)
        {
            point = Vector3.Zero;
            Vector3 safe;
            if (!NativeFunction.Natives.GET_SAFE_COORD_FOR_PED<bool>(
                desired.X, desired.Y, desired.Z, true, out safe, 0)) return false;
            if (safe.DistanceTo(scenePosition) > 18f) return false;
            float? ground = World.GetGroundZ(safe + new Vector3(0f, 0f, 5f), false, false);
            if (!ground.HasValue || Math.Abs(ground.Value - scenePosition.Z) > 3f) return false;
            point = new Vector3(safe.X, safe.Y, ground.Value + 0.1f);
            return true;
        }

        private Vector3 Offset(float right, float forward)
        {
            double radians = heading * Math.PI / 180.0;
            return scenePosition + new Vector3(
                (float)(right * Math.Cos(radians) - forward * Math.Sin(radians)),
                (float)(right * Math.Sin(radians) + forward * Math.Cos(radians)), 0f);
        }

        private void SpawnScene()
        {
            vehicle = SpawnVehicle(VehicleModel, scenePosition, heading);
            subject = SpawnPed(Kind == SceneKind.PoliceAccident ? "s_m_y_cop_01" : "a_m_y_business_02", subjectPoint);
            if (HasWitness)
                witness = SpawnPed(Kind == SceneKind.Bus ? "s_m_m_gentransport" : "a_f_y_business_02", witnessPoint);

            if (DriverSuspect)
            {
                subject.WarpIntoVehicle(vehicle, -1);
                vehicle.IsEngineOn = true;
            }
            else
            {
                vehicle.IsEngineOn = false;
                CalloutUtils.SetHazards(vehicle, true);
            }
            if (Kind == SceneKind.Bus)
            {
                // The driver is outside the front door. The passenger is seated,
                // then asked to get off the bus before the warning.
                if (vehicle.PassengerCapacity < 1) throw new InvalidOperationException("Bus has no passenger seat.");
                subject.WarpIntoVehicle(vehicle, 0);
                NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle, 0, false, false);
            }
            if (Kind == SceneKind.PoliceAccident)
            {
                Functions.SetPedAsCop(subject);
                otherVehicle = SpawnVehicle("ASEA", secondVehiclePoint, heading + 170f);
                CalloutUtils.SetHazards(otherVehicle, true);
                NativeFunction.Natives.SET_VEHICLE_DAMAGE(vehicle, 0f, 2f, 0f, 500f, 1f, true);
                NativeFunction.Natives.SET_VEHICLE_DAMAGE(otherVehicle, 0f, 2f, 0f, 500f, 1f, true);
            }
        }

        private Vehicle SpawnVehicle(string model, Vector3 position, float orientation)
        {
            Vehicle result = new Vehicle(model, position, orientation);
            if (result == null || !result.Exists()) throw new InvalidOperationException("Could not spawn " + model);
            // Assign ownership immediately so a later native failure cannot leak it.
            if (vehicle == null) vehicle = result; else otherVehicle = result;
            result.MakePersistent();
            if (!NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY<bool>(result))
                throw new InvalidOperationException("Vehicle ground placement failed.");
            return result;
        }

        private Ped SpawnPed(string model, Vector3 position)
        {
            Ped result = new Ped(model, position, heading);
            if (result == null || !result.Exists()) throw new InvalidOperationException("Could not spawn " + model);
            if (subject == null) subject = result; else witness = result;
            result.MakePersistent();
            result.BlockPermanentEvents = true;
            NativeFunction.Natives.TASK_STAND_STILL(result, -1);
            return result;
        }

        private Entity GetTarget()
        {
            if (Kind == SceneKind.Bus) return progress.Step == 0 ? (Entity)witness :
                progress.Step == 1 ? (Entity)vehicle : subject;
            if (Kind == SceneKind.PoliceAccident) return progress.Step == 0 ? (Entity)subject :
                progress.Step == 1 ? (Entity)witness : vehicle;
            if (Kind == SceneKind.GasStation) return progress.Step == 0 ? (Entity)witness :
                progress.Step == 1 ? (Entity)vehicle : subject;
            if (Kind == SceneKind.Abandoned) return progress.Step == 0 ? (Entity)vehicle :
                progress.Step == 1 ? witness : subject;
            return vehicle;
        }

        private string TargetName()
        {
            if (Kind == SceneKind.Bus) return progress.Step == 0 ? "the bus driver" :
                progress.Step == 1 ? "the bus front door" : "the passenger";
            if (Kind == SceneKind.PoliceAccident) return progress.Step == 0 ? "the officer" :
                progress.Step == 1 ? "the other driver" : "the damaged police car";
            if (Kind == SceneKind.GasStation) return progress.Step == 0 ? "the employee" :
                progress.Step == 1 ? "the customer's vehicle" : "the customer";
            if (Kind == SceneKind.Abandoned) return progress.Step == 0 ? "the abandoned car" :
                progress.Step == 1 ? "the witness" : "the owner";
            return "the suspect's driver-side window";
        }

        private string ActionText()
        {
            if (Kind == SceneKind.Bus) return progress.Step == 0 ? "Ask the driver what happened" :
                progress.Step == 1 ? "Ask the passenger to step outside" : "Warn the passenger and finish";
            if (Kind == SceneKind.PoliceAccident) return progress.Step == 0 ? "Check the officer for injuries" :
                progress.Step == 1 ? "Take the other driver's statement" : "Record collision damage and finish";
            if (Kind == SceneKind.GasStation) return progress.Step == 0 ? "Interview the employee" :
                progress.Step == 1 ? "Check the license plate" : "Speak to the customer and finish";
            if (Kind == SceneKind.Abandoned) return progress.Step == 0 ? "Inspect the car and run the plate" :
                progress.Step == 1 ? "Interview the witness" : "Speak to the owner and finish";
            return progress.Step == 0 ? "Ask the suspect to exit" :
                progress.Step == 1 ? "Confirm the warrant with dispatch" : "Order the suspect out";
        }

        private void Advance()
        {
            int step = progress.Step;
            if (Kind == SceneKind.Barricaded && step == 2) { StartPursuit(); return; }
            if (Kind == SceneKind.Bus && step == 1)
            {
                // Clear the seated task and let GTA play the exit animation.
                NativeFunction.Natives.TASK_LEAVE_VEHICLE(subject, vehicle, 0);
                busExitStarted = Game.GameTime;
            }
            if (Kind == SceneKind.Bus && step == 2 && subject.IsInAnyVehicle(false))
            {
                Notify("Wait for the passenger to exit. Stand clear of the front door.");
                return;
            }

            string speaker, line;
            GetDialogue(step, out speaker, out line);
            Game.DisplaySubtitle("~b~" + speaker + ":~s~ " + line, 6500);
            dialogueUntil = Game.GameTime + 6500;
            HidePrompt();
            if (progress.Advance())
            {
                Notify("Statements recorded. Code four.");
                End();
            }
            else Main.Log(DispatchName + ": advanced to step " + progress.Step);
        }

        private void GetDialogue(int step, out string speaker, out string line)
        {
            speaker = "Dispatch";
            line = EvidenceText;
            switch (Kind)
            {
                case SceneKind.Abandoned:
                    if (step == 0) line = "Registration is clear. The engine is cold. Speak to the witness.";
                    if (step == 1) { speaker = "Witness"; line = "The owner went to get help. That is him over there."; }
                    if (step == 2) { speaker = "Owner"; line = "It broke down. Recovery is arranged. Thank you for checking."; }
                    break;
                case SceneKind.GasStation:
                    if (step == 0) { speaker = "Employee"; line = "That customer has been circling the pumps. Could you check on him?"; }
                    if (step == 1) line = "Plate checked: registered keeper matches, no stolen report.";
                    if (step == 2) { speaker = "Customer"; line = "I was looking for my wallet. I will pay inside. Sorry for the confusion."; }
                    break;
                case SceneKind.PoliceAccident:
                    if (step == 0) { speaker = "Officer"; line = InterviewText; }
                    if (step == 1) { speaker = "Driver"; line = "I thought the light had changed. I am not injured. Here are my details."; }
                    if (step == 2) line = "Both drivers are uninjured. Collision damage and statements recorded.";
                    break;
                case SceneKind.Bus:
                    if (step == 0) { speaker = "Bus driver"; line = "The passenger has his feet on the seats and will not listen. Please speak to him."; }
                    if (step == 1) { speaker = "Officer"; line = "Please step outside so we can talk. Keep the doorway clear."; }
                    if (step == 2) { speaker = "Passenger"; line = InterviewText; }
                    break;
                case SceneKind.Barricaded:
                    if (step == 0) { speaker = "Suspect"; line = InterviewText; }
                    break;
            }
        }

        private void StartPursuit()
        {
            if (pursuitStarted) return;
            if (!subject.IsInVehicle(vehicle, false))
            { Complete("The suspect left the vehicle. Handle the subject with LSPDFR."); return; }
            NativeFunction.Natives.CLEAR_PED_TASKS(subject);
            vehicle.IsPositionFrozen = false;
            vehicle.IsEngineOn = true;
            CalloutUtils.SetHazards(vehicle, false);
            subject.BlockPermanentEvents = false;
            subject.Tasks.CruiseWithVehicle(25f, VehicleDrivingFlags.Emergency);
            pursuit = Functions.CreatePursuit();
            pursuitStarted = true;
            Functions.AddPedToPursuit(pursuit, subject);
            Functions.SetPursuitIsActiveForPlayer(pursuit, true);
            DeleteBlip(ref targetBlip);
            HidePrompt();
            RequestBackup();
            Notify("Suspect fleeing in the vehicle! Pursuit active.");
            Main.Log(DispatchName + ": pursuit started.");
        }

        private Entity lastTarget;
        private void SetTarget(Entity target)
        {
            if (target == lastTarget) return;
            DeleteBlip(ref targetBlip);
            lastTarget = target;
            targetBlip = target.AttachBlip();
            targetBlip.Color = Color.Blue;
            // No route attached to a pedestrian on scene.
        }

        private void RequestBackup()
        {
            if (backupRequested) { Notify("One patrol unit has already been requested."); return; }
            backupRequested = true;
            Main.RequestSinglePursuitBackup(ready && vehicle != null && vehicle.Exists() ? vehicle.Position : scenePosition);
            Notify("One patrol unit requested.");
        }

        private static bool Pressed(Keys key, ref bool previous)
        {
            bool down = Game.IsKeyDown(key);
            bool edge = down && !previous;
            previous = down;
            return edge;
        }

        private void Prompt(string message)
        {
            if (message == lastPrompt && Game.GameTime < promptAt) return;
            lastPrompt = message;
            promptAt = Game.GameTime + 1800;
            Game.DisplayHelp(message, 2500); // milliseconds, not the old 1 ms flicker
            ownsHelp = true;
        }

        private void HidePrompt()
        {
            if (ownsHelp) Game.HideHelp();
            ownsHelp = false;
            lastPrompt = null;
        }

        private void Notify(string message)
        {
            Game.DisplayNotification("~b~" + SceneTitle + "~s~\n" + message);
        }

        private void Fail(string phase, Exception ex)
        {
            Main.Log("EXCEPTION in " + DispatchName + "." + phase + ": " + ex);
            if (accepted) Notify("Callout interrupted. Details were written to patrols_callouts_debug.log.");
            End();
        }

        private void Complete(string message) { Notify(message); End(); }

        private static void DeleteBlip(ref Blip blip)
        {
            if (blip != null && blip.Exists()) blip.Delete();
            blip = null;
        }

        private void Cleanup(Action action)
        {
            try { action(); } catch (Exception ex) { Main.Log(DispatchName + " cleanup: " + ex.Message); }
        }

        public override void End()
        {
            if (ended) return;
            ended = true;
            Cleanup(HidePrompt);
            Cleanup(() => DeleteBlip(ref destination));
            Cleanup(() => DeleteBlip(ref targetBlip));
            Cleanup(() => { if (pursuitStarted && Functions.IsPursuitStillRunning(pursuit)) Functions.ForceEndPursuit(pursuit); });
            Cleanup(() => ReleasePed(subject));
            Cleanup(() => ReleasePed(witness));
            Cleanup(() => { if (vehicle != null && vehicle.Exists()) vehicle.Dismiss(); });
            Cleanup(() => { if (otherVehicle != null && otherVehicle.Exists()) otherVehicle.Dismiss(); });
            base.End();
        }

        private static void ReleasePed(Ped ped)
        {
            if (ped == null || !ped.Exists() || Functions.IsPedArrested(ped)) return;
            NativeFunction.Natives.CLEAR_PED_TASKS(ped);
            ped.BlockPermanentEvents = false;
            ped.Dismiss();
        }
    }
}

