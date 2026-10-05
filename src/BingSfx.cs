// 冰与火之舞 (A Dance of Fire and Ice) —— 「冰冰冰」音效 mod  v1.0.0（内部迭代第 7 版）
//
// 触发：全键盘，每个键可绑定不同音效；未绑定的键播默认音效。
// 新增（v7）：
//   1. 游戏内「添加音频」按钮：弹出 Windows 文件选择框，选任意 .wav 自动复制进
//      mod 的 audio\ 目录并立即可用（无需手动拷文件）
//   2. 界面语言切换：简体中文 / English / 한국어（默认简体中文，存 config）
//   3. 布局修复：去掉固定宽度（UMM 面板实际 ~370px，固定 430 会把文字挤成三行），
//      按钮改自适应宽度、短文案，诊断长文本拆行
//
// 保留（v5/v6）：音频延迟初始化、按键绑定、无游戏音效跟随（倒计时/UI 不触发）
//
// 设置自动保存到 mod 目录 config.json

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

namespace BingSfx
{
    // ---------- Win32 文件选择框 ----------
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class OpenFileName
    {
        public int lStructSize = Marshal.SizeOf(typeof(OpenFileName));
        public IntPtr hwndOwner = IntPtr.Zero;
        public IntPtr hInstance = IntPtr.Zero;
        public string lpstrFilter = null;
        public IntPtr lpstrCustomFilter = IntPtr.Zero;
        public int nMaxCustFilter = 0;
        public int nFilterIndex = 0;
        public IntPtr lpstrFile = IntPtr.Zero;
        public int nMaxFile = 0;
        public IntPtr lpstrFileTitle = IntPtr.Zero;
        public int nMaxFileTitle = 0;
        public string lpstrInitialDir = null;
        public string lpstrTitle = null;
        public int Flags = 0;
        public short nFileOffset = 0;
        public short nFileExtension = 0;
        public string lpstrDefExt = null;
        public IntPtr lCustData = IntPtr.Zero;
        public IntPtr lpfnHook = IntPtr.Zero;
        public string lpTemplateName = null;
        public IntPtr pvReserved = IntPtr.Zero;
        public int dwReserved = 0;
        public int FlagsEx = 0;
    }

    // ---------- 配置 ----------
    public class Config
    {
        public bool enabled = true;
        public int volume = 100;
        public bool keyTrigger = true;
        public bool onlyInLevel = false;
        public string defaultSound = null;
        public string bindingsFlat = "";   // "D=xx.wav|Space=yy.wav"
        public string language = "zh";     // zh / en / ko
    }

    public static class Main
    {
        public static Config cfg = new Config();

        private static UnityModManager.ModEntry entry;
        private static string modPath;

        private static GameObject host;
        private static AudioSource src;
        private static bool audioReady = false;
        private static bool clipsLoaded = false;
        private static string clipState = "";

        private static List<string> soundFiles = new List<string>();
        private static Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        private static Dictionary<string, string> bindings = new Dictionary<string, string>();
        private static KeyCode[] allKeys;
        private static string captureTarget = null;

        private static int lastPlayedFrame = -1;
        private static int triggerCount = 0;
        private static long heartbeat = 0;
        private static string lastPlayedInfo = "";

        // ================= 多语言 =================
        // 复合键 "lang.key" -> 文本
        private static readonly Dictionary<string, string> I18N = new Dictionary<string, string>
        {
            // ---- 简体中文 ----
            ["zh.lang"] = "简体中文",
            ["zh.enable"] = "启用音效",
            ["zh.volume"] = "音量：{0}%",
            ["zh.keyTrigger"] = "全键盘触发",
            ["zh.onlyInLevel"] = "只在关卡内响",
            ["zh.default"] = "默认音效（未绑定的键播这个）：",
            ["zh.bindTitle"] = "按键绑定：",
            ["zh.capture"] = ">> 正在为「{0}」捕获按键，按任意键… <<",
            ["zh.unbound"] = "未绑定",
            ["zh.bind"] = "绑定",
            ["zh.clear"] = "清除",
            ["zh.add"] = "添加音频",
            ["zh.refresh"] = "刷新列表",
            ["zh.added"] = "已添加: {0}",
            ["zh.addFail"] = "添加失败: {0}",
            ["zh.addNotWav"] = "不是 16-bit PCM WAV，已拒绝",
            ["zh.diag"] = "—— 诊断 ——",
            ["zh.heartbeat"] = "按键心跳: {0}",
            ["zh.alive"] = "活着",
            ["zh.dead"] = "没在跑",
            ["zh.ready"] = "音频就绪: {0}",
            ["zh.readyYes"] = "✓",
            ["zh.readyNo"] = "按一下键初始化",
            ["zh.clips"] = "音效加载: {0}",
            ["zh.triggered"] = "已触发: {0} 次",
            ["zh.last"] = "最近播放: {0}",
            ["zh.none"] = "（无）",
            ["zh.wavFilter"] = "WAV 音频",
            ["zh.langLabel"] = "语言",
            // ---- English ----
            ["en.lang"] = "English",
            ["en.enable"] = "Enable SFX",
            ["en.volume"] = "Volume: {0}%",
            ["en.keyTrigger"] = "Keyboard trigger",
            ["en.onlyInLevel"] = "Only in level",
            ["en.default"] = "Default sound (for unbound keys):",
            ["en.bindTitle"] = "Key bindings:",
            ["en.capture"] = ">> Capturing for '{0}', press any key… <<",
            ["en.unbound"] = "unbound",
            ["en.bind"] = "Bind",
            ["en.clear"] = "Clear",
            ["en.add"] = "Add audio",
            ["en.refresh"] = "Refresh",
            ["en.added"] = "Added: {0}",
            ["en.addFail"] = "Failed: {0}",
            ["en.addNotWav"] = "Not a 16-bit PCM WAV, rejected",
            ["en.diag"] = "—— Diagnostics ——",
            ["en.heartbeat"] = "Heartbeat: {0}",
            ["en.alive"] = "alive",
            ["en.dead"] = "not running",
            ["en.ready"] = "Audio ready: {0}",
            ["en.readyYes"] = "✓",
            ["en.readyNo"] = "press a key to init",
            ["en.clips"] = "Clips: {0}",
            ["en.triggered"] = "Triggered: {0}",
            ["en.last"] = "Last: {0}",
            ["en.none"] = "(none)",
            ["en.wavFilter"] = "WAV audio",
            ["en.langLabel"] = "Language",
            // ---- 한국어 ----
            ["ko.lang"] = "한국어",
            ["ko.enable"] = "효과음 활성화",
            ["ko.volume"] = "볼륨: {0}%",
            ["ko.keyTrigger"] = "전체 키보드 트리거",
            ["ko.onlyInLevel"] = "레벨에서만 재생",
            ["ko.default"] = "기본 효과음 (바인딩 안 된 키):",
            ["ko.bindTitle"] = "키 바인딩:",
            ["ko.capture"] = ">> '{0}' 에 키를 누르세요… <<",
            ["ko.unbound"] = "없음",
            ["ko.bind"] = "바인딩",
            ["ko.clear"] = "해제",
            ["ko.add"] = "오디오 추가",
            ["ko.refresh"] = "새로고침",
            ["ko.added"] = "추가됨: {0}",
            ["ko.addFail"] = "실패: {0}",
            ["ko.addNotWav"] = "16-bit PCM WAV 가 아닙니다",
            ["ko.diag"] = "—— 진단 ——",
            ["ko.heartbeat"] = "하트비트: {0}",
            ["ko.alive"] = "작동 중",
            ["ko.dead"] = "정지됨",
            ["ko.ready"] = "오디오 준비: {0}",
            ["ko.readyYes"] = "✓",
            ["ko.readyNo"] = "아무 키나 누르면 초기화",
            ["ko.clips"] = "클립: {0}",
            ["ko.triggered"] = "트리거: {0}회",
            ["ko.last"] = "최근: {0}",
            ["ko.none"] = "(없음)"
            ,
            ["ko.wavFilter"] = "WAV 오디오",
            ["ko.langLabel"] = "언어"
        };

        private static string[] LangCodes = { "zh", "en", "ko" };
        private static string LangName(string code) { return I18N.ContainsKey(code + ".lang") ? I18N[code + ".lang"] : code; }

        private static string T(string key) { return T(key, null); }
        private static string T(string key, object arg)
        {
            string full = cfg.language + "." + key;
            string s;
            if (I18N.TryGetValue(full, out s)) { return arg == null ? s : string.Format(s, arg); }
            // 回退到中文
            if (I18N.TryGetValue("zh." + key, out s)) { return arg == null ? s : string.Format(s, arg); }
            return key;
        }

        // ================= UMM 入口 =================
        public static bool Load(UnityModManager.ModEntry mod)
        {
            entry = mod;
            modPath = mod.Path;
            LoadConfig();
            ParseBindings();

            var keys = new List<KeyCode>();
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
            {
                string n = k.ToString();
                if (k == KeyCode.None || n.StartsWith("Mouse")) continue;
                keys.Add(k);
            }
            allKeys = keys.ToArray();

            GameObject keyGo = new GameObject("BingSfx_KeyHook");
            UnityEngine.Object.DontDestroyOnLoad(keyGo);
            keyGo.AddComponent<KeyHook>();

            RefreshSoundList();

            mod.OnGUI = OnGUI;
            mod.OnSaveGUI = OnSaveGUI;

            mod.Logger.Log("BingSfx v1.0.0 by wos111(机人) loaded. sounds=" + soundFiles.Count + " bindings=" + bindings.Count + " lang=" + cfg.language);
            return true;
        }

        internal static void Heartbeat() { heartbeat++; }

        internal static KeyCode[] AllKeys { get { return allKeys; } }
        internal static bool TryGetBinding(string key, out string file)
        {
            return bindings.TryGetValue(key, out file);
        }

        // ================= 延迟初始化 =================
        private static void EnsureAudio()
        {
            if (audioReady) return;
            try
            {
                if (host == null)
                {
                    host = new GameObject("BingSfx_Host");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                }
                if (src == null)
                {
                    src = host.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.volume = cfg.volume / 100f;
                    src.spatialBlend = 0f;
                    src.ignoreListenerPause = true;
                }
                if (!clipsLoaded)
                {
                    foreach (var f in soundFiles)
                    {
                        string path = Path.Combine(modPath, "audio", f);
                        if (!File.Exists(path)) path = Path.Combine(modPath, f);
                        try
                        {
                            AudioClip c = LoadWav(path);
                            if (c != null) clipCache[f] = c;
                            else entry.Logger.Error("Parse failed (16-bit PCM WAV required): " + f);
                        }
                        catch (Exception e) { entry.Logger.Error("Read failed " + f + " : " + e.Message); }
                    }
                    clipsLoaded = true;
                    clipState = clipCache.Count + "/" + soundFiles.Count;
                    entry.Logger.Log("Clips loaded: " + clipState);
                }
                audioReady = (src != null);
            }
            catch (Exception e)
            {
                entry.Logger.Error("Audio init failed: " + e);
            }
        }

        // ================= 触发 =================
        internal static void TryPlayIce(string file)
        {
            if (!cfg.enabled) return;
            if (Time.frameCount == lastPlayedFrame) return;
            EnsureAudio();
            if (src == null) return;
            if (cfg.onlyInLevel && !InLevel()) return;

            AudioClip clip = null;
            if (file != null) clipCache.TryGetValue(file, out clip);
            // 一个音效都没有时 defaultSound 为 null，直接查会抛 ArgumentNullException
            if (clip == null && cfg.defaultSound != null) clipCache.TryGetValue(cfg.defaultSound, out clip);
            if (clip == null) return;

            lastPlayedFrame = Time.frameCount;
            src.PlayOneShot(clip);
            triggerCount++;
            lastPlayedInfo = file ?? "";
        }

        private static bool InLevel()
        {
            try { return SceneManager.GetActiveScene().name == "scnGame"; }
            catch { return true; }
        }

        // ================= 音效列表 =================
        private static void RefreshSoundList()
        {
            soundFiles.Clear();
            string audioDir = Path.Combine(modPath, "audio");
            if (Directory.Exists(audioDir))
            {
                foreach (var f in Directory.GetFiles(audioDir, "*.wav"))
                    soundFiles.Add(Path.GetFileName(f));
            }
            foreach (var f in Directory.GetFiles(modPath, "*.wav"))
            {
                string n = Path.GetFileName(f);
                if (!soundFiles.Contains(n)) soundFiles.Add(n);
            }
            soundFiles.Sort(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(cfg.defaultSound) || !soundFiles.Contains(cfg.defaultSound))
                cfg.defaultSound = soundFiles.Count > 0 ? soundFiles[0] : null;
        }

        // ================= 添加音频（Win32 文件框） =================
        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetOpenFileNameW([In, Out] OpenFileName ofn);

        private static void AddAudioViaFileDialog()
        {
            try
            {
                var ofn = new OpenFileName();
                ofn.lpstrFilter = T("wavFilter") + "\0*.wav\0";
                // nMaxFile 的单位是「字符」(UTF-16)，缓冲区要按字节 *2，否则长路径会写越界
                ofn.lpstrFile = Marshal.AllocHGlobal(1024 * 2);
                // AllocHGlobal 不清零，必须先写入空串，否则对话框里初始路径是随机内存
                Marshal.WriteInt16(ofn.lpstrFile, 0);
                ofn.nMaxFile = 1024;
                ofn.lpstrInitialDir = Path.Combine(modPath, "audio");
                ofn.lpstrTitle = T("add");
                ofn.Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000004; // EXPLORER|FILEMUSTEXIST|PATHMUSTEXIST|HIDEREADONLY
                ofn.lpstrDefExt = "wav";

                bool ok = GetOpenFileNameW(ofn);
                string chosen = ok ? Marshal.PtrToStringUni(ofn.lpstrFile) : null;
                Marshal.FreeHGlobal(ofn.lpstrFile);

                if (!ok || string.IsNullOrEmpty(chosen)) return; // 用户取消

                // 快速校验 16-bit PCM WAV
                byte[] head = new byte[64];
                using (var fs = File.OpenRead(chosen))
                {
                    if (fs.Length < 44) { lastPlayedInfo = T("addNotWav"); return; }
                    int rd = fs.Read(head, 0, 64);
                    if (Encoding.ASCII.GetString(head, 0, 4) != "RIFF" ||
                        Encoding.ASCII.GetString(head, 8, 4) != "WAVE" ||
                        head[20] != 1 || BitConverter.ToUInt16(head, 34) != 16)
                    { lastPlayedInfo = T("addNotWav"); return; }
                }

                // 复制进 audio\（重名自动加序号）
                string destName = Path.GetFileName(chosen);
                string dest = Path.Combine(modPath, "audio", destName);
                int seq = 1;
                while (File.Exists(dest))
                {
                    string ext = Path.GetExtension(destName);
                    string baseName = Path.GetFileNameWithoutExtension(destName);
                    dest = Path.Combine(modPath, "audio", baseName + "_" + seq + ext);
                    seq++;
                }
                File.Copy(chosen, dest);

                RefreshSoundList();
                clipCache.Clear();
                clipsLoaded = false;
                audioReady = false;
                lastPlayedInfo = string.Format(T("added"), Path.GetFileName(dest));
                entry.Logger.Log("Audio added: " + Path.GetFileName(dest));
            }
            catch (Exception e)
            {
                lastPlayedInfo = string.Format(T("addFail"), e.Message);
                entry.Logger.Error("AddAudio failed: " + e);
            }
        }

        // ================= WAV 解析 =================
        private static AudioClip LoadWav(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 44 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF") return null;

            int pos = 12;
            int format = 0, rate = 0, bits = 0, channels = 0, dataLen = 0, dataPos = -1;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                int size = BitConverter.ToInt32(b, pos + 4);
                if (id == "fmt ")
                {
                    // fmt: +8 wFormatTag(2B) +10 nChannels(2B) +12 nSamplesPerSec(4B) +16 nAvgBytesPerSec(4B) +20 nBlockAlign(2B) +22 wBitsPerSample(2B)
                    format = b[pos + 8];
                    channels = BitConverter.ToUInt16(b, pos + 10);
                    rate = BitConverter.ToInt32(b, pos + 12);
                    bits = BitConverter.ToUInt16(b, pos + 22);
                }
                else if (id == "data") { dataLen = size; dataPos = pos + 8; }
                pos += 8 + size + (size & 1);
            }
            if (format != 1 || bits != 16 || dataPos < 0 || dataLen <= 0) return null;
            if (channels < 1 || channels > 2) return null;

            int frameCount = dataLen / 2 / channels;
            if (frameCount <= 0) return null;

            AudioClip ac = AudioClip.Create("bing", frameCount, channels, rate, false);
            int total = frameCount * channels;
            float[] d = new float[total];
            for (int i = 0; i < total; i++)
                d[i] = BitConverter.ToInt16(b, dataPos + i * 2) / 32768f;
            ac.SetData(d, 0);
            return ac;
        }

        // ================= 设置面板 =================
        private static void OnGUI(UnityModManager.ModEntry mod)
        {
            // 按键捕获
            if (captureTarget != null && Event.current != null &&
                Event.current.type == EventType.KeyDown && Event.current.keyCode != KeyCode.None)
            {
                bindings[Event.current.keyCode.ToString()] = captureTarget;
                SaveBindings();
                lastPlayedInfo = Event.current.keyCode + " → " + captureTarget;
                captureTarget = null;
                Event.current.Use();
            }

            GUILayout.BeginVertical();

            // 语言切换（放最上面）
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("langLabel"), GUILayout.Width(70));
            int langIdx = Array.IndexOf(LangCodes, cfg.language);
            if (langIdx < 0) langIdx = 0;
            int newLang = GUILayout.SelectionGrid(langIdx, new string[] { LangName("zh"), LangName("en"), LangName("ko") }, 3);
            if (newLang != langIdx && newLang >= 0 && newLang < LangCodes.Length)
            {
                cfg.language = LangCodes[newLang];
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            cfg.enabled = GUILayout.Toggle(cfg.enabled, T("enable"));

            GUILayout.Space(4);
            GUILayout.Label(string.Format(T("volume"), cfg.volume));
            int v = (int)GUILayout.HorizontalSlider(cfg.volume, 0, 100);
            if (v != cfg.volume) { cfg.volume = v; if (src != null) src.volume = v / 100f; }

            GUILayout.Space(4);
            cfg.keyTrigger = GUILayout.Toggle(cfg.keyTrigger, T("keyTrigger"));
            cfg.onlyInLevel = GUILayout.Toggle(cfg.onlyInLevel, T("onlyInLevel"));

            GUILayout.Space(6);
            GUILayout.Label(T("default"));
            if (soundFiles.Count > 0)
            {
                int defIdx = Math.Max(0, soundFiles.IndexOf(cfg.defaultSound));
                int sel = GUILayout.SelectionGrid(defIdx, soundFiles.ToArray(), 1);
                if (sel != defIdx) { cfg.defaultSound = soundFiles[sel]; }
            }

            GUILayout.Space(6);
            GUILayout.Label(T("bindTitle"));
            if (captureTarget != null)
                GUILayout.Label(string.Format(T("capture"), captureTarget));
            foreach (var f in soundFiles)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(f, GUILayout.MinWidth(120), GUILayout.ExpandWidth(true));
                string bound = FindBinding(f);
                GUILayout.Label(bound ?? T("unbound"), GUILayout.Width(70));
                if (GUILayout.Button(T("bind"), GUILayout.Width(55)))
                {
                    captureTarget = f;
                }
                if (bound != null && GUILayout.Button(T("clear"), GUILayout.Width(50)))
                {
                    RemoveBinding(f);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("add")))
            {
                AddAudioViaFileDialog();
            }
            if (GUILayout.Button(T("refresh")))
            {
                RefreshSoundList();
                clipCache.Clear();
                clipsLoaded = false;
                audioReady = false;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label(T("diag"));
            GUILayout.Label(string.Format(T("heartbeat"), heartbeat) + (heartbeat > 0 ? "  ✓ " + T("alive") : "  ✗ " + T("dead")));
            GUILayout.Label(string.Format(T("ready"), audioReady ? T("readyYes") : T("readyNo")));
            GUILayout.Label(string.Format(T("clips"), clipState));
            GUILayout.Label(string.Format(T("triggered"), triggerCount));
            GUILayout.Label(string.Format(T("last"), string.IsNullOrEmpty(lastPlayedInfo) ? T("none") : lastPlayedInfo));

            GUILayout.EndVertical();
            SaveConfig();
        }

        private static string FindBinding(string file)
        {
            foreach (var kv in bindings)
                if (kv.Value == file) return kv.Key;
            return null;
        }
        private static void RemoveBinding(string file)
        {
            string key = FindBinding(file);
            if (key != null) bindings.Remove(key);
            SaveBindings();
        }
        private static void SaveBindings()
        {
            var parts = new List<string>();
            foreach (var kv in bindings) parts.Add(kv.Key + "=" + kv.Value);
            cfg.bindingsFlat = string.Join("|", parts.ToArray());
        }

        private static void OnSaveGUI(UnityModManager.ModEntry mod) { SaveBindings(); SaveConfig(); }

        // ================= 配置读写 =================
        private static string ConfigPath { get { return Path.Combine(modPath, "config.json"); } }

        private static void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                cfg.enabled = GetBool(json, "enabled", cfg.enabled);
                cfg.volume = GetInt(json, "volume", cfg.volume);
                cfg.keyTrigger = GetBool(json, "keyTrigger", cfg.keyTrigger);
                cfg.onlyInLevel = GetBool(json, "onlyInLevel", cfg.onlyInLevel);
                cfg.defaultSound = GetStr(json, "defaultSound", cfg.defaultSound);
                cfg.bindingsFlat = GetStr(json, "bindings", "");
                cfg.language = GetStr(json, "language", "zh");
                if (cfg.language != "en" && cfg.language != "ko") cfg.language = "zh";
            }
            catch { }
        }

        private static void ParseBindings()
        {
            bindings.Clear();
            if (string.IsNullOrEmpty(cfg.bindingsFlat)) return;
            foreach (var item in cfg.bindingsFlat.Split('|'))
            {
                int eq = item.IndexOf('=');
                if (eq <= 0 || eq >= item.Length - 1) continue;
                bindings[item.Substring(0, eq)] = item.Substring(eq + 1);
            }
        }

        private static string lastSavedJson = null;

        private static void SaveConfig()
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\n");
                sb.Append("  \"enabled\": ").Append(cfg.enabled ? "true" : "false").Append(",\n");
                sb.Append("  \"volume\": ").Append(cfg.volume).Append(",\n");
                sb.Append("  \"keyTrigger\": ").Append(cfg.keyTrigger ? "true" : "false").Append(",\n");
                sb.Append("  \"onlyInLevel\": ").Append(cfg.onlyInLevel ? "true" : "false").Append(",\n");
                sb.Append("  \"defaultSound\": \"").Append(Esc(cfg.defaultSound)).Append("\",\n");
                sb.Append("  \"bindings\": \"").Append(Esc(cfg.bindingsFlat)).Append("\",\n");
                sb.Append("  \"language\": \"").Append(Esc(cfg.language)).Append("\"\n");
                sb.Append("}");
                string json = sb.ToString();
                if (json == lastSavedJson) return;
                lastSavedJson = json;
                File.WriteAllText(ConfigPath, json, Encoding.UTF8);
            }
            catch { }
        }

        private static string Esc(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool GetBool(string json, string key, bool def)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)");
            return m.Success ? m.Groups[1].Value == "true" : def;
        }
        private static int GetInt(string json, string key, int def)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : def;
        }
        private static string GetStr(string json, string key, string def)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : def;
        }
    }

    // 全键盘按键钩子
    public class KeyHook : MonoBehaviour
    {
        private void Update()
        {
            BingSfx.Main.Heartbeat();
            if (!BingSfx.Main.cfg.keyTrigger || !BingSfx.Main.cfg.enabled) return;

            foreach (KeyCode k in BingSfx.Main.AllKeys)
            {
                if (Input.GetKeyDown(k))
                {
                    string file;
                    if (!BingSfx.Main.TryGetBinding(k.ToString(), out file))
                        file = BingSfx.Main.cfg.defaultSound;
                    BingSfx.Main.TryPlayIce(file);
                    break;
                }
            }
        }
    }
}
