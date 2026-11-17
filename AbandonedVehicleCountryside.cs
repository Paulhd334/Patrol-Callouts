using LSPD_First_Response.Mod.Callouts;

namespace PatrolsCallouts.Callouts
{
    [CalloutInfo("AbandonedVehicleCountryside", CalloutProbability.Medium)]
    public sealed class AbandonedVehicleCountryside : ScenarioCalloutBase
    {
        protected override SceneKind Kind { get { return SceneKind.Abandoned; } }
        protected override string DispatchName { get { return "Abandoned rural vehicle"; } }
        protected override string DispatchAdvisory { get { return "An unattended car was reported on a rural road."; } }
        protected override string SceneTitle { get { return "ABANDONED VEHICLE"; } }
        protected override string InitialBriefing { get { return "Inspect the car, interview the witness, then locate the owner."; } }
        protected override string InterviewText { get { return "I saw the owner looking for roadside assistance."; } }
        protected override string EvidenceText { get { return "The vehicle is registered and has not been reported stolen."; } }
        protected override string VehicleModel { get { return "ASEA"; } }
        protected override bool HasWitness { get { return true; } }
        protected override bool EscalatesToPursuit { get { return false; } }
    }
}

