using LSPD_First_Response.Mod.Callouts;

namespace PatrolsCallouts.Callouts
{
    [CalloutInfo("BarricadedSuspect", CalloutProbability.Medium)]
    public sealed class BarricadedSuspect : ScenarioCalloutBase
    {
        protected override SceneKind Kind { get { return SceneKind.Barricaded; } }
        protected override string DispatchName { get { return "Barricaded suspect in vehicle"; } }
        protected override string DispatchAdvisory { get { return "A wanted driver is refusing to leave a parked SUV."; } }
        protected override string SceneTitle { get { return "BARRICADED SUSPECT"; } }
        protected override string InitialBriefing { get { return "Approach the driver-side window. Establish contact and confirm the warrant."; } }
        protected override string InterviewText { get { return "I am not going back to jail! Leave me alone!"; } }
        protected override string EvidenceText { get { return "Dispatch confirms an outstanding warrant. Order the driver out."; } }
        protected override string VehicleModel { get { return "GRANGER"; } }
        protected override bool HasWitness { get { return false; } }
        protected override bool EscalatesToPursuit { get { return true; } }
    }
}

