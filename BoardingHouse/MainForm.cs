namespace BoardingHouse;

class MainForm : Form
{
    public MainForm(string username)
    {
        Text = $"Boarding House Management - {username}";
        Size = new Size(950, 600);
        StartPosition = FormStartPosition.CenterScreen;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        foreach (var (title, ctl) in new (string, UserControl)[] {
            ("Rooms", new RoomsPanel()), ("Tenants", new TenantsPanel()),
            ("Payments", new PaymentsPanel()), ("Reports", new ReportsPanel()) })
        {
            ctl.Dock = DockStyle.Fill;
            var tp = new TabPage(title);
            tp.Controls.Add(ctl);
            tabs.TabPages.Add(tp);
        }
        // refresh when a tab is shown so cross-tab changes appear
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedTab?.Controls[0] is IRefreshable r) r.RefreshData();
        };
        Controls.Add(tabs);
    }
}

interface IRefreshable { void RefreshData(); }

static class UI
{
    public static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false,
        BackgroundColor = SystemColors.Window,
    };

    public static void Error(string msg) => MessageBox.Show(msg, "Boarding House", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool Confirm(string msg) =>
        MessageBox.Show(msg, "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static Button Btn(string text, EventHandler onClick)
    {
        var b = new Button { Text = text, AutoSize = true };
        b.Click += onClick;
        return b;
    }

    public static FlowLayoutPanel Bar(params Control[] c)
    {
        var p = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4) };
        p.Controls.AddRange(c);
        return p;
    }

    public static long? SelectedId(DataGridView g) =>
        g.CurrentRow == null ? null : Convert.ToInt64(g.CurrentRow.Cells["Id"].Value);

    // Modal form with labelled fields and Save/Cancel. Returns true when saved and validation passed.
    public static bool Dialog(string title, (string label, Control ctl)[] fields, Func<string?> validate)
    {
        using var f = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false, MinimizeBox = false, ClientSize = new Size(360, 30 + fields.Length * 34 + 50),
        };
        int y = 15;
        foreach (var (label, ctl) in fields)
        {
            f.Controls.Add(new Label { Text = label, Left = 15, Top = y + 4, Width = 110 });
            ctl.Left = 130; ctl.Top = y; ctl.Width = 210;
            f.Controls.Add(ctl);
            y += 34;
        }
        var ok = new Button { Text = "Save", Left = 170, Top = y + 5, Width = 80 };
        var cancel = new Button { Text = "Cancel", Left = 260, Top = y + 5, Width = 80, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) =>
        {
            var err = validate();
            if (err != null) Error(err); else f.DialogResult = DialogResult.OK;
        };
        f.Controls.AddRange(new Control[] { ok, cancel });
        f.AcceptButton = ok; f.CancelButton = cancel;
        return f.ShowDialog() == DialogResult.OK;
    }
}
