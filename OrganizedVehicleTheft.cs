using LSPD_First_Response.Mod.Callouts;

namespace PatrolsCallouts.Callouts
{
    [CalloutInfo("OrganizedVehicleTheft", CalloutProbability.Medium)]
    public sealed class OrganizedVehicleTheft : ScenarioCalloutBase
    {
        protected override SceneKind Kind { get { return SceneKind.Theft; } }
        protected override string DispatchName { get { return "Organized vehicle theft"; } }
        protected override string DispatchAdvisory { get { return "A stolen vehicle has been located. The driver may flee."; } }
        protected override string SceneTitle { get { return "STOLEN VEHICLE"; } }
        protected override string InitialBriefing { get { return "Approach the marked vehicle. Be prepared for a vehicle pursuit."; } }
        protected override string InterviewText { get { return "I was paid to move this car."; } }
        protected override string EvidenceText { get { return "The VIN is flagged as stolen."; } }
        protected override string VehicleModel { get { return "FUGITIVE"; } }
        protected override bool HasWitness { get { return false; } }
        protected override bool EscalatesToPursuit { get { return true; } }
    }
}

