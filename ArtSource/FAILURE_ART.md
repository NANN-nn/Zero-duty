# Failure screen illustration

- Date: 2026-10-16
- Workflow: built-in image generation/edit tool (imagegen skill), no CLI fallback.
- Approved preview: `C:/Users/边南/.codex/generated_images/01a09e8b-4894-7523-a0c6-55a47b4f9747/exec-0c4eabb0-a17d-48cc-97d9-72585c843ea9.png`
- Final text-free edit: `C:/Users/边南/.codex/generated_images/01a09e8b-4894-7523-a0c6-55a47b4f9747/exec-6ca805e8-e432-497e-aab1-2c6a47eaafcb.png`
- Unity asset: `Assets/Resources/EndingArt/ShiftFailed-v1.png`
- Runtime consumer: `Assets/scripts/Gameplay/ShiftEndingScreen.cs`
- Uses the existing centered 1344 x 756 image layout on a 1920 x 1080 black result screen. Title and short description are separate live UI below the art. The other two ending illustrations are unchanged.
- Title: `SHIFT FAILED`
- Description: `You didn't finish your shift.`

## Final edit prompt

Edit only the attached approved horror illustration: remove the words SHIFT FAILED and every other text from the image. Naturally continue the dark worker uniform and corridor shadows through the former bottom caption strip. Preserve the exact worker, terrified eyes, black hair, two monster hands covering his face and gripping his head, smoky black monster, underground laboratory corridor, blue/red lighting, painterly style, existing composition and landscape 16:9 framing. No crop, zoom, additional characters, extra hands, blood or UI. No text whatsoever; Unity will draw the title separately below this illustration.
