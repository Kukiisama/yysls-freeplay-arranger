using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace YyslsAutoPlayer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--selftest")
            {
                Environment.Exit(SelfTest.Run());
                return;
            }

            if (args.Length > 0 && args[0] == "--uicheck")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    MainForm form = new MainForm(null);
                    form.CreateControl();
                    form.Dispose();
                    FilePickerForm picker = new FilePickerForm(Application.StartupPath);
                    picker.CreateControl();
                    picker.Dispose();
                    Console.WriteLine("UI_OK");
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("UI_FAIL: " + ex);
                    Environment.Exit(1);
                }
                return;
            }

            if (args.Length > 0 && args[0] == "--uipreview")
            {
                NativeMethods.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                string outPath = args.Length > 1 ? args[1] : "uipreview.png";
                string temp = Path.Combine(Path.GetTempPath(), "yysls-ui-preview.txt");
                File.WriteAllText(temp,
                    "# 预览用示例\r\nKEY Q 250\r\nKEY W 250\r\nKEY Shift+E 250\r\n250ms\r\n" +
                    "KEY Ctrl+U 500\r\nKEY A 250\r\nKEY S 250\r\nKEY D 250\r\nKEY F 500\r\n",
                    Encoding.UTF8);
                MainForm form = new MainForm(temp);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-3000, -3000);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                form.Refresh();
                form.PreviewHotkeyRegistered();
                Bitmap bmp = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
                bmp.Dispose();
                form.Dispose();

                FilePickerForm picker = new FilePickerForm(Application.StartupPath);
                picker.StartPosition = FormStartPosition.Manual;
                picker.Location = new Point(-3000, -3000);
                picker.ShowInTaskbar = false;
                picker.Show();
                Application.DoEvents();
                picker.Refresh();
                string pickerPath = Path.Combine(Path.GetDirectoryName(outPath), "选择器预览.png");
                Bitmap pb = new Bitmap(picker.Width, picker.Height);
                picker.DrawToBitmap(pb, new Rectangle(0, 0, picker.Width, picker.Height));
                pb.Save(pickerPath, System.Drawing.Imaging.ImageFormat.Png);
                pb.Dispose();
                picker.Dispose();

                Console.WriteLine("PREVIEW_OK " + outPath);
                Environment.Exit(0);
                return;
            }

            NativeMethods.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                CrashLog.Write(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                CrashLog.Write(e.ExceptionObject as Exception);
            };
            string initialFile = args.Length > 0 ? args[0] : null;
            Application.Run(new MainForm(initialFile));
        }
    }

    /* =====================================================================
     * Win32
     * ===================================================================== */
    internal static class NativeMethods
    {
        public const int WM_HOTKEY = 0x0312;
        public const int MOD_NOREPEAT = 0x4000;

        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint KEYEVENTF_SCANCODE = 0x0008;
        public const uint MAPVK_VK_TO_VSC = 0;

        public const ushort ScanShift = 0x2A;
        public const ushort ScanCtrl = 0x1D;
        public const ushort ScanAlt = 0x38;
        public const ushort VkLShift = 0xA0;
        public const ushort VkLCtrl = 0xA2;
        public const ushort VkLAlt = 0xA4;

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("winmm.dll")]
        public static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        public static extern uint timeEndPeriod(uint uPeriod);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();
    }

    /* =====================================================================
     * 命令模型与解析
     * ===================================================================== */
    internal enum CommandType
    {
        Key,
        Wait
    }

    internal sealed class ComboKey
    {
        public bool Shift;
        public bool Ctrl;
        public bool Alt;
        public Keys KeyCode;
        public ushort ScanCode;
        public string Display;
    }

    internal sealed class Command
    {
        public CommandType Type;
        public ComboKey Key;
        public int DurationMs;
        public int LineNumber;
        public string Raw;
    }

    internal sealed class ParseResult
    {
        public readonly List<Command> Commands = new List<Command>();
        public readonly List<string> Errors = new List<string>();
        public int KeyCount;
        public int WaitCount;
        public double TotalMs;

        public bool HasKeys
        {
            get { return KeyCount > 0; }
        }

        public bool Ok
        {
            get { return Errors.Count == 0 && KeyCount > 0; }
        }
    }

    internal static class CommandParser
    {
        public static ParseResult Parse(string text)
        {
            ParseResult result = new ParseResult();
            if (string.IsNullOrEmpty(text))
            {
                result.Errors.Add("还没有命令：请从编排器「命令」页复制后粘贴/导入");
                return result;
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#", StringComparison.Ordinal) ||
                    line.StartsWith("//", StringComparison.Ordinal)) continue;

                int hash = line.IndexOf('#');
                if (hash > 0) line = line.Substring(0, hash).Trim();
                if (line.Length == 0) continue;
                if (line.Length > 2000)
                {
                    AddError(result, i, "这一行太长（超过 2000 字符），可能不是命令文本");
                    continue;
                }

                if (line.StartsWith("KEY", StringComparison.OrdinalIgnoreCase))
                {
                    string rest = line.Substring(3).Trim();
                    if (rest.Length == 0)
                    {
                        AddError(result, i, "KEY 后面缺少按键和按住时长");
                        continue;
                    }

                    Match m = Regex.Match(rest, @"^(.*?)\s+(\d+)\s*(?:ms)?$", RegexOptions.IgnoreCase);
                    if (!m.Success)
                    {
                        AddError(result, i, "格式应为 KEY 按键 按住ms，例如 KEY Shift+Q 250");
                        continue;
                    }

                    string comboText = m.Groups[1].Value.Trim();
                    int hold;
                    if (!int.TryParse(m.Groups[2].Value, out hold) || hold < 1 || hold > 60000)
                    {
                        AddError(result, i, "按住时长需在 1–60000ms 之间");
                        continue;
                    }

                    ComboKey key;
                    string err;
                    if (!TryParseCombo(comboText, out key, out err))
                    {
                        AddError(result, i, err);
                        continue;
                    }

                    Command cmd = new Command();
                    cmd.Type = CommandType.Key;
                    cmd.Key = key;
                    cmd.DurationMs = hold;
                    cmd.LineNumber = i + 1;
                    cmd.Raw = line;
                    result.Commands.Add(cmd);
                    result.KeyCount++;
                    result.TotalMs += hold;
                }
                else
                {
                    Match m = Regex.Match(line, @"^(\d+)\s*(?:ms)?$", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        int wait;
                        if (!int.TryParse(m.Groups[1].Value, out wait) || wait < 0 || wait > 600000)
                        {
                            AddError(result, i, "等待时长需在 0–600000ms 之间");
                            continue;
                        }

                        Command cmd = new Command();
                        cmd.Type = CommandType.Wait;
                        cmd.DurationMs = wait;
                        cmd.LineNumber = i + 1;
                        cmd.Raw = line;
                        result.Commands.Add(cmd);
                        result.WaitCount++;
                        result.TotalMs += wait;
                    }
                    else
                    {
                        AddError(result, i, "无法识别这一行（应为 KEY ... 或 250ms）");
                    }
                }
            }

            if (result.KeyCount == 0 && result.Errors.Count == 0)
            {
                result.Errors.Add("没有找到 KEY 行：请在编排器切到「命令」页，点「复制命令」后再导入");
            }
            return result;
        }

        private static void AddError(ParseResult result, int zeroBasedLine, string message)
        {
            result.Errors.Add(string.Format("第 {0} 行：{1}", zeroBasedLine + 1, message));
        }

        private static bool TryParseCombo(string text, out ComboKey key, out string error)
        {
            key = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "缺少按键";
                return false;
            }

            string s = text.Trim();
            s = s.Replace("↑", "Shift+").Replace("↓", "Ctrl+")
                 .Replace("⇧", "Shift+").Replace("⌃", "Ctrl+");
            s = Regex.Replace(s, @"\s*\+\s*", "+");

            string[] parts = s.Split('+');
            bool shift = false, ctrl = false, alt = false;
            string basePart = null;

            foreach (string rawPart in parts)
            {
                string p = rawPart.Trim();
                if (p.Length == 0)
                {
                    error = "按键里有多余的 +";
                    return false;
                }

                string u = p.ToUpperInvariant();
                if (u == "SHIFT" || u == "LSHIFT" || u == "L-SHIFT" || u == "左SHIFT")
                {
                    if (shift) { error = "重复的 Shift"; return false; }
                    shift = true;
                }
                else if (u == "CTRL" || u == "CONTROL" || u == "LCTRL" || u == "L-CTRL" || u == "左CTRL")
                {
                    if (ctrl) { error = "重复的 Ctrl"; return false; }
                    ctrl = true;
                }
                else if (u == "ALT" || u == "LALT" || u == "左ALT")
                {
                    if (alt) { error = "重复的 Alt"; return false; }
                    alt = true;
                }
                else
                {
                    if (basePart != null)
                    {
                        error = "一个 KEY 只能有一个主键";
                        return false;
                    }
                    basePart = p;
                }
            }

            if (basePart == null)
            {
                error = "缺少主键";
                return false;
            }

            Keys keyCode;
            ushort scan;
            if (!TryParseKey(basePart, out keyCode, out scan, out error)) return false;

            key = new ComboKey();
            key.Shift = shift;
            key.Ctrl = ctrl;
            key.Alt = alt;
            key.KeyCode = keyCode;
            key.ScanCode = scan;
            key.Display = BuildDisplay(shift, ctrl, alt, keyCode);
            return true;
        }

        private static bool TryParseKey(string part, out Keys keyCode, out ushort scan, out string error)
        {
            keyCode = Keys.None;
            scan = 0;
            error = null;

            string p = part.Trim();
            if (p.Length == 1)
            {
                char c = char.ToUpperInvariant(p[0]);
                if (c >= 'A' && c <= 'Z')
                {
                    keyCode = (Keys)c;
                }
                else if (c >= '0' && c <= '9')
                {
                    keyCode = (Keys)((int)Keys.D0 + (c - '0'));
                }
                else
                {
                    error = "不支持这个键：" + p;
                    return false;
                }
            }
            else
            {
                int num;
                if ((p[0] == 'F' || p[0] == 'f') && int.TryParse(p.Substring(1), out num) && num >= 1 && num <= 12)
                {
                    keyCode = (Keys)((int)Keys.F1 + num - 1);
                }
                else
                {
                    error = "不支持这个键：" + p;
                    return false;
                }
            }

            scan = (ushort)NativeMethods.MapVirtualKey((uint)keyCode, NativeMethods.MAPVK_VK_TO_VSC);
            if (scan == 0)
            {
                error = "无法取得这个键的扫描码：" + p;
                return false;
            }
            return true;
        }

        private static string BuildDisplay(bool shift, bool ctrl, bool alt, Keys keyCode)
        {
            string name;
            if (keyCode >= Keys.A && keyCode <= Keys.Z) name = ((char)keyCode).ToString();
            else if (keyCode >= Keys.D0 && keyCode <= Keys.D9) name = ((char)keyCode).ToString();
            else name = keyCode.ToString();

            return (ctrl ? "Ctrl+" : "") + (alt ? "Alt+" : "") + (shift ? "Shift+" : "") + name;
        }
    }

    /* =====================================================================
     * 播放引擎
     * ===================================================================== */
    internal sealed class ForegroundLostException : Exception
    {
    }

    internal sealed class InputBlockedException : Exception
    {
    }

    internal sealed class Player
    {
        public event Action<string> StatusChanged;
        public event Action<int, int, double> ProgressChanged;
        public event Action<string, bool> Finished;

        private Thread _thread;
        private volatile bool _stop;
        private bool _foregroundOnly;
        private bool _useScanCodes = true;
        private IntPtr _target;
        private uint _targetPid;
        private int _warmupMs;
        private List<Command> _commands;
        private readonly object _sendLock = new object();
        private readonly List<ushort> _downKeys = new List<ushort>();
        private static readonly int InputSize = Marshal.SizeOf(typeof(NativeMethods.INPUT));

        public bool IsPlaying
        {
            get
            {
                Thread t = _thread;
                return t != null && t.IsAlive;
            }
        }

        public bool Start(List<Command> commands, int warmupMs, bool foregroundOnly, bool useScanCodes)
        {
            if (IsPlaying) return false;
            if (commands == null || commands.Count == 0) return false;

            _commands = commands;
            _warmupMs = Math.Max(0, warmupMs);
            _foregroundOnly = foregroundOnly;
            _useScanCodes = useScanCodes;
            _stop = false;

            _target = NativeMethods.GetForegroundWindow();
            _targetPid = GetPid(_target);
            if (_target == IntPtr.Zero)
            {
                EmitStatus("没有找到前台窗口，请先切到游戏");
                return false;
            }
            if (_targetPid == (uint)Process.GetCurrentProcess().Id)
            {
                EmitStatus("当前前台是本工具：请先切到游戏，再按 F8");
                return false;
            }
            if (IsShellProcess(_targetPid))
            {
                EmitStatus("当前前台是桌面/任务栏：请先切到游戏，再按 F8");
                return false;
            }

            _downKeys.Clear();
            _thread = new Thread(Run);
            _thread.IsBackground = true;
            _thread.Start();
            return true;
        }

        public void Stop()
        {
            _stop = true;
        }

        public void Shutdown()
        {
            _stop = true;
            Thread t = _thread;
            if (t != null) t.Join(900);
        }

        private void Run()
        {
            NativeMethods.timeBeginPeriod(1);
            Stopwatch sw = Stopwatch.StartNew();
            double t = 0;
            int done = 0;
            string message = "播放完成";
            bool isError = false;

            try
            {
                if (_warmupMs > 0)
                {
                    EmitStatus(string.Format("预热 {0}ms…", _warmupMs));
                    t = _warmupMs;
                    WaitUntil(sw, t);
                }

                for (int i = 0; i < _commands.Count; i++)
                {
                    if (_stop) break;
                    Command c = _commands[i];
                    if (c.Type == CommandType.Wait)
                    {
                        t += c.DurationMs;
                        WaitUntil(sw, t);
                    }
                    else
                    {
                        CheckForeground();
                        lock (_sendLock)
                        {
                            SendDown(c.Key);
                        }
                        t += c.DurationMs;
                        WaitUntil(sw, t);
                        lock (_sendLock)
                        {
                            SendUp(c.Key);
                        }
                    }
                    done = i + 1;
                    EmitProgress(done, _commands.Count, sw.Elapsed.TotalMilliseconds);
                }

                if (_stop) message = "已停止";
                else message = string.Format("播放完成 · {0:0.0} 秒", sw.Elapsed.TotalSeconds);
            }
            catch (ForegroundLostException)
            {
                isError = true;
                message = "前台窗口已切换，已停止（避免把按键发到别的窗口）";
            }
            catch (InputBlockedException)
            {
                isError = true;
                message = "系统拒绝发送按键：请点「以管理员重启」，或右键以管理员身份运行";
            }
            catch (Exception ex)
            {
                isError = true;
                message = "播放出错：" + ex.Message;
            }
            finally
            {
                lock (_sendLock)
                {
                    ReleaseAll();
                }
                NativeMethods.timeEndPeriod(1);
                _thread = null;
                if (Finished != null) Finished(message, isError);
            }
        }

        private bool WaitUntil(Stopwatch sw, double targetMs)
        {
            while (true)
            {
                if (_stop) return false;
                if (_foregroundOnly) CheckForeground();

                double remain = targetMs - sw.Elapsed.TotalMilliseconds;
                if (remain <= 0) return true;
                if (remain > 4) Thread.Sleep((int)Math.Min(remain - 2, 15));
                else Thread.SpinWait(220);
            }
        }

        private void CheckForeground()
        {
            if (!_foregroundOnly) return;
            IntPtr fg = NativeMethods.GetForegroundWindow();
            if (fg == IntPtr.Zero || !NativeMethods.IsWindow(_target))
                throw new ForegroundLostException();
            if (GetPid(fg) != _targetPid)
                throw new ForegroundLostException();
        }

        private void SendDown(ComboKey key)
        {
            List<ushort> values = new List<ushort>();
            if (key.Ctrl) values.Add(_useScanCodes ? NativeMethods.ScanCtrl : NativeMethods.VkLCtrl);
            if (key.Alt) values.Add(_useScanCodes ? NativeMethods.ScanAlt : NativeMethods.VkLAlt);
            if (key.Shift) values.Add(_useScanCodes ? NativeMethods.ScanShift : NativeMethods.VkLShift);
            values.Add(_useScanCodes ? key.ScanCode : (ushort)key.KeyCode);

            _downKeys.AddRange(values);
            SendValues(values, false);
        }

        private void SendUp(ComboKey key)
        {
            List<ushort> values = new List<ushort>();
            values.Add(_useScanCodes ? key.ScanCode : (ushort)key.KeyCode);
            if (key.Shift) values.Add(_useScanCodes ? NativeMethods.ScanShift : NativeMethods.VkLShift);
            if (key.Alt) values.Add(_useScanCodes ? NativeMethods.ScanAlt : NativeMethods.VkLAlt);
            if (key.Ctrl) values.Add(_useScanCodes ? NativeMethods.ScanCtrl : NativeMethods.VkLCtrl);

            SendValues(values, true);
            foreach (ushort v in values) _downKeys.Remove(v);
        }

        private void ReleaseAll()
        {
            if (_downKeys.Count == 0) return;
            for (int i = _downKeys.Count - 1; i >= 0; i--)
            {
                try
                {
                    List<ushort> one = new List<ushort>();
                    one.Add(_downKeys[i]);
                    SendValues(one, true);
                }
                catch
                {
                    // 松开失败也不再重试，避免卡住退出流程
                }
            }
            _downKeys.Clear();
        }

        private void SendValues(List<ushort> values, bool up)
        {
            if (values.Count == 0) return;

            NativeMethods.INPUT[] inputs = new NativeMethods.INPUT[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                NativeMethods.INPUT input = new NativeMethods.INPUT();
                input.type = 1; // INPUT_KEYBOARD
                if (_useScanCodes)
                {
                    input.U.ki.wVk = 0;
                    input.U.ki.wScan = values[i];
                    input.U.ki.dwFlags = NativeMethods.KEYEVENTF_SCANCODE;
                }
                else
                {
                    input.U.ki.wVk = values[i];
                    input.U.ki.wScan = 0;
                    input.U.ki.dwFlags = 0;
                }
                if (up) input.U.ki.dwFlags |= NativeMethods.KEYEVENTF_KEYUP;
                inputs[i] = input;
            }

            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, InputSize);
            if (sent != (uint)inputs.Length) throw new InputBlockedException();
        }

        private static uint GetPid(IntPtr hwnd)
        {
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            return pid;
        }

        private static bool IsShellProcess(uint pid)
        {
            try
            {
                string name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
                return name == "explorer" || name == "searchhost" ||
                       name == "startmenuexperiencehost" || name == "shellexperiencehost";
            }
            catch
            {
                return false;
            }
        }

        private void EmitStatus(string text)
        {
            if (StatusChanged != null) StatusChanged(text);
        }

        private void EmitProgress(int done, int total, double elapsedMs)
        {
            if (ProgressChanged != null) ProgressChanged(done, total, elapsedMs);
        }
    }

    /* =====================================================================
     * 崩溃日志
     * ===================================================================== */
    internal static class CrashLog
    {
        public static void Write(Exception ex)
        {
            Append("ERROR\r\n" + (ex == null ? "(unknown exception)" : ex.ToString()));
        }

        public static void Trace(string message)
        {
            Append("TRACE " + message);
        }

        private static void Append(string text)
        {
            try
            {
                string path = Path.Combine(Application.StartupPath, "crash.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + "\r\n";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    /* =====================================================================
     * 轻量文件选择器
     * 不用 Windows 的 OpenFileDialog / 资源管理器对话框，避免第三方
     * shell 扩展在普通权限下把整个进程带崩。
     * ===================================================================== */
    internal sealed class FilePickerForm : Form
    {
        private static readonly Color PickerBg = Color.FromArgb(11, 14, 19);
        private static readonly Color PickerPanel = Color.FromArgb(15, 19, 26);
        private static readonly Color PickerInput = Color.FromArgb(12, 16, 22);
        private static readonly Color PickerLine = Color.FromArgb(42, 52, 68);
        private static readonly Color PickerText = Color.FromArgb(231, 236, 243);
        private static readonly Color PickerMuted = Color.FromArgb(139, 152, 171);
        private static readonly Color PickerGold = Color.FromArgb(232, 196, 106);

        private sealed class Entry
        {
            public string Name;
            public string FullPath;
            public bool IsDir;

            public override string ToString()
            {
                return (IsDir ? "[文件夹] " : "") + Name;
            }
        }

        private readonly TextBox _folderBox;
        private readonly ListBox _list;
        private readonly TextBox _fileBox;
        private readonly Label _hint;
        private readonly CheckBox _showAll;
        private readonly List<Entry> _entries = new List<Entry>();
        private Button _goButton;
        private Button _upButton;
        private Button _openButton;
        private Button _cancelButton;
        private string _folder = "";
        private bool _navigating;

        public string SelectedPath { get; private set; }

        public string SelectedFolder
        {
            get { return _folder; }
        }

        public FilePickerForm(string startFolder)
        {
            Text = "导入命令文件";
            ClientSize = new Size(780, 540);
            MinimumSize = new Size(640, 440);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = PickerBg;
            ForeColor = PickerText;
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            KeyPreview = true;

            Label folderLabel = new Label();
            folderLabel.Text = "文件夹";
            folderLabel.ForeColor = PickerMuted;
            folderLabel.AutoSize = true;
            folderLabel.Location = new Point(12, 18);

            _folderBox = new TextBox();
            _folderBox.Location = new Point(66, 13);
            _folderBox.Width = 510;
            _folderBox.BackColor = PickerInput;
            _folderBox.ForeColor = PickerText;
            _folderBox.BorderStyle = BorderStyle.FixedSingle;
            _folderBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _folderBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    GoFolder();
                }
            };

            _goButton = MakeButton("转到", 62);
            _goButton.Location = new Point(584, 11);
            _goButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _goButton.Click += delegate { GoFolder(); };

            _upButton = MakeButton("上一级", 74);
            _upButton.Location = new Point(654, 11);
            _upButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _upButton.Click += delegate { GoUp(); };

            Button desktop = MakeButton("桌面", 64);
            desktop.Location = new Point(66, 48);
            desktop.Click += delegate { Navigate(Environment.GetFolderPath(Environment.SpecialFolder.Desktop)); };

            Button downloads = MakeButton("下载", 64);
            downloads.Location = new Point(138, 48);
            downloads.Click += delegate
            {
                Navigate(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            };

            Button docs = MakeButton("文档", 64);
            docs.Location = new Point(210, 48);
            docs.Click += delegate { Navigate(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); };

            Button toolDir = MakeButton("工具目录", 86);
            toolDir.Location = new Point(282, 48);
            toolDir.Click += delegate { Navigate(Application.StartupPath); };

            _showAll = new CheckBox();
            _showAll.Text = "显示所有文件";
            _showAll.ForeColor = PickerMuted;
            _showAll.AutoSize = true;
            _showAll.Location = new Point(382, 50);
            _showAll.CheckedChanged += delegate
            {
                if (!string.IsNullOrEmpty(_folder)) Navigate(_folder);
            };

            _list = new ListBox();
            _list.Location = new Point(12, 86);
            _list.Size = new Size(756, 340);
            _list.BackColor = PickerInput;
            _list.ForeColor = PickerText;
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.Font = new Font("Microsoft YaHei UI", 9.5F);
            _list.IntegralHeight = false;
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _list.DoubleClick += delegate { OpenSelected(); };
            _list.SelectedIndexChanged += delegate
            {
                if (_navigating) return;
                Entry entry = _list.SelectedItem as Entry;
                if (entry != null && !entry.IsDir) _fileBox.Text = entry.Name;
            };

            Label fileLabel = new Label();
            fileLabel.Text = "文件名";
            fileLabel.ForeColor = PickerMuted;
            fileLabel.AutoSize = true;
            fileLabel.Location = new Point(12, 446);
            fileLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            _fileBox = new TextBox();
            _fileBox.Location = new Point(66, 441);
            _fileBox.Width = 510;
            _fileBox.BackColor = PickerInput;
            _fileBox.ForeColor = PickerText;
            _fileBox.BorderStyle = BorderStyle.FixedSingle;
            _fileBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            _openButton = MakeButton("导入", 80);
            _openButton.Location = new Point(584, 439);
            _openButton.Height = 32;
            _openButton.BackColor = PickerGold;
            _openButton.ForeColor = Color.FromArgb(13, 18, 24);
            _openButton.FlatAppearance.BorderColor = PickerGold;
            _openButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _openButton.Click += delegate { OpenSelected(); };

            _cancelButton = MakeButton("取消", 80);
            _cancelButton.Location = new Point(672, 439);
            _cancelButton.Height = 32;
            _cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };

            _hint = new Label();
            _hint.Location = new Point(12, 486);
            _hint.Size = new Size(756, 40);
            _hint.ForeColor = PickerMuted;
            _hint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _hint.Text = "双击文件夹进入，双击文件导入；也可以把完整文件路径粘到「文件名」。";

            Controls.Add(folderLabel);
            Controls.Add(_folderBox);
            Controls.Add(_goButton);
            Controls.Add(_upButton);
            Controls.Add(desktop);
            Controls.Add(downloads);
            Controls.Add(docs);
            Controls.Add(toolDir);
            Controls.Add(_showAll);
            Controls.Add(_list);
            Controls.Add(fileLabel);
            Controls.Add(_fileBox);
            Controls.Add(_openButton);
            Controls.Add(_cancelButton);
            Controls.Add(_hint);

            AcceptButton = _openButton;
            CancelButton = _cancelButton;

            Resize += delegate { LayoutPicker(); };
            LayoutPicker();

            string start = Application.StartupPath;
            if (!string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder)) start = startFolder;
            Navigate(start);
        }

        private static Button MakeButton(string text, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 28;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = PickerLine;
            b.BackColor = Color.FromArgb(29, 38, 52);
            b.ForeColor = Color.FromArgb(217, 227, 240);
            b.Font = new Font("Microsoft YaHei UI", 9F);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            return b;
        }

        private void LayoutPicker()
        {
            int w = ClientSize.Width;
            _upButton.Left = w - 12 - _upButton.Width;
            _goButton.Left = _upButton.Left - 8 - _goButton.Width;
            _folderBox.Width = Math.Max(160, _goButton.Left - 8 - _folderBox.Left);

            _cancelButton.Left = w - 12 - _cancelButton.Width;
            _openButton.Left = _cancelButton.Left - 8 - _openButton.Width;
            _fileBox.Width = Math.Max(160, _openButton.Left - 8 - _fileBox.Left);

            _list.Width = w - 24;
            _list.Height = Math.Max(80, _fileBox.Top - 8 - _list.Top);
            _hint.Width = w - 24;
        }

        private void Navigate(string folder)
        {
            try
            {
                string target = (folder ?? "").Trim().Trim('"');
                if (target.Length == 0 || !Directory.Exists(target))
                {
                    _hint.Text = "文件夹不存在：" + target;
                    return;
                }

                _folder = Path.GetFullPath(target);
                _folderBox.Text = _folder;
                _entries.Clear();

                string[] dirs = Directory.GetDirectories(_folder);
                string[] files = Directory.GetFiles(_folder);
                foreach (string d in dirs)
                {
                    Entry e = new Entry();
                    e.Name = Path.GetFileName(d);
                    e.FullPath = d;
                    e.IsDir = true;
                    _entries.Add(e);
                }
                foreach (string f in files)
                {
                    if (!_showAll.Checked &&
                        !string.Equals(Path.GetExtension(f), ".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    Entry e = new Entry();
                    e.Name = Path.GetFileName(f);
                    e.FullPath = f;
                    e.IsDir = false;
                    _entries.Add(e);
                }
                _entries.Sort(delegate(Entry a, Entry b)
                {
                    if (a.IsDir != b.IsDir) return a.IsDir ? -1 : 1;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });

                _navigating = true;
                _list.BeginUpdate();
                _list.Items.Clear();
                foreach (Entry e in _entries) _list.Items.Add(e);
                _list.EndUpdate();
                _navigating = false;

                _fileBox.Text = "";
                int firstFile = -1;
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (!_entries[i].IsDir) { firstFile = i; break; }
                }
                if (firstFile >= 0) _list.SelectedIndex = firstFile;
                else if (_list.Items.Count > 0) _list.SelectedIndex = 0;

                int dirCount = 0;
                int fileCount = 0;
                foreach (Entry e in _entries)
                {
                    if (e.IsDir) dirCount++;
                    else fileCount++;
                }
                _hint.Text = string.Format("{0} 个文件夹 / {1} 个文件 · 双击文件夹进入，双击文件导入{2}",
                    dirCount, fileCount, _showAll.Checked ? "" : "（默认只显示 .txt）");
            }
            catch (Exception ex)
            {
                _navigating = false;
                _hint.Text = "读取文件夹失败：" + ex.Message;
            }
        }

        private void GoFolder()
        {
            string text = _folderBox.Text.Trim().Trim('"');
            if (File.Exists(text))
            {
                AcceptFile(text);
                return;
            }
            Navigate(text);
        }

        private void GoUp()
        {
            if (string.IsNullOrEmpty(_folder)) return;
            DirectoryInfo parent = Directory.GetParent(_folder);
            if (parent != null) Navigate(parent.FullName);
        }

        private void OpenSelected()
        {
            Entry entry = _list.SelectedItem as Entry;
            if (entry != null)
            {
                if (entry.IsDir)
                {
                    Navigate(entry.FullPath);
                    return;
                }
                AcceptFile(entry.FullPath);
                return;
            }

            string typed = _fileBox.Text.Trim().Trim('"');
            if (typed.Length == 0)
            {
                _hint.Text = "请选择一个文件，或在「文件名」里填完整路径。";
                return;
            }

            string path = typed;
            if (!Path.IsPathRooted(path)) path = Path.Combine(_folder, typed);
            if (Directory.Exists(path))
            {
                Navigate(path);
                return;
            }
            if (File.Exists(path))
            {
                AcceptFile(path);
                return;
            }
            _hint.Text = "找不到这个文件：" + path;
        }

        private void AcceptFile(string path)
        {
            if (!File.Exists(path))
            {
                _hint.Text = "找不到这个文件：" + path;
                return;
            }
            SelectedPath = path;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /* =====================================================================
     * 主界面
     * ===================================================================== */
    internal sealed class MainForm : Form
    {
        private const int HotkeyStart = 0x9001;
        private const int HotkeyStop = 0x9002;

        private static readonly Color Bg = Color.FromArgb(11, 14, 19);
        private static readonly Color PanelBg = Color.FromArgb(15, 19, 26);
        private static readonly Color InputBg = Color.FromArgb(12, 16, 22);
        private static readonly Color Line = Color.FromArgb(42, 52, 68);
        private static readonly Color TextColor = Color.FromArgb(231, 236, 243);
        private static readonly Color Muted = Color.FromArgb(139, 152, 171);
        private static readonly Color Gold = Color.FromArgb(232, 196, 106);
        private static readonly Color Err = Color.FromArgb(255, 154, 166);
        private static readonly Color BtnBg = Color.FromArgb(29, 38, 52);
        private static readonly Color BtnHover = Color.FromArgb(42, 54, 72);
        private static readonly Color DangerBg = Color.FromArgb(58, 29, 34);
        private static readonly Color DangerHover = Color.FromArgb(82, 40, 48);

        private TextBox _txt;
        private Label _lblStatus;
        private Label _lblSummary;
        private Label _lblProgress;
        private ProgressBar _bar;
        private Button _btnStart;
        private Button _btnStop;
        private Button _btnClip;
        private Button _btnFile;
        private Button _btnSample;
        private Button _btnClear;
        private Button _btnAdmin;
        private Button _btnKillOld;
        private Label _lblHotkey;
        private NumericUpDown _numWarmup;
        private ComboBox _cmbMode;
        private CheckBox _chkForeground;
        private CheckBox _chkTray;
        private NotifyIcon _tray;
        private Player _player;
        private ParseResult _parsed = new ParseResult();
        private System.Windows.Forms.Timer _countdown;
        private System.Windows.Forms.Timer _hotkeyTimer;
        private int _countdownLeft;
        private bool _hotkeysRegistered;
        private bool _f8Registered;
        private bool _f9Registered;
        private bool _exiting;
        private bool _closing;
        private bool _inDialog;
        private bool _wasNormalShown;
        private bool _trayTipShown;
        private string _lastFolder = "";

        public MainForm(string initialFile)
        {
            Text = "燕云自动演奏器";
            ClientSize = new Size(900, 720);
            MinimumSize = new Size(780, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = TextColor;
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            BuildUi();
            BuildTray();

            _lastFolder = Application.StartupPath;

            AllowDrop = true;
            DragEnter += OnFileDragEnter;
            DragDrop += OnFileDragDrop;
            _txt.AllowDrop = true;
            _txt.DragEnter += OnFileDragEnter;
            _txt.DragDrop += OnFileDragDrop;

            _player = new Player();
            _player.StatusChanged += OnPlayerStatus;
            _player.ProgressChanged += OnPlayerProgress;
            _player.Finished += OnPlayerFinished;

            LoadSettings();
            if (!string.IsNullOrEmpty(initialFile) && File.Exists(initialFile))
            {
                LoadFile(initialFile);
            }
            else if (File.Exists(LastCommandsPath))
            {
                try { _txt.Text = File.ReadAllText(LastCommandsPath, Encoding.UTF8); }
                catch { }
            }

            ParseNow();
            UpdateAdminButton();
        }

        private string SettingsPath
        {
            get { return Path.Combine(Application.StartupPath, "settings.ini"); }
        }

        private string LastCommandsPath
        {
            get { return Path.Combine(Application.StartupPath, "last-commands.txt"); }
        }

        /* ---------------- 界面 ---------------- */
        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 7;
            root.BackColor = Bg;
            root.Padding = new Padding(16, 10, 16, 10);
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            Controls.Add(root);

            // 标题
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Bg;
            header.Margin = new Padding(0);
            Label title = new Label();
            title.Text = "燕云自动演奏器";
            title.Font = new Font("Microsoft YaHei UI", 17F, FontStyle.Bold);
            title.ForeColor = Gold;
            title.AutoSize = true;
            title.Location = new Point(0, 0);
            Label sub = new Label();
            sub.Text = "导入命令 → 切到游戏 → F8 开始/停止；工具可以留在后台或托盘";
            sub.Font = new Font("Microsoft YaHei UI", 9F);
            sub.ForeColor = Muted;
            sub.AutoSize = true;
            sub.Location = new Point(2, 36);
            header.Controls.Add(title);
            header.Controls.Add(sub);
            root.Controls.Add(header, 0, 0);

            // 命令区标题
            Label cmdHead = new Label();
            cmdHead.Text = "演奏命令　（KEY Shift+Q 250 · 250ms · # 注释）";
            cmdHead.Dock = DockStyle.Fill;
            cmdHead.TextAlign = ContentAlignment.MiddleLeft;
            cmdHead.ForeColor = Color.FromArgb(195, 205, 219);
            cmdHead.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            cmdHead.Margin = new Padding(0);
            root.Controls.Add(cmdHead, 0, 1);

            // 命令文本框
            _txt = new TextBox();
            _txt.Dock = DockStyle.Fill;
            _txt.Multiline = true;
            _txt.ScrollBars = ScrollBars.Vertical;
            _txt.WordWrap = false;
            _txt.AcceptsReturn = true;
            _txt.AcceptsTab = false;
            _txt.Font = new Font("Consolas", 10F);
            _txt.BackColor = InputBg;
            _txt.ForeColor = Color.FromArgb(207, 227, 221);
            _txt.BorderStyle = BorderStyle.FixedSingle;
            _txt.Margin = new Padding(0, 4, 0, 4);
            _txt.TextChanged += delegate { ParseNow(); };
            root.Controls.Add(_txt, 0, 2);

            // 工具栏
            FlowLayoutPanel toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.FlowDirection = FlowDirection.LeftToRight;
            toolbar.WrapContents = false;
            toolbar.BackColor = Bg;
            toolbar.Padding = new Padding(0, 6, 0, 0);
            toolbar.Margin = new Padding(0);

            _btnClip = MakeButton("从剪贴板导入", 128, false, false);
            _btnClip.Click += delegate { ImportClipboard(); };
            _btnFile = MakeButton("导入文件…", 108, false, false);
            _btnFile.Click += delegate { ImportFile(); };
            _btnSample = MakeButton("载入示例", 96, false, false);
            _btnSample.Click += delegate { LoadSample(); };
            _btnClear = MakeButton("清空", 70, false, true);
            _btnClear.Click += delegate { _txt.Text = ""; };

            toolbar.Controls.Add(_btnClip);
            toolbar.Controls.Add(_btnFile);
            toolbar.Controls.Add(_btnSample);
            toolbar.Controls.Add(_btnClear);
            root.Controls.Add(toolbar, 0, 3);

            // 选项
            FlowLayoutPanel options = new FlowLayoutPanel();
            options.Dock = DockStyle.Fill;
            options.FlowDirection = FlowDirection.LeftToRight;
            options.WrapContents = true;
            options.BackColor = Bg;
            options.Padding = new Padding(0, 4, 0, 0);
            options.Margin = new Padding(0);

            options.Controls.Add(MakeOptionLabel("预热"));
            _numWarmup = new NumericUpDown();
            _numWarmup.Minimum = 0;
            _numWarmup.Maximum = 10000;
            _numWarmup.Increment = 100;
            _numWarmup.Value = 500;
            _numWarmup.Width = 74;
            _numWarmup.BackColor = InputBg;
            _numWarmup.ForeColor = TextColor;
            _numWarmup.BorderStyle = BorderStyle.FixedSingle;
            _numWarmup.Margin = new Padding(0, 2, 4, 0);
            _numWarmup.ValueChanged += delegate { RefreshSummary(); };
            options.Controls.Add(_numWarmup);
            options.Controls.Add(MakeOptionLabel("ms"));

            options.Controls.Add(MakeOptionLabel("发送方式"));
            _cmbMode = new ComboBox();
            _cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbMode.Width = 130;
            _cmbMode.BackColor = InputBg;
            _cmbMode.ForeColor = TextColor;
            _cmbMode.FlatStyle = FlatStyle.Flat;
            _cmbMode.Margin = new Padding(0, 2, 12, 0);
            _cmbMode.Items.Add("扫描码（推荐）");
            _cmbMode.Items.Add("虚拟键");
            _cmbMode.SelectedIndex = 0;
            options.Controls.Add(_cmbMode);

            _chkForeground = new CheckBox();
            _chkForeground.Text = "只在启动时的前台窗口播放";
            _chkForeground.Checked = true;
            _chkForeground.AutoSize = true;
            _chkForeground.ForeColor = Color.FromArgb(195, 205, 219);
            _chkForeground.Margin = new Padding(0, 6, 16, 0);
            options.Controls.Add(_chkForeground);

            _chkTray = new CheckBox();
            _chkTray.Text = "关闭时最小化到托盘";
            _chkTray.Checked = true;
            _chkTray.AutoSize = true;
            _chkTray.ForeColor = Color.FromArgb(195, 205, 219);
            _chkTray.Margin = new Padding(0, 6, 0, 0);
            options.Controls.Add(_chkTray);
            root.Controls.Add(options, 0, 4);

            // 状态区
            Panel status = new Panel();
            status.Dock = DockStyle.Fill;
            status.BackColor = PanelBg;
            status.Padding = new Padding(12, 10, 12, 10);
            status.Margin = new Padding(0, 6, 0, 0);

            _lblStatus = new Label();
            _lblStatus.Text = "就绪";
            _lblStatus.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            _lblStatus.ForeColor = TextColor;
            _lblStatus.AutoSize = false;
            _lblStatus.AutoEllipsis = true;
            _lblStatus.Width = 500;
            _lblStatus.Height = 22;
            _lblStatus.Location = new Point(12, 10);

            _lblSummary = new Label();
            _lblSummary.Text = "未导入命令";
            _lblSummary.Font = new Font("Microsoft YaHei UI", 9F);
            _lblSummary.ForeColor = Muted;
            _lblSummary.AutoSize = true;
            _lblSummary.Location = new Point(300, 12);

            _bar = new ProgressBar();
            _bar.Minimum = 0;
            _bar.Maximum = 1000;
            _bar.Value = 0;
            _bar.Location = new Point(12, 38);
            _bar.Size = new Size(600, 16);
            _bar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            _lblProgress = new Label();
            _lblProgress.Text = "0 / 0";
            _lblProgress.Font = new Font("Consolas", 9F);
            _lblProgress.ForeColor = Muted;
            _lblProgress.AutoSize = true;
            _lblProgress.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _lblProgress.Location = new Point(760, 38);

            _btnStart = MakeButton("▶ 开始（F8）", 176, true, false);
            _btnStart.Height = 38;
            _btnStart.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _btnStart.Location = new Point(12, 66);
            _btnStart.Click += delegate { BeginStartFromButton(); };

            _btnStop = MakeButton("■ 停止（F9）", 132, false, true);
            _btnStop.Height = 38;
            _btnStop.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            _btnStop.Location = new Point(196, 66);
            _btnStop.Enabled = false;
            _btnStop.Click += delegate { StopPlayback(); };

            _btnAdmin = MakeButton("以管理员重启", 132, false, false);
            _btnAdmin.Height = 30;
            _btnAdmin.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnAdmin.Location = new Point(720, 102);
            _btnAdmin.Click += delegate { RestartAsAdmin(); };

            _btnKillOld = MakeButton("清理旧实例", 110, false, true);
            _btnKillOld.Height = 30;
            _btnKillOld.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnKillOld.Location = new Point(602, 102);
            _btnKillOld.Click += delegate { KillOldInstances(); };

            _lblHotkey = new Label();
            _lblHotkey.Text = "热键：正在检查…";
            _lblHotkey.Font = new Font("Microsoft YaHei UI", 8.5F);
            _lblHotkey.ForeColor = Muted;
            _lblHotkey.AutoSize = true;
            _lblHotkey.Location = new Point(12, 102);

            Label hint = new Label();
            hint.Text = "F8 全局开始/停止 · F9 停止 · 游戏保持前台 · 切到别的窗口会自动停止";
            hint.Font = new Font("Microsoft YaHei UI", 8.5F);
            hint.ForeColor = Muted;
            hint.AutoSize = true;
            hint.Location = new Point(12, 126);

            status.Controls.Add(_lblStatus);
            status.Controls.Add(_lblSummary);
            status.Controls.Add(_bar);
            status.Controls.Add(_lblProgress);
            status.Controls.Add(_btnStart);
            status.Controls.Add(_btnStop);
            status.Controls.Add(_btnKillOld);
            status.Controls.Add(_btnAdmin);
            status.Controls.Add(_lblHotkey);
            status.Controls.Add(hint);
            status.Resize += delegate { LayoutStatus(); };
            root.Controls.Add(status, 0, 5);

            Label footer = new Label();
            footer.Dock = DockStyle.Fill;
            footer.Text = "仅发送标准键盘输入，不读取游戏内存、不绕过反作弊；请遵守游戏用户规则。";
            footer.Font = new Font("Microsoft YaHei UI", 8.5F);
            footer.ForeColor = Color.FromArgb(91, 104, 121);
            footer.TextAlign = ContentAlignment.MiddleLeft;
            footer.Margin = new Padding(0);
            root.Controls.Add(footer, 0, 6);
        }

        private static Label MakeOptionLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.ForeColor = Muted;
            l.Margin = new Padding(0, 6, 6, 0);
            return l;
        }

        private static Button MakeButton(string text, int width, bool accent, bool danger)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 30;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.Margin = new Padding(0, 0, 8, 0);
            b.UseVisualStyleBackColor = false;
            if (accent)
            {
                b.BackColor = Gold;
                b.ForeColor = Color.FromArgb(13, 18, 24);
                b.FlatAppearance.BorderColor = Gold;
            }
            else if (danger)
            {
                b.BackColor = DangerBg;
                b.ForeColor = Color.FromArgb(255, 180, 189);
                b.FlatAppearance.BorderColor = Color.FromArgb(122, 53, 64);
            }
            else
            {
                b.BackColor = BtnBg;
                b.ForeColor = Color.FromArgb(217, 227, 240);
                b.FlatAppearance.BorderColor = Line;
            }
            Color normal = b.BackColor;
            Color hover = accent ? Color.FromArgb(242, 216, 145) : (danger ? DangerHover : BtnHover);
            b.MouseEnter += delegate { b.BackColor = hover; };
            b.MouseLeave += delegate { b.BackColor = normal; };
            return b;
        }

        private void BuildTray()
        {
            _tray = new NotifyIcon();
            _tray.Icon = SystemIcons.Application;
            _tray.Text = "燕云自动演奏器";
            _tray.Visible = true;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("显示主界面", null, delegate { ShowFromTray(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate
            {
                _exiting = true;
                Close();
            });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowFromTray(); };
        }

        /* ---------------- 热键 ---------------- */
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // 通过 UAC / 计划任务 / start /min 启动时，窗口可能是最小化的；
            // 先恢复成正常窗口，避免刚启动就自己躲进托盘。
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
                Show();
                Activate();
            }
            _wasNormalShown = true;
            RegisterHotkeys();
        }

        private void RegisterHotkeys()
        {
            if (_closing || IsDisposed) return;
            if (!_f8Registered)
            {
                _f8Registered = NativeMethods.RegisterHotKey(Handle, HotkeyStart, NativeMethods.MOD_NOREPEAT, (uint)Keys.F8);
            }
            if (!_f9Registered)
            {
                _f9Registered = NativeMethods.RegisterHotKey(Handle, HotkeyStop, NativeMethods.MOD_NOREPEAT, (uint)Keys.F9);
            }

            _hotkeysRegistered = _f8Registered && _f9Registered;
            UpdateHotkeyLabel();
            if (_hotkeysRegistered)
            {
                StopHotkeyTimer();
            }
            else
            {
                StartHotkeyTimer();
            }
        }

        private void StartHotkeyTimer()
        {
            if (_closing || IsDisposed) return;
            if (_hotkeyTimer == null)
            {
                _hotkeyTimer = new System.Windows.Forms.Timer();
                _hotkeyTimer.Interval = 3000;
                _hotkeyTimer.Tick += delegate { RegisterHotkeys(); };
            }
            if (!_hotkeyTimer.Enabled) _hotkeyTimer.Start();
        }

        private void StopHotkeyTimer()
        {
            if (_hotkeyTimer != null)
            {
                _hotkeyTimer.Stop();
                _hotkeyTimer.Dispose();
                _hotkeyTimer = null;
            }
        }

        internal void PreviewHotkeyRegistered()
        {
            _f8Registered = true;
            _f9Registered = true;
            _hotkeysRegistered = true;
            UpdateHotkeyLabel();
        }

        private void UpdateHotkeyLabel()
        {
            if (_lblHotkey == null || _lblHotkey.IsDisposed) return;
            if (_f8Registered && _f9Registered)
            {
                _lblHotkey.Text = "热键：F8 开始/停止 · F9 停止（已注册）";
                _lblHotkey.ForeColor = Color.FromArgb(79, 209, 197);
            }
            else
            {
                string missing;
                if (!_f8Registered && !_f9Registered) missing = "F8/F9";
                else if (!_f8Registered) missing = "F8";
                else missing = "F9";
                _lblHotkey.Text = "热键：" + missing + " 被占用 · 先退出旧实例，或点「清理旧实例」";
                _lblHotkey.ForeColor = Err;
            }
        }

        private void KillOldInstances()
        {
            DialogResult confirm = MessageBox.Show(this,
                "会退出其他所有「燕云自动演奏器」实例（包括旧的托盘实例）。\r\n" +
                "如果旧窗口里有还没保存的演奏命令，请先复制出来；继续吗？",
                "清理旧实例", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            int killed = 0;
            int failed = 0;
            int currentPid = Process.GetCurrentProcess().Id;
            try
            {
                Process[] all = Process.GetProcesses();
                foreach (Process p in all)
                {
                    if (p.Id == currentPid) continue;
                    string name;
                    try { name = p.ProcessName; }
                    catch { continue; }
                    if (name != "燕云自动演奏器" &&
                        !name.StartsWith("燕云自动演奏器_", StringComparison.Ordinal)) continue;
                    try
                    {
                        p.Kill();
                        p.WaitForExit(1500);
                        killed++;
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
            }

            if (killed > 0)
            {
                SetStatus(string.Format("已退出 {0} 个旧实例，正在重试热键…", killed));
                RegisterHotkeys();
            }
            else if (failed > 0)
            {
                SetStatus("旧实例是管理员权限，普通权限关不掉：请右键托盘退出，或用管理员身份运行本工具后再点一次", true);
            }
            else
            {
                SetStatus("没有发现旧的「燕云自动演奏器」实例");
            }
            UpdateHotkeyLabel();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HotkeyStart)
                {
                    ToggleFromHotkey();
                    return;
                }
                if (id == HotkeyStop)
                {
                    StopPlayback();
                    return;
                }
            }
            base.WndProc(ref m);
        }

        private void ToggleFromHotkey()
        {
            if (_closing) return;
            if (_player.IsPlaying)
            {
                StopPlayback();
                return;
            }
            StartPlayback();
        }

        private void StopPlayback()
        {
            if (_player.IsPlaying)
            {
                _player.Stop();
                SetStatus("正在停止…");
            }
        }

        /* ---------------- 导入 / 解析 ---------------- */
        private void ImportClipboard()
        {
            try
            {
                if (!Clipboard.ContainsText())
                {
                    SetStatus("剪贴板里没有文本", true);
                    return;
                }
                _txt.Text = Clipboard.GetText();
                SetStatus("已从剪贴板导入");
            }
            catch (Exception ex)
            {
                SetStatus("读取剪贴板失败：" + ex.Message, true);
            }
        }

        private void ImportFile()
        {
            _inDialog = true;
            try
            {
                CrashLog.Trace("ImportFile: open picker, folder=" + _lastFolder);
                using (FilePickerForm picker = new FilePickerForm(_lastFolder))
                {
                    DialogResult result = picker.ShowDialog(this);
                    CrashLog.Trace("ImportFile: picker result=" + result + ", file=" + picker.SelectedPath);
                    if (result == DialogResult.OK && !string.IsNullOrEmpty(picker.SelectedPath))
                    {
                        if (!string.IsNullOrEmpty(picker.SelectedFolder)) _lastFolder = picker.SelectedFolder;
                        LoadFile(picker.SelectedPath);
                        SaveSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                SetStatus("打开文件选择器失败：" + ex.Message, true);
                try
                {
                    MessageBox.Show(this, "打开文件选择器失败：\r\n" + ex.Message +
                        "\r\n\r\n可以改用「从剪贴板导入」，或把 .txt 直接拖进窗口。",
                        "燕云自动演奏器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch
                {
                }
            }
            finally
            {
                _inDialog = false;
                if (WindowState == FormWindowState.Minimized)
                {
                    WindowState = FormWindowState.Normal;
                    Show();
                    Activate();
                }
            }
        }

        private void OnFileDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void OnFileDragDrop(object sender, DragEventArgs e)
        {
            try
            {
                string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0) LoadFile(files[0]);
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                SetStatus("拖入文件失败：" + ex.Message, true);
            }
        }

        private void LoadFile(string path)
        {
            try
            {
                CrashLog.Trace("LoadFile: start " + path);
                if (Directory.Exists(path))
                {
                    SetStatus("拖入的是文件夹，请拖 .txt 文件", true);
                    return;
                }
                FileInfo info = new FileInfo(path);
                if (info.Length > 2 * 1024 * 1024)
                {
                    SetStatus("文件太大（超过 2MB），请只导入命令文本", true);
                    return;
                }
                string text = File.ReadAllText(path, Encoding.UTF8);
                CrashLog.Trace("LoadFile: read " + text.Length + " chars");
                if (text.IndexOf('\0') >= 0)
                {
                    SetStatus("这不是文本文件，请导入命令 .txt", true);
                    return;
                }
                _txt.Text = text;
                CrashLog.Trace("LoadFile: assigned to textbox");
                SetStatus("已导入 " + Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                CrashLog.Write(ex);
                SetStatus("导入失败：" + ex.Message, true);
            }
        }

        private void LoadSample()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# 示例：高音 C 大调 + 半音（可先在记事本里试）");
            sb.AppendLine("KEY Q 300");
            sb.AppendLine("KEY W 300");
            sb.AppendLine("KEY E 300");
            sb.AppendLine("KEY R 300");
            sb.AppendLine("KEY T 300");
            sb.AppendLine("KEY Y 300");
            sb.AppendLine("KEY U 600");
            sb.AppendLine("400ms");
            sb.AppendLine("KEY Shift+Q 300");
            sb.AppendLine("KEY Shift+R 300");
            sb.AppendLine("KEY Shift+T 300");
            sb.AppendLine("KEY Ctrl+E 300");
            sb.AppendLine("KEY Ctrl+U 600");
            _txt.Text = sb.ToString();
            SetStatus("已载入示例");
        }

        private void ParseNow()
        {
            _parsed = CommandParser.Parse(_txt.Text);
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            if (_parsed == null) return;

            if (_parsed.Errors.Count > 0)
            {
                _lblStatus.Text = _parsed.Errors[0];
                _lblStatus.ForeColor = Err;
                _lblSummary.Text = string.Format("{0} 处问题 · {1} 个按键", _parsed.Errors.Count, _parsed.KeyCount);
                _lblSummary.ForeColor = Err;
                _bar.Value = 0;
                _lblProgress.Text = "0 / " + _parsed.KeyCount;
                LayoutStatus();
                return;
            }

            if (!_parsed.HasKeys)
            {
                _lblStatus.Text = "还没有可播放的 KEY 行";
                _lblStatus.ForeColor = Muted;
                _lblSummary.Text = "未导入命令";
                _lblSummary.ForeColor = Muted;
                _bar.Value = 0;
                _lblProgress.Text = "0 / 0";
                LayoutStatus();
                return;
            }

            double totalMs = _parsed.TotalMs + (double)_numWarmup.Value;
            _lblStatus.Text = "就绪，按 F8 开始（或在游戏里按 F8）";
            _lblStatus.ForeColor = TextColor;
            _lblSummary.Text = string.Format("{0} 个按键 · {1} 段等待 · 预计 {2:0.0} 秒",
                _parsed.KeyCount, _parsed.WaitCount, totalMs / 1000.0);
            _lblSummary.ForeColor = Muted;
            _bar.Value = 0;
            _lblProgress.Text = "0 / " + _parsed.Commands.Count;
            LayoutStatus();
        }

        private void LayoutStatus()
        {
            if (_lblSummary == null || _lblSummary.Parent == null) return;
            Control p = _lblSummary.Parent;
            int right = p.ClientSize.Width - 12;
            int summaryLeft = right - _lblSummary.Width;
            if (summaryLeft < 300) summaryLeft = 300;
            _lblSummary.Left = summaryLeft;
            _lblProgress.Left = Math.Max(0, right - _lblProgress.Width);
            _btnAdmin.Left = Math.Max(0, right - _btnAdmin.Width);
            _btnKillOld.Left = Math.Max(0, _btnAdmin.Left - 8 - _btnKillOld.Width);
            _bar.Width = Math.Max(200, p.ClientSize.Width - 24 - 80);
            _lblStatus.Width = Math.Max(160, summaryLeft - 24);
        }

        /* ---------------- 播放控制 ---------------- */
        private void BeginStartFromButton()
        {
            if (_player.IsPlaying)
            {
                StopPlayback();
                return;
            }
            ParseNow();
            if (!_parsed.Ok)
            {
                SetStatus(_parsed.Errors.Count > 0 ? _parsed.Errors[0] : "没有可播放的命令", true);
                return;
            }

            CancelCountdown();
            _countdownLeft = 3;
            _btnStart.Enabled = false;
            SetStatus("3 秒后开始，请切到游戏窗口…");
            _countdown = new System.Windows.Forms.Timer();
            _countdown.Interval = 1000;
            _countdown.Tick += delegate
            {
                _countdownLeft--;
                if (_countdownLeft > 0)
                {
                    SetStatus(string.Format("{0} 秒后开始，请切到游戏窗口…", _countdownLeft));
                }
                else
                {
                    CancelCountdown();
                    StartPlayback();
                }
            };
            _countdown.Start();
        }

        private void CancelCountdown()
        {
            if (_countdown != null)
            {
                _countdown.Stop();
                _countdown.Dispose();
                _countdown = null;
            }
            if (!_player.IsPlaying && !_closing) _btnStart.Enabled = true;
        }

        private void StartPlayback()
        {
            CancelCountdown();
            if (_player.IsPlaying) return;

            ParseNow();
            if (!_parsed.Ok)
            {
                string msg = _parsed.Errors.Count > 0 ? _parsed.Errors[0] : "没有可播放的命令";
                SetStatus(msg, true);
                ShowTrayTip("无法开始", msg, ToolTipIcon.Warning);
                return;
            }

            IntPtr fg = NativeMethods.GetForegroundWindow();
            uint fgPid;
            NativeMethods.GetWindowThreadProcessId(fg, out fgPid);
            if (fg == IntPtr.Zero || fgPid == (uint)Process.GetCurrentProcess().Id)
            {
                string msg = "当前前台是本工具：请先切到游戏，再按 F8";
                SetStatus(msg, true);
                ShowTrayTip("请先切到游戏", msg, ToolTipIcon.Warning);
                return;
            }

            bool useScanCodes = _cmbMode.SelectedIndex != 1;
            bool started = _player.Start(_parsed.Commands, (int)_numWarmup.Value,
                _chkForeground.Checked, useScanCodes);
            if (!started)
            {
                _btnStart.Enabled = true;
                return;
            }

            _btnStart.Enabled = false;
            _btnStop.Enabled = true;
            _txt.ReadOnly = true;
            _bar.Minimum = 0;
            _bar.Maximum = Math.Max(1, _parsed.Commands.Count);
            _bar.Value = 0;
            _lblProgress.Text = "0 / " + _parsed.Commands.Count;
            _lblStatus.ForeColor = Gold;
            _lblStatus.Text = "播放中…（F8 停止）";
            _tray.Text = "燕云自动演奏器 · 播放中";
            ShowTrayTip("开始播放", string.Format("{0} 个按键，预计 {1:0.0} 秒",
                _parsed.KeyCount, (_parsed.TotalMs + (double)_numWarmup.Value) / 1000.0), ToolTipIcon.Info);
        }

        private void OnPlayerStatus(string text)
        {
            if (_closing || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnPlayerStatus), text);
                return;
            }
            SetStatus(text);
        }

        private void OnPlayerProgress(int done, int total, double elapsedMs)
        {
            if (_closing || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<int, int, double>(OnPlayerProgress), done, total, elapsedMs);
                return;
            }
            if (total > 0)
            {
                _bar.Maximum = total;
                _bar.Value = Math.Min(done, total);
            }
            _lblProgress.Text = done + " / " + total;
        }

        private void OnPlayerFinished(string message, bool error)
        {
            if (_closing || IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, bool>(OnPlayerFinished), message, error);
                return;
            }

            _btnStart.Enabled = true;
            _btnStop.Enabled = false;
            _txt.ReadOnly = false;
            _tray.Text = "燕云自动演奏器";
            SetStatus(message, error);
            if (error)
            {
                ShowFromTray();
                ShowTrayTip("播放中断", message, ToolTipIcon.Error);
            }
            else
            {
                ShowTrayTip("播放结束", message, ToolTipIcon.Info);
            }
        }

        private void SetStatus(string text, bool error)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string, bool>(SetStatus), text, error);
                return;
            }
            _lblStatus.Text = text;
            _lblStatus.ForeColor = error ? Err : Color.FromArgb(195, 205, 219);
        }

        private void SetStatus(string text)
        {
            SetStatus(text, false);
        }

        private void ShowTrayTip(string title, string text, ToolTipIcon icon)
        {
            try
            {
                _tray.ShowBalloonTip(2200, title, text, icon);
            }
            catch
            {
            }
        }

        /* ---------------- 托盘 / 关闭 ---------------- */
        private void HideToTray()
        {
            Hide();
            if (!_trayTipShown)
            {
                ShowTrayTip("仍在后台运行", "按 F8 开始/停止，F9 停止；托盘图标可重新打开主界面。", ToolTipIcon.Info);
                _trayTipShown = true;
            }
            SaveSettings();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!_wasNormalShown || _inDialog) return;
            if (WindowState == FormWindowState.Minimized && _chkTray != null && _chkTray.Checked)
            {
                HideToTray();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_inDialog)
            {
                e.Cancel = true;
                return;
            }
            if (!_exiting && _chkTray.Checked && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            _exiting = true;
            Cleanup();
            base.OnFormClosing(e);
        }

        private void Cleanup()
        {
            _closing = true;
            CancelCountdown();
            try { _player.Shutdown(); }
            catch { }
            try
            {
                StopHotkeyTimer();
                if (_f8Registered)
                {
                    NativeMethods.UnregisterHotKey(Handle, HotkeyStart);
                    _f8Registered = false;
                }
                if (_f9Registered)
                {
                    NativeMethods.UnregisterHotKey(Handle, HotkeyStop);
                    _f9Registered = false;
                }
                _hotkeysRegistered = false;
            }
            catch { }
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch { }
            SaveSettings();
        }

        /* ---------------- 管理员 ---------------- */
        private static bool IsAdmin()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private void UpdateAdminButton()
        {
            if (IsAdmin())
            {
                _btnAdmin.Text = "已是管理员";
                _btnAdmin.Enabled = false;
            }
        }

        private void RestartAsAdmin()
        {
            if (IsAdmin())
            {
                _btnAdmin.Text = "已是管理员";
                _btnAdmin.Enabled = false;
                return;
            }
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                _exiting = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "未能以管理员身份启动：" + ex.Message,
                    "燕云自动演奏器", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /* ---------------- 设置 ---------------- */
        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                string[] lines = File.ReadAllLines(SettingsPath);
                foreach (string line in lines)
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();
                    if (k == "warmup")
                    {
                        int n;
                        if (int.TryParse(v, out n))
                            _numWarmup.Value = Math.Max(_numWarmup.Minimum, Math.Min(_numWarmup.Maximum, n));
                    }
                    else if (k == "foreground")
                    {
                        _chkForeground.Checked = v == "1";
                    }
                    else if (k == "tray")
                    {
                        _chkTray.Checked = v == "1";
                    }
                    else if (k == "mode")
                    {
                        int n;
                        if (int.TryParse(v, out n) && n >= 0 && n <= 1) _cmbMode.SelectedIndex = n;
                    }
                    else if (k == "folder")
                    {
                        if (Directory.Exists(v)) _lastFolder = v;
                    }
                }
            }
            catch
            {
            }
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllText(LastCommandsPath, _txt.Text, new UTF8Encoding(false));
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("warmup=" + (int)_numWarmup.Value);
                sb.AppendLine("foreground=" + (_chkForeground.Checked ? "1" : "0"));
                sb.AppendLine("tray=" + (_chkTray.Checked ? "1" : "0"));
                sb.AppendLine("mode=" + _cmbMode.SelectedIndex);
                sb.AppendLine("folder=" + _lastFolder);
                File.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }
    }

    /* =====================================================================
     * 自检（--selftest，不弹窗）
     * ===================================================================== */
    internal static class SelfTest
    {
        public static int Run()
        {
            StringBuilder sb = new StringBuilder();
            int failures = 0;

            Action<bool, string> check = delegate(bool ok, string msg)
            {
                sb.AppendLine((ok ? "ok   " : "FAIL ") + msg);
                if (!ok) failures++;
            };

            string sample =
                "# 注释\r\n" +
                "KEY Q 100\r\n" +
                "KEY Shift+E 250\r\n" +
                "50ms\r\n" +
                "KEY Ctrl+U 120\r\n" +
                "100ms\r\n" +
                "KEY Z 80\r\n";

            ParseResult r = CommandParser.Parse(sample);
            check(r.Errors.Count == 0, "sample 无错误: " + Join(r.Errors));
            check(r.KeyCount == 4, "按键数 4: " + r.KeyCount);
            check(r.WaitCount == 2, "等待数 2: " + r.WaitCount);
            check(Math.Abs(r.TotalMs - 700) < 0.001, "总时长 700ms: " + r.TotalMs);
            check(r.Commands.Count == 6, "命令数 6: " + r.Commands.Count);
            check(r.Commands[1].Type == CommandType.Key && r.Commands[1].Key.Shift &&
                  r.Commands[1].Key.KeyCode == Keys.E, "Shift+E 解析");
            check(r.Commands[3].Type == CommandType.Key && r.Commands[3].Key.Ctrl &&
                  r.Commands[3].Key.KeyCode == Keys.U, "Ctrl+U 解析");
            check(r.Commands[0].Key.ScanCode != 0, "Q 有扫描码: " + r.Commands[0].Key.ScanCode);

            ParseResult r2 = CommandParser.Parse("KEY ↑Q 100\nKEY ⇧A 100\nKEY Ctrl + E 100\nKEY q 100");
            check(r2.Errors.Count == 0, "别名无错误: " + Join(r2.Errors));
            check(r2.Commands[0].Key.Shift && r2.Commands[0].Key.KeyCode == Keys.Q, "↑Q -> Shift+Q");
            check(r2.Commands[1].Key.Shift && r2.Commands[1].Key.KeyCode == Keys.A, "⇧A -> Shift+A");
            check(r2.Commands[2].Key.Ctrl && r2.Commands[2].Key.KeyCode == Keys.E, "Ctrl + E -> Ctrl+E");
            check(!r2.Commands[3].Key.Shift && r2.Commands[3].Key.KeyCode == Keys.Q, "小写 q");

            ParseResult r3 = CommandParser.Parse("KEY ??? 100\n250ms");
            check(r3.Errors.Count >= 1, "非法主键报错: " + Join(r3.Errors));

            ParseResult r4 = CommandParser.Parse("Q W E");
            check(r4.Errors.Count >= 1, "没有 KEY 行报错: " + Join(r4.Errors));

            ParseResult r5 = CommandParser.Parse("KEY Q 100 # 行尾注释\nKEY W 250ms");
            check(r5.Errors.Count == 0 && r5.KeyCount == 2, "行尾注释 + ms 后缀");

            sb.AppendLine("failures=" + failures);
            try
            {
                File.WriteAllText(Path.Combine(Application.StartupPath, "selftest.log"), sb.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
            return failures == 0 ? 0 : 1;
        }

        private static string Join(List<string> list)
        {
            if (list == null || list.Count == 0) return "(无)";
            return string.Join("; ", list.ToArray());
        }
    }
}
