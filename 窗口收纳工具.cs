using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new PocketForm());
    }
}

internal static class Native
{
    public const int SW_HIDE = 0, SW_SHOW = 5, SW_RESTORE = 9, SW_SHOWMAXIMIZED = 3;
    public const int GWL_EXSTYLE = -20;
    public const int GWL_STYLE = -16;
    public const long WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000;
    public const long WS_CHILD = 0x40000000L, WS_POPUP = 0x80000000L,
        WS_CAPTION = 0x00C00000L, WS_THICKFRAME = 0x00040000L,
        WS_SYSMENU = 0x00080000L, WS_MINIMIZEBOX = 0x00020000L, WS_MAXIMIZEBOX = 0x00010000L;
    public const uint GW_OWNER = 4;
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Placement
    {
        public int Length, Flags, ShowCmd;
        public Point MinPosition, MaxPosition;
        public Rect NormalPosition;
    }
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindowEnabled(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool EnableWindow(IntPtr hwnd, bool enable);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after,
        int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int length);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetLong64(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetLong32(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetLong64(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetLong32(IntPtr hwnd, int index, int value);
    [DllImport("user32.dll")] public static extern bool GetWindowPlacement(IntPtr hwnd, ref Placement placement);
    [DllImport("user32.dll")] public static extern bool SetWindowPlacement(IntPtr hwnd, ref Placement placement);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
    public static long ExStyle(IntPtr hwnd) { return IntPtr.Size == 8 ? GetLong64(hwnd, GWL_EXSTYLE).ToInt64() : (uint)GetLong32(hwnd, GWL_EXSTYLE); }
    public static long Style(IntPtr hwnd) { return IntPtr.Size == 8 ? GetLong64(hwnd, GWL_STYLE).ToInt64() : (uint)GetLong32(hwnd, GWL_STYLE); }
    public static void SetLong(IntPtr hwnd, int index, long value)
    {
        if (IntPtr.Size == 8) SetLong64(hwnd, index, new IntPtr(value));
        else SetLong32(hwnd, index, unchecked((int)value));
    }
    public static string Title(IntPtr hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        if (length <= 0) return "";
        StringBuilder text = new StringBuilder(length + 1);
        GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }
    public static string App(uint pid)
    {
        try { using (Process process = Process.GetProcessById((int)pid)) return process.ProcessName; }
        catch { return "PID " + pid; }
    }
}

internal sealed class PocketItem
{
    public IntPtr Handle;
    public uint ProcessId;
    public string Group;
    public string Title;
    public string App;
    public Native.Placement Placement;
    public IntPtr Parent;
    public long Style, ExStyle;
    public Panel Preview;
    public PictureBox PreviewImage;
    public Button PinButton;
    public Bitmap Snapshot;
    public bool Pinned;
    public bool Docked;
    public bool WasEnabled;
    public override string ToString() { return Title; }
}

internal sealed class PocketForm : Form
{
    private readonly List<PocketItem> items = new List<PocketItem>();
    private readonly TreeView tree = new TreeView();
    private readonly Label summary = new Label();
    private readonly FlowLayoutPanel previews = new FlowLayoutPanel();
    private readonly NotifyIcon tray = new NotifyIcon();
    private readonly Timer watch = new Timer();
    private string activeGroup;
    private DateTime activatedAt;
    private DateTime outsideSince;
    private bool exiting;

    public PocketForm()
    {
        Text = "窗口收纳盒";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ClientSize = new Size(365, 355);
        StartPosition = FormStartPosition.Manual;
        Rectangle work = Screen.PrimaryScreen.WorkingArea;
        Location = new System.Drawing.Point(work.Right - Width - 14, work.Top + 14);
        TopMost = true;
        ShowInTaskbar = false;
        Font = new Font("Microsoft YaHei UI", 9F);

        Label heading = new Label { Text = "窗口收纳盒", Font = new Font(Font, FontStyle.Bold), Location = new System.Drawing.Point(10, 9), AutoSize = true };
        Button add = new Button { Text = "+ 收纳窗口", Location = new System.Drawing.Point(219, 5), Size = new Size(100, 27) };
        add.Click += delegate { OpenManager(); };
        tree.SetBounds(10, 38, 344, 95);
        tree.HideSelection = false;
        tree.NodeMouseClick += delegate(object sender, TreeNodeMouseClickEventArgs e) {
            if (e.Button == MouseButtons.Right)
            {
                ContextMenuStrip nodeMenu = new ContextMenuStrip();
                PocketItem selected = e.Node.Tag as PocketItem;
                if (selected != null)
                {
                    nodeMenu.Items.Add(selected.Pinned ? "取消临时固定" : "临时固定此窗口", null, delegate { TogglePin(selected); });
                    nodeMenu.Items.Add("取消收纳此窗口", null, delegate { RemoveItem(selected); });
                }
                else if (e.Node.Tag is string)
                {
                    string selectedGroup = (string)e.Node.Tag;
                    nodeMenu.Items.Add("取消收纳整组", null, delegate { RemoveGroup(selectedGroup); });
                }
                nodeMenu.Show(tree, e.Location);
                return;
            }
            PocketItem item = e.Node.Tag as PocketItem;
            if (item != null) ActivateGroup(item.Group, item);
            else if (e.Node.Tag is string) ActivateGroup((string)e.Node.Tag, null);
        };
        previews.SetBounds(10, 139, 344, 187);
        previews.AutoScroll = true;
        previews.WrapContents = true;
        previews.BackColor = Color.FromArgb(35, 35, 35);
        summary.SetBounds(10, 330, 344, 20);
        Controls.AddRange(new Control[] { heading, add, tree, previews, summary });

        tray.Icon = SystemIcons.Application;
        tray.Text = "窗口收纳盒";
        tray.Visible = true;
        tray.DoubleClick += delegate { ShowPocket(); };
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add("显示收纳盒", null, delegate { ShowPocket(); });
        menu.Items.Add("添加窗口", null, delegate { OpenManager(); });
        menu.Items.Add("恢复全部并退出", null, delegate { ExitAndRestore(); });
        tray.ContextMenuStrip = menu;

        watch.Interval = 300;
        watch.Tick += delegate { WatchFocus(); };
        watch.Start();
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            watch.Stop();
            RestoreAll();
            tray.Visible = false;
            tray.Dispose();
        };
        RefreshTree();
    }

    private void ShowPocket()
    {
        Show();
        Activate();
    }

    private void OpenManager()
    {
        ShowPocket();
        using (PickerForm picker = new PickerForm(items))
        {
            if (picker.ShowDialog(this) != DialogResult.OK) return;
            foreach (PickerForm.Choice choice in picker.Selected)
            {
                try { Collect(choice.Handle, picker.GroupName); }
                catch (Exception ex) { MessageBox.Show(this, "收纳失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }
        RefreshTree();
    }

    private void Collect(IntPtr hwnd, string group)
    {
        if (!Native.IsWindow(hwnd)) return;
        foreach (PocketItem item in items) if (item.Handle == hwnd) return;
        uint pid;
        Native.GetWindowThreadProcessId(hwnd, out pid);
        Native.Placement placement = new Native.Placement { Length = Marshal.SizeOf(typeof(Native.Placement)) };
        if (!Native.GetWindowPlacement(hwnd, ref placement)) throw new InvalidOperationException("无法读取窗口位置。");
        PocketItem collected = new PocketItem {
            Handle = hwnd, ProcessId = pid, Group = group, Title = Native.Title(hwnd),
            App = Native.App(pid), Placement = placement,
            Parent = Native.GetParent(hwnd), Style = Native.Style(hwnd), ExStyle = Native.ExStyle(hwnd),
            WasEnabled = Native.IsWindowEnabled(hwnd)
        };
        items.Add(collected);
        CreatePreview(collected);
        DockToPreview(collected, false);
    }

    private void CreatePreview(PocketItem item)
    {
        Panel card = new Panel { Size = new Size(160, 151), Margin = new Padding(3), BackColor = Color.White };
        Button open = new Button { Text = item.Title.Length > 8 ? item.Title.Substring(0, 7) + "…" : item.Title,
            Location = new System.Drawing.Point(0, 0), Size = new Size(160, 26), FlatStyle = FlatStyle.Flat };
        open.Click += delegate { ActivateGroup(item.Group, item); };
        Button pin = new Button { Text = "临时固定", Location = new System.Drawing.Point(0, 123),
            Size = new Size(80, 27), FlatStyle = FlatStyle.Flat };
        pin.Click += delegate { TogglePin(item); };
        Button remove = new Button { Text = "取消收纳", Location = new System.Drawing.Point(80, 123),
            Size = new Size(80, 27), FlatStyle = FlatStyle.Flat };
        remove.Click += delegate { RemoveItem(item); };
        Panel image = new Panel { Location = new System.Drawing.Point(2, 28), Size = new Size(156, 93),
            BackColor = Color.Black };
        PictureBox picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black };
        picture.Click += delegate { ActivateGroup(item.Group, item); };
        image.Controls.Add(picture);
        card.Controls.AddRange(new Control[] { open, pin, remove, image });
        previews.Controls.Add(card);
        item.Preview = image;
        item.PreviewImage = picture;
        item.PinButton = pin;
    }

    private void TogglePin(PocketItem item)
    {
        if (!items.Contains(item)) return;
        item.Pinned = !item.Pinned;
        if (item.Pinned) UndockToNormal(item);
        else if (!string.Equals(activeGroup, item.Group, StringComparison.OrdinalIgnoreCase)) DockToPreview(item, true);
        if (item.PinButton != null) item.PinButton.Text = item.Pinned ? "取消固定" : "临时固定";
        RefreshTree();
    }

    private void DockToPreview(PocketItem item, bool updatePlacement)
    {
        if (item.Pinned) return;
        if (!Native.IsWindow(item.Handle)) return;
        uint pid;
        Native.GetWindowThreadProcessId(item.Handle, out pid);
        if (pid != item.ProcessId) return;
        if (updatePlacement)
        {
            Native.Placement placement = new Native.Placement { Length = Marshal.SizeOf(typeof(Native.Placement)) };
            if (Native.GetWindowPlacement(item.Handle, ref placement)) item.Placement = placement;
        }
        CaptureSnapshot(item);
        Native.ShowWindow(item.Handle, Native.SW_HIDE);
        item.Docked = false;
    }

    private void CaptureSnapshot(PocketItem item)
    {
        Native.Rect rect;
        if (!Native.GetWindowRect(item.Handle, out rect)) return;
        int width = Math.Min(1920, Math.Max(1, rect.Right - rect.Left));
        int height = Math.Min(1080, Math.Max(1, rect.Bottom - rect.Top));
        Bitmap bitmap = new Bitmap(width, height);
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = graphics.GetHdc();
            bool printed;
            try { printed = Native.PrintWindow(item.Handle, hdc, 0); }
            finally { graphics.ReleaseHdc(hdc); }
            if (!printed)
            {
                try { graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height)); }
                catch { }
            }
        }
        Bitmap previous = item.Snapshot;
        item.Snapshot = bitmap;
        if (item.PreviewImage != null) item.PreviewImage.Image = bitmap;
        if (previous != null) previous.Dispose();
    }

    private void UndockToNormal(PocketItem item)
    {
        if (!Native.IsWindow(item.Handle)) return;
        uint pid;
        Native.GetWindowThreadProcessId(item.Handle, out pid);
        if (pid != item.ProcessId) return;
        // The window was hidden, not minimized. Showing it preserves its exact
        // size and position; restoring placement can resize game render targets.
        Native.ShowWindow(item.Handle, Native.SW_SHOW);
    }

    private void RemoveItem(PocketItem item)
    {
        if (!items.Contains(item)) return;
        UndockToNormal(item);
        items.Remove(item);
        if (item.Preview != null)
        {
            Control card = item.Preview.Parent;
            previews.Controls.Remove(card);
            card.Dispose();
        }
        if (item.Snapshot != null) item.Snapshot.Dispose();
        if (activeGroup != null && !items.Exists(i => string.Equals(i.Group, activeGroup, StringComparison.OrdinalIgnoreCase)))
            activeGroup = null;
        RefreshTree();
    }

    private void RemoveGroup(string group)
    {
        foreach (PocketItem item in new List<PocketItem>(items))
            if (string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase)) RemoveItem(item);
    }

    private void ActivateGroup(string group, PocketItem preferred)
    {
        if (activeGroup != null && !string.Equals(activeGroup, group, StringComparison.OrdinalIgnoreCase)) HideActive();
        activeGroup = group;
        PocketItem foreground = preferred;
        foreach (PocketItem item in items)
        {
            if (!string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Native.IsWindow(item.Handle)) continue;
            uint pid;
            Native.GetWindowThreadProcessId(item.Handle, out pid);
            if (pid != item.ProcessId) continue;
            UndockToNormal(item);
            if (foreground == null) foreground = item;
        }
        if (foreground != null && Native.IsWindow(foreground.Handle))
        {
            Native.BringWindowToTop(foreground.Handle);
            Native.SetForegroundWindow(foreground.Handle);
        }
        activatedAt = DateTime.UtcNow;
        outsideSince = DateTime.MinValue;
        RefreshTree();
    }

    private void HideActive()
    {
        if (activeGroup == null) return;
        foreach (PocketItem item in items)
        {
            if (!string.Equals(item.Group, activeGroup, StringComparison.OrdinalIgnoreCase) || !Native.IsWindow(item.Handle)) continue;
            uint pid;
            Native.GetWindowThreadProcessId(item.Handle, out pid);
            if (pid == item.ProcessId && !item.Pinned) DockToPreview(item, true);
        }
        activeGroup = null;
        outsideSince = DateTime.MinValue;
        RefreshTree();
    }

    private void WatchFocus()
    {
        bool changed = false;
        foreach (PocketItem item in new List<PocketItem>(items))
        {
            if (!Native.IsWindow(item.Handle))
            {
                if (item.Preview != null) { Control card = item.Preview.Parent; previews.Controls.Remove(card); card.Dispose(); }
                if (item.Snapshot != null) item.Snapshot.Dispose();
                items.Remove(item); changed = true;
            }
        }
        if (activeGroup != null && !items.Exists(item => string.Equals(item.Group, activeGroup, StringComparison.OrdinalIgnoreCase)))
            activeGroup = null;
        if (changed) RefreshTree();
        if (activeGroup == null || (DateTime.UtcNow - activatedAt).TotalMilliseconds < 700) return;
        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return;
        uint pid;
        Native.GetWindowThreadProcessId(foreground, out pid);
        bool inGroup = false;
        foreach (PocketItem item in items)
            if (string.Equals(item.Group, activeGroup, StringComparison.OrdinalIgnoreCase) && item.ProcessId == pid)
                { inGroup = true; break; }
        if (inGroup || pid == (uint)Process.GetCurrentProcess().Id)
        {
            outsideSince = DateTime.MinValue;
            return;
        }
        if (outsideSince == DateTime.MinValue) outsideSince = DateTime.UtcNow;
        else if ((DateTime.UtcNow - outsideSince).TotalMilliseconds >= 500) HideActive();
    }

    private void RefreshTree()
    {
        tree.BeginUpdate();
        tree.Nodes.Clear();
        Dictionary<string, TreeNode> groups = new Dictionary<string, TreeNode>(StringComparer.OrdinalIgnoreCase);
        foreach (PocketItem item in items)
        {
            TreeNode group;
            if (!groups.TryGetValue(item.Group, out group))
            {
                group = new TreeNode((string.Equals(item.Group, activeGroup, StringComparison.OrdinalIgnoreCase) ? "▶ " : "") + item.Group);
                group.Tag = item.Group;
                groups.Add(item.Group, group);
                tree.Nodes.Add(group);
            }
            TreeNode child = new TreeNode((item.Pinned ? "[固定] " : "") + item.Title);
            child.Tag = item;
            group.Nodes.Add(child);
        }
        tree.ExpandAll();
        tree.EndUpdate();
        summary.Text = items.Count == 0 ? "点击“+ 收纳窗口”开始。" :
            "已收纳 " + items.Count + " 个窗口；临时固定可暂停自动收纳。";
    }

    private void RestoreAll()
    {
        foreach (PocketItem item in new List<PocketItem>(items))
        {
            if (!Native.IsWindow(item.Handle)) continue;
            uint pid;
            Native.GetWindowThreadProcessId(item.Handle, out pid);
            if (pid != item.ProcessId) continue;
            UndockToNormal(item);
        }
        items.Clear();
        activeGroup = null;
        RefreshTree();
    }

    private void ExitAndRestore()
    {
        exiting = true;
        Close();
    }
}

internal sealed class PickerForm : Form
{
    internal sealed class Choice
    {
        public IntPtr Handle;
        public string App, Title;
    }
    private readonly ListView list = new ListView();
    private readonly ComboBox group = new ComboBox();
    private readonly HashSet<IntPtr> already = new HashSet<IntPtr>();
    public readonly List<Choice> Selected = new List<Choice>();
    public string GroupName { get { return string.IsNullOrWhiteSpace(group.Text) ? "未分类" : group.Text.Trim(); } }

    public PickerForm(IEnumerable<PocketItem> collected)
    {
        foreach (PocketItem item in collected) already.Add(item.Handle);
        Text = "选择要收纳的窗口";
        ClientSize = new Size(680, 460);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Microsoft YaHei UI", 9F);
        Label label = new Label { Text = "分组名称", Location = new System.Drawing.Point(12, 14), AutoSize = true };
        group.SetBounds(82, 10, 210, 27);
        group.Items.AddRange(new object[] { "游戏", "工具", "其他" });
        group.Text = "游戏";
        Button refresh = new Button { Text = "刷新", Location = new System.Drawing.Point(560, 8), Size = new Size(105, 29) };
        refresh.Click += delegate { RefreshList(); };
        list.SetBounds(12, 45, 653, 360);
        list.View = View.Details;
        list.FullRowSelect = true;
        list.MultiSelect = true;
        list.HideSelection = false;
        list.Columns.Add("程序", 130);
        list.Columns.Add("窗口标题", 510);
        Button add = new Button { Text = "收纳选中窗口", Location = new System.Drawing.Point(525, 416), Size = new Size(140, 30) };
        add.Click += delegate {
            foreach (ListViewItem row in list.SelectedItems) Selected.Add((Choice)row.Tag);
            if (Selected.Count == 0) return;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.AddRange(new Control[] { label, group, refresh, list, add });
        RefreshList();
    }

    private void RefreshList()
    {
        list.BeginUpdate();
        list.Items.Clear();
        Native.EnumWindows(delegate(IntPtr hwnd, IntPtr param) {
            if (!Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd) || already.Contains(hwnd)) return true;
            string title = Native.Title(hwnd);
            if (title.Length == 0) return true;
            long style = Native.ExStyle(hwnd);
            if ((style & Native.WS_EX_TOOLWINDOW) != 0 && (style & Native.WS_EX_APPWINDOW) == 0) return true;
            if (Native.GetWindow(hwnd, Native.GW_OWNER) != IntPtr.Zero && (style & Native.WS_EX_APPWINDOW) == 0) return true;
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return true;
            Choice choice = new Choice { Handle = hwnd, App = Native.App(pid), Title = title };
            ListViewItem row = new ListViewItem(new string[] { choice.App, choice.Title });
            row.Tag = choice;
            list.Items.Add(row);
            return true;
        }, IntPtr.Zero);
        list.EndUpdate();
    }
}
