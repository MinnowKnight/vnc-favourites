// VNC Favourites - favourites and saved passwords for TightVNC 2 Viewer.
// Copyright (c) 2026 MinnowKnight. Released under the MIT License (see LICENSE).
// https://github.com/MinnowKnight/vnc-favourites
// Saved passwords are encrypted with Windows DPAPI (current user).
// Icons: Material Design Icons by Pictogrammers, via Iconify, Apache License 2.0
// (see THIRD-PARTY-NOTICES.md). Not affiliated with GlavSoft or TightVNC.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

[assembly: AssemblyTitle("VNC Favourites")]
[assembly: AssemblyProduct("VNC Favourites")]
[assembly: AssemblyDescription("Favourites and saved passwords for TightVNC Viewer")]
[assembly: AssemblyCopyright("Copyright (c) 2026 MinnowKnight. MIT License.")]
[assembly: AssemblyCompany("MinnowKnight")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3.0")]

namespace VNCFavourites
{
    public class Favourite
    {
        public string Name = "";
        public string Host = "";
        public int Port = 5900;
        public string Password = "";   // DPAPI-encrypted, base64
        public string Extra = "";
        public string Icon = "monitor";
        public string Scale = "";      // "" = TightVNC saved settings, "auto", or a percentage
    }

    public class AppData
    {
        public string ViewerPath = "";
        public List<Favourite> Favourites = new List<Favourite>();
    }

    static class Icons
    {
        public static readonly string[] Names = {
            "monitor", "laptop", "desktop-tower", "server", "server-network", "database", "nas", "router-wireless", "raspberry-pi", "home",
            "home-city", "office-building", "factory", "store", "warehouse", "briefcase", "school", "hospital-building", "television", "gamepad-variant",
            "cellphone", "tablet", "printer", "camera", "cctv", "cloud", "web", "lan", "wifi", "shield-lock",
            "lock", "key", "cog", "wrench", "hammer-wrench", "tools", "flash", "lightbulb", "thermometer", "fire",
            "water", "leaf", "star", "heart", "account", "account-group", "music", "movie", "chart-line", "rocket-launch" };

        static readonly Dictionary<string, Image> cache = new Dictionary<string, Image>();

        public static Image Get(string name, int size)
        {
            if (Array.IndexOf(Names, name) < 0) name = Names[0];
            string key = name + "_" + size;
            Image img;
            if (cache.TryGetValue(key, out img)) return img;
            var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon." + key + ".png");
            img = s != null ? Image.FromStream(s) : new Bitmap(size, size);
            cache[key] = img;
            return img;
        }

        public static string Pretty(string name)
        {
            var t = name.Replace('-', ' ');
            return char.ToUpper(t[0]) + t.Substring(1);
        }
    }

    static class Store
    {
        static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VNCFavourites");
        static readonly string FilePath = Path.Combine(Dir, "favourites.xml");
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VNCFavourites.v1");

        public static AppData Load()
        {
            AppData d = null;
            try
            {
                if (File.Exists(FilePath))
                    using (var fs = File.OpenRead(FilePath))
                        d = (AppData)new XmlSerializer(typeof(AppData)).Deserialize(fs);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't read " + FilePath + "\n" + ex.Message, "VNC Favourites",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            if (d == null) d = new AppData();
            if (d.Favourites == null) d.Favourites = new List<Favourite>();
            foreach (var f in d.Favourites) if (string.IsNullOrEmpty(f.Icon)) f.Icon = Icons.Names[0];
            if (string.IsNullOrEmpty(d.ViewerPath)) d.ViewerPath = FindViewer();
            return d;
        }

        public static void Save(AppData d)
        {
            Directory.CreateDirectory(Dir);
            string tmp = FilePath + ".tmp";
            using (var fs = File.Create(tmp))
                new XmlSerializer(typeof(AppData)).Serialize(fs, d);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath);
        }

        public static string FindViewer()
        {
            var list = new List<string>();
            list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"TightVNC\tvnviewer.exe"));
            string pf64 = Environment.GetEnvironmentVariable("ProgramW6432");
            string pf86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(pf64)) list.Add(Path.Combine(pf64, @"TightVNC\tvnviewer.exe"));
            if (!string.IsNullOrEmpty(pf86)) list.Add(Path.Combine(pf86, @"TightVNC\tvnviewer.exe"));
            list.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tvnviewer.exe"));
            foreach (var p in list) if (File.Exists(p)) return p;
            return "";
        }

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }

        public static string Unprotect(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return "";
            try
            {
                byte[] dec = ProtectedData.Unprotect(Convert.FromBase64String(cipher), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch { return ""; }
        }
    }

    // Reads the per-server options TightVNC Viewer keeps in
    // HKCU\Software\TightVNC\Viewer\History\<server>, so command-line launches behave
    // like launches from TightVNC's own connection dialog.
    static class TightVncSettings
    {
        const string HistoryKey = @"Software\TightVNC\Viewer\History";

        static bool ParseServer(string s, out string host, out int port)
        {
            host = s.Trim(); port = 5900;
            int dbl = host.LastIndexOf("::", StringComparison.Ordinal);
            if (dbl > 0)
            {
                int p;
                if (int.TryParse(host.Substring(dbl + 2), out p)) port = p;
                host = host.Substring(0, dbl);
                return host.Length > 0;
            }
            int colon = host.IndexOf(':');
            if (colon > 0 && colon == host.LastIndexOf(':'))
            {
                int disp;
                if (int.TryParse(host.Substring(colon + 1), out disp))
                    port = disp < 100 ? 5900 + disp : disp;
                host = host.Substring(0, colon);
            }
            return host.Length > 0;
        }

        // Returns the options for this server, or for the most recent connection if this
        // server has none (that's what TightVNC's own dialog falls back to). null if nothing saved.
        public static Dictionary<string, int> Find(string host, int port, out string source)
        {
            source = null;
            try
            {
                using (var hist = Registry.CurrentUser.OpenSubKey(HistoryKey))
                {
                    if (hist == null) return null;
                    string exact = null, sameHost = null;
                    foreach (var name in hist.GetSubKeyNames())
                    {
                        string h; int p;
                        if (!ParseServer(name, out h, out p)) continue;
                        if (!string.Equals(h, host, StringComparison.OrdinalIgnoreCase)) continue;
                        if (p == port) { exact = name; break; }
                        if (sameHost == null) sameHost = name;
                    }
                    string chosen = exact ?? sameHost;
                    if (chosen == null)
                    {
                        var recent = hist.GetValue("0") as string;
                        if (!string.IsNullOrEmpty(recent) && Array.IndexOf(hist.GetSubKeyNames(), recent) >= 0)
                        {
                            chosen = recent;
                            source = "most recent TightVNC connection (" + recent + ")";
                        }
                    }
                    else source = "TightVNC's saved settings for " + chosen;
                    if (chosen == null) return null;

                    using (var k = hist.OpenSubKey(chosen))
                    {
                        if (k == null) return null;
                        var opts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        foreach (var v in k.GetValueNames())
                        {
                            if (k.GetValueKind(v) == RegistryValueKind.DWord)
                                opts[v] = (int)k.GetValue(v);
                        }
                        return opts.Count > 0 ? opts : null;
                    }
                }
            }
            catch { return null; }
        }

        // TightVNC's own options-file password format: the standard VNC scheme
        // (DES-ECB with VNC's fixed key, key bits reversed), as 16 hex digits.
        // VNC authentication only ever uses the first 8 characters.
        public static string EncodeVncPassword(string password)
        {
            byte[] plain = new byte[8];
            byte[] raw = Encoding.Default.GetBytes(password ?? "");
            Array.Copy(raw, plain, Math.Min(8, raw.Length));
            Array.Clear(raw, 0, raw.Length);
            byte[] key = { 23, 82, 107, 6, 35, 78, 88, 7 };
            for (int i = 0; i < 8; i++)
            {
                byte b = key[i], r = 0;
                for (int bit = 0; bit < 8; bit++) if ((b & (1 << bit)) != 0) r |= (byte)(0x80 >> bit);
                key[i] = r;
            }
            byte[] enc;
            using (var des = DES.Create())
            {
                des.Mode = CipherMode.ECB;
                des.Padding = PaddingMode.None;
                des.Key = key;
                using (var e = des.CreateEncryptor()) enc = e.TransformFinalBlock(plain, 0, 8);
            }
            Array.Clear(plain, 0, plain.Length);
            var sb = new StringBuilder(16);
            foreach (var b in enc) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        static string TempDir { get { return Path.Combine(Path.GetTempPath(), "VNCFavourites"); } }

        public static string WriteOptionsFile(string host, int port, string password, Dictionary<string, int> opts)
        {
            Directory.CreateDirectory(TempDir);
            string path = Path.Combine(TempDir, Guid.NewGuid().ToString("N") + ".vnc");
            var sb = new StringBuilder();
            sb.AppendLine("[connection]");
            sb.AppendLine("host=" + host);
            sb.AppendLine("port=" + port);
            if (!string.IsNullOrEmpty(password)) sb.AppendLine("password=" + EncodeVncPassword(password));
            sb.AppendLine("[options]");
            if (opts != null) foreach (var kv in opts) sb.AppendLine(kv.Key + "=" + kv.Value);
            File.WriteAllText(path, sb.ToString(), Encoding.Unicode);   // UTF-16 with BOM, read by GetPrivateProfile*
            return path;
        }

        // Overwrite then delete, so the password doesn't linger on disk.
        public static void Shred(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                long len = new FileInfo(path).Length;
                File.WriteAllBytes(path, new byte[len]);
                File.Delete(path);
            }
            catch { }
        }

        public static void WaitForYoungFiles(TimeSpan age)
        {
            try
            {
                if (!Directory.Exists(TempDir)) return;
                DateTime newest = DateTime.MinValue;
                foreach (var f in Directory.GetFiles(TempDir, "*.vnc"))
                {
                    var t = File.GetLastWriteTimeUtc(f);
                    if (t > newest) newest = t;
                }
                var wait = newest + age - DateTime.UtcNow;
                if (wait > TimeSpan.Zero && wait <= age) System.Threading.Thread.Sleep(wait);
            }
            catch { }
        }

        public static void CleanTemp(TimeSpan olderThan)
        {
            try
            {
                if (!Directory.Exists(TempDir)) return;
                foreach (var f in Directory.GetFiles(TempDir, "*.vnc"))
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(f) > olderThan) Shred(f);
            }
            catch { }
        }
    }

    class MainForm : Form
    {
        static readonly Color SelectedBack = Color.FromArgb(204, 228, 247);
        static readonly Color SelectedBorder = Color.FromArgb(0, 120, 215);
        static readonly string[] ScaleValues = { "", "auto", "100", "75", "50", "125", "150", "200" };
        static readonly string[] ScaleLabels = { "Same as TightVNC", "Fit to window", "100%", "75%", "50%", "125%", "150%", "200%" };

        AppData data;
        FlowLayoutPanel tiles;
        TextBox txtName, txtHost, txtPort, txtPass, txtExtra, txtViewer;
        CheckBox chkSave, chkShow;
        ComboBox cboScale;
        Label lblStatus;
        Label lblEditing, lblIconName, lblEmpty;
        Button btnDelete;
        readonly List<Button> pickerButtons = new List<Button>();
        readonly ToolTip tips = new ToolTip();
        int editIndex = -1;
        string selectedIcon = Icons.Names[0];

        public MainForm()
        {
            data = Store.Load();

            Text = "VNC Favourites " + Application.ProductVersion + " (TightVNC)";
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(684, 560);
            MinimumSize = new Size(640, 600);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // ----- Favourite tiles -----
            var lblFavs = AddLabel("Favourites  (click to connect, right-click to edit)", 12, 10);
            lblFavs.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            tiles = new FlowLayoutPanel();
            tiles.Location = new Point(12, 30);
            tiles.Size = new Size(350, 476);
            tiles.AutoScroll = true;
            tiles.BorderStyle = BorderStyle.FixedSingle;
            tiles.BackColor = Color.White;
            tiles.Padding = new Padding(4);
            tiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(tiles);

            lblEmpty = new Label();
            lblEmpty.Text = "No favourites yet.\r\nFill in the details on the right, pick an icon and press Save.";
            lblEmpty.ForeColor = Color.Gray;
            lblEmpty.AutoSize = false;
            lblEmpty.Size = new Size(320, 60);
            lblEmpty.Margin = new Padding(8);

            // ----- Editor -----
            const int x = 378, w = 294;
            lblEditing = AddLabel("New favourite", x, 10);
            lblEditing.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblEditing.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            AddLabel("Name", x, 32); txtName = AddBox(x, 50, w);
            AddLabel("Host (name or IP)", x, 76); txtHost = AddBox(x, 94, 200);
            AddLabel("Port", x + 210, 76); txtPort = AddBox(x + 210, 94, w - 210); txtPort.Text = "5900";
            AddLabel("Password", x, 120); txtPass = AddBox(x, 138, w); txtPass.UseSystemPasswordChar = true;

            chkSave = new CheckBox { Text = "Save password", Checked = true, AutoSize = true, Location = new Point(x, 164) };
            chkShow = new CheckBox { Text = "Show", AutoSize = true, Location = new Point(x + 130, 164) };
            chkShow.CheckedChanged += delegate { txtPass.UseSystemPasswordChar = !chkShow.Checked; };
            AddRight(chkSave); AddRight(chkShow);

            AddLabel("Scaling", x, 188);
            cboScale = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(x, 206), Size = new Size(150, 23) };
            cboScale.Items.AddRange(ScaleLabels);
            cboScale.SelectedIndex = 0;
            AddRight(cboScale);
            tips.SetToolTip(cboScale, "Same as TightVNC = the same scaling TightVNC uses when you connect from its own window");
            AddLabel("Extra options (optional)", x + 158, 188);
            txtExtra = AddBox(x + 158, 206, w - 158);
            tips.SetToolTip(txtExtra, "Extra tvnviewer options, e.g. -fullscreen=yes -viewonly=yes");

            AddLabel("Icon:", x, 236);
            lblIconName = AddLabel("", x + 36, 236);
            lblIconName.ForeColor = Color.DimGray;
            lblIconName.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            var picker = new Panel();
            picker.Location = new Point(x, 256);
            picker.Size = new Size(w, 150);
            picker.BorderStyle = BorderStyle.FixedSingle;
            picker.BackColor = Color.White;
            picker.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(picker);
            for (int i = 0; i < Icons.Names.Length; i++)
            {
                string n = Icons.Names[i];
                var b = new Button();
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Color.White;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 243, 251);
                b.BackColor = Color.White;
                b.Size = new Size(28, 28);
                b.Location = new Point(2 + (i % 10) * 29, 2 + (i / 10) * 29);
                b.Image = Icons.Get(n, 24);
                b.Tag = n;
                b.TabStop = false;
                b.Click += delegate { SelectIcon(n); };
                tips.SetToolTip(b, Icons.Pretty(n));
                picker.Controls.Add(b);
                pickerButtons.Add(b);
            }

            var btnNew = AddButton("New", x, 416, 94);
            var btnSave = AddButton("Save", x + 100, 416, 94);
            btnDelete = AddButton("Delete", x + 200, 416, 94);
            btnNew.Click += delegate { ClearEditor(); };
            btnSave.Click += delegate { SaveCurrent(); };
            btnDelete.Click += delegate { if (editIndex >= 0) DeleteAt(editIndex); };

            var btnConnect = AddButton("Connect with these details", x, 452, w);
            btnConnect.Height = 32;
            btnConnect.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btnConnect.Click += delegate { ConnectFromEditor(); };
            AcceptButton = btnConnect;

            lblStatus = new Label { Location = new Point(x, 490), Size = new Size(w, 30), ForeColor = Color.DimGray, AutoEllipsis = true };
            lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(lblStatus);

            // ----- Viewer path -----
            var lblViewer = AddLabel("Viewer:", 12, 527);
            lblViewer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            txtViewer = new TextBox { Location = new Point(62, 524), Size = new Size(518, 23), Text = data.ViewerPath };
            txtViewer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(txtViewer);
            var btnBrowse = new Button { Text = "Browse...", Location = new Point(586, 522), Size = new Size(86, 27) };
            btnBrowse.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnBrowse.Click += OnBrowse;
            Controls.Add(btnBrowse);

            FormClosing += delegate
            {
                SaveViewerPath();
                // Give a viewer that was started a moment ago time to read its file, then wipe them all.
                TightVncSettings.WaitForYoungFiles(TimeSpan.FromSeconds(3));
                TightVncSettings.CleanTemp(TimeSpan.Zero);
            };
            TightVncSettings.CleanTemp(TimeSpan.FromMinutes(1));
            SelectIcon(Icons.Names[0]);
            ClearEditor();
            RefreshTiles();
        }

        // ----- small builders -----
        Label AddLabel(string text, int px, int py)
        {
            var l = new Label { Text = text, AutoSize = true, Location = new Point(px, py) };
            if (px > 370) l.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(l);
            return l;
        }

        TextBox AddBox(int px, int py, int w)
        {
            var t = new TextBox { Location = new Point(px, py), Size = new Size(w, 23) };
            t.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(t);
            return t;
        }

        Button AddButton(string text, int px, int py, int w)
        {
            var b = new Button { Text = text, Location = new Point(px, py), Size = new Size(w, 28) };
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(b);
            return b;
        }

        void AddRight(Control c) { c.Anchor = AnchorStyles.Top | AnchorStyles.Right; Controls.Add(c); }

        // ----- tiles -----
        void RefreshTiles()
        {
            tiles.SuspendLayout();
            foreach (Control c in tiles.Controls) if (c != lblEmpty) c.Dispose();
            tiles.Controls.Clear();
            if (data.Favourites.Count == 0) tiles.Controls.Add(lblEmpty);
            for (int i = 0; i < data.Favourites.Count; i++)
                tiles.Controls.Add(MakeTile(i));
            tiles.ResumeLayout();
        }

        Button MakeTile(int index)
        {
            var f = data.Favourites[index];
            var b = new Button();
            b.Size = new Size(104, 92);
            b.Margin = new Padding(4);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = index == editIndex ? SelectedBorder : Color.FromArgb(210, 210, 210);
            b.FlatAppearance.BorderSize = index == editIndex ? 2 : 1;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 243, 251);
            b.BackColor = index == editIndex ? SelectedBack : Color.White;
            b.Image = Icons.Get(f.Icon, 48);
            b.ImageAlign = ContentAlignment.TopCenter;
            b.TextAlign = ContentAlignment.BottomCenter;
            b.TextImageRelation = TextImageRelation.ImageAboveText;
            b.Padding = new Padding(2, 6, 2, 2);
            b.AutoEllipsis = true;
            b.Text = f.Name;
            b.UseMnemonic = false;
            b.Cursor = Cursors.Hand;
            tips.SetToolTip(b, f.Name + "\r\n" + f.Host + ":" + f.Port + "\r\nClick to connect - right-click for options");

            b.Click += delegate { ConnectFavourite(index); };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Connect", null, delegate { ConnectFavourite(index); }).Font =
                new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add("Edit", null, delegate { LoadIntoEditor(index); });
            menu.Items.Add(new ToolStripSeparator());
            var left = menu.Items.Add("Move earlier", null, delegate { MoveTile(index, -1); });
            var right = menu.Items.Add("Move later", null, delegate { MoveTile(index, +1); });
            left.Enabled = index > 0;
            right.Enabled = index < data.Favourites.Count - 1;
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Delete", null, delegate { DeleteAt(index); });
            b.ContextMenuStrip = menu;
            return b;
        }

        void MoveTile(int index, int delta)
        {
            int j = index + delta;
            if (j < 0 || j >= data.Favourites.Count) return;
            var t = data.Favourites[index];
            data.Favourites[index] = data.Favourites[j];
            data.Favourites[j] = t;
            if (editIndex == index) editIndex = j;
            else if (editIndex == j) editIndex = index;
            SaveAll();
            RefreshTiles();
        }

        // ----- editor -----
        void SelectIcon(string name)
        {
            selectedIcon = name;
            foreach (var b in pickerButtons)
            {
                bool sel = (string)b.Tag == name;
                b.BackColor = sel ? SelectedBack : Color.White;
                b.FlatAppearance.BorderColor = sel ? SelectedBorder : Color.White;
            }
            lblIconName.Text = Icons.Pretty(name);
        }

        void LoadIntoEditor(int i)
        {
            var f = data.Favourites[i];
            editIndex = i;
            txtName.Text = f.Name;
            txtHost.Text = f.Host;
            txtPort.Text = f.Port.ToString();
            txtExtra.Text = f.Extra;
            cboScale.SelectedIndex = Math.Max(0, Array.IndexOf(ScaleValues, f.Scale ?? ""));
            txtPass.Text = Store.Unprotect(f.Password);
            chkSave.Checked = !string.IsNullOrEmpty(f.Password);
            SelectIcon(f.Icon);
            lblEditing.Text = "Editing: " + f.Name;
            btnDelete.Enabled = true;
            RefreshTiles();
            txtName.Focus();
        }

        void ClearEditor()
        {
            editIndex = -1;
            txtName.Text = ""; txtHost.Text = ""; txtPort.Text = "5900";
            txtPass.Text = ""; txtExtra.Text = ""; chkSave.Checked = true;
            cboScale.SelectedIndex = 0;
            SelectIcon(Icons.Names[0]);
            lblEditing.Text = "New favourite";
            btnDelete.Enabled = false;
            if (tiles != null) RefreshTiles();
            txtName.Focus();
        }

        bool ValidateEditor(out int port)
        {
            port = 0;
            if (txtHost.Text.Trim().Length == 0)
            {
                MessageBox.Show(this, "Please enter a host.", "VNC Favourites");
                txtHost.Focus();
                return false;
            }
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
            {
                MessageBox.Show(this, "Port must be a number from 1 to 65535.", "VNC Favourites");
                txtPort.Focus();
                return false;
            }
            return true;
        }

        void SaveCurrent()
        {
            int port;
            if (!ValidateEditor(out port)) return;
            string name = txtName.Text.Trim();
            if (name.Length == 0) name = txtHost.Text.Trim();
            var f = new Favourite
            {
                Name = name,
                Host = txtHost.Text.Trim(),
                Port = port,
                Password = chkSave.Checked ? Store.Protect(txtPass.Text) : "",
                Extra = txtExtra.Text.Trim(),
                Icon = selectedIcon,
                Scale = ScaleValues[Math.Max(0, cboScale.SelectedIndex)]
            };
            if (editIndex >= 0) data.Favourites[editIndex] = f;
            else { data.Favourites.Add(f); editIndex = data.Favourites.Count - 1; }
            data.ViewerPath = txtViewer.Text.Trim();
            SaveAll();
            txtName.Text = name;
            lblEditing.Text = "Editing: " + name;
            btnDelete.Enabled = true;
            RefreshTiles();
        }

        void DeleteAt(int i)
        {
            if (i < 0 || i >= data.Favourites.Count) return;
            if (MessageBox.Show(this, "Delete favourite '" + data.Favourites[i].Name + "'?", "VNC Favourites",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            data.Favourites.RemoveAt(i);
            SaveAll();
            if (editIndex == i) ClearEditor();
            else { if (editIndex > i) editIndex--; RefreshTiles(); }
        }

        // ----- persistence -----
        void SaveAll()
        {
            try { Store.Save(data); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't save favourites:\n" + ex.Message, "VNC Favourites",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void SaveViewerPath()
        {
            string p = txtViewer.Text.Trim();
            if (p != data.ViewerPath) { data.ViewerPath = p; SaveAll(); }
        }

        void OnBrowse(object sender, EventArgs e)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Locate tvnviewer.exe";
                dlg.Filter = "TightVNC Viewer (tvnviewer.exe)|tvnviewer.exe|Programs (*.exe)|*.exe";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txtViewer.Text = dlg.FileName;
                    SaveViewerPath();
                }
            }
        }

        // ----- connecting -----
        void ConnectFavourite(int i)
        {
            var f = data.Favourites[i];
            Launch(f.Name, f.Host, f.Port, Store.Unprotect(f.Password), f.Scale, f.Extra);
        }

        void ConnectFromEditor()
        {
            int port;
            if (!ValidateEditor(out port)) return;
            string nm = txtName.Text.Trim().Length > 0 ? txtName.Text.Trim() : txtHost.Text.Trim();
            Launch(nm, txtHost.Text.Trim(), port, txtPass.Text, ScaleValues[Math.Max(0, cboScale.SelectedIndex)], txtExtra.Text.Trim());
        }

        void Launch(string name, string host, int port, string password, string scale, string extra)
        {
            string viewer = txtViewer.Text.Trim();
            if (viewer.Length == 0 || !File.Exists(viewer))
            {
                MessageBox.Show(this, "Can't find tvnviewer.exe. Use Browse... to locate it.", "VNC Favourites",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SaveViewerPath();

            var args = new StringBuilder();
            string status;
            string source;
            var saved = TightVncSettings.Find(host, port, out source);
            string optionsFile;
            try { optionsFile = TightVncSettings.WriteOptionsFile(host, port, password, saved); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't prepare the connection:\n" + ex.Message, "VNC Favourites",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            args.Append("-optionsfile=\"").Append(optionsFile).Append("\"");
            if (saved != null) status = "Using " + source;
            else
            {
                status = "No saved TightVNC settings found";
                if (string.IsNullOrEmpty(scale)) { scale = "auto"; status += " - fitting to window"; }
            }
            if (!string.IsNullOrEmpty(scale))
            {
                args.Append(" -scale=").Append(scale);
                if (saved != null) status += ", scaling: " + ScaleLabels[Array.IndexOf(ScaleValues, scale)];
            }
            if (!string.IsNullOrEmpty(extra))
                args.Append(' ').Append(extra);

            try
            {
                var psi = new ProcessStartInfo(viewer, args.ToString());
                psi.UseShellExecute = false;
                psi.WorkingDirectory = Path.GetDirectoryName(viewer);
                var proc = Process.Start(psi);
                lblStatus.Text = "Opened " + name + ". " + status + ".";
                tips.SetToolTip(lblStatus, lblStatus.Text);
                // The viewer reads the options file as its very first step. Remove it as soon as
                // the viewer is up and idle (or after 15 seconds at the latest).
                string fileToDelete = optionsFile;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try { if (proc != null) proc.WaitForInputIdle(15000); } catch { }
                    System.Threading.Thread.Sleep(1500);
                    TightVncSettings.Shred(fileToDelete);
                });
            }
            catch (Exception ex)
            {
                TightVncSettings.Shred(optionsFile);
                MessageBox.Show(this, "Couldn't start the viewer:\n" + ex.Message, "VNC Favourites",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
