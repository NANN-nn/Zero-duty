using System;
public static class StationLanguageCatalogTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Main()
    {
        Check(StationLanguageCatalog.Translate("SETTINGS") == "设置", "Menu translation");
        Check(StationLanguageCatalog.Translate("ESC — Close") == "ESC — 关闭", "Escape hint");
        Check(StationLanguageCatalog.Translate("Robots collected: 3 / 5") == "已收集机器人：3 / 5", "Dynamic progress");
        Check(StationLanguageCatalog.Translate("CAM 02 / 03") == "监控 02 / 03", "Dynamic monitor");
        Check(StationLanguageCatalog.Translate("1.2 / 1.5 s") == "1.2 / 1.5 秒", "Gas dwell");
        Check(StationLanguageCatalog.Translate("END OF SHIFT") == "夜尽之前", "Chinese title");
        Check(StationLanguageCatalog.Translate("LANGUAGE") == "语言", "Language label is not bilingual");
        Check(StationLanguageCatalog.Translate("Just another quiet night") == "今晚，应该也不会有事。", "Revised tagline");
        Check(StationLanguageCatalog.Translate("<b>YOUR DUTIES</b>\n\nWASD — Move\fFOR THE NEXT SHIFT")
            == "<b>岗位职责</b>\n\nWASD — 移动\f留给下一班的话", "Rich text and spread boundaries");
        Check(StationLanguageCatalog.Translate("I know it feels like you've only just sat down.")
            == "I know it feels like you've only just sat down.", "Never replace substrings inside prose");
        Check(StationLanguageCatalog.Translate(null) == "", "Empty labels");
        Console.WriteLine("PASS: localized title, language label, tagline, dynamic labels, rich text and page breaks.");
    }
}
