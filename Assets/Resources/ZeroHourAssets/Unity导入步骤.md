# 新建 Unity 项目后的导入步骤

1. 关闭 Unity，打开你的 Unity 项目文件夹。
2. 把整个 `ZeroHourAssets` 文件夹复制到项目的 `Assets` 文件夹内。最终路径必须是 `你的项目/Assets/ZeroHourAssets`，不要只复制 `Models`。
3. 打开 Unity，等待右下角导入进度结束。首次导入 88 个模型和贴图会需要一些时间。
4. 在 Unity 顶部菜单执行 `Tools > Zero Hour > Build Materials and Prefabs`。
5. 等脚本运行完成后，使用 `Assets/ZeroHourAssets/Prefabs/ZH_SCENE_Station_Complete.prefab` 放入完整实验室。
6. 要单独摆放或替换设备时，在同一个 `Prefabs` 文件夹搜索以下前缀：
   - `ZH_ENV_`：实验室原有设备与房间组件
   - `ZH_PROP_TOOL_`：检测仪、脉冲枪、日志板
   - `ZH_PROP_ANOMALY_`：异常照片、渗出物、鬼影
   - `ZH_PROP_LAB_`：样本与实验室小道具
   - `ZH_PROP_UTILITY_`：线缆、接口和设施装饰

不要把 88 个模型全部拖进场景。完整场景已经包含实验室设备，单独 Prefab 是留给你替换、移动或额外摆放的，否则会出现重叠模型。

如果顶部没有 `Tools > Zero Hour`，先打开 Unity 的 Console，检查 `ZeroHourAssetSetup.cs` 是否编译报错。自动材质脚本支持 Built-in 和 URP，不支持 HDRP 自动转换。
