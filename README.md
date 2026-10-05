# ADOFAI 冰音效 Mod（Bing Sfx）

> **A Dance of Fire and Ice（冰与火之舞）按键音效 Mod** —— 按任意键就播放「冰」音效，
> 全键盘每键可绑不同声音，支持游戏内添加自定义音频、音量调节、中/英/韩三语界面。

**作者：By wos111(机人)** ｜ 版本：v7.0.0 ｜ 游戏：A Dance of Fire and Ice

---

## 功能

- 🔊 **按键触发音效** —— 全键盘任意键，按下即响（长按不叠音）
- 🎹 **按键绑定** —— 每个键可以绑定不同音效，实现「按什么发什么声」
- ➕ **游戏内添加音频** —— 面板上点「添加音频」弹出文件选择框，选 `.wav` 自动装进 Mod
- 🔉 **音量 0~100%**、启用开关、可设「只在关卡内响」
- 🌐 **简体中文 / English / 한국어** 三语界面
- 🚫 **倒计时和 UI 音效不会触发**（不跟随游戏音频，只监听真实按键）
- 📊 内置诊断区：按键心跳、音频就绪状态、音效加载数、触发次数

## 安装

1. 已装 [Unity Mod Manager](https://www.nexusmods.com/site/mods/21)。
2. 下载 Release 里的 `冰音效Mod-v7.0.0.zip`，解压得到 `冰音效Mod` 文件夹，
   整个丢进 `游戏目录\Mods\`：

```
A Dance of Fire and Ice\Mods\冰音效Mod\
├─ Info.json
├─ BingSfx.dll
├─ audio\冰-拖尾.wav
├─ audio\鸡-拖尾.wav
└─ 安装说明.md
```

3. 进游戏 → Mods 菜单 → 冰音效 (Bing Sfx)。

> ⚠️ `Mods\` 下只能有一个 `Id = BingSfx` 的文件夹，重复会报
> `Id 'BingSfx' already uses another mod`。

## 自带音效

| 文件 | 内容 | 时长 |
|---|---|---|
| `冰-拖尾.wav` | 「冰冰冰」最后一个冰，带自然拖尾 | 0.28s |
| `鸡-拖尾.wav` | 带拖尾的「鸡～」 | 0.37s |

自定义音频必须是 **16-bit PCM `.wav`**。mp3 可用游戏自带 ffmpeg 转：

```bash
游戏目录\ffmpeg\ffmpeg.exe -i 输入.mp3 -ac 1 -ar 44100 -sample_fmt s16 输出.wav
```

## 自己编译

不需要 Unity 编辑器，直接引用游戏自带的托管程序集：

```bash
cd src
dotnet build BingSfx.csproj -c Release
```

产物 `BingSfx.dll` 输出到 mod 根目录。目标框架 `netstandard2.1`。

## 目录结构

```
冰音效Mod/
├─ Info.json          # UMM 清单（Id / DisplayName / Author / Version / 入口）
├─ BingSfx.dll        # 编译产物
├─ config.json        # 游戏内改设置后生成（不随包分发）
├─ audio/             # 音效文件，放 16-bit PCM wav
├─ src/               # C# 源码 + csproj
├─ 安装说明.md
└─ CHANGELOG.md       # 完整开发日志（v1 → v7）
```

## 更新日志

见 [CHANGELOG.md](CHANGELOG.md)。

v7.0.0 修复了三个关键问题：面板文字被挤成三行、文件对话框缓冲区越界与未清零、
空音效目录时每帧抛 `ArgumentNullException`；并新增游戏内添加音频与三语界面。

## 许可

音效素材来源于网络公开音效站，仅作个人娱乐用途；代码可自由修改分发。
