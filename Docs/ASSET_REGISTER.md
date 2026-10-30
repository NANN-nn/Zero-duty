# Asset and reference register

This register connects the runtime content to editable sources and external credits.
It records known origins; it does not assign unverified authorship to supplied files.

| Asset family | Source and runtime location | Use and reference |
| --- | --- | --- |
| Station and equipment | `Assets/Resources/ZeroHourAssets/Models/ZH_ENV_*`, `ZH_SCENE_*`; historical Blender V9 source documented in `ZeroHourAssets/使用说明.md` | Modular underground station. Model names, source-object mapping, dimensions and triangle counts: `ZeroHourAssets/模型清单.csv`. |
| Twelve prop collections | `ArtSource/Props_v1/ZeroDuty_Props_v1.blend`, `build_props.py`; `Assets/Resources/ZeroHourAssets/Props_v1` | Editable frames, open/closed books, clipboard, papers, food and robot; FBX export plus material manifest. |
| Reference photographs | `ArtSource/Props_v1/Textures/CAM_*_Normal.png` | Saved Unity normal-state camera captures; static textures on wall props. |
| Ghost sources | `ArtSource/GhostSpirit/Ghost Spirit Shader.blend`, `render_frames.py`; `Resources/GhostSpirit`, `Resources/GhostShadow` | Supplied Blender source plus rendered frames; current runtime uses shadow pose cutouts. No public origin URL is recorded for the supplied Blender file. |
| Menu and UI artwork | `Resources/StartMenu`, `PuzzleUI`, `PauseUI`, `NoteUI`, `FinalChoiceUI` | AI-assisted static artwork, composed with native Unity controls and text. |
| Result artwork | `Resources/EndingArt` | Static transfer, lockdown and failure artwork; no ending/failure video dependency. |
| Background music and environment recordings | `Resources/Monitor/Audio/Music/MUS_*.ogg`, `Enviroment/ENV_*.ogg` | Existing project audio. Active bed: `MUS_Explore.ogg`. No composer or public source URL is stored in the current source ledger. |
| User-supplied clips | `Player_04_UserMusic.wav`, `SFX_AnomalyAlarm_User.wav`, `SFX_MusicButton_User.wav` | Supplied as `10月15日.wav`, `10月12日.wav`, `10月12日(1).wav`; see `ArtSource/AUDIO_SOURCES.md`. |
| Interaction recordings | `SFX_FoodChew_CC0.mp3`, `SFX_DrinkSip_CC0.mp3`, `SFX_GhostHit_CC0.mp3` | Freesound source pages linked below; project records identify these as CC0 recordings. |
| Generated sounds | `ShiftInteractionSounds.cs`, `ArtSource/Audio/*.py` | Runtime tones/impacts, tension bed, leak loop, player sketches and final flicker; retain generation scripts. |
| Fonts | `Resources/StartMenu/Fonts`, `Resources/NoteFonts`, `Resources/Localization` | Rajdhani, Patrick Hand, Kalam and Noto Sans SC; retain the bundled OFL files and font-source notes. |
| Runtime code and engine packages | `Assets/scripts`, `Packages/manifest.json`, `packages-lock.json` | Project gameplay implementation and versioned Unity package dependencies; engine/package API documentation below. |

## Public references

- Project repository: https://github.com/NANN-nn/Zero-duty
- Unity 6.4 manual: https://docs.unity3d.com/6000.4/Documentation/Manual/index.html
- URP: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.4/manual/index.html
- Input System: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/index.html
- Cinemachine: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/index.html
- Blender manual: https://docs.blender.org/manual/en/latest/
- Git LFS: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-git-large-file-storage
- Eating a cracker, Rudmer_Rotteveel: https://freesound.org/people/Rudmer_Rotteveel/sounds/364924/
- Drink and gulp, AndersHolm: https://freesound.org/people/AndersHolm/sounds/411219/
- Short Sci fi Whoosh and Impact, doudar41: https://freesound.org/people/doudar41/sounds/728520/
- Rajdhani: https://github.com/google/fonts/tree/main/ofl/rajdhani
- Patrick Hand: https://github.com/google/fonts/tree/main/ofl/patrickhand
- Kalam: https://github.com/google/fonts/tree/main/ofl/kalam
- Noto CJK: https://github.com/notofonts/noto-cjk

Supplied recordings are cited by local filename rather than an invented music link.
Adding a later verified composer/source should update both this register and the report.
