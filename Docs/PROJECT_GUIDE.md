# End of Shift project guide

This guide describes the maintained desktop virtual environment, its source assets,
and the contracts between gameplay systems. Start with the root README to run it.
The production entry scene is `Assets/Resources/Scenes/first.unity`.

## Runtime ownership

| Concern | Main implementation | Contract |
| --- | --- | --- |
| Stage progression | `GameFlowController`, `GameplaySignals` | Every required completion signal must arrive before advancing. |
| Contextual selection | `PlayerInteractor`, `IPlayerInteractable` | The focused target owns the E action; a held tool does not block controls. |
| Item ownership | `PickupItem`, `CorridorItemViewmodel` | One held item and an optional stowed tool; return/drop restores scene ownership. |
| Monitor views | `MonitorViewManager`, `MonitorCameraController` | Standing, overview and focused feed modes restore captured input and camera state. |
| Incident scheduling | `CorridorPressureSequence`, `LaterAnomalyPressure` | Next incidents wait for renewed monitor observation; deadlines belong to active tasks. |
| Gas repair | `GasPressureState`, `PressureTimingPuzzle` | Hold moves right, release moves left; uninterrupted safe dwell completes each channel. |
| Power repair | `CircuitRoutingUI`, `SequencePuzzle` | All contacts must conduct; the resulting signal defines the lever order. |
| Specimens | `ScannerTool`, `ScannableAnomaly`, `SpecimenRoomCompletion` | Detect, reveal, pick up and dispose; both specimen completions are required. |
| Threat and shooting | `GhostApproachProfile`, `RenderedGhostBillboard`, `GunTool`, `IGunHitTarget` | Progress determines approach; hit targets own their response. |
| Optional activities | `OptionalPropInteraction`, `OptionalPropActivities`, `RobotDeskCollection` | Reading, consumption and collection have separate presentation states. |
| Language | `StationLocalization`, `StationLanguageCatalog` | Bind an English source string once; restore the original font on language changes. |
| Audio | `ShiftAudioDirector`, `ShiftInteractionSounds`, `ShortMusicPlayer` | One-shot alarm, growing tension, world-space player music and interaction feedback are distinct. |
| Ending | `FinalMonitorEvent`, `FinalChoiceTerminal`, `ShiftEndingScreen` | Complete incidents, unlock the terminal, select a consequence, show static art and caption. |

## Event boundaries

Important semantic signals include `corridor_disposed`, `gas_restored`,
`ghost_destroyed`, `power_restored`, `specimen_room_complete` and
`final_protocol_unlocked`. Do not replace these conditions with renderer visibility:
a hidden visual can be a normal presentation state rather than a completed task.
`GameplaySignals` stores history for late subscribers. Stage-local evidence is reset
on entering a new stage. Repeated events must not award duplicate progress.

The timing controller waits for about ten seconds of monitor watching between
incidents. The specimen task has a three-minute budget. Opening the power repair
panel suspends its deadline. Pausing any puzzle preserves the current session.

## Input and modal views

1. A readable note, reference photograph or monitor handles its own ESC close.
2. The pause menu handles ESC when no closing view has already consumed it.
3. Gas and power repair sessions stay open beneath the pause menu.
4. Reward camera shots capture the camera, input, cursor, held-item visibility and
   time scale, then restore their captured values.

Use `StationLocalization.Set` when changing UI text. Changing a translated `.text`
directly can bypass the binding and lose the English source during later switches.
Keep controls in the same coordinate system as their artwork. `PuzzlePanelSkin`
creates hit areas beneath the art panel rather than trusting old scene rectangles.

## Modeling and asset import

`ArtSource/Props_v1/ZeroDuty_Props_v1.blend` retains editable prop collections,
metric scale, material slots and a separate presentation scene. `build_props.py`
uses cubes, cylinders, bevels and weighted normals for the frames, books,
clipboard, paper, food and robot. `export_unity.py` converts and joins each prop's
export geometry, resets its root, exports FBX with Y up and -Z forward, and records
material parameters in `import_manifest.json`. The source `.blend` stays editable.

The main station FBX set lives in `Assets/Resources/ZeroHourAssets/Models`.
The historical station source path is recorded in the source notes; the current
maintained runtime project is this repository, not a second copy under that path.
Do not place both the complete station FBX and every nested equipment export into
the same scene: the library contains intentionally reusable nested exports.

Three normal-state photographs are exported from the Unity monitor cameras, packed
into the Blender source and imported as static frame textures. They deliberately
do not use live RenderTextures. Runtime monitors use separate camera feeds.

The ghost source library contains Blender render frames and the current shadow
pose cutouts. The current `RenderedGhostBillboard` loads `GhostShadow/Pose_*`;
keep that distinction when describing or replacing the active visual.

## Visual and audio consistency

Use graphite metal, restrained cool highlights, low-saturation paper and small warm
practical lights. Brightness communicates device state. Lamp glass and local lights
follow the station power state together; frames do not receive the glass emission.
Clone materials for runtime-only effects and restore original assignments on cleanup.

The active result presentation uses static images in `EndingArt`, a black
background and captions underneath. AI ending/failure videos are not part of this
presentation. Static AI-assisted menu, UI and ending artwork remains a separate
asset category and is documented in the source register.

Audio provenance is in `ArtSource/AUDIO_SOURCES.md`. `MUS_Explore.ogg` is the active
shared menu/gameplay bed. The optional player's current clip is
`Player_04_UserMusic.wav`. These are different playback roles and must not be
credited as the same track. Keep alarm onset separate from the escalating tension
layer. The final breakdown cue has its low kick layer removed.

## Repository and review workflow

Keep `Assets`, its `.meta` files, `Packages` and `ProjectSettings` together.
Unity asset references use GUIDs, so moving or renaming an asset through the editor
must also preserve its metadata. `Library`, local logs, IDE state and builds are
regenerable/local and excluded from version control. The large original ghost
`.blend` is tracked by Git LFS. Run `git lfs pull` after cloning.

Use focused changes for gameplay, asset imports, presentation and documentation.
Before integration, inspect `git status`, test affected behavior and review the
diff. Avoid simultaneous edits to the same serialized scene; prefer component
changes and isolated prefabs when possible. Current history contains iterative
commits to `main`; this guide does not imply a historical PR or branch workflow.
Existing local changes must be preserved when performing documentation maintenance.

## Validation and evidence

- `ArtSource/Tests/*Tests.cs`: deterministic pressure, robot, monitor, language and
  approach-state checks outside rendering.
- `StationLocalizationValidation`: language round trips, source font restoration,
  note coverage and handbook layout.
- `RobotCollectionValidation`: one/three/five display states, desktop clearance,
  all-five framing through the reward motion and optional Play-state restoration.
- `OverheadLampValidation`: eight fixtures, independent materials, dimming,
  blackout, restoration and cleanup.
- `GhostShadowValidation`: transparent pose assets and runtime ghost behavior checks.
- `ReportEvidenceCapture`: production scene and UI screenshots for reports. This
  Edit-mode evidence export does not replace an end-to-end Play test.
- `python ArtSource/Tools/audit_project.py`: reproducible file inventory, package
  versions, source-register links and project hygiene checks. Output is in
  `ArtSource/Documentation` by default; `--output` selects an external directory.

Batch screenshot command (only when the project is not open in another editor):

```powershell
$env:EOS_REPORT_CAPTURE_DIR = 'F:/Codex/ReportEvidence'
& 'C:/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe' `
  -batchmode -projectPath 'F:/Zero-duty' `
  -executeMethod ReportEvidenceCapture.RunBatch `
  -logFile 'F:/Codex/ReportEvidence.log'
```

Evidence images identify their capture method in `capture-manifest.txt`.
Report figures must distinguish a Blender presentation render, a scene-camera
capture and a reconstructed production UI state. Do not label them all as Play captures.
