# Native UI project copy

Independent copy made on 2026-10-28 from F:\Zero-duty, including its uncommitted work.

Open this directory in Unity Hub with Unity 6000.4.10f1. Start scene: Assets/Resources/Scenes/first.unity.

The original project was not edited. Git history/remotes and rebuildable caches were not copied. Unity rebuilds its Library on first opening.

## Rebuilt screens

- Start menu and introduction use a RenderTexture of the actual control room, not generated artwork.
- The closer menu framing keeps all three monitors to the right of the fixed menu and crops the foreground chair's lower half. NativeMenuAtmosphere adds independent live-feed interference bursts and a menu-only double-flashing red practical light; its overlays and light are disabled before gameplay.
- Menu buttons, breaker chassis, lever grips, indicator lamps, gas gauge and hold button use editable native UI geometry and live text.
- Existing rotary-contact puzzle, color order, lever logic, three-channel gas regulation and language support are retained.
- Both endings and time-out failure now play short real-time films in the centered 70% picture area, then reveal the original localized captions and native mesh buttons. Films copy the actual corridor's visual meshes into an isolated stage, add a sliding lift door, and animate a faceless procedural mesh shadow. No ending illustration, ghost sprite or imported button artwork is loaded by these screens. The live level and original project are not modified.
- Data transfer: the shadow enters the lift from the lab, the leaves close, and upward indicators signal departure. Lockdown: the lift never opens; the shadow stays in the lab, faces the player and approaches the camera, leaving the sealed lift visible behind it.
- Editor previews after START/CONTINUE: Ctrl+Alt+1 = data transfer, Ctrl+Alt+2 = lockdown, Ctrl+Alt+3 = captured/failure. Start a new Play session to test another outcome.
- NativePanelGraphic.cs creates geometric meshes; it does not load a bitmap or call an image-generation model.
- On 2026-10-30 the in-game pause/audio pages and final-choice terminal were rebuilt from native geometry. Page arrows, volume adjustment, independent choice buttons, hover feedback, existing English/Chinese text and ending callbacks are retained.
- Book, sticky note and record sheet now use NativePaperGraphic vertex geometry: two open pages and a spine, a folded sticky note, and an unruled sheet with a small clip. Existing note contents/handbook spread and close controls are retained; long notes fit to the writing area in either language.
- Replaced PauseUI, FinalChoiceUI and StreamingAssets/NoteUI artwork is archived outside Assets in LegacyAIArtwork/2026-10-30, so it is not included as Resources or StreamingAssets. The original F:\Zero-duty project remains unchanged.

## Scope and provenance

Gameplay ghost cutouts are deliberately retained at the user's request. Their images, facing/pose selection and shooting/approach behavior were not modified by this UI replacement. Old StartMenu/PuzzleUI/EndingArt source images also remain as legacy assets, but the rebuilt screens do not use them. Do not present those legacy assets as non-AI work or claim the whole project is AI-image-free.

The new code and layout are AI-assisted. Confirm that the submission rules allow AI-assisted code/design; replacing image-generation assets does not by itself establish compliance with a broader AI ban.

No GitHub push was performed.
