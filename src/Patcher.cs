// Tangy TD 简体中文补丁工具（单文件，免安装）
// 编译：由 build_patcher.py 调用系统自带 csc，payload 以 deflate 压缩资源内嵌
//（Manifest.cs 由构建脚本按 payload 实际内容生成，勿手工编辑）
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Tangy TD 简体中文补丁")]
[assembly: AssemblyProduct("Tangy TD 简体中文补丁")]
[assembly: AssemblyDescription("Tangy TD 简体中文补丁：安装/卸载（还原原版）。仅支持游戏版本 1.0.393。")]
[assembly: AssemblyVersion("1.0.393.0")]
[assembly: AssemblyFileVersion("1.0.393.0")]

namespace TangyPatch
{
    static class Program
    {
        // ---------------- 资源读写 ----------------
        static byte[] LoadRes(string id)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(id))
            {
                if (s == null) throw new Exception("内嵌资源缺失：" + id);
                using (DeflateStream d = new DeflateStream(s, CompressionMode.Decompress))
                using (MemoryStream m = new MemoryStream())
                {
                    d.CopyTo(m);
                    return m.ToArray();
                }
            }
        }

        static void WriteRes(string gameDir, Entry e)
        {
            byte[] data = LoadRes(e.Res);
            if (data.Length != e.Size)
                throw new Exception("资源 " + e.Res + " 大小不符（" + data.Length + " != " + e.Size + "），工具与数据不配套");
            string dst = Path.Combine(gameDir, e.Path.Replace('/', '\\'));
            string par = Path.GetDirectoryName(dst);
            if (!Directory.Exists(par)) Directory.CreateDirectory(par);
            File.WriteAllBytes(dst, data);
        }

        // ---------------- 游戏目录探测 ----------------
        static string TryRoot(string root, string marker)
        {
            try
            {
                if (File.Exists(Path.Combine(root, marker)))
                    return Path.Combine(root, Path.GetDirectoryName(marker));
            }
            catch { }
            return null;
        }

        static List<string> ParseLibraries(string vdfPath)
        {
            List<string> libs = new List<string>();
            foreach (string raw in File.ReadAllLines(vdfPath))
            {
                string line = raw.Trim();
                if (!line.StartsWith("\"path\"", StringComparison.Ordinal)) continue;
                // 形如  "path"		"D:\\steam"
                int q = line.IndexOf('"', 1);          // "path" 的闭引号
                if (q < 0) continue;
                int r = line.IndexOf('"', q + 1);      // 值的开引号
                if (r < 0) continue;
                int s = line.IndexOf('"', r + 1);      // 值的闭引号
                if (s <= r + 1) continue;
                libs.Add(line.Substring(r + 1, s - r - 1).Replace("\\\\", "\\"));
            }
            return libs;
        }

        static string DetectGameDir()
        {
            string marker = Path.Combine("steamapps", "common", "Tangy TD", "TangyTD.exe");

            // 1. 本工具所在目录（把补丁拷进游戏目录直接运行）
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(baseDir, "TangyTD.exe"))) return baseDir;

            // 2. 注册表 Steam + libraryfolders.vdf 各库路径
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
                {
                    if (k != null)
                    {
                        string root = k.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(root))
                        {
                            root = root.Replace('/', '\\');
                            string hit = TryRoot(root, marker);
                            if (hit != null) return hit;
                            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                            if (File.Exists(vdf))
                                foreach (string lib in ParseLibraries(vdf))
                                {
                                    hit = TryRoot(lib, marker);
                                    if (hit != null) return hit;
                                }
                        }
                    }
                }
            }
            catch { }

            // 3. 简单搜索：各固定盘符的常见 Steam 位置 + 根目录下名字含 steam 的一级目录
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    if (d.DriveType != DriveType.Fixed) continue;
                    string[] cands = new string[] {
                        Path.Combine(d.Name, "SteamLibrary"),
                        Path.Combine(d.Name, "Steam"),
                        Path.Combine(d.Name, "steam"),
                        Path.Combine(d.Name, "Games"),
                        Path.Combine(d.Name, "Program Files (x86)\\Steam")
                    };
                    foreach (string c in cands)
                    {
                        string hit = TryRoot(c, marker);
                        if (hit != null) return hit;
                    }
                    string[] subs;
                    try { subs = Directory.GetDirectories(d.Name); } catch { continue; }
                    foreach (string sub in subs)
                    {
                        if (sub.IndexOf("steam", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string hit = TryRoot(sub, marker);
                        if (hit != null) return hit;
                    }
                }
            }
            catch { }
            return null;
        }

        // ---------------- 校验与动作 ----------------
        static long FileSize(string dir, string rel)
        {
            try { return new FileInfo(Path.Combine(dir, rel.Replace('/', '\\'))).Length; }
            catch { return -1; }
        }

        // null = 通过；否则为错误信息（仅支持 原版 或 已打补丁 两种状态）
        static string ValidateDir(string dir)
        {
            if (string.IsNullOrEmpty(dir))
                return "未找到游戏目录。请把本补丁复制到游戏目录（包含 TangyTD.exe 的文件夹）后重新运行，或点击“选择目录”手动指定。";
            if (!File.Exists(Path.Combine(dir, "TangyTD.exe")) || !File.Exists(Path.Combine(dir, "game.dll")))
                return "目录 " + dir + " 中没有 TangyTD.exe / game.dll，不是有效的游戏目录。";
            long ex = FileSize(dir, "TangyTD.exe"), dl = FileSize(dir, "game.dll");
            bool orig = (ex == Manifest.ORIG_EXE && dl == Manifest.ORIG_DLL);
            bool patched = (ex == Manifest.PATCHED_EXE && dl == Manifest.PATCHED_DLL);
            if (!orig && !patched)
                return "游戏文件版本不匹配，本补丁仅支持 Tangy TD v" + Manifest.Version + "。\r\n" +
                       "当前 TangyTD.exe = " + ex + " 字节，game.dll = " + dl + " 字节。\r\n" +
                       "若游戏已更新请等待补丁更新；若文件损坏，请先在 Steam 中“验证文件完整性”。";
            return null;
        }

        static string CheckProcess()
        {
            if (Process.GetProcessesByName("TangyTD").Length > 0)
                return "检测到 Tangy TD 正在运行，请先完全退出游戏后再操作。";
            return null;
        }

        static void SetLanguage(string dir, string value)
        {
            string cfg = Path.Combine(dir, "config.ini");
            if (File.Exists(cfg))
            {
                // 只改 language= 一行，保留玩家其它设置
                string[] lines = File.ReadAllLines(cfg, Encoding.ASCII);
                bool found = false;
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].StartsWith("language=", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = "language=" + value;
                        found = true;
                    }
                if (found)
                {
                    File.WriteAllLines(cfg, lines, Encoding.ASCII);
                    return;
                }
                File.AppendAllText(cfg, "\r\nlanguage=" + value + "\r\n", Encoding.ASCII);
            }
            else
            {
                File.WriteAllText(cfg, "language=" + value + "\r\n", Encoding.ASCII);
            }
        }

        static string RunInstall(string dir)
        {
            string err = ValidateDir(dir);
            if (err != null) return err;
            err = CheckProcess();
            if (err != null) return err;
            try
            {
                foreach (Entry e in Manifest.Patched) WriteRes(dir, e);
                SetLanguage(dir, "1");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return "权限不足：请右键本补丁，选择“以管理员身份运行”后再试。";
            }
            catch (Exception ex)
            {
                return "安装失败：" + ex.Message;
            }
        }

        static string RunUninstall(string dir)
        {
            string err = ValidateDir(dir);
            if (err != null) return err;
            err = CheckProcess();
            if (err != null) return err;
            try
            {
                foreach (Entry e in Manifest.Original) WriteRes(dir, e);
                SetLanguage(dir, "0");
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return "权限不足：请右键本补丁，选择“以管理员身份运行”后再试。";
            }
            catch (Exception ex)
            {
                return "还原失败：" + ex.Message;
            }
        }

        // ---------------- 命令行模式（自动化测试用） ----------------
        static int RunCli(string[] args)
        {
            string dir = null;
            bool install = false, uninstall = false;
            foreach (string a in args)
            {
                if (string.Equals(a, "/install", StringComparison.OrdinalIgnoreCase)) install = true;
                else if (string.Equals(a, "/uninstall", StringComparison.OrdinalIgnoreCase)) uninstall = true;
                else if (a.StartsWith("/dir=", StringComparison.OrdinalIgnoreCase))
                {
                    dir = a.Substring(5).Trim('"');
                    if (dir.Length > 0 && dir[dir.Length - 1] != '\\' && dir[dir.Length - 1] != ':')
                        dir = dir + "\\";
                }
            }
            if (dir == null) dir = DetectGameDir();
            string err;
            if (install && !uninstall) err = RunInstall(dir);
            else if (uninstall && !install) err = RunUninstall(dir);
            else err = "用法：TangyTD_简体中文补丁 /install|/uninstall [/dir=路径]";
            if (err == null) return 0;
            Console.Error.WriteLine(err);
            return 2;
        }

        // ---------------- 界面 ----------------
        static TextBox dirBox;
        static Button btnInstall, btnUninstall, btnBrowse;
        static Label status;

        static void RefreshDir(string dir)
        {
            if (dir == null)
            {
                dirBox.Text = "（未找到）";
                btnInstall.Enabled = false;
                btnUninstall.Enabled = false;
                btnBrowse.Visible = true;
                SetStatus("未找到游戏目录：请把本补丁复制到游戏目录后运行，或点击“选择游戏目录”。", Color.Firebrick);
            }
            else
            {
                dirBox.Text = dir;
                btnInstall.Enabled = true;
                btnUninstall.Enabled = true;
                btnBrowse.Visible = false;
                SetStatus("游戏目录已找到。点“安装补丁”切换为简体中文；点“卸载补丁”还原官方原版。\r\n存档与其它设置不受影响。", SystemColors.ControlText);
            }
        }

        static void SetStatus(string text, Color c)
        {
            status.Text = text;
            status.ForeColor = c;
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Environment.Exit(RunCli(args));
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form f = new Form();
            f.Text = "Tangy TD 简体中文补丁 v" + Manifest.Version;
            f.FormBorderStyle = FormBorderStyle.FixedSingle;
            f.MaximizeBox = false;
            f.StartPosition = FormStartPosition.CenterScreen;
            f.ClientSize = new Size(560, 218);

            Label lbl = new Label();
            lbl.Text = "游戏目录：";
            lbl.AutoSize = true;
            lbl.Location = new Point(12, 15);

            dirBox = new TextBox();
            dirBox.ReadOnly = true;
            dirBox.SetBounds(85, 12, 463, dirBox.PreferredHeight);

            Font btnFont = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            btnInstall = new Button();
            btnInstall.Text = "安装补丁";
            btnInstall.Font = btnFont;
            btnInstall.SetBounds(85, 52, 220, 46);
            btnInstall.Click += delegate
            {
                DoAction(delegate { return RunInstall(dirBox.Text); },
                    "正在安装，请稍候…", "安装完成！启动游戏即为简体中文。");
            };

            btnUninstall = new Button();
            btnUninstall.Text = "卸载补丁（还原原版）";
            btnUninstall.Font = btnFont;
            btnUninstall.SetBounds(328, 52, 220, 46);
            btnUninstall.Click += delegate
            {
                DoAction(delegate { return RunUninstall(dirBox.Text); },
                    "正在还原，请稍候…", "已还原为官方英文原版。");
            };

            btnBrowse = new Button();
            btnBrowse.Text = "选择游戏目录…";
            btnBrowse.AutoSize = true;
            btnBrowse.Location = new Point(85, 108);
            btnBrowse.Visible = false;
            btnBrowse.Click += delegate
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "选择 Tangy TD 游戏目录（包含 TangyTD.exe）";
                    if (dlg.ShowDialog(f) == DialogResult.OK)
                        RefreshDir(File.Exists(Path.Combine(dlg.SelectedPath, "TangyTD.exe")) ? dlg.SelectedPath : null);
                }
            };

            status = new Label();
            status.AutoSize = false;
            status.SetBounds(12, 140, 536, 64);
            status.TextAlign = ContentAlignment.TopLeft;

            f.Controls.AddRange(new Control[] { lbl, dirBox, btnInstall, btnUninstall, btnBrowse, status });
            f.Shown += delegate { RefreshDir(DetectGameDir()); };
            Application.Run(f);
        }

        delegate string ActionFn();

        static void DoAction(ActionFn fn, string busy, string done)
        {
            btnInstall.Enabled = false;
            btnUninstall.Enabled = false;
            SetStatus(busy + "\r\n", SystemColors.ControlText);
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                string err = fn();
                if (err == null) SetStatus(done + "\r\n存档与其它设置不受影响。", Color.Green);
                else
                {
                    SetStatus(err, Color.Firebrick);
                    MessageBox.Show(err, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                Cursor.Current = Cursors.Default;
                Application.DoEvents();
                bool ok = dirBox.Text != "（未找到）";
                btnInstall.Enabled = ok;
                btnUninstall.Enabled = ok;
            }
        }
    }
}
