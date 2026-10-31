# Duval Drive — Driving School Simulator (Design Doc)

> Working title. A realistic, first-person driving school simulator set in
> Jacksonville, Florida, built in Unity.

## 1. Pitch

You're a 15-year-old in Duval County with a fresh Florida learner's license.
Over a series of lessons with a driving instructor you work your way from an
empty parking lot to I-95 in a summer thunderstorm, logging the supervised
hours Florida requires, and finally sit the **Class E driving skills test**.

The challenge is driving *correctly*, not fast: full stops, mirror checks,
signals, school zones, the Move Over Law, railroad crossings, and Jacksonville
drivers who don't always do the same.

## 2. Pillars

1. **Realistic rules** — every fault is a real Florida traffic rule or test criterion.
2. **Driver's seat** — first-person cockpit; you must physically *look* (mirrors,
   blind spot) and the game tracks it.
3. **Recognizably Jacksonville** — live oaks and Spanish moss, the St. Johns
   River bridges, sudden afternoon storms, sprawling arterials, trains.
4. **Readable feedback** — every mistake is explained and replayable.

## 3. Core loop

```
Lesson briefing → Drive the route (instructor gives directions)
→ Live fault callouts → Debrief scorecard + mistake replays
→ Hours added to logbook → Unlock next lesson / mock test / final test
```

## 4. Florida rules the game models

Values must be double-checked against the current **Florida Driver License
Handbook** (FLHSMV) before shipping — laws change (e.g. the Move Over Law was
expanded in 2024).

| Rule | Game behavior |
|---|---|
| Default speed limits (30 mph residential/business, 55 other roads, up to 70 on interstates) | Speed zones; tolerance before minor fault, larger overage = critical |
| School zones (typically 20 mph when beacons flash) | Time-of-day–driven flashing beacons; handheld phone use banned there |
| Full stop at stop signs, behind the line | Speed must hit ~0 inside stop box |
| Right on red after full stop unless posted "No Turn on Red" | Allowed, but rolling it = fault |
| School bus stop arm | Stop both directions, unless divided by a raised barrier or ≥5 ft median |
| Move Over Law | Change lanes away from stopped emergency / tow / utility / disabled vehicles, or slow to 20 mph under the limit |
| Headlights required when wipers are on for weather | Wipers on + headlights off = fault |
| Railroad crossings | Stop when signals active; never stop on tracks |
| Texting while driving is a primary offense | In-game phone buzzes as a distraction; touching it = fault |
| Signal before turns/lane changes | Signal must be on N seconds/feet before the maneuver |
| Following distance | Measured time gap to the car ahead |
| Pedestrians in crosswalks | Must yield; failure = critical |

### Learner progression (mirrors real FL requirements)

- Learner's license at 15 — the game's opening (knowledge test as a short quiz minigame).
- First 3 months: **daylight driving only** → night lessons locked at first.
- Must be accompanied by a licensed driver 21+ → your instructor.
- **50 supervised hours, 10 at night** → the in-game **logbook** is the meta-progression.
  (Game-time is compressed; each lesson credits "hours".)
- Final: Class E driving skills test.

### Skills test maneuvers (verify against handbook)

Three-point turn · straight-in parking · stop and quick stop · backing up in a
straight line · obeying signs & signals · signaling · right-of-way · following
distance · approaching crosswalks. (Florida's test historically does **not**
include parallel parking — keep it as an optional lesson.)

## 5. Jacksonville setting

The map is a **compressed, Jacksonville-inspired district**, not a 1:1 city
(Jacksonville is the largest city by land area in the contiguous US — far too
big). Real street and neighborhood names, fictional businesses.

### Districts (each unlocks with lessons)

| District | Inspiration | What it teaches |
|---|---|---|
| Strip-mall parking lot | Southside / Baymeadows | Controls, steering, parking, backing |
| Riverside / Avondale | Live oaks, brick streets, 4-way stops | Residential 30 mph, cyclists, parked cars, pedestrians |
| School zone | Any neighborhood elementary | Beacons, buses, crossing guards |
| Downtown | One-way streets, Skyway guideway overhead, JTA buses | One-ways, lane choice, pedestrians, rail crossings |
| River crossings | Main Street Bridge, Acosta, Hart, Fuller Warren | Bridges, merging, wind/visual distraction |
| Arterials | Beach Blvd, Atlantic Blvd, Philips Hwy | Multi-lane, center turn lanes, driveways, U-turns |
| Interstate | I-95, I-295, I-10 | Merging, exits, Move Over Law, lane discipline |
| Beaches | Jax Beach / A1A | Tourists, pedestrians, sunset glare, sand |
| Bonus: Mayport ferry | St. Johns River Ferry | Boarding/queueing on a ferry |

### Local flavor checklist

- Live oaks with Spanish moss, palms, palmettos
- Afternoon summer thunderstorms that arrive fast: heavy rain, hydroplaning, flooded low spots ("Turn Around, Don't Drown")
- Lovebug season (May & September) smearing the windshield — wiper mechanic
- Freight trains blocking crossings (Jacksonville is a big rail town)
- JSO patrol cars, FHP on the interstate, Road Rangers on the shoulder
- Navy jets near Mayport / NAS Jax (ambient audio)
- Football game-day traffic near the stadium (event scenario)
- Hurricane-evacuation traffic scenario (late-game challenge)
- Local radio-style ambient chatter in the car

### IP / likeness rules

Use **real public place names** (streets, bridges, neighborhoods) freely.
**Do not** use real logos/brands: no NFL/Jaguars marks, no real store chains,
no official FLHSMV/Tax Collector branding or replica forms. Use fictional
parody businesses (e.g. "Gator Gas", "First Coast Grocers").

## 6. Systems & architecture (Unity, C#)

Target: **Unity 6 LTS**, **URP**, new **Input System**, **Splines** package for roads.

```
Assets/
  _Project/
    Scripts/
      Vehicle/      CarController, Gearbox, Indicators, Lights, Wipers
      Player/       DriverHeadLook, MirrorCheckTracker, PhoneDistraction
      Rules/        RulesEngine, Violation, zones/* (SpeedZone, StopZone,
                    SignalZone, SchoolZone, RailCrossing, Crosswalk, MoveOverZone)
      Scoring/      ScoreSheet, FaultDefinition (ScriptableObject)
      Instructor/   Instructor, RouteNavigator, VoiceLines
      Traffic/      LaneGraph, AICar, TrafficLightController, Pedestrian
      World/        WeatherSystem, TimeOfDay, SchoolZoneSchedule
      Lessons/      LessonDefinition (ScriptableObject), LessonRunner
      Progression/  Logbook, SaveSystem
      UI/           HUD, Debrief, Menus
    Scenes/  Prototypes/, Districts/, Menus/
    Data/    Lessons/, Faults/
```

### Key designs

- **Vehicle:** start with Unity's `WheelCollider` + a tuned `CarController`
  (automatic first; manual later). Rule-relevant state is exposed:
  speed, indicator state, lights, wipers, gear.
- **Driver POV:** cockpit camera; mouse/right-stick head look, quick-look keys
  (left mirror / rear-view / right mirror / blind spots). Mirrors are render
  textures (cap their resolution/frame rate for performance).
- **Observation tracking:** `MirrorCheckTracker` records *when* the player last
  looked at each mirror/blind spot. Lane changes and turns check "looked within
  the last N seconds".
- **Rules engine:** world is annotated with trigger volumes (zones). Each zone
  watches the player car and raises `Violation` events
  (`{ fault, severity, time, position }`). The engine is the single source of
  truth for scoring, instructor reactions and debrief replays.
- **Scoring:** like a real test — minor faults add points; critical faults
  (running a red, hitting a pedestrian, passing a stopped school bus) fail the
  test instantly.
- **Instructor:** drives a route (list of turns on the lane graph), gives
  directions with a distance lead ("At the next light, turn left"), and reacts
  to violations. Text + voice lines.
- **Traffic AI:** cars follow a **lane graph** (splines with connections at
  intersections), obey lights/stop signs, keep a gap, and yield. No free-roaming
  AI. A small "imperfect driver" chance (late merges, running yellows) for realism.
- **Replays:** record car transform + inputs at ~20 Hz in a ring buffer; debrief
  replays the 10 s around each fault.
- **Lessons as data:** `LessonDefinition` ScriptableObjects (scene, start
  point, route, time of day, weather, objectives, faults emphasized,
  hours credited). Adding a lesson needs no code.

## 7. Lesson list (v1)

| # | Lesson | Location | Focus |
|---|---|---|---|
| 0 | Permit test | Menu quiz | Florida rules & signs |
| 1 | First drive | Parking lot | Start, stop, steer, gentle braking |
| 2 | Backing & parking | Parking lot | Straight-in parking, backing, 3-point turn |
| 3 | Neighborhood streets | Riverside/Avondale | Stop signs, 4-way right-of-way, 30 mph |
| 4 | School zone | Neighborhood | Beacons, buses, crossing guard, phone ban |
| 5 | Lights & turns | Arterial | Protected/unprotected lefts, right on red |
| 6 | Downtown | Downtown | One-ways, pedestrians, rail crossing, buses |
| 7 | Over the river | Bridges | Merging, lane choice, bridge grade |
| 8 | Arterial traffic | Beach/Atlantic Blvd | Center turn lanes, driveways, U-turns |
| 9 | First interstate | I-95 | On-ramps, merging, exits, Move Over Law |
| 10 | Storm drive | Any | Wipers + headlights, hydroplaning, flooding |
| 11 | Night drive | Any (unlocked after "3 months") | Visibility, high beams |
| 12 | Beach run | Jax Beach / A1A | Pedestrians, glare, tourists |
| M | Mock test | Test route | Full test conditions |
| F | **Class E skills test** | Test route | Pass to get your license |

## 8. Roadmap (beginner-friendly)

You haven't used Unity before, so the early phases double as learning.

**Phase 0 — Learn Unity (1–2 weeks)**
Install Unity Hub + Unity 6 LTS. Do Unity Learn's *Unity Essentials* and the
first units of *Junior Programmer* (they include a simple vehicle project).

**Phase 1 — Drivable car (≈2 weeks)**
Flat test scene, WheelCollider car, cockpit camera, head look, speedometer,
indicators. *Done when:* driving feels controllable and you can look at mirrors.

**Phase 2 — Rules & scoring (≈2–3 weeks)**
SpeedZone, StopZone, signal check, traffic light; ScoreSheet; debrief screen.
*Done when:* a lap of a test block produces a correct scorecard.

**Phase 3 — First real district (≈3–4 weeks)**
Riverside-style neighborhood with Splines roads, lane graph, simple AI cars,
pedestrians at crosswalks, instructor directions. Lessons 1–3 playable.

**Phase 4 — Jacksonville expansion**
Downtown, bridges, arterials, interstate; weather & time of day; lessons 4–12.

**Phase 5 — Test & progression**
Logbook, save system, mock + final test, permit quiz.

**Phase 6 — Polish**
Audio (engine, rain, turn signal tick, radio), instructor voice, settings,
steering wheel support, performance.

## 9. Assets & tools

- **Prototype art:** Kenney's CC0 car/city kits, Unity primitives.
- **Car with interior:** needed for POV — a Unity Asset Store car model with a
  cockpit, or a simple custom dashboard to start.
- **Roads:** Unity Splines (free); paid alternative: EasyRoads3D.
- **Map reference:** OpenStreetMap for layout reference (ODbL — credit it if
  data is imported).
- **Audio:** freesound.org (check each license).
- **Version control:** Git + Git LFS for models/textures/audio; Unity `.gitignore`.

## 10. Risks

| Risk | Mitigation |
|---|---|
| Traffic AI complexity | Lane graph only, few cars, grow gradually |
| Scope (big city) | Compressed districts; ship lessons 1–6 first |
| Car feel in first person | Prototype & tune early (Phase 1) before content |
| Mirror render cost | Low-res, low-FPS render textures |
| Rule accuracy | Cross-check every fault with the FL Driver Handbook |
| First Unity project | Small milestones, each playable |

## 11. Open questions

- Scope/goal: hobby, school project, or public release (itch.io / Steam)?
- Automatic only, or manual transmission option?
- Steering wheel + pedals support priority?
- Instructor personality and voice (text-only at first?)
