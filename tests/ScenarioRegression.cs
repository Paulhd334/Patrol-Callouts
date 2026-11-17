using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Rage;
using Rage.Native;
using LSPD_First_Response.Mod.API;
using PatrolsCallouts.Callouts;

internal static class Regression
{
    private static int passed;
    private static void Assert(bool condition,string message) { if (!condition) throw new Exception(message); }
    private static T Field<T>(object o,string name) { return (T)typeof(ScenarioCalloutBase).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o); }
    private static void Reset()
    {
        Entity.All.Clear(); Blip.All.Clear();
        Game.LocalPlayer.Character=new Ped("player",new Vector3(0,0,10),0);
        Game.GameTime=100; Game.KeysDown.Clear(); Game.Help=null; Game.HelpCount=0;
        World.GroundAvailable=true; World.GroundHeight=10f; NativeFunction.Natives=new FakeNatives();
        Functions.Pursuits=Functions.Added=Functions.Activated=0; Functions.Last=null;
        PatrolsCallouts.Main.Backup=0; PatrolsCallouts.Main.Logs.Clear();
    }
    private static T Offer<T>() where T:ScenarioCalloutBase,new()
    {
        T c=new T(); Assert(c.OnBeforeCalloutDisplayed(),"offer rejected");
        World.GroundHeight=c.CalloutPosition.Z;
        c.Process(); Assert(c.EndCount==0,"Process terminated an unaccepted offer");
        Assert(c.OnCalloutAccepted(),"accept rejected");
        c.Process(); Assert(Entity.All.Count==1,"spawned before arriving");
        return c;
    }
    private static void Arrive(ScenarioCalloutBase c)
    {
        Game.LocalPlayer.Character.Position=c.CalloutPosition;
        c.Process();
        Assert(Field<bool>(c,"ready"),"scene did not initialize: "+string.Join(";",PatrolsCallouts.Main.Logs));
    }
    private static void Interact(ScenarioCalloutBase c)
    {
        Game.GameTime+=7000;
        Game.KeysDown.Clear();
        var target=(Entity)typeof(ScenarioCalloutBase).GetMethod("GetTarget",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);
        Game.LocalPlayer.Character.Position=target.Position;
        c.Process(); Game.KeysDown.Add(Keys.Y); c.Process(); Game.KeysDown.Clear();
    }
    private static void Test(string name,Action action)
    {
        Reset(); action(); passed++; Console.WriteLine("PASS "+name);
    }
    public static int Main()
    {
        try
        {
            Test("Pending offer and distant acceptance do not abort or spawn",()=>{ var c=Offer<BusIncidentFeetOnSeats>(); c.End(); });
            Test("Ground is required; arrival marker disappears while waiting",()=>{
                var c=Offer<PoliceVehicleAccident>(); World.GroundAvailable=false;
                Game.LocalPlayer.Character.Position=c.CalloutPosition; c.Process();
                Assert(Entity.All.Count==1,"spawned without ground");
                Assert(!Blip.All.Any(b=>b.Alive),"arrival marker remains");
                Game.GameTime+=600; World.GroundAvailable=true; c.Process();
                Assert(Field<bool>(c,"ready"),"did not recover when ground loaded"); c.End();
            });
            Test("Invalid navigation points time out without floating peds",()=>{
                var c=Offer<AbandonedVehicleCountryside>(); NativeFunction.Natives.Safe=false;
                Game.LocalPlayer.Character.Position=c.CalloutPosition; c.Process();
                Game.GameTime+=16000; c.Process();
                Assert(c.EndCount==1 && Entity.All.Count==1,"unsafe scene spawned or did not end");
            });
            Test("Occupied spawn does not overlap traffic",()=>{
                var c=Offer<BusIncidentFeetOnSeats>(); NativeFunction.Natives.Occupied=true;
                Game.LocalPlayer.Character.Position=c.CalloutPosition; c.Process();
                Assert(Entity.All.Count==1,"spawned into occupied road"); c.End();
            });
            Test("Ground-placement failure releases partially created entities",()=>{
                var c=Offer<PoliceVehicleAccident>(); NativeFunction.Natives.FailPlacement=true;
                Game.LocalPlayer.Character.Position=c.CalloutPosition; c.Process();
                Assert(c.EndCount==1,"spawn error did not end");
                Assert(Entity.All.Skip(1).All(e=>e.Dismissed),"entity leaked");
            });
            Test("Theft starts vehicle pursuit at proximity with exactly one backup",()=>{
                var c=Offer<OrganizedVehicleTheft>();
                Game.LocalPlayer.Character.Position=c.CalloutPosition+new Vector3(100,0,0); c.Process();
                Assert(Functions.Pursuits==0,"pursuit began too early");
                Game.LocalPlayer.Character.Position=c.CalloutPosition;
                c.Process(); c.Process();
                var suspect=Field<Ped>(c,"subject");
                Assert(Functions.Pursuits==1 && Functions.Added==1 && Functions.Activated==1,"missing or duplicated pursuit");
                Assert(suspect.InVehicle!=null && suspect.Tasks.Driving,"suspect not driving");
                Assert(PatrolsCallouts.Main.Backup==1,"backup not requested exactly once");
                Functions.Last.Running=false; c.Process(); Assert(c.EndCount==1,"pursuit did not conclude");
            });
            Test("Barricaded driver stays inside until ordered out, then flees",()=>{
                var c=Offer<BarricadedSuspect>(); Arrive(c);
                Assert(Field<Ped>(c,"subject").InVehicle!=null,"driver spawned outside");
                Interact(c); Interact(c); Assert(Functions.Pursuits==0,"premature pursuit");
                Interact(c); Assert(Functions.Pursuits==1,"no pursuit after order"); c.End();
                Assert(!Functions.Last.Running,"pursuit leaked on cancellation");
            });
            Test("Police collision has two damaged cars and an officer; all three steps finish",()=>{
                var c=Offer<PoliceVehicleAccident>(); Arrive(c);
                Assert(Entity.All.OfType<Vehicle>().Count()==2,"missing second car");
                Assert(Field<Ped>(c,"subject").Cop,"subject not an officer");
                Assert(NativeFunction.Natives.DamageCalls==2,"damage not applied");
                Interact(c); Interact(c); Assert(c.EndCount==0,"finished before inspecting");
                Interact(c); Assert(c.EndCount==1,"did not finish");
                Assert(Entity.All.Skip(1).All(e=>e.Dismissed),"entities leaked");
            });
            Test("Bus driver, seated passenger, exit and warning complete in order",()=>{
                var c=Offer<BusIncidentFeetOnSeats>(); Arrive(c);
                Assert(Field<Ped>(c,"subject").InVehicle!=null,"passenger not seated");
                Assert(Field<Ped>(c,"witness").InVehicle==null,"driver contact inaccessible");
                Interact(c); Interact(c); Assert(Field<Ped>(c,"subject").InVehicle==null,"passenger did not exit");
                Interact(c); Assert(c.EndCount==1,"bus cannot finish");
            });
            Test("Blocked bus exit recovers on safe ground after timeout",()=>{
                var c=Offer<BusIncidentFeetOnSeats>(); Arrive(c); NativeFunction.Natives.BlockBusExit=true;
                Interact(c); Interact(c); Interact(c); Assert(c.EndCount==0,"finished while passenger seated");
                Game.GameTime+=13000; c.Process();
                Assert(Field<Ped>(c,"subject").InVehicle==null,"stuck exit not recovered");
                Interact(c); Assert(c.EndCount==1,"warning did not finish");
            });
            Test("Held interaction key cannot skip steps; help persists in milliseconds",()=>{
                var c=Offer<SuspiciousGasStation>(); Arrive(c);
                Game.LocalPlayer.Character.Position=Field<Ped>(c,"witness").Position;
                Game.KeysDown.Add(Keys.Y); c.Process(); Game.GameTime+=7000; c.Process();
                Assert(Field<ScenarioProgress>(c,"progress").Step==1,"held key skipped steps");
                Assert(Game.HelpDuration>=2000,"help flickers");
                c.End(); Assert(Game.Help==null,"help leaked");
            });
            Test("Abandoned vehicle investigation completes after all contacts",()=>{
                var c=Offer<AbandonedVehicleCountryside>(); Arrive(c);
                Assert(Entity.All.OfType<Ped>().Skip(1).All(p=>Math.Abs(p.Position.Z-10.1f)<0.01f),"ped floating");
                Interact(c); Interact(c); Interact(c); Assert(c.EndCount==1,"investigation cannot finish");
            });
            Test("Backup and cancel keys are edge-triggered; cleanup is idempotent",()=>{
                var c=Offer<PoliceVehicleAccident>(); Arrive(c);
                Game.KeysDown.Add(Keys.B); c.Process(); c.Process();
                Game.KeysDown.Clear(); c.Process(); Game.KeysDown.Add(Keys.B); c.Process();
                Assert(PatrolsCallouts.Main.Backup==1,"duplicate backup");
                Game.KeysDown.Add(Keys.End); c.Process(); c.End();
                Assert(c.EndCount==1 && !Blip.All.Any(b=>b.Alive),"double end or blip leak");
            });
            Console.WriteLine(passed+" regression scenarios passed (API doubles, not in-game tests).");
            return 0;
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

