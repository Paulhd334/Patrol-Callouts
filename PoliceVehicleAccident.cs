using LSPD_First_Response.Mod.Callouts;

namespace PatrolsCallouts.Callouts
{
    [CalloutInfo("PoliceVehicleAccident", CalloutProbability.Medium)]
    public sealed class PoliceVehicleAccident : ScenarioCalloutBase
    {
        protected override SceneKind Kind { get { return SceneKind.PoliceAccident; } }
        protected override string DispatchName { get { return "Police vehicle collision"; } }
        protected override string DispatchAdvisory { get { return "A patrol car and a civilian vehicle have collided."; } }
        protected override string SceneTitle { get { return "OFFICER ACCIDENT"; } }
        protected override string InitialBriefing { get { return "Check the officer, interview the other driver, then inspect the damage."; } }
        protected override string InterviewText { get { return "I am okay. The other driver entered the intersection against the light."; } }
        protected override string EvidenceText { get { return "Both drivers are uninjured. Statements and damage have been recorded."; } }
        protected override string VehicleModel { get { return "POLICE"; } }
        protected override bool HasWitness { get { return true; } }
        protected override bool EscalatesToPursuit { get { return false; } }
    }
}

