# Patrols Callouts — LSPDFR Callouts & Police Menu

> **Status:** Work in progress — all callouts listed below are planned and will be implemented progressively.

---

## 🚔 Overview

**Patrols Callouts** is an LSPDFR plugin project combining a **police menu** with a large set of **realistic callouts** for GTA V. The goal is to give players a coherent, immersive patrol experience: routine traffic stops, roadside assistance, investigations, and escalating scenarios that can link together into a larger case.

This README is the **roadmap**: every callout listed here is planned. Some are already prototyped (`ArmoredVehiclePursuit`, `VehiculeEnPanne`), the rest are on the backlog.

---

## 📋 Table of Contents

- [Police Menu](#-police-menu)
- [Planned Callouts](#-planned-callouts)
  - [Traffic & Roadside](#traffic--roadside)
  - [Arrests & Suspects](#arrests--suspects)
  - [Investigations](#investigations)
  - [Public Order](#public-order)
  - [Accidents & Incidents](#accidents--incidents)
  - [Special Scenarios](#special-scenarios)
- [Implementation Status](#-implementation-status)
- [Tech Stack](#-tech-stack)
- [How to Install](#-how-to-install)
- [How to Contribute](#-how-to-contribute)

---

## 🧭 Police Menu

**Patrols Callouts — Police Menu**

A main menu giving quick access to:

- 🚦 **Traffic stops** — check license, insurance, registration, vehicle condition, sobriety and driver documents.
- 🧾 **Fines & citations** — issue tickets directly from the menu.
- 🚔 **Arrests** — search a suspect, check identity, discover illegal items, and select an arrest reason.
- 📻 **Backup requests** — call additional units, specialized support, or a tow truck.
- 🛠️ **Roadside services** — request a tow, ambulance, or fire department depending on the situation.

---

## 📞 Planned Callouts

### Traffic & Roadside

**1. Broken Down Vehicle on the Highway** *(in progress — see `VehiculeEnPanne.cs`)*  
A driver is stranded on the emergency lane. Officers must secure the area, interview the driver, and call a tow truck if needed. The driver may be cooperative, panicked, or hiding something.

**2. Suspect Truck on the Highway**  
A truck is reported with a potentially stolen cargo. Officers must check the driver's documents and verify the origin of the load. The driver may cooperate, provide fake papers, or attempt to flee.

**3. Abandoned Vehicle in the Countryside**  
A car is found on an isolated road. Officers must examine the vehicle, look for clues, and interview potential witnesses to understand why it was abandoned.

**4. Suspicious Gas Station**  
An employee reports suspicious behavior. Officers respond and check whether it is a theft, fraud, or the presence of a wanted vehicle.

---

### Arrests & Suspects

**5. Realistic Arrests** *(part of the police menu)*  
Search a suspect, discover possible illegal items, verify their identity, and select the reason for arrest.

**6. Barricaded Suspect in a House**  
A wanted person barricades themselves in a residence and refuses to come out. Officers must secure the perimeter, attempt dialogue, and request a specialized unit if necessary.

**7. Organized Vehicle Theft**  
Several similar vehicles have been reported stolen. Officers must recover the vehicles, identify suspects, and possibly uncover a clandestine storage location.

---

### Investigations

**8. Hit-and-Run Accident**  
Two vehicles are involved in an accident, but one driver has fled. Officers must interview witnesses, collect information, and search for the fleeing vehicle.

**9. Witness Who Disappears**  
A witness to an assault refuses to cooperate and leaves the scene. Officers must find them again and verify the information they provided.

**10. Suspicious Object Found**  
A passerby reports an abandoned bag in an alley. Officers must secure the perimeter, look for the owner, and call the appropriate services depending on the situation.

**11. Disturbing Disappearance**  
A person is reported missing and their relatives have had no news. Officers must gather testimonies, search for the person's vehicle, and follow clues to try to find them.

**12. The Mysterious Case** *(multi-part arc)*  
Several seemingly unrelated interventions gradually lead to the same investigation. Officers must preserve evidence, cross-reference testimonies, and identify links between different suspects.

---

### Public Order

**13. Person in Distress**  
A disoriented person asks for police help. Officers must verify their identity, try to locate their relatives, or request medical assistance if needed.

**14. Bus Incident — Passenger with Feet on the Seats**  
A bus driver contacts the police after noticing a passenger placing their feet on passenger seats. Officers must listen to the driver, talk to the passenger, and ask them to remove their feet to respect cleanliness and good conduct rules in public transport.

Several scenarios can occur:
- The passenger cooperates immediately.
- The passenger refuses to comply.
- The passenger becomes disruptive.

Depending on the situation, officers can remind the rules, ask the passenger to leave the bus according to applicable procedures, or request backup if the incident escalates. The objective is a realistic intervention where dialogue is the priority, and the response is adapted to the passenger's behavior. 🚌👮

---

### Accidents & Incidents

**15. Accident Involving a Police Vehicle**  
A patrol car is involved in an accident. Officers must secure the scene, assist the officers involved, and determine the circumstances of the accident.

**16. Suspicious Abandoned Vehicle**  
A car is found with the engine running, doors open, and no driver nearby. Officers must examine the scene and look for clues that could lead to another intervention.

**17. Warehouse Intrusion**  
An alarm goes off in an industrial area. On arrival, officers must check the premises and determine whether it is a false alarm, an intrusion, or part of a larger criminal operation.

---

### Special Scenarios

**18. Armored Vehicle Pursuit** *(in progress — see `ArmoredVehiclePursuit.cs`)*  
A full-chase pursuit of a heavily armored vehicle with two occupants, including a turret gunner. Standard PIT maneuvers may be ineffective.

**19. Highway Broken-Down Vehicle — Tow Required** *(variant of #1)*  
Same as #1, but with a mandatory tow-truck service and a longer on-scene interaction.

*(More scenarios will be added as the project grows.)*

---

## 📊 Implementation Status

| # | Callout | Status |
|---|---------|--------|
| — | Police Menu | 🟡 Planned |
| 1 | Broken Down Vehicle on Highway | 🟢 In progress (`VehiculeEnPanne.cs`) |
| 2 | Suspect Truck on Highway | 🔴 Backlog |
| 3 | Abandoned Vehicle in Countryside | 🔴 Backlog |
| 4 | Suspicious Gas Station | 🔴 Backlog |
| 5 | Realistic Arrests | 🟡 Planned |
| 6 | Barricaded Suspect | 🔴 Backlog |
| 7 | Organized Vehicle Theft | 🔴 Backlog |
| 8 | Hit-and-Run Accident | 🔴 Backlog |
| 9 | Witness Who Disappears | 🔴 Backlog |
| 10 | Suspicious Object Found | 🔴 Backlog |
| 11 | Disturbing Disappearance | 🔴 Backlog |
| 12 | The Mysterious Case | 🔴 Backlog (multi-part arc) |
| 13 | Person in Distress | 🔴 Backlog |
| 14 | Bus Incident — Feet on Seats | 🔴 Backlog |
| 15 | Accident Involving Police Vehicle | 🔴 Backlog |
| 16 | Suspicious Abandoned Vehicle | 🔴 Backlog |
| 17 | Warehouse Intrusion | 🔴 Backlog |
| 18 | Armored Vehicle Pursuit | 🟢 In progress (`ArmoredVehiclePursuit.cs`) |

**Legend:** 🟢 In progress · 🟡 Planned · 🔴 Backlog

---

## 🧱 Tech Stack

- **Language:** C# (.NET Framework 4.8)
- **API:** [RAGE Plugin Hook](https://ragepluginhook.net/) + [LSPDFR API](https://www.lcpdfr.com/lspdfr/)
- **Game:** Grand Theft Auto V (PC)
- **Architecture:** One class per callout, shared helpers (`Main.Log`, hostile-relations setup, etc.)

### Conventions used in this project

- All callouts derive from `LSPD_First_Response.Mod.Callouts.Callout`.
- The plugin entry point (`Main.cs`) registers all callouts once, guarded by a `bool calloutsEnregistres` flag.
- Native calls are wrapped in `try/catch` and logged — a failing native must **never** crash the game.
- Hostile relationships between custom groups and `COP` / `PLAYER` are initialized **once**, thread-safely, in `InitialiserRelationsSiBesoin()`.
- Every spawned entity is checked with `.Exists()` before use.
- Logs go to `C:\Temp\patrols_callouts_debug.log` (rolling at 1 MB).

---

## 📥 How to Install

1. Install **LSPDFR** and **RAGE Plugin Hook** if not already done.
2. Copy `PatrolsCallouts.dll` (and any dependency) into `Grand Theft Auto V\Plugins\LSPDFR\`.
3. Launch GTA V, open the RAGE console, and type `LoadPlugin PatrolsCallouts`.
4. Go on duty — the callouts should start appearing based on their probability.

---

## 🤝 How to Contribute

- **Report a bug:** open an issue with the log file (`C:\Temp\patrols_callouts_debug.log`) attached.
- **Suggest a callout:** describe the scenario, the expected player interactions, and any branching outcomes (cooperation / flight / combat).
- **Code style:** keep the French-comment style used in existing files, one file per callout, all natives wrapped in `try/catch`.

---

## 📜 License

*(To be defined — likely MIT or a custom LSPDFR-friendly license.)*

---

## 🙏 Credits

- **UNWARE STUDIO** — concept & development
- **LSPDFR team** — API and mod framework
- **RAGE Plugin Hook team** — native bindings

---

*Last updated: project kickoff — more callouts coming soon.*
