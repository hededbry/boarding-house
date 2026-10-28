using System.Data;
using System.Text;

namespace BoardingHouse;

class ReportsPanel : UserControl, IRefreshable
{
    readonly DataGridView grid = UI.Grid();
    readonly Label summary = new() { Dock = DockStyle.Top, Height = 30, Padding = new Padding(6, 8, 0, 0) };
    DataTable current = new();
    string currentName = "report";

    public ReportsPanel()
    {
        Controls.Add(grid);
        Controls.Add(summary);
        Controls.Add(UI.Bar(UI.Btn("Occupancy", (_, _) => Occupancy()),
            UI.Btn("Tenant balances", (_, _) => Balances()),
            UI.Btn("Monthly income", (_, _) => Income()),
            UI.Btn("Export CSV", (_, _) => Export())));
        Occupancy();
    }

    public void RefreshData() => Occupancy();

    void Show(string name, DataTable dt, string text)
    {
        currentName = name; current = dt; grid.DataSource = dt; summary.Text = text;
    }

    void Occupancy()
    {
        var dt = Db.Query(@"
SELECT r.RoomNo AS Room, r.Capacity, COUNT(t.Id) AS Occupied, r.Capacity - COUNT(t.Id) AS 'Beds free'
FROM Rooms r LEFT JOIN Tenants t ON t.RoomId=r.Id AND t.Active=1 GROUP BY r.Id ORDER BY r.RoomNo");
        long cap = 0, occ = 0;
        foreach (DataRow r in dt.Rows) { cap += Convert.ToInt64(r["Capacity"]); occ += Convert.ToInt64(r["Occupied"]); }
        Show("occupancy", dt, $"Beds: {occ}/{cap} occupied ({(cap == 0 ? 0 : occ * 100 / cap)}%)");
    }

    // Due = months from check-in month through the current month (or check-out month) x room rate, minus payments.
    void Balances()
    {
        var src = Db.Query(@"
SELECT t.Name AS Tenant, r.RoomNo AS Room, r.MonthlyRate AS Rate, t.CheckIn, t.CheckOut,
  IFNULL((SELECT SUM(Amount) FROM Payments p WHERE p.TenantId=t.Id),0) AS Paid
FROM Tenants t LEFT JOIN Rooms r ON r.Id=t.RoomId ORDER BY t.Name");
        var dt = new DataTable();
        foreach (var c in new[] { "Tenant", "Room" }) dt.Columns.Add(c);
        foreach (var c in new[] { "Months", "Total due", "Paid", "Balance" }) dt.Columns.Add(c, typeof(decimal));
        decimal totalBal = 0;
        foreach (DataRow r in src.Rows)
        {
            var start = DateTime.Parse(r["CheckIn"].ToString()!);
            var end = r["CheckOut"] == DBNull.Value ? DateTime.Today : DateTime.Parse(r["CheckOut"].ToString()!);
            int months = Math.Max(1, (end.Year - start.Year) * 12 + end.Month - start.Month + 1);
            decimal rate = r["Rate"] == DBNull.Value ? 0 : Convert.ToDecimal(r["Rate"]);
            decimal due = months * rate, paid = Convert.ToDecimal(r["Paid"]);
            totalBal += due - paid;
            dt.Rows.Add(r["Tenant"], r["Room"], months, due, paid, due - paid);
        }
        Show("balances", dt, $"Total outstanding balance: {totalBal:N2}  (positive = owed, negative = overpaid)");
    }

    void Income()
    {
        var dt = Db.Query(@"SELECT ForMonth AS Month, COUNT(*) AS Payments, SUM(Amount) AS Total
FROM Payments GROUP BY ForMonth ORDER BY ForMonth DESC");
        decimal sum = 0;
        foreach (DataRow r in dt.Rows) sum += Convert.ToDecimal(r["Total"]);
        Show("income", dt, $"All-time income: {sum:N2}");
    }

    void Export()
    {
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"{currentName}.csv" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", current.Columns.Cast<DataColumn>().Select(c => Csv(c.ColumnName))));
        foreach (DataRow r in current.Rows)
            sb.AppendLine(string.Join(",", r.ItemArray.Select(v => Csv(v?.ToString() ?? ""))));
        File.WriteAllText(dlg.FileName, sb.ToString());
        MessageBox.Show("Saved.", "Export");
    }

    static string Csv(string s) => s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
