# 人物、双开门与三屏监控：Inspector 挂载说明

适用本项目 Unity 6000.4.10f1、Cinemachine 3.1.7、URP、新版 Input System。只提供脚本，不修改场景、动画控制器或现有 RenderTexture。脚本直接读取新版 Input System 的 Keyboard/Mouse，不需要额外配置 Input Actions，也不要再挂其他脚本重复处理同一套输入。

## 1. 脚本与挂载位置

| 脚本 | 挂载物体 | 功能 |
| --- | --- | --- |
| `DoorManager` | 门口固定的触发范围物体 | 人物进入开双门，全部人物碰撞体离开后关双门 |
| `playerController` | 人物根节点 | WASD、Shift 疾跑、重力与 CharacterController 碰撞 |
| `FirstPersonCameraController` | Camera_Player | 鼠标转向、上下限制、走路/跑步摇晃 |
| `MonitorInteraction` | 监控桌或其交互范围中心物体 | 距离检测、淡入淡出提示、F 坐下 |
| `MonitorViewManager` | 三个监控 Camera 的共同父物体 | 坐姿视角、单屏视角、切换和退出 |
| `MonitorCameraController` | 与 MonitorViewManager 同一个物体 | 专注时 WASD 转动指定监控 Camera |

## 2. 人物移动

建议层级：

```text
Player                         ← playerController + CharacterController
├─ Body                        ← 人物模型（可选）
└─ Camera_Player               ← FirstPersonCameraController
   └─ vertical
      └─ horizontal
         └─ shake
            └─ RL_Camera       ← CinemachineCamera

Main Camera                    ← Unity Camera + CinemachineBrain + AudioListener
```

1. 在人物根节点添加 `playerController`，Unity 会同时添加 `CharacterController`。
2. 按人物尺寸设置 CharacterController 的 Height、Radius、Center；确保胶囊脚底接近地面，地面和墙壁有实体 Collider。人物根节点保持 Scale = (1,1,1)。
3. 移动方向固定使用挂载 `playerController` 的人物根节点朝向。默认 Walk Speed = 2.5、Sprint Speed = 5，单位米/秒，可自行调整。
4. 人物不要同时用动态 Rigidbody 驱动，也不要保留重复的实体胶囊碰撞体；移动碰撞由 CharacterController 负责。
5. WASD 沿人物水平朝向移动，斜向已限制速度。按住任意 Shift 疾跑，松开立即使用走路速度。没有跳跃功能。

## 3. 第一人称相机

在 `Camera_Player` 添加 `FirstPersonCameraController`：

| 字段 | 绑定/设置 |
| --- | --- |
| Player | 人物根节点上的 playerController；鼠标左右直接旋转此物体，Camera_Player 必须放在其子层级中 |
| Vertical | vertical 节点 |
| Horizontal | horizontal 节点 |
| Shake | shake 节点 |
| Sensitivity | 鼠标灵敏度，默认 0.12 度/像素 |
| Min Pitch / Max Pitch | 默认 -75 / 75 度；负数抬头、正数低头 |
| Invert Y | 是否反转鼠标上下 |
| Walk / Run Frequency | 走路/跑步摇晃频率 |
| Walk / Run Amplitude | X 左右位移、Y 上下位移，单位米 |
| Walk / Run Roll | 轻微左右摇头的角度 |
| Shake Smoothing | 停步和切换速度时的平滑程度 |

名称与层级一致时可以自动查找三个子节点；有改名则手动拖引用。把相机高度放在 Camera_Player 的位置上，vertical/horizontal/shake 的初始局部旋转建议为 (0,0,0)。上下角度限制相对于 vertical 的初始旋转。

水平鼠标旋转直接作用于 Player 字段对应的人物根节点，没有角度限制。WASD 始终使用这个节点的水平朝向：人物右转 90 度后，W 就沿新的前方移动。相机作为人物子物体跟随转向；vertical 负责俯仰，shake 负责摇晃，horizontal 保留为中性节点。旧版的 Yaw Root 和 Movement Orientation 字段已移除，避免引用不同节点导致人物朝向与移动方向不一致。人物模型也应放在该人物根节点下面。

摇晃只在有移动输入、实际发生水平位移且接地时播放。停止、顶墙、离地、坐下后平滑回到原位。默认锁定并隐藏鼠标；Unity 编辑器按 Esc 可能自行解锁鼠标，回到站立视角后点击 Game 窗口可重新锁定。

### Cinemachine 输出相机

- `Main Camera` 是实际输出到屏幕的 Unity Camera，挂 `CinemachineBrain`，保留唯一的 AudioListener。建议放在人物层级外，避免输出相机自身叠加人物旋转。
- `RL_Camera` 使用 Cinemachine 3 的 `CinemachineCamera`；设置 Position Control 和 Rotation Control 为 None，由以上层级驱动。不要附加另一个鼠标输入控制器。
- Main Camera 的 Brain 建议使用 Late Update，Default Blend 可设置 Ease In Out / 0.35 秒，控制站立与坐下之间的过渡。Main Camera 使用透视投影。
- 下面的坐姿 CinemachineCamera 与 RL_Camera 必须使用能被同一个 Brain 接收的 Output Channel；场景中其他 Cinemachine 相机的 Priority 不应高于正在使用的坐姿相机。

## 4. 双开门

1. 在门口建立独立、固定的 `DoorTrigger`，添加 BoxCollider 并勾选 **Is Trigger**。触发范围要覆盖人物靠近门的位置，不能挂在会随开关门移动的门扇上。
2. 为确保物理触发消息稳定，可在 DoorTrigger 上添加 Rigidbody，设置 **Is Kinematic = true、Use Gravity = false**。人物使用上面的 CharacterController。
3. 挂 `DoorManager`，把左门 Animator 拖到 `DoorAnim_Left`，右门 Animator 拖到 `DoorAnim_Right`。旧脚本的字段类型是 Animation，**需要重新绑定 Animator 引用**。
4. Animator 第 0 层必须有下列状态，名称大小写一致，状态的 Motion 指向对应 Clip：

| 左门 | 右门 |
| --- | --- |
| OpenDoorL | OpenDoorR |
| CloseDoorL | CloseDoorR |

5. **本项目现有的两个门 Animator Controller 带有无条件、启用 Has Exit Time 的自动过渡。请删除 Open 与 Close 之间这些无条件过渡，以及任何会自动离开这四个状态的过渡。** 让脚本直接播放状态即可，不需要 Trigger/Bool 参数。否则会出现人物还在范围内，门已经自行关闭或循环开关的情况。
6. 四个动画 Clip 关闭 **Loop Time**，两边 Animator 建议取消 **Apply Root Motion**。保留独立的门扇实体 Collider，使关闭的门能阻挡人物。
7. 开关门中途反向时，左右门分别读取自己当前动画的完成比例，并从反向动画的 `1 - 当前比例` 处直接播放。例如开门完成 20% 后离开，关门从 80% 处开始，只播放剩余 20%；关门完成 30% 后重新进入，开门从 70% 处开始。已完成的动画按 100% 处理，反向动画从 0% 开始。旧版 Blend Seconds 已移除，避免混合过渡影响对应姿态。启动时取关闭动画的最后一帧作为关门姿态；首次检测人物进入后播放开门动画。
8. 为使反向切换的位置连续，Open 与 Close Clip 在互补比例处必须对应同一门姿态（包括运动曲线）。建议关门动画使用开门动画的时间反转版本；如果两段动画不是对应的反向曲线，比例映射正确时也可能出现位置跳变。

只识别 Collider 自身或父节点上有 `playerController` 的对象。支持一个人物多个碰撞体；直到最后一个离开才关门。碰撞体被禁用或销毁也会清理占用记录。

## 5. 监控桌、坐姿与三屏切换

建议层级：

```text
MonitorDesk                    ← MonitorInteraction
├─ InteractionPoint            ← 可选，靠近人物脚部高度的交互中心
└─ 三块实际屏幕模型

MonitorSystem                  ← MonitorViewManager + MonitorCameraController
├─ CCTV_Left                   ← Unity Camera，拍摄监控区域 A
├─ CCTV_Center                 ← Unity Camera，拍摄监控区域 B
├─ CCTV_Right                  ← Unity Camera，拍摄监控区域 C
├─ OverviewPose                ← 空物体：坐姿眼睛位置，能看清三屏
├─ LeftPose                    ← 空物体：左屏前方，朝向左屏
├─ CenterPose                  ← 空物体：中屏前方，朝向中屏
└─ RightPose                   ← 空物体：右屏前方，朝向右屏

SeatedCamera                   ← CinemachineCamera，放在人物层级外
Canvas                         ← Screen Space - Overlay
└─ MonitorPrompt               ← TextMeshProUGUI
```

**区分两类相机：** SeatedCamera 是玩家看桌面屏幕的视角；三个 CCTV Camera 是拍摄其他区域并生成屏幕纹理的摄像机。四个 Pose 是玩家眼睛的位置和朝向，不能把它们绑定成 CCTV 摄像机。

### MonitorViewManager

| 字段 | 绑定 |
| --- | --- |
| Player Camera | RL_Camera 上的 CinemachineCamera |
| Seated Camera | SeatedCamera 上的 CinemachineCamera |
| Seated Priority | 默认 100，运行时至少比 Player Camera 高 1 |
| Overview Pose | OverviewPose |
| Focus Poses，Size = 3 | Element 0 = LeftPose，1 = CenterPose，2 = RightPose |
| Overview Field Of View | 总览视野，默认 60 |
| Focus Field Of View | 单屏视野，默认 40；结合 Pose 位置调整屏幕占比 |
| View Smoothing | 总览/专注/屏幕切换的平滑速度，默认 8 |
| Sway Frequency / Position / Degrees | 坐姿轻微摇晃，可分别调频率、位移和角度 |
| Focused Sway Multiplier | 专注时保留多少摇晃，默认 0.25；设 0 可完全稳定 |

SeatedCamera 的 Position Control、Rotation Control 设为 **None**，不配置跟随/瞄准输入控制器，也不要额外挂 Noise，避免叠加。GameObject 必须保持激活，组件 Enabled 可以由脚本管理：初始化自动关闭，坐下启用，起身关闭。

四个 Pose 不能放在 SeatedCamera 下，否则相机移动会同时移动目标，引起漂移。坐下后，SeatedCamera 的世界旋转 Y 始终为 90° 加呼吸产生的小幅偏移，总览、专注及切换过程均如此；Pose 的 Y 旋转会被忽略，只使用其位置和 X/Z 旋转。请将视点摆在屏幕前，使朝世界 +X（Y=90°）时能看到屏幕。这里固定的是世界旋转：若相机父物体有旋转，Inspector 显示的局部 Y 不一定为 90°，建议将 SeatedCamera 放在场景根层级。相机只平滑插值到作者摆好的 Pose，不做沿途避障，所以视点之间应避开桌面、椅子和显示器外壳。

坐下采用移动玩家视角来模拟，人物实体保留在进入交互时的位置，锁定移动和鼠标转向；没有人物坐下骨骼动画或身体传送。起身后恢复原位置、原朝向和原输入状态。

### MonitorInteraction 与提示

1. 在 MonitorDesk 上挂 `MonitorInteraction`，绑定 Player、Player Look、View Manager。
2. Interaction Point 可留空使用物体位置，也可绑定专门的交互中心。Interaction Distance 默认 2 米，按人物根节点到交互点的三维距离计算。选中物体可看到青色范围球。
3. Canvas 下新建 TextMeshProUGUI，放在屏幕中下方，拖到 Prompt Text。文字内容由脚本设为 **按F看监控**，靠近时淡入、离开或坐下时淡出。
4. **提示文字物体保持激活**，脚本通过透明度控制显隐。TMP 字体需包含中文字形，或配置中文 Fallback Font Asset，避免中文显示方框。Fade Speed 控制淡入淡出速度。
5. 距离检测不要求触发碰撞体，也不检查玩家是否正看着桌面或中间是否有墙；把交互中心和距离设在桌前可到达范围内。

## 6. 监控转动与 RenderTexture

在 MonitorSystem 的 `MonitorCameraController` 中设置 Cameras，Size 必须为 3：

| 索引 | Camera | 纹理对应屏幕 |
| --- | --- | --- |
| 0 | CCTV_Left | 左屏 |
| 1 | CCTV_Center | 中屏 |
| 2 | CCTV_Right | 右屏 |

每项可分别设置：

- **Yaw Limits**：相对开始运行时局部朝向的左右范围，默认 -60 到 60 度。
- **Pitch Limits**：相对初始朝向的俯仰范围，默认 -35 到 35 度；负数向上、正数向下。
- **Degrees Per Second**：转动速度，默认 35 度/秒。

先在编辑器摆好三个 Camera 的初始位置和朝向。专注时 W 上仰、S 下俯、A 左转、D 右转；只改变当前摄像机。返回总览或站立时停止控制，已转动的朝向会保留，下一次专注继续从该朝向操作。三个 Camera 不要再挂其他旋转控制脚本。

### 复用项目已有 RenderTexture

1. 三个 CCTV Camera 分别把 **Target Texture** 绑定到 `Assets/Resources/Monitor/Texture/Screen1.renderTexture`、`Screen2.renderTexture`、`Screen3.renderTexture`。
2. 给三块屏幕分别创建/指定独立的 **Universal Render Pipeline/Unlit** 材质，Base Map 分别使用对应 RenderTexture；只替换屏幕发光面的材质槽。用 Unlit 可以避免画面随房间灯光变暗。如果显示器用 UI RawImage，则把纹理绑定到 Texture。
3. 三个 CCTV Camera 使用普通 Unity **Camera**，Render Type 使用 Base，保持 Enabled。它们不需要 CinemachineBrain，也不要保留 AudioListener 或 MainCamera 标签。
4. 纹理分辨率可从 512×512 或 1024×576 开始，宽高比尽量匹配屏幕模型。三个摄像机会持续渲染，因此总览也能同时看到三个实时画面。
5. 设置 Culling Mask，只渲染需要监控的场景；通常排除监控屏幕自身、提示 UI 和玩家专用相机物件，避免画中画递归和无用渲染。

脚本不改动现有 RenderTexture 和材质资产，也不自动生成纹理，方便后续更换场景及屏幕。

## 7. 最终按键

| 状态 | 输入 | 结果 |
| --- | --- | --- |
| 站立 | WASD | 移动 |
| 站立 | 按住 Shift / 松开 | 疾跑 / 恢复走路 |
| 站立 | 鼠标 | 水平无限转向、俯仰受限 |
| 靠近监控桌 | F | 坐下，进入三屏总览 |
| 三屏总览 | Z / X / C | 专注左 / 中 / 右屏 |
| 专注 | Z / C | 切到左邻 / 右邻屏幕，首尾循环 |
| 专注 | X | 直接专注中屏 |
| 专注 | WASD | 转动当前监控 Camera |
| 专注 | Esc | 返回三屏总览 |
| 三屏总览 | Esc | 起身，恢复第一人称移动和鼠标 |

## 8. 场景绑定后的验收

- 门：从两边进入均开门；停留不自动关闭；完全离开才关门；快速进出可以反向播放开关；非人物物体不触发。
- 移动：WASD 和斜向速度正确，Shift 松开立即退出疾跑；墙壁能阻挡；不能跳跃；站立和顶墙不持续摇晃。
- 鼠标：左右连续旋转超过一圈；上下停在设置的极限；停止移动后摇晃归位。
- 提示：范围外隐藏，进入淡入，F 坐下后隐藏；检查中文正常显示。
- 监控：坐姿不能用鼠标转向，WASD 不移动人物；总览 Z/X/C 对应正确屏幕；专注 Z/C 循环切换。
- 转动：WASD 只影响当前监控，分别测试三台上下左右极限；切走再切回保持朝向；总览时 WASD 不改变任何监控。
- 退出：专注 Esc 只退到总览，再按一次才起身；反复进出可恢复移动；坐下时禁用监控管理物体也能恢复人物输入。
- 画面：三屏均实时更新，单屏视点没有穿模，过渡与摇晃幅度舒适，没有重复 AudioListener。

脚本已使用本机 Unity 6000.4.10f1 自带的 C# 编译器和本项目的 Cinemachine/Input System/TMP 程序集完成编译检查。由于本次不配置场景，实际视点、碰撞、材质和手感需要按以上步骤在 Play Mode 中验收。
