# Final choice panel live typography

- Workflow: built-in image generation/edit tool, following the imagegen skill. No CLI fallback.
- Original panel retained unchanged: `Assets/Resources/FinalChoiceUI/FinalProtocolPanel.png`.
- Text-free final panel: `Assets/Resources/FinalChoiceUI/FinalProtocolPanel-Clean-v1.png`.
- Generated output: `C:/Users/边南/.codex/generated_images/01a09e8b-4894-7523-a0c6-55a47b4f9747/exec-625a7b3b-bcd0-47fe-8ee2-cc0db35cfc21.png`.
- Consumer: `Assets/scripts/Gameplay/FinalChoiceTerminal.cs`.
- Live labels: `FINAL PROTOCOL`, `SEND RECORDS`, `SEAL THE FACILITY`.
- Per the user's final correction, neither option has a description. Existing inner thoughts and ending signals are unchanged.

## Final edit prompt

Use case: precise-object-edit. Edit target is the attached existing Unity UI panel image. Make a clean TEXT-FREE VERSION of this exact panel asset for live text overlay in Unity. Remove ALL lettering: FINAL PROTOCOL, SELECT ONE RESPONSE, DATA TRANSFER, FACILITY LOCKDOWN. Fill the former lettering with the same matching dark brushed metal / fine hexagonal panel texture, with no visible rectangular patch edges. Preserve everything else as precisely as possible: original landscape 16:9 canvas, exact outer panel size and position within the canvas, screws, silver mechanical border, cyan outline, two symmetrical button assemblies at the same coordinates, left cyan indicator strip, right orange indicator strip, two square brushed-steel press buttons, blank metal nameplates in the original label positions, all colors, illumination, material precision, shadows, and geometry. Do not redesign, rearrange, crop, zoom, add extra buttons or decorations, or add any text, symbols, labels or watermark. Only the mechanical panel itself, on a genuinely transparent background outside its perimeter, retaining existing transparency. Interior metal remains opaque. No environmental scene or baked black backdrop. This is a production UI asset, not a screenshot or mockup.
