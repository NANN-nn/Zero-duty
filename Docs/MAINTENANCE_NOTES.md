# Documentation and project maintenance 2026-10-27

## Scope

This maintenance pass preserves the existing scene, asset GUIDs and gameplay behavior.
It adds English explanations of the interaction contract, stage/event ownership,
camera restoration, specimen revelation and rotated-case collider construction.
Chinese inline comments in the affected first-party runtime scripts were translated.
Serialized field names and player-facing localization strings were not renamed.

The project already contained local ghost and ceiling-light changes before this
pass. Those changes were retained rather than reverted or attributed to this work.

## Maintained entry points

- `README.md`: current launch, controls, language, testing and documentation links.
- `Docs/PROJECT_GUIDE.md`: runtime architecture, modeling/import pipeline and review workflow.
- `Docs/ASSET_REGISTER.md`: asset families, exact file origins and public references.
- `ArtSource/Documentation/asset_inventory.csv`: reproducible runtime input inventory.
- `ArtSource/Documentation/project_audit.json`: versions, metadata and GUID checks.
- `ArtSource/Tools/audit_project.py`: regenerate the inventory without importing assets.
- `ArtSource/Tools/run_state_tests.ps1`: compile and execute five existing pure state tests.
- `ReportEvidenceCapture`: repeatable scene-camera and production UI evidence export.

Historical component and source-stage prop instructions now point readers to the
current launch guide. Nineteen macOS `.DS_Store` files were removed from the asset
tree; `.DS_Store` and local `.idea` configuration are ignored going forward.

## Verification performed

- Unity 6000.4.10f1 batch editor imported and compiled the current runtime/editor scripts.
- The evidence export produced thirteen 1920x1080 captures plus its capture manifest.
- Five standalone state suites passed: pressure, approach, monitor routing, robot
  uniqueness and language source/translation round trips.
- The final inventory contained 825 non-metadata asset files, zero missing asset
  metadata files and zero duplicate GUIDs.
- The illustrated report was rendered through Word to PDF and all eight pages
  were visually inspected. The report uses 10.5 pt body text and contains seven
  figure groups, including a separate project overview graphic.

The evidence exporter renders representative UI states in Edit mode. This pass did
not replace the existing full-game Play walkthrough with an automated Play claim.
AI ending/failure videos are excluded from the current report and presentation.
