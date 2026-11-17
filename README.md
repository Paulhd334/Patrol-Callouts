# Patrols Callouts

![Version](https://img.shields.io/badge/version-1.1.1-blue)
![Platform](https://img.shields.io/badge/platform-GTA_V_PC-lightgrey)

An LSPDFR / RAGE Plugin Hook plugin by **UNWARE STUDIO**.
Version 1.1.1 keeps nine callouts and reworks the six recent scenarios following player testing.
All new prompts and dialogue use US English.

## Install

1. Close GTA V and RAGE Plugin Hook.
2. Copy `PatrolsCallouts.dll` to `GTA V/plugins/LSPDFR/`, replacing the previous copy.
3. Start GTA through RAGE Plugin Hook and go on duty in LSPDFR.
4. Run one command below in the RPH console. Accept the dispatch if prompted.
5. Finish or cancel that callout before starting another.

Only install one copy of this plugin. Sources and test files are not game plugins.

## Available callouts

| Console command | Current behavior |
| --- | --- |
| `StartCallout BrokenDownVehicle` | Original highway breakdown; unchanged in 1.1.1. |
| `StartCallout SuspectTruckHighway` | Original suspicious truck; unchanged in 1.1.1. |
| `StartCallout HitAndRunAccident` | Previously repaired hit-and-run; unchanged in 1.1.1. |
| `StartCallout AbandonedVehicleCountryside` | Rural road: inspect the car, interview the witness, contact the owner. |
| `StartCallout SuspiciousGasStation` | Fixed gas-station locations: employee statement, plate check, customer explanation. |
| `StartCallout BarricadedSuspect` | Driver remains inside an SUV. Establish contact, confirm the warrant, order the driver out; the driver flees. |
| `StartCallout OrganizedVehicleTheft` | Driver starts in a stolen car. Approaching within 60 meters automatically starts a vehicle pursuit and requests one patrol unit. |
| `StartCallout PoliceVehicleAccident` | Two damaged vehicles, a uniformed officer and a civilian driver; check injuries, record the driver's statement and inspect damage. |
| `StartCallout BusIncidentFeetOnSeats` | Speak to the driver outside the bus, ask the seated passenger to get out, then warn the passenger. |

The original three callouts retain their existing controls. For the six reworked callouts:

| Key / marker | Action |
| --- | --- |
| Yellow GPS point | Travel to the scene. It disappears on arrival. |
| Blue entity marker | The person or vehicle required for the current step. |
| Y | Perform the displayed action, on foot and close to the marked target. Release between actions. |
| B | Request one patrol unit, at most once per callout. |
| End | Cancel the callout and clean up its owned markers and entities. |

There is no scrolling interaction menu in the six reworked scenarios.
Dialogue stays on screen before the next step becomes available.
The bus interaction is performed from the front door; boarding the bus is not required.
If a replacement bus model blocks the passenger's exit, the callout attempts a validated ground placement after 12 seconds.

## Fixes in 1.1.1

- Pending offers are no longer ended because entities have not yet spawned.
- Pedestrian positions are validated against the navigation mesh and ground height.
- Scene creation waits until the player is close enough for collision to load.
- A blocked or unavailable scene times out with a message instead of spawning floating pedestrians.
- Street vehicles use the road heading and ground placement; gas stations use fixed forecourts.
- Help text lasts 2.5 seconds and is refreshed at intervals, replacing the previous 1-millisecond display.
- One interaction per key press; dialogue is not overwritten every frame by menu feedback.
- Vehicle theft starts a pursuit on approach, including while the player is in a patrol car.
- Police accident and bus scenes have their own entities and ordered interactions.
- Canceling an active pursuit ends that pursuit and releases the callout's owned entities.
- The three original scenario files and their shared menu/utilities are unchanged.

## Removed at the player's request

The following six untested scenarios are no longer compiled or registered:
`WitnessWhoDisappears`, `SuspiciousObjectFound`, `DisturbingDisappearance`,
`MysteriousCase`, `PersonInDistress`, and `SuspiciousAbandonedVehicle`.
Their previous source remains recoverable through Git history.

## Validation and remaining in-game checks

Release x64 compiles against the locally installed LSPDFR/RPH assemblies.
The automated regression harness exercises the actual six scenario classes with deterministic API doubles:

- lifecycle before acceptance, delayed ground loading and blocked spawn positions;
- cleanup after partial spawn failure, cancellation and pursuit completion;
- automatic vehicle pursuit with exactly one backup request;
- barricaded driver, accident statements, bus exit and blocked-door recovery;
- input edge detection, help duration and ordered completion.

Run it with:

~~~powershell
.\tests\Run-Regression.ps1
~~~

These are logic tests, **not GTA V playtests**. They cannot validate native execution, physical placement,
AI driving, vehicle replacements or the final appearance of the HUD. Retest all six reworked scenarios
in game, including canceling each one and starting another. The 1.1.1 in-game pass is still pending.

If a scene fails, provide the callout name, the last displayed instruction, `RagePluginHook.log`
and `C:/Temp/patrols_callouts_debug.log`. The plugin logs scene readiness, interaction steps,
pursuit startup and exceptions.

## Build

Requirements: Windows, .NET Framework 4.8 targeting pack, .NET SDK, GTA V with LSPDFR and RAGE Plugin Hook.

~~~powershell
dotnet build "Patrol Callout.csproj" -c Release -p:Platform=x64 -p:GtaVDirectory="C:\Program Files\Epic Games\GTAV"
~~~

The old nested `Patrol Callout/` project is retained for history and excluded from compilation.
Build the root project. Test doubles under `tests/` are also excluded from the game DLL.

## API references

- [LSPDFR API: stolen vehicle example](https://github.com/LMSDev/LSPDFR-API/blob/master/API%20Examples/Albo1125-LSPDFR-API-Guide-Final-Project/Callouts/StolenVehicle.cs) — pursuit creation, suspect registration and player activation.
- RAGE Plugin Hook XML documentation shipped with package 1.124.0 — `World.GetGroundZ` requires loaded collision and returns null when unavailable; `Game.DisplayHelp` durations are milliseconds.

## Credits

- **UNWARE STUDIO** — Concept and development
- **Thomas** — Updates and maintenance

