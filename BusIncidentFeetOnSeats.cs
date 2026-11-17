using LSPD_First_Response.Mod.Callouts;

namespace PatrolsCallouts.Callouts
{
    [CalloutInfo("BusIncidentFeetOnSeats", CalloutProbability.Medium)]
    public sealed class BusIncidentFeetOnSeats : ScenarioCalloutBase
    {
        protected override SceneKind Kind { get { return SceneKind.Bus; } }
        protected override string DispatchName { get { return "Bus passenger disturbance"; } }
        protected override string DispatchAdvisory { get { return "A passenger is refusing the driver's request to remove his feet from the seats."; } }
        protected override string SceneTitle { get { return "BUS INCIDENT"; } }
        protected override string InitialBriefing { get { return "Meet the driver outside the bus, ask the passenger out, then issue a warning."; } }
        protected override string InterviewText { get { return "Understood, officer. I will keep my feet off the seats."; } }
        protected override string EvidenceText { get { return "The passenger has been warned; no damage or injuries were reported."; } }
        protected override string VehicleModel { get { return "BUS"; } }
        protected override bool HasWitness { get { return true; } }
        protected override bool EscalatesToPursuit { get { return false; } }
    }
}

