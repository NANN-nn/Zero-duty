using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>Authored EN / Simplified Chinese copy. Source strings stay English.</summary>
public static class StationLanguageCatalog
{
    public static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "END OF SHIFT", "夜尽之前" },
        { "Just another quiet night", "今晚，应该也不会有事。" },
        { "START", "开始游戏" }, { "SETTINGS", "设置" }, { "EXIT GAME", "退出游戏" },
        { "MASTER VOLUME", "主音量" }, { "BACK", "返回" }, { "<  BACK", "<  返回" },
        { "LANGUAGE", "语言" }, { "NIGHT SHIFT", "夜班" }, { "CONTINUE", "继续" },
        { "The last shift has gone home.", "上一班的人已经回去了。" },
        { "You're on your own down here until morning.", "天亮之前，\n你得独自守在这地下。" },
        { "They said it would be a quiet night.", "他们说，今晚不会有什么事。" },
        { "PAUSED", "已暂停" }, { "RESUME", "继续游戏" }, { "AUDIO", "声音" },
        { "ESC — Close", "ESC — 关闭" }, { "WASD — Move", "WASD — 移动" },
        { "Press E", "按 E 交互" }, { "Press Q to drop", "按 Q 放下物品" },
        { "Click to inspect", "点击查看" }, { "Shift complete", "值班结束" },
        { "HANDBOOK", "员工手册" }, { "DISPOSAL", "粉碎机" },
        { "GAS  /  POWER", "气阀 / 电闸" }, { "CONTROL ROOM", "控制室" },
        { "SHOOTING RANGE", "射击室" }, { "SPECIMEN ROOM", "样本室" },
        { "SELECT A CAMERA", "选择监控画面" },
        { "Wrong order. Try again", "顺序不对，再试一次" }, { "System restored", "系统已恢复" },
        { "PRESSURE CONTROL", "压力控制" }, { "SAFE", "安全区" },
        { "ADJUST PRESSURE", "调整压力" }, { "STABILIZING", "正在稳定" },
        { "PRESSURE STABLE", "压力已稳定" }, { "WAIT", "等待" }, { "ACTIVE", "进行中" },
        { "OK", "完成" }, { "HOLD", "按住" }, { "PRESS", "按下" },
        { "CIRCUIT ROUTING", "电路连接" }, { "CONNECT TO REVEAL SEQUENCE", "接通电路，查看拉杆顺序" },
        { "FINAL PROTOCOL", "最终协议" }, { "SEND RECORDS", "发送记录" },
        { "SEAL THE FACILITY", "封锁设施" },
        { "Made it this far... and there's still a choice to make?", "都走到这里了……还要我做选择？" },
        { "Send the records. At least someone would know what happened here.", "把记录发出去，至少会有人知道这里发生了什么。" },
        { "But would the records be the only thing getting out?", "可跟着出去的，真的只有记录吗？" },
        { "Seal this place, and everyone outside stays safe.", "封死这里，外面的人就安全了。" },
        { "But I'm still in here.", "可我还在里面。" },
        { "FINAL REPORT  /  01", "最终报告 / 01" }, { "FINAL REPORT  /  02", "最终报告 / 02" },
        { "SHIFT REPORT  /  FAILED", "值班报告 / 失败" },
        { "DATA TRANSMITTED", "记录已发送" }, { "FACILITY SEALED", "设施已封锁" },
        { "SHIFT FAILED", "值班失败" }, { "RESTART SHIFT", "重新开始" },
        { "The truth got out. So might the anomaly.", "真相传了出去。那东西，也许也一样。" },
        { "Outside is safe. You're locked in with it.", "外面安全了。你和它一起留在了里面。" },
        { "You didn't finish your shift.", "你没能完成这次值班。" },
        { "All 5 robots collected!", "五个小机器人都找齐了！" },

        // Short, contextual thoughts — not objective-list instructions.
        { "I should read the handbook first. The arrow will point me to it.", "先看看员工手册吧，箭头指的应该就是。" },
        { "Those pictures on the wall... Is that how this place should look?", "墙上的那些照片……这里原本是这个样子吗？" },
        { "I'll see if the camera feeds match.", "看看监控里是不是也一样。" },
        { "Wait. Where did that picture come from?", "等等，那幅画是哪来的？" },
        { "There's a disposal unit in the lab... That should take care of this.", "实验室里有台粉碎机……应该能处理掉。" },
        { "That's done. Back to the monitors.", "处理好了，回去看看监控。" },
        { "The hissing's stopped. I'll check the monitors again.", "漏气声停了，再回监控那边看看。" },
        { "It's gone... Better check the monitors.", "消失了……还是回去看一眼监控吧。" },
        { "Finally. Back to the monitors.", "总算恢复了，回去看看监控。" },
        { "What's that hiss... A leak in the control room?", "哪来的嘶嘶声……控制室漏气了？" },
        { "Did something just move in the shooting room?", "射击室里……刚才是不是有东西动了？" },
        { "Everything's out... The breakers are in the control room.", "全灭了……电闸在控制室里。" },
        { "Let's see where these circuits lead before pulling anything.", "先看看这几条线路怎么接，不能乱拉。" },
        { "Nothing looks different... Maybe the scanner will pick something up.", "看不出哪里不对……探测器也许能查出来。" },
        { "There it is. This one needs disposing of too.", "原来在这里。这个也得处理掉。" },
        { "Nothing now. I'll head back to the monitors.", "没有反应了，回去看看监控。" },
        { "Still getting a reading... Did I miss one?", "还有反应……是不是漏了一个？" },
        { "There's still a light by the elevator... Maybe there's a way out.", "电梯口还有一盏灯……也许能从那里出去。" },
        { "I'd better read the handbook first. It's marked by an arrow.", "还是先看手册吧，有箭头指着。" },
        { "Let's start with the jars in the cabinet and on the table.", "先查查柜子里和桌上的罐子。" },
        { "I'm not going in empty-handed. There's a gun on the rack.", "不能空着手进去，架子上有把枪。" },
        { "This must be it. Let's get the pressure steady.", "应该就是这里了，先把压力稳住。" },

        // Notes: each paragraph is paired so page breaks and line breaks survive.
        { "NIGHT SHIFT HANDBOOK", "夜班员工手册" }, { "YOUR DUTIES", "岗位职责" },
        { "SHIFT PROCEDURE", "值班流程" }, { "CONTROLS", "操作方式" },
        { "FOR THE NEXT SHIFT", "留给下一班的话" }, { "MONITORING LOG", "监控记录" },
        { "THE PHOTOS", "墙上的照片" }, { "AFTER THE LEAK", "漏气之后" },
        { "CORRIDOR INCIDENT", "走廊事件记录" }, { "FOOTSTEPS", "脚步声" },
        { "AFTER THE FIRST SHOT", "第一枪之后" }, { "SPECIMEN ROOM LOG", "样本室记录" },
        { "LITTLE HELPERS", "小帮手们" }, { "DISPOSAL UNIT", "粉碎机" },
        { "Monitor the facility and investigate any changes during your shift. The photographs on the wall show each room in its normal condition.", "值班期间请留意设施内的情况，发现变化后前往核查。墙上的照片记录了各个房间的正常状态，可作为比对参考。" },
        { "A brief alarm will sound whenever a new incident occurs. Check the camera feeds, attend the affected area, then return to the monitors once the problem is resolved.", "每次出现异常时，都会响起一小段警报。请查看监控，前往对应区域处理；处理完毕后，返回控制室继续观察。" },
        { "Do not leave abnormal objects unattended. Take them to the disposal unit in the laboratory.", "发现异常物品后，请勿留在原地，应送往实验室的粉碎机进行销毁。" },
        { "Mouse — Look around", "鼠标 — 观察" }, { "E — Interact or pick up", "E — 交互或拿取" },
        { "Q — Put down held items", "Q — 放下手中物品" }, { "Left mouse — Use held equipment", "鼠标左键 — 使用手中设备" },
        { "ESC — Close notes or camera views; otherwise open the pause menu", "ESC — 关闭笔记或监控画面；其他时候打开暂停菜单" },
        { "The gas and power controls are right beside the monitors. Same panel for both.", "气阀和电闸就在监控旁边，都在那块控制面板上。" },
        { "Hold the pressure button down. A quick tap won't do much. Holding raises the pressure; letting go lowers it. Keep the needle in the green section for a moment. One light isn't enough—you need all three.", "气阀的按钮要按住，点一下没什么用。按住压力会上升，松开就会下降。让指针在绿色区域里稳一会儿。一盏灯亮了还没完，三盏都亮才行。" },
        { "For the breakers, turn the three connectors at the top until the circuit connects. The lights will come on one after another. Remember the colors, then pull the levers below in that order. Replay it if you missed it. Don't guess.", "电闸先转上面的三个接头，把线路接通。灯会依次亮起来，记住颜色，再按那个顺序拉下面的拉杆。没看清就重播一遍，别靠猜。" },
        { "The gun and scanner are on the equipment rack. Use the gun for whatever shows up in the shooting room. Take the scanner to the specimen room. Looking at the containers won't tell you much.", "枪和探测器都在装备架上。射击室里要是出现了什么，带上枪。去样本室就带探测器，光盯着那些罐子看，什么也看不出来。" },
        { "Scan the jars in the cabinet and on the table. Last time I only checked the cabinet. Found another one on the table after I'd cleaned up.", "柜子里和桌上的罐子都查一遍。上次我只查了柜子，收拾完才发现桌上还有一个。" },
        { "Come back and check the monitors when you're done. I know it feels like you've only just sat down. Just because something's gone doesn't mean the night's over.", "处理完记得回来看看监控。我知道，你可能觉得自己才刚坐下。可那东西不见了，不代表这一晚就结束了。" },
        { "02:14 — Someone was standing by the shooting-room door on Camera 03. I went over to check. The room was empty.", "02:14 — 03号监控里，射击室门口站着一个人。我过去看了，房间是空的。" },
        { "02:19 — The same person appeared in the corridor on Camera 01. I checked the recording. None of the doors had opened.", "02:19 — 同一个人又出现在01号监控的走廊里。我查了录像，没有任何一扇门打开过。" },
        { "I put it down as a camera fault.", "我暂时记成了监控故障。" },
        { "I haven't signed the report yet.", "报告还没签字。" },
        { "I took these when I first started working nights. I kept forgetting what was in each room.", "刚开始上夜班那会儿拍的。我老是记不住房间里原来放着什么。" },
        { "A few times, something on the monitors looked out of place. Comparing it with the photos was how I knew I wasn't remembering it wrong.", "有几次，监控里有些东西看着不太对。拿照片一比，才确定不是自己记错了。" },
        { "The hissing stopped once the pressure settled.", "压力稳住以后，漏气声就停了。" },
        { "I sat back down at the monitors. Behind me, someone let out a long breath.", "我回到监控前坐下。背后，有人长长地呼了一口气。" },
        { "The next shift wasn't due for another three hours.", "离下一班来接替，还有三个小时。" },
        { "01:42 — Found a framed picture on the corridor floor.", "01:42 — 走廊地上发现一幅带框的画。" },
        { "01:49 — Destroyed it in the laboratory disposal unit.", "01:49 — 已投入实验室粉碎机销毁。" },
        { "01:53 — It's back.", "01:53 — 它又回来了。" },
        { "The same corner is missing. That's where I dropped it.", "缺的还是同一个角。是我摔出来的。" },
        { "I watched that frame go through the machine. The pieces are still in there.", "我亲眼看着那个画框被卷进机器里。碎片现在还在里面。" },
        { "I'm not going back out there.", "我不会再出去了。" },
        { "I reported footsteps in the corridor. The cameras showed nobody there.", "我报了走廊里的脚步声，监控上却一个人都没有。" },
        { "They wrote \"pipe noise\" on the report and sent it back.", "他们在报告上写了“管道噪音”，又退了回来。" },
        { "Pipes don't stop when I stop walking.", "可管道不会在我停下的时候，也跟着停下。" },
        { "It disappeared when I fired. I thought that was it, so I lowered the gun.", "开枪以后，它消失了。我以为结束了，就把枪放了下来。" },
        { "Then I caught its reflection in the glass.", "然后，我在玻璃上看见了它的倒影。" },
        { "It was standing behind me.", "它就站在我身后。" },
        { "I hadn't heard a single footstep.", "我连一声脚步都没听见。" },
        { "03:08 — The container in the cabinet looked intact. The scanner gave a positive reading.", "03:08 — 柜内容器外观完好，探测器显示异常。" },
        { "03:12 — A second reading came from the container on the worktable.", "03:12 — 工作台上的容器出现第二处异常读数。" },
        { "Both labels carry the same sample number and collection date.", "两个标签上的样本编号和采集日期完全相同。" },
        { "I checked the inventory twice. We only received one.", "我核对了两遍入库记录。我们只收到过一个。" },
        { "Bought five of these little robots for the desk. Nobody else seemed to like them.", "买了五个小机器人，想摆在桌上。好像也就我觉得它们挺好。" },
        { "Now they're scattered all over the place. No idea who split them up.", "现在到处都是，也不知道是谁把它们拆散了。" },
        { "I still think they look better together.", "我还是觉得，摆在一起好看些。" },
        { "This is the machine from the handbook. I put the thing I found in the corridor through it last night.", "手册里说的就是这台机器。昨晚走廊里捡到的东西，我扔进这里了。" },
        { "It took the whole frame. Nothing left big enough to recognize.", "整个画框都吞进去了，出来的碎片已经看不出原样。" },
        { "Loud as hell, but I felt better once it was gone.", "声音吵得要命。不过看它没了，心里确实踏实了些。" },
    };

    private static readonly Regex Collection = new Regex(@"^Robots collected: (\d+) / (\d+)$");
    private static readonly Regex Camera = new Regex(@"^CAM (\d+) / (\d+)$");
    private static readonly Regex Seconds = new Regex(@"^(\d+[.,]\d+) / (\d+[.,]\d+) s$");
    private static readonly Regex Blocks = new Regex(@"([\r\n\f]+|<[^>]+>)");

    public static string Translate(string english)
    {
        if (string.IsNullOrEmpty(english)) return english ?? string.Empty;
        if (Chinese.TryGetValue(english, out string value)) return value;
        Match match = Collection.Match(english);
        if (match.Success) return $"已收集机器人：{match.Groups[1].Value} / {match.Groups[2].Value}";
        match = Camera.Match(english);
        if (match.Success) return $"监控 {match.Groups[1].Value} / {match.Groups[2].Value}";
        match = Seconds.Match(english);
        if (match.Success) return $"{match.Groups[1].Value} / {match.Groups[2].Value} 秒";
        // Split only at authored boundaries, never replace words inside prose.
        string[] parts = Blocks.Split(english);
        if (parts.Length == 1) return english;
        for (int i = 0; i < parts.Length; i += 2)
            if (Chinese.TryGetValue(parts[i], out value)) parts[i] = value;
        return string.Concat(parts);
    }
}
