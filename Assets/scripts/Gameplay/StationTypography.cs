using TMPro;
using UnityEngine;
using System.Collections.Generic;

/// <summary>Shared mechanical typeface for system UI; handwritten notes keep their own fonts.</summary>
public static class StationTypography
{
    private static Font uiFont;
    private static TMP_FontAsset uiAsset;

    public static Font UIFont => uiFont != null ? uiFont : uiFont =
        Resources.Load<Font>("StartMenu/Fonts/Rajdhani-Bold")
        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static TMP_FontAsset UIAsset
    {
        get
        {
            if (uiAsset != null) return uiAsset;
            uiAsset = TMP_FontAsset.CreateFontAsset(UIFont);
            if (uiAsset == null)
            {
                Debug.LogWarning("Station UI font could not be created; using the default TMP font.");
                return TMP_Settings.defaultFontAsset;
            }
            uiAsset.name = "Station Rajdhani UI (Runtime)";
            // CreateFontAsset can leave this optional table null. Initialize it
            // before adding a fallback, otherwise the entire start UI aborts.
            if (uiAsset.fallbackFontAssetTable == null)
                uiAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (TMP_Settings.defaultFontAsset != null && TMP_Settings.defaultFontAsset != uiAsset)
                uiAsset.fallbackFontAssetTable.Add(TMP_Settings.defaultFontAsset);
            return uiAsset;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { uiFont = null; uiAsset = null; }
}
