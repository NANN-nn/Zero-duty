using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Text;

/// <summary>Explicit label bindings: no scene-wide polling, no gameplay changes.</summary>
public static class StationLocalization
{
    public enum Language { English, Chinese }
    private const string PreferenceKey = "ZeroDuty.Language";
    private static bool loaded;
    private static Language current;
    private static Font chineseFont;
    private static TMP_FontAsset chineseAsset;
    private sealed class Binding
    {
        // Preserve the source string and English typeface when switching languages.
        // Translating an already translated label would otherwise lose that source.
        public Graphic target;
        public string source;
        public Font font;
        public TMP_FontAsset asset;
    }
    private static readonly Dictionary<Graphic, Binding> labels = new Dictionary<Graphic, Binding>();
    public static Language Current
    {
        get
        {
            if (!loaded)
            {
                current = PlayerPrefs.GetInt(PreferenceKey, 0) == 1 ? Language.Chinese : Language.English;
                loaded = true;
            }
            return current;
        }
    }
    public static bool IsChinese => Current == Language.Chinese;
    public static Font ChineseFont => chineseFont != null ? chineseFont : chineseFont =
        Resources.Load<Font>("Localization/NotoSansSC-Regular");
    public static TMP_FontAsset ChineseAsset
    {
        get
        {
            if (chineseAsset != null) return chineseAsset;
            if (ChineseFont == null) return null;
            chineseAsset = TMP_FontAsset.CreateFontAsset(ChineseFont, 40, 5,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 2048, 2048);
            chineseAsset.name = "Station Simplified Chinese (Runtime)";
            chineseAsset.isMultiAtlasTexturesEnabled = true;
            return chineseAsset;
        }
    }
    public static string T(string source) => IsChinese ? StationLanguageCatalog.Translate(source) : source ?? string.Empty;

    public static void SetLanguage(Language value)
    {
        current = value == Language.Chinese ? Language.Chinese : Language.English;
        loaded = true;
        PlayerPrefs.SetInt(PreferenceKey, (int)current);
        PlayerPrefs.Save();
        var dead = new List<Graphic>();
        foreach (var pair in labels)
        {
            if (pair.Key == null) dead.Add(pair.Key);
            else Apply(pair.Value);
        }
        foreach (Graphic key in dead) labels.Remove(key);
    }

    public static void Set(Text text, string source)
    {
        if (text == null) return;
        Binding binding = Get(text, source);
        if (text.font != ChineseFont) binding.font = text.font;
        Apply(binding);
    }
    public static void Set(TMP_Text text, string source)
    {
        if (text == null) return;
        Binding binding = Get(text, source);
        if (text.font != chineseAsset) binding.asset = text.font;
        Apply(binding);
    }
    private static Binding Get(Graphic target, string source)
    {
        if (!labels.TryGetValue(target, out Binding binding))
        {
            binding = new Binding { target = target };
            labels.Add(target, binding);
        }
        binding.source = source ?? string.Empty;
        return binding;
    }
    private static bool HasChinese(string text)
    {
        foreach (char c in text) if (c >= '\u3400' && c <= '\u9fff') return true;
        return false;
    }

    // Unity's legacy Text does not apply Chinese punctuation line-breaking
    // rules. Wrap authored note paragraphs at their measured glyph advances.
    // Keep the original source in the binding so EN still restores losslessly.
    public static void ReflowChineseNote(Text label)
    {
        if (!IsChinese || label == null || !labels.TryGetValue(label, out Binding binding)) return;
        string content = T(binding.source);
        if (!HasChinese(content)) return;
        Font font = ChineseFont;
        font.RequestCharactersInTexture(content, label.fontSize, label.fontStyle);
        float limit = label.rectTransform.rect.width - 8f;
        var result = new StringBuilder();
        foreach (string paragraph in content.Split('\n'))
        {
            if (result.Length > 0) result.Append('\n');
            if (paragraph.Contains("<b>")) { result.Append(paragraph); continue; }
            var line = new StringBuilder(); float width = 0f;
            foreach (char c in paragraph)
            {
                font.GetCharacterInfo(c, out CharacterInfo glyph, label.fontSize, label.fontStyle);
                if (width + glyph.advance > limit && line.Length > 0)
                {
                    char last = line[line.Length - 1];
                    bool carry = "，。！？；：、）》】”’」』…".IndexOf(c) >= 0
                        || "（《【“‘「『".IndexOf(last) >= 0;
                    if (carry && line.Length > 1)
                    {
                        line.Length--;
                        result.Append(line).Append('\n'); line.Clear().Append(last);
                        font.GetCharacterInfo(last, out CharacterInfo carried, label.fontSize, label.fontStyle);
                        width = carried.advance;
                    }
                    else { result.Append(line).Append('\n'); line.Clear(); width = 0f; }
                }
                line.Append(c); width += glyph.advance;
            }
            result.Append(line);
        }
        label.text = result.ToString();
    }
    private static void Apply(Binding binding)
    {
        string content = T(binding.source);
        bool needsChinese = HasChinese(content);
        if (binding.target is Text legacy)
        {
            Font font = needsChinese ? ChineseFont : binding.font;
            if (font != null && legacy.font != font) legacy.font = font;
            if (legacy.text != content) legacy.text = content;
        }
        else if (binding.target is TMP_Text tmp)
        {
            TMP_FontAsset font = needsChinese ? ChineseAsset : binding.asset;
            if (font != null && tmp.font != font) tmp.font = font;
            if (tmp.text != content) tmp.text = content;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConnectCleanup()
    {
        SceneManager.sceneUnloaded -= CleanUnloadedLabels;
        SceneManager.sceneUnloaded += CleanUnloadedLabels;
    }
    private static void CleanUnloadedLabels(Scene scene)
    {
        var dead = new List<Graphic>();
        foreach (Graphic label in labels.Keys) if (label == null) dead.Add(label);
        foreach (Graphic label in dead) labels.Remove(label);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        labels.Clear(); loaded = false; chineseFont = null; chineseAsset = null;
    }
}
