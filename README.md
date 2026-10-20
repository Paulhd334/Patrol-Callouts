# Patrols Callouts

> **Patrols Callouts** is an LSPDFR plugin for GTA V combining a **police menu** with a growing collection of **realistic callouts** — traffic stops, roadside assistance, investigations, and multi-part scenarios.

![Version](https://img.shields.io/badge/version-1.0.0-blue?style=flat-square)
![LSPDFR](https://img.shields.io/badge/LSPDFR-0.4.x-1f6feb?style=flat-square)
![RAGE Plugin Hook](https://img.shields.io/badge/RAGE%20Plugin%20Hook-latest-orange?style=flat-square)
![Platform](https://img.shields.io/badge/platform-GTA%20V%20(PC)-lightgrey?style=flat-square)
![Status](https://img.shields.io/badge/status-work%20in%20progress-yellow?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)

---

## Table of Contents

- [Overview](#overview)
- [Planned Callouts](#planned-callouts)
- [Implementation Status](#implementation-status)
- [Tech Stack](#tech-stack)
- [How to Contribute](#how-to-contribute)
- [Credits](#credits)

---

## Overview

This README is the **roadmap**: every callout listed here is planned. Some are already prototyped (`ArmoredVehiclePursuit`, `VehiculeEnPanne`), the rest are on the backlog.

**Patrols Callouts** is built on top of the **LSPDFR** framework and **RAGE Plugin Hook**, and follows a strict "one class per callout" architecture so that each scenario can be developed, tested, and reviewed independently.

---


## Planned Callouts

### Traffic & Roadside

**1. Broken Down Vehicle on the Highway** *(in progress — `BrokenDownVehicle.cs`)*  
> A driver is stranded on the emergency lane. Officers must secure the area, interview the driver, and call a tow truck if needed. The driver may be cooperative, panicked, or hiding something.

**2. Suspect Truck on the Highway**  
> A truck is reported with a potentially stolen cargo. Officers must check the driver's documents and verify the origin of the load. The driver may cooperate, provide fake papers, or attempt to flee.

**3. Abandoned Vehicle in the Countryside**  
> A car is found on an isolated road. Officers must examine the vehicle, look for clues, and interview potential witnesses to understand why it was abandoned.

**4. Suspicious Gas Station**  
> An employee reports suspicious behavior. Officers respond and check whether it is a theft, fraud, or the presence of a wanted vehicle.

---


### Investigations

**8. Hit-and-Run Accident**  
> Two vehicles are involved in an accident, but one driver has fled. Officers must interview witnesses, collect information, and search for the fleeing vehicle.

**9. Witness Who Disappears**  
> A witness to an assault refuses to cooperate and leaves the scene. Officers must find them again and verify the information they provided.

**10. Suspicious Object Found**  
> A passerby reports an abandoned bag in an alley. Officers must secure the perimeter, look for the owner, and call the appropriate services depending on the situation.

**11. Disturbing Disappearance**  
> A person is reported missing and their relatives have had no news. Officers must gather testimonies, search for the person's vehicle, and follow clues to try to find them.

**12. The Mysterious Case** *(multi-part arc)*  
> Several seemingly unrelated interventions gradually lead to the same investigation. Officers must preserve evidence, cross-reference testimonies, and identify links between different suspects.

---

### Public Order

**13. Person in Distress**  
> A disoriented person asks for police help. Officers must verify their identity, try to locate their relatives, or request medical assistance if needed.

**14. Bus Incident — Passenger with Feet on the Seats**  
> A bus driver contacts the police after noticing a passenger placing their feet on passenger seats. Officers must listen to the driver, talk to the passenger, and ask them to remove their feet to respect cleanliness and good conduct rules in public transport.

> Several scenarios can occur: the passenger cooperates immediately, refuses to comply, or becomes disruptive. Depending on the situation, officers can remind the rules, ask the passenger to leave the bus according to applicable procedures, or request backup if the incident escalates. The objective is a realistic intervention where dialogue is the priority, and the response is adapted to the passenger's behavior.

---

### Accidents & Incidents

**15. Accident Involving a Police Vehicle**  
> A patrol car is involved in an accident. Officers must secure the scene, assist the officers involved, and determine the circumstances of the accident.

**16. Suspicious Abandoned Vehicle**  
> A car is found with the engine running, doors open, and no driver nearby. Officers must examine the scene and look for clues that could lead to another intervention.

**17. Warehouse Intrusion**  
> An alarm goes off in an industrial area. On arrival, officers must check the premises and determine whether it is a false alarm, an intrusion, or part of a larger criminal operation.

---

### Special Scenarios

**18. Armored Vehicle Pursuit** *(in progress — `ArmoredVehiclePursuit.cs`)*  
> A full-chase pursuit of a heavily armored vehicle with two occupants, including a turret gunner. Standard PIT maneuvers may be ineffective.

**19. Highway Broken-Down Vehicle — Tow Required** *(variant of #1)*  
> Same as #1, but with a mandatory tow-truck service and a longer on-scene interaction.

*(More scenarios will be added as the project grows.)*

---

## Implementation Status

| # | Callout | Status |
|---|---------|--------|
| 1 | Broken Down Vehicle on Highway | In progress (`BrokenDownVehicle.cs`) |
| 2 | Suspect Truck on Highway | Backlog |
| 3 | Abandoned Vehicle in Countryside |
| 4 | Suspicious Gas Station | Backlog |
| 6 | Barricaded Suspect | Backlog |
| 7 | Organized Vehicle Theft | Backlog |
| 8 | Hit-and-Run Accident | Backlog |
| 9 | Witness Who Disappears | Backlog |
| 10 | Suspicious Object Found | Backlog |
| 11 | Disturbing Disappearance | Backlog |
| 12 | The Mysterious Case | Backlog (multi-part arc) |
| 13 | Person in Distress | Backlog |
| 14 | Bus Incident — Feet on Seats | Backlog |
| 15 | Accident Involving Police Vehicle | Backlog |
| 16 | Suspicious Abandoned Vehicle | Backlog |


**Legend:** In progress · Planned · Backlog

---

## Tech Stack

### Backend / Game Runtime

| Technologie | Rôle |
|---|---|
| **C# (.NET Framework 4.8)** | Langage principal du plugin |
| **.NET Framework 4.8** | Runtime ciblé |
| **Visual Studio 2022** | IDE de développement |
| **Windows 10/11** | OS cible |

### Game APIs

| Technologie | Rôle |
|---|---|
| **RAGE Plugin Hook** | API natives GTA V + hooking |
| **LSPDFR API** | Framework callouts / police |
| **Git** | Version control |
| **GitHub** | Hébergement du code |


---

## Credits

- **UNWARE STUDIO** — Concept & development
- **Thomas** — Updates & maintenance

---
---

