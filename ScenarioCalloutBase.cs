using System;
using System.Drawing;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;
using PatrolsCallouts.Utils;

namespace PatrolsCallouts.Callouts
{
    // Shared, deliberately conservative lifecycle for the smaller Patrols
    // Callouts. Each derived callout supplies its scenario text and entities,
    // while this class owns spawning, interaction, pursuit and cleanup.
    public abstract class ScenarioCalloutBase : Callout
    {
        protected abstract string DispatchName { get; }
        protected abstract string DispatchAdvisory { get; }
        protected abstract string SceneTitle { get; }
        protected abstract string InitialBriefing { get; }
        protected abstract string InterviewText { get; }
        protected abstract string EvidenceText { get; }
        protected virtual string VehicleModel { get { return null; } }
        protected virtual bool HasWitness { get { return false; } }
        protected virtual bool EscalatesToPursuit { get { return false; } }

        private Vector3 scenePosition;
        private Vehicle sceneVehicle;
        private Ped subject;
        private Ped witness;
        private Blip areaBlip;
        private Blip subjectBlip;
        private CalloutMenu menu;
        private LHandle pursuit;
        private bool sceneReady;
        private bool ended;
        private bool eWasDown;
        private bool backupRequested;

        public override bool OnBeforeCalloutDisplayed()
        {
            try
            {
                scenePosition = World.GetNextPositionOnStreet(
                    Game.LocalPlayer.Character.Position.Around2D(350f, 900f));
                if (scenePosition == Vector3.Zero) return false;

                CalloutMessage = DispatchName;
                CalloutAdvisory = DispatchAdvisory;
                CalloutPosition = scenePosition;
                ShowCalloutAreaBlipBeforeAccepting(scenePosition, 75f);
                AddMinimumDistanceCheck(200f, scenePosition);
                return base.OnBeforeCalloutDisplayed();
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION in " + DispatchName + ".OnBeforeCalloutDisplayed: " + ex);
                return false;
            }
        }

        public override bool OnCalloutAccepted()
        {
            try
            {
                areaBlip = new Blip(scenePosition, 75f);
                areaBlip.Color = Color.Yellow;
                areaBlip.Alpha = 0.5f;
                areaBlip.IsRouteEnabled = true;

                if (!string.IsNullOrEmpty(VehicleModel))
                {
                    sceneVehicle = new Vehicle(VehicleModel, scenePosition, 0f);
                    if (sceneVehicle == null || !sceneVehicle.Exists()) return false;
                    sceneVehicle.MakePersistent();
                    sceneVehicle.IsPositionFrozen = true;
                    NativeFunction.CallByName<uint>("REQUEST_COLLISION_AT_COORD", scenePosition.X, scenePosition.Y, scenePosition.Z);
                }

                Vector3 subjectPosition = sceneVehicle != null && sceneVehicle.Exists()
                    ? sceneVehicle.GetOffsetPosition(new Vector3(2.5f, 1.5f, 0f))
                    : scenePosition.Around(2f);
                subject = new Ped(subjectPosition);
                if (subject == null || !subject.Exists()) return false;
                subject.MakePersistent();
                subject.BlockPermanentEvents = true;
                subject.IsPositionFrozen = true;

                if (HasWitness)
                {
                    witness = new Ped(scenePosition.Around(5f));
                    if (witness != null && witness.Exists())
                    {
                        witness.MakePersistent();
                        witness.BlockPermanentEvents = true;
                        witness.IsPositionFrozen = true;
                    }
                }

                menu = new CalloutMenu();
                menu.SetItems(SceneTitle, DispatchName,
                    "Speak with the involved person",
                    "Inspect the scene",
                    "Request one patrol unit",
                    EscalatesToPursuit ? "Attempt a detention" : "Complete the callout");

                Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                    "Dispatch", "~b~" + DispatchName, InitialBriefing);
                return base.OnCalloutAccepted();
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION in " + DispatchName + ".OnCalloutAccepted: " + ex);
                End();
                return false;
            }
        }

        public override void Process()
        {
            base.Process();
            if (ended || subject == null || !subject.Exists()) { if (!ended) End(); return; }

            try
            {
                Ped player = Game.LocalPlayer.Character;
                if (!sceneReady)
                {
                    if (player.DistanceTo(scenePosition) >= 110f) return;
                    sceneReady = true;
                    RemoveAreaBlip();
                    if (sceneVehicle != null && sceneVehicle.Exists()) sceneVehicle.IsPositionFrozen = false;
                    subject.IsPositionFrozen = false;
                    if (witness != null && witness.Exists()) witness.IsPositionFrozen = false;
                    CalloutUtils.StartScenario(subject, "WORLD_HUMAN_STAND_IMPATIENT");
                    if (witness != null && witness.Exists()) CalloutUtils.StartScenario(witness, "WORLD_HUMAN_STAND_MOBILE");
                    subjectBlip = subject.AttachBlip();
                    subjectBlip.Color = Color.Yellow;
                    subjectBlip.IsRouteEnabled = true;
                    Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                        "Dispatch", "~b~Scene reached", "Speak with the involved person to continue.");
                }

                if (pursuit != null)
                {
                    if (!Functions.IsPursuitStillRunning(pursuit)) Complete("Pursuit concluded. Scene is code four.");
                    return;
                }

                bool nearSubject = player.DistanceTo(subject) < (menu.IsOpen ? 7f : 5f) && !player.IsInAnyVehicle(false);
                if (!menu.IsOpen && nearSubject)
                {
                    Game.DisplayHelp("Press ~INPUT_CONTEXT~ to interact", 1);
                    bool down = Game.IsKeyDown(System.Windows.Forms.Keys.E);
                    if (down && !eWasDown) menu.Open();
                    eWasDown = down;
                }
                else if (!Game.IsKeyDown(System.Windows.Forms.Keys.E)) eWasDown = false;

                if (!menu.IsOpen) return;
                if (!nearSubject) { menu.Close(); return; }

                int selected = menu.Update();
                if (selected >= 0) HandleMenuSelection(selected);
            }
            catch (Exception ex)
            {
                Main.Log("EXCEPTION in " + DispatchName + ".Process: " + ex);
                Complete("The callout ended safely after an error.");
            }
        }

        private void HandleMenuSelection(int selected)
        {
            switch (selected)
            {
                case 0:
                    menu.MarkDone(0);
                    CalloutUtils.Say("Civilian", InterviewText);
                    menu.SetFeedback("Statement recorded.");
                    break;
                case 1:
                    menu.MarkDone(1);
                    menu.SetFeedback(EvidenceText);
                    break;
                case 2:
                    if (!backupRequested)
                    {
                        backupRequested = true;
                        Main.RequestSinglePursuitBackup(scenePosition);
                        menu.MarkDone(2);
                        menu.SetFeedback("One patrol unit has been requested.");
                    }
                    else menu.SetFeedback("A patrol unit is already responding.");
                    break;
                case 3:
                    menu.Close();
                    if (EscalatesToPursuit) StartPursuit();
                    else Complete("Callout completed. Dispatch is code four.");
                    break;
            }
        }

        private void StartPursuit()
        {
            if (sceneVehicle != null && sceneVehicle.Exists() && !subject.IsInVehicle(sceneVehicle, false))
                subject.WarpIntoVehicle(sceneVehicle, -1);
            pursuit = Functions.CreatePursuit();
            Functions.AddPedToPursuit(pursuit, subject);
            Functions.SetPursuitIsActiveForPlayer(pursuit, true);
            if (!backupRequested) { backupRequested = true; Main.RequestSinglePursuitBackup(scenePosition); }
            if (subjectBlip != null && subjectBlip.Exists()) subjectBlip.Delete();
            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept",
                "Dispatch", "~r~Suspect fleeing", "Pursuit initiated. One patrol unit is responding.");
        }

        protected void Complete(string message)
        {
            if (ended) return;
            Game.DisplayNotification("web_lossantospolicedept", "web_lossantospolicedept", "Dispatch", "Callout completed", message);
            End();
        }

        private void RemoveAreaBlip()
        {
            if (areaBlip != null && areaBlip.Exists()) areaBlip.Delete();
            areaBlip = null;
        }

        public override void End()
        {
            if (!ended)
            {
                ended = true;
                if (menu != null) menu.Dispose();
                RemoveAreaBlip();
                if (subjectBlip != null && subjectBlip.Exists()) subjectBlip.Delete();
                if (sceneVehicle != null && sceneVehicle.Exists()) { sceneVehicle.IsPositionFrozen = false; sceneVehicle.Dismiss(); }
                if (subject != null && subject.Exists()) { subject.IsPositionFrozen = false; subject.Dismiss(); }
                if (witness != null && witness.Exists()) { witness.IsPositionFrozen = false; witness.Dismiss(); }
            }
            base.End();
        }
    }
}
