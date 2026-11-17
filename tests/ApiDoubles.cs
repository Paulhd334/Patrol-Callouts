// Deterministic API doubles: exercise the real scenario lifecycle without GTA.
// These do not simulate GTA physics, native validity, rendering or LSPDFR AI.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace Rage
{
    public struct Vector3
    {
        public float X, Y, Z;
        public Vector3(float x,float y,float z) { X=x; Y=y; Z=z; }
        public static Vector3 Zero { get { return new Vector3(); } }
        public float DistanceTo(Vector3 b) { return (float)Math.Sqrt((X-b.X)*(X-b.X)+(Y-b.Y)*(Y-b.Y)+(Z-b.Z)*(Z-b.Z)); }
        public Vector3 Around2D(float min,float max) { return this+new Vector3(min,0,0); }
        public static Vector3 operator +(Vector3 a,Vector3 b) { return new Vector3(a.X+b.X,a.Y+b.Y,a.Z+b.Z); }
        public static bool operator ==(Vector3 a,Vector3 b) { return a.X==b.X && a.Y==b.Y && a.Z==b.Z; }
        public static bool operator !=(Vector3 a,Vector3 b) { return !(a==b); }
        public override bool Equals(object o) { return o is Vector3 && this==(Vector3)o; }
        public override int GetHashCode() { return X.GetHashCode(); }
    }
    public class Entity
    {
        public static List<Entity> All=new List<Entity>();
        public Vector3 Position;
        public bool Alive=true, Dismissed;
        public bool IsPositionFrozen, BlockPermanentEvents;
        public string Model;
        public Entity() { All.Add(this); }
        public bool Exists() { return Alive; }
        public void MakePersistent() {}
        public float DistanceTo(Entity b) { return DistanceTo(b.Position); }
        public float DistanceTo(Vector3 b) { return Position.DistanceTo(b); }
        public void Dismiss() { Dismissed=true; }
        public Blip AttachBlip() { return new Blip(Position); }
    }
    public class Vehicle: Entity
    {
        public bool IsEngineOn;
        public int PassengerCapacity=8;
        public Vehicle(string model,Vector3 p,float h) { Model=model; Position=p; }
    }
    public class Ped: Entity
    {
        public bool IsDead, Arrested, Cop;
        public Vehicle InVehicle;
        public TaskInvoker Tasks=new TaskInvoker();
        public Ped(string m,Vector3 p,float h) { Model=m; Position=p; }
        public bool IsInAnyVehicle(bool b) { return InVehicle!=null; }
        public bool IsInVehicle(Vehicle v,bool b) { return InVehicle==v; }
        public void WarpIntoVehicle(Vehicle v,int seat) { InVehicle=v; Position=v.Position; }
    }
    public class TaskInvoker
    {
        public bool Driving;
        public void CruiseWithVehicle(float speed,VehicleDrivingFlags flags) { Driving=true; }
    }
    public enum VehicleDrivingFlags { Emergency }
    public class Blip
    {
        public static List<Blip> All=new List<Blip>();
        public bool Alive=true, IsRouteEnabled;
        public Color Color;
        public Blip(Vector3 p) { All.Add(this); }
        public bool Exists() { return Alive; }
        public void Delete() { Alive=false; }
    }
    public class Player { public Ped Character; }
    public static class Game
    {
        public static Player LocalPlayer=new Player();
        public static uint GameTime=100;
        public static HashSet<Keys> KeysDown=new HashSet<Keys>();
        public static int HelpDuration, HelpCount;
        public static string Help;
        public static bool IsKeyDown(Keys k) { return KeysDown.Contains(k); }
        public static void DisplayHelp(string s,int duration) { Help=s; HelpDuration=duration; HelpCount++; }
        public static void HideHelp() { Help=null; }
        public static void DisplayNotification(string s) {}
        public static void DisplaySubtitle(string s,int duration) {}
    }
    public static class World
    {
        public static bool GroundAvailable=true;
        public static float GroundHeight=10f;
        public static float? GetGroundZ(Vector3 p,bool water,bool any) { return GroundAvailable ? (float?)GroundHeight : null; }
    }
}
namespace Rage.Native
{
    using Rage;
    public static class NativeFunction { public static FakeNatives Natives=new FakeNatives(); }
    public class FakeNatives
    {
        public bool Occupied, Safe=true, FailPlacement, BlockBusExit;
        public int DamageCalls;
        public bool GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<T>(float x,float y,float z,out Vector3 p,out float h,int a,float b,int c)
        { p=new Vector3(x,y,10); h=0; return true; }
        public void REQUEST_COLLISION_AT_COORD(float x,float y,float z) {}
        public bool IS_ANY_VEHICLE_NEAR_POINT<T>(float x,float y,float z,float radius) { return Occupied; }
        public bool GET_SAFE_COORD_FOR_PED<T>(float x,float y,float z,bool ground,out Vector3 p,int flags)
        { p=new Vector3(x,y,World.GroundHeight); return Safe; }
        public bool SET_VEHICLE_ON_GROUND_PROPERLY<T>(Vehicle v) { return !FailPlacement; }
        public void TASK_STAND_STILL(Ped p,int ms) {}
        public void SET_VEHICLE_DOOR_OPEN(Vehicle v,int door,bool a,bool b) {}
        public void SET_VEHICLE_DAMAGE(Vehicle v,float x,float y,float z,float d,float r,bool f) { DamageCalls++; }
        public void TASK_LEAVE_VEHICLE(Ped p,Vehicle v,int f) { if (!BlockBusExit) { p.InVehicle=null; p.Position=v.Position+new Vector3(4,0,0); } }
        public void CLEAR_PED_TASKS(Ped p) {}
        public void CLEAR_PED_TASKS_IMMEDIATELY(Ped p) { p.InVehicle=null; }
    }
}
namespace LSPD_First_Response.Mod.Callouts
{
    using Rage;
    public enum CalloutProbability { Medium }
    public class CalloutInfoAttribute: Attribute { public CalloutInfoAttribute(string n,CalloutProbability p) {} }
    public class Callout
    {
        public string CalloutMessage, CalloutAdvisory;
        public Vector3 CalloutPosition;
        public int EndCount;
        public void ShowCalloutAreaBlipBeforeAccepting(Vector3 p,float r) {}
        public void AddMinimumDistanceCheck(float d,Vector3 p) {}
        public virtual bool OnBeforeCalloutDisplayed() { return true; }
        public virtual bool OnCalloutAccepted() { return false; }
        public virtual void Process() {}
        public virtual void End() { EndCount++; }
    }
}
namespace LSPD_First_Response.Mod.API
{
    using Rage;
    public class LHandle { public bool Running=true; }
    public static class Functions
    {
        public static int Pursuits, Added, Activated;
        public static LHandle Last;
        public static LHandle CreatePursuit() { Pursuits++; return Last=new LHandle(); }
        public static void AddPedToPursuit(LHandle h,Ped p) { Added++; }
        public static void SetPursuitIsActiveForPlayer(LHandle h,bool b) { Activated++; }
        public static bool IsPursuitStillRunning(LHandle h) { return h.Running; }
        public static void ForceEndPursuit(LHandle h) { h.Running=false; }
        public static bool IsPedArrested(Ped p) { return p.Arrested; }
        public static void SetPedAsCop(Ped p) { p.Cop=true; }
    }
}
namespace PatrolsCallouts
{
    public static class Main
    {
        public static int Backup;
        public static List<string> Logs=new List<string>();
        public static void Log(string s) { Logs.Add(s); }
        public static void RequestSinglePursuitBackup(Rage.Vector3 p) { Backup++; }
    }
}
namespace PatrolsCallouts.Utils
{
    public static class CalloutUtils { public static void SetHazards(Rage.Vehicle v,bool b) {} }
}

