# 更新日志 / Changelog

**作者：By wos111(机人)** ｜ 仓库：https://github.com/wos111a/ADOFAI-BingSfx

---

## v1.0.0 — 2026-10-05（首个公开发布）

> 说明：下面的 v2~v7 是**开发过程中的内部迭代编号**，对外的正式版本号统一为 **v1.0.0**。

### 新增

- **游戏内「添加音频」**：点按钮弹出 Windows 文件选择框，选任意 `.wav`
  自动校验格式 → 复制进 `audio\` → 立即可用，不用再手动拷文件。
  重名自动追加序号（`xx_1.wav`），非 16-bit PCM 直接拒绝并提示原因。
- **界面多语言**：简体中文 / English / 한국어 三语切换，默认简体中文，选择会存进 `config.json`。
  共 28 条界面文案 × 3 语种 = 84 条，缺失时自动回退中文。
- **诊断信息**逐条拆行显示，不再挤成一坨。

### 修复

- **面板文字被挤成三行**：UMM 设置面板实际可用宽度只有约 370px，
  代码里写死的 `GUILayout.Width(430)` 导致文件名、按钮文案被折行。
  改为自适应宽度（文件名 `ExpandWidth(true)`，绑定按钮 55px、清除按钮 50px）。
- **文件名缓冲区越界风险**：`GetOpenFileNameW` 的 `nMaxFile` 单位是**字符**，
  之前只分配了 1024 **字节**（=512 字符），长路径会写越界。改为 `1024 * 2` 字节。
- **文件对话框初始路径是随机内存**：`Marshal.AllocHGlobal` 不清零，
  补上 `Marshal.WriteInt16(ptr, 0)` 写入空串。
- **空音效目录时每帧抛异常**：`defaultSound` 为 `null` 时直接查字典会抛
  `ArgumentNullException`，加了判空。
- 日志里的版本号写死成 `v6.1`，改为 `v1.0.0 by wos111(机人)`。
- 语言那一栏的标签误用了语言自身名字（显示「简体中文」），改用 `langLabel`（显示「语言」）。
- 移除已不再使用的 Harmony 引用（v7 完全不依赖 Harmony）。

---

## v6 — 按键绑定 + 音效换血

- **按键绑定**：每个音效可单独绑定一个键，实现「按什么发什么声」；未绑定的键播默认音效。
  绑定存 `config.json` 的 `bindings`（格式 `D=xx.wav|Space=yy.wav`）。
- 修：绑定捕获逻辑只设了 `captureTarget` 却没有消费，功能等于没做通。
  补上 `Event.current` 的 `KeyDown` 处理 + `SaveBindings()`。
- 修：「刷新音效列表」不会加载新加的文件，补 `clipCache.Clear(); clipsLoaded=false; audioReady=false`。
- 修：音频解析失败时会反复读盘，加 `clipsLoaded` 标志只尝试一次。
- 修：`GUILayout.Toggle` 的返回值没赋回，开关点了没反应。
- **音效替换**：删掉 `鸡-单字.wav` / `鸡你太美-清唱.wav` / `smile-截取.wav`，
  换成带拖尾的 `鸡-拖尾.wav`（0.37s）。

## v5 — 延迟初始化

- 音频系统改**延迟初始化**：第一次按键时才创建 `AudioSource` / 解析 wav，
  避开 UMM 早期加载时 Unity 音频系统还没就绪的问题。
- 同一帧内只响一次（`Time.frameCount` 去重），长按不会叠音。

## v4 — 找到「没声音」的真正原因

- **根因**：`AudioClip.Create(name, lengthSamples, channels, frequency, stream)`
  的 `channels` 和 `frequency` 传反了，实际创建出「44100 声道 / 1Hz」的 clip，
  Unity 不报错但永远不发声。修正为 `AudioClip.Create("bing", frameCount, channels, rate, false)`。
- **根因 2**：WAV 采样率读错偏移，读到了 `nAvgBytesPerSec`（88200）而不是
  `nSamplesPerSec`（44100），会导致播放速度翻倍、音调变尖。修正为 `pos + 12`。

## v3 — 放弃 Harmony

- 确认 Harmony **钩不到** Unity 的原生 `extern` 方法（`AudioSource.Play` / `PlayOneShot`），
  改为在游戏的托管代码上做触发，后于 v7 彻底改为直接轮询 `Input.GetKeyDown`。

## v2 — 游戏内设置面板

- 加设置面板：启用开关、音量滑块、自定义默认音效。

## v1 — 首个可加载版本

- 搭通「不装 Unity 也能编译 Unity Mod」的链路：直接引用游戏自带的
  `UnityEngine.CoreModule` / `AudioModule` / `InputLegacyModule` / `IMGUIModule`
  + `UnityModManager.dll`，目标框架 `netstandard2.1`，`dotnet build` 出 DLL。
- 自己解析 16-bit PCM WAV，不依赖 Unity 的资源加载。

---

## 技术备注

| 项 | 说明 |
|---|---|
| 目标框架 | `netstandard2.1`（游戏的 AudioModule 依赖它，用 net472/net48 会 CS1705） |
| 编译 | `dotnet build src\BingSfx.csproj -c Release`，产物输出到 mod 根目录 |
| 依赖 | 不需要 Unity 编辑器、不需要 Harmony |
| 触发方式 | 轮询 `Input.GetKeyDown`（全键码，排除 `None` 和 `Mouse*`） |
| 音频格式 | 仅 16-bit PCM `.wav`（单声道/立体声均可） |
