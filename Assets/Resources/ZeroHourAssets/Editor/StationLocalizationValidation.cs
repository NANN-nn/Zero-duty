using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Test-owned UI previews; never saves or rewrites the user's scene.</summary>
[InitializeOnLoad]
public static class StationLocalizationValidation
{
    private const string Request = "Library/StationLocalizationValidation.request";
    private const string Output = "Temp/LocalizationQA";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static StationLocalizationValidation() => EditorApplication.delayCall += RunPending;
    private static void RunPending()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        { EditorApplication.delayCall += RunPending; return; }
        File.Delete(Request);
        Validate();
    }
    private static T Field<T>(object owner, string name)
        => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Call(object owner, string name, params object[] args)
        => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    private static void Check(bool ok, string reason)
    { if (!ok) throw new Exception(reason); }

    [MenuItem("Tools/Zero Duty/Validate bilingual UI")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Output);
        StationLocalization.Language previous = StationLocalization.Current;
        bool hadPreference = PlayerPrefs.HasKey("ZeroDuty.Language");
        GameObject menuRoot = null, notesRoot = null, propRoot = null;
        try
        {
            Check(StationLocalization.ChineseFont != null, "Bundled Chinese font missing.");
            var characters = new HashSet<char>();
            foreach (string translation in StationLanguageCatalog.Chinese.Values)
                foreach (char c in translation) if (c >= '\u3400' && c <= '\u9fff') characters.Add(c);
            string glyphs = string.Join("", characters);
            Check(StationLocalization.ChineseAsset.TryAddCharacters(glyphs, out string missing),
                "Chinese glyphs missing: " + missing);

            StationLocalization.SetLanguage(StationLocalization.Language.English);
            menuRoot = new GameObject("Localization QA Menu") { hideFlags = HideFlags.HideAndDontSave };
            ShiftStartMenu menu = menuRoot.AddComponent<ShiftStartMenu>();
            Call(menu, "BuildUI");
            Call(menu, "ShowPage", Field<GameObject>(menu, "settingsPage"));
            Canvas menuCanvas = Field<Canvas>(menu, "canvas");
            menuCanvas.enabled = true;
            Capture(menuCanvas, "settings-english.png");
            Call(menu, "ToggleLanguage");
            Check(StationLocalization.IsChinese && PlayerPrefs.GetInt("ZeroDuty.Language") == 1,
                "Settings language choice was not persisted.");
            Capture(menuCanvas, "settings-chinese.png");
            Check(Field<TMP_Text>(menu, "languageValue").text.Contains("简体中文"), "Language value stale.");
            foreach (TMP_Text label in menuRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!label.gameObject.activeInHierarchy) continue;
                label.ForceMeshUpdate();
                Check(!label.isTextOverflowing, "Settings overflow: " + label.name);
            }
            Call(menu, "ShowPage", Field<GameObject>(menu, "mainPage"));
            Capture(menuCanvas, "main-chinese.png");
            Call(menu, "ShowPage", Field<GameObject>(menu, "introductionPage"));
            Capture(menuCanvas, "intro-chinese.png");

            notesRoot = new GameObject("Localization QA Notes") { hideFlags = HideFlags.HideAndDontSave };
            OptionalPropActivities activities = notesRoot.AddComponent<OptionalPropActivities>();
            Call(activities, "Awake");
            propRoot = new GameObject("ShiftLog_Open1") { hideFlags = HideFlags.HideAndDontSave };
            OptionalPropInteraction prop = propRoot.AddComponent<OptionalPropInteraction>();
            prop.Configure(OptionalPropInteraction.PropKind.Note);
            // The whole handbook and every numbered note must be translated,
            // including paragraphs containing controls and manual page breaks.
            string[] names = { "ShiftLog_Open1", "InspectionClipboard2", "FoldedPersonalNote3",
                "FoldedPersonalNote4", "InspectionClipboard5", "FoldedPersonalNote6",
                "FoldedPersonalNote7", "InspectionClipboard8", "FoldedPersonalNote9", "FoldedPersonalNote10" };
            foreach (string name in names)
            {
                propRoot.name = name; prop.EnsureNoteContent();
                string translated = StationLocalization.T(prop.NoteBody);
                string withoutKeys = translated.Replace("WASD", "").Replace("ESC", "");
                Check(!Regex.IsMatch(withoutKeys, "[A-Za-z]{3,}"), "Untranslated prose: " + name);
                if (name != "ShiftLog_Open1")
                {
                    Call(activities, "ConfigureNoteLayout", prop.NoteStyle);
                    StationLocalization.Set(Field<Text>(activities, "noteTitle"), prop.NoteTitle);
                    Text body = Field<Text>(activities, "noteBody");
                    StationLocalization.Set(body, prop.NoteBody);
                    Call(activities, "FitChineseNote", body);
                    Check(body.preferredHeight <= body.rectTransform.rect.height, "Chinese note clipping: " + name);
                    Check(!Regex.IsMatch(body.text, @"\n[，。！？；：、）》】”’]"), "Punctuation orphan: " + name);
                }
            }
            propRoot.name = "ShiftLog_Open1"; prop.EnsureNoteContent();
            typeof(OptionalPropActivities).GetField("currentNote", Private).SetValue(activities, prop);
            Call(activities, "ConfigureNoteLayout", OptionalPropInteraction.NotePaperStyle.Book);
            Field<GameObject>(activities, "notePanel").SetActive(true);
            Call(activities, "ConfigureHandbookPages");
            Canvas notesCanvas = notesRoot.GetComponentInChildren<Canvas>(true);
            Capture(notesCanvas, "handbook-chinese.png");
            foreach (string field in new[] { "noteBody", "noteRightBody" })
            {
                Text body = Field<Text>(activities, field);
                Check(body.font == StationLocalization.ChineseFont, "Handbook font override.");
                Check(body.preferredHeight <= body.rectTransform.rect.height, "Handbook text clipped: " + field);
            }
            propRoot.name = "FoldedPersonalNote7"; prop.EnsureNoteContent();
            Call(activities, "ConfigureNoteLayout", OptionalPropInteraction.NotePaperStyle.Sticky);
            StationLocalization.Set(Field<Text>(activities, "noteTitle"), prop.NoteTitle);
            StationLocalization.Set(Field<Text>(activities, "noteBody"), prop.NoteBody);
            Call(activities, "FitChineseNote", Field<Text>(activities, "noteBody"));
            Capture(notesCanvas, "note-chinese.png");

            // Switching back restores source text and original English fonts.
            Call(menu, "ToggleLanguage");
            Check(!StationLocalization.IsChinese && PlayerPrefs.GetInt("ZeroDuty.Language") == 0,
                "English preference not restored.");
            Check(Field<Text>(activities, "noteBody").text == prop.NoteBody, "English note roundtrip.");
            Check(Field<Text>(activities, "noteBody").font != StationLocalization.ChineseFont,
                "English handwriting lost.");
            File.WriteAllText(Output + "/validation.txt",
                "PASS: settings EN/ZH roundtrip, saved language, " + characters.Count
                + " Chinese glyphs, all 10 notes translated, both handbook pages fit, original English fonts restore.");
            Debug.Log("[Localization] PASS. Previews and report: " + Output);
        }
        catch (Exception exception)
        {
            File.WriteAllText(Output + "/validation.txt", "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (propRoot != null) UnityEngine.Object.DestroyImmediate(propRoot);
            if (notesRoot != null) UnityEngine.Object.DestroyImmediate(notesRoot);
            if (menuRoot != null) UnityEngine.Object.DestroyImmediate(menuRoot);
            StationLocalization.SetLanguage(previous);
            if (!hadPreference) { PlayerPrefs.DeleteKey("ZeroDuty.Language"); PlayerPrefs.Save(); }
        }
    }

    private static void Capture(Canvas canvas, string file)
    {
        var cameraRoot = new GameObject("Localization QA Camera") { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture texture = new RenderTexture(1920, 1080, 24);
        Texture2D snapshot = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        Camera previousCamera = canvas.worldCamera;
        RenderMode previousMode = canvas.renderMode;
        var layers = new Dictionary<GameObject, int>();
        try
        {
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
            { layers.Add(child.gameObject, child.gameObject.layer); child.gameObject.layer = 31; }
            Camera view = cameraRoot.AddComponent<Camera>();
            view.transform.position = new Vector3(0, 0, -10);
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Color.black;
            view.cullingMask = 1 << 31; view.targetTexture = texture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = view; canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            view.Render(); RenderTexture.active = texture;
            snapshot.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); snapshot.Apply();
            File.WriteAllBytes(Output + "/" + file, snapshot.EncodeToPNG());
        }
        finally
        {
            canvas.worldCamera = previousCamera; canvas.renderMode = previousMode;
            foreach (var entry in layers) if (entry.Key != null) entry.Key.layer = entry.Value;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(cameraRoot);
            UnityEngine.Object.DestroyImmediate(snapshot); UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
