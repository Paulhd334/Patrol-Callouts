using LSPD_First_Response.Mod.Callouts;

namespace PatrolsCallouts.Callouts
{
    [CalloutInfo("SuspiciousGasStation", CalloutProbability.Medium)]
    public sealed class SuspiciousGasStation : ScenarioCalloutBase
    {
        protected override SceneKind Kind { get { return SceneKind.GasStation; } }
        protected override string DispatchName { get { return "Suspicious gas station activity"; } }
        protected override string DispatchAdvisory { get { return "An employee needs assistance at the gas station."; } }
        protected override string SceneTitle { get { return "GAS STATION CHECK"; } }
        protected override string InitialBriefing { get { return "Meet the employee at the blue marker, then check the customer."; } }
        protected override string InterviewText { get { return "That customer has been circling the pumps."; } }
        protected override string EvidenceText { get { return "The plate is clear and the customer matches the registered keeper."; } }
        protected override string VehicleModel { get { return "STANIER"; } }
        protected override bool HasWitness { get { return true; } }
        protected override bool EscalatesToPursuit { get { return false; } }
    }
}

