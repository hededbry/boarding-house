using System.Data;

namespace BoardingHouse;

class PaymentsPanel : UserControl, IRefreshable
{
    readonly DataGridView grid = UI.Grid();

    public PaymentsPanel()
    {
        Controls.Add(grid);
        Controls.Add(UI.Bar(UI.Btn("Record payment", (_, _) => Add()),
            UI.Btn("Delete", (_, _) => Delete()),
            UI.Btn("Receipt", (_, _) => Receipt())));
        RefreshData();
    }

    public void RefreshData()
    {
        grid.DataSource = Db.Query(@"
SELECT p.Id, p.PaidOn AS 'Date paid', t.Name AS Tenant, r.RoomNo AS Room, p.ForMonth AS 'For month', p.Amount, p.Notes
FROM Payments p JOIN Tenants t ON t.Id=p.TenantId LEFT JOIN Rooms r ON r.Id=t.RoomId
ORDER BY p.PaidOn DESC, p.Id DESC");
        grid.Columns["Id"]!.Visible = false;
    }

    void Add()
    {
        var tenant = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };
        var tenants = Db.Query("SELECT Id, Name FROM Tenants WHERE Active=1 ORDER BY Name");
        tenant.DataSource = tenants;
        var month = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM yyyy", Value = DateTime.Today };
        var amount = new NumericUpDown { Minimum = 0, Maximum = 1000000, DecimalPlaces = 2 };
        var paidOn = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
        var notes = new TextBox();
        void FillRate()
        {
            // combo isn't populated until the dialog is shown, so read the table row directly
            int idx = Math.Max(tenant.SelectedIndex, 0);
            if (idx < tenants.Rows.Count && Convert.ToInt64(tenants.Rows[idx]["Id"]) is long tid)
                amount.Value = (decimal)Db.Scalar<double>("SELECT IFNULL((SELECT r.MonthlyRate FROM Tenants t JOIN Rooms r ON r.Id=t.RoomId WHERE t.Id=@i),0)", ("@i", tid));
        }
        tenant.SelectedIndexChanged += (_, _) => FillRate();
        FillRate();

        bool ok = UI.Dialog("Record Payment",
            new[] { ("Tenant", (Control)tenant), ("For month", month), ("Amount", amount), ("Date paid", paidOn), ("Notes", notes) },
            () =>
            {
                if (tenant.SelectedValue == null) return "No active tenants.";
                if (amount.Value <= 0) return "Amount must be greater than 0.";
                return null;
            });
        if (!ok) return;
        Db.Exec("INSERT INTO Payments(TenantId,PaidOn,ForMonth,Amount,Notes) VALUES(@t,@p,@m,@a,@n)",
            ("@t", Convert.ToInt64(tenant.SelectedValue)), ("@p", paidOn.Value.ToString("yyyy-MM-dd")),
            ("@m", month.Value.ToString("yyyy-MM")), ("@a", (double)amount.Value), ("@n", notes.Text.Trim()));
        RefreshData();
    }

    void Delete()
    {
        if (UI.SelectedId(grid) is not long id) return;
        if (!UI.Confirm("Delete this payment record?")) return;
        Db.Exec("DELETE FROM Payments WHERE Id=@i", ("@i", id));
        RefreshData();
    }

    void Receipt()
    {
        if (UI.SelectedId(grid) is not long id) return;
        var r = Db.Query(@"SELECT p.*, t.Name, rm.RoomNo FROM Payments p JOIN Tenants t ON t.Id=p.TenantId
LEFT JOIN Rooms rm ON rm.Id=t.RoomId WHERE p.Id=@i", ("@i", id)).Rows[0];
        MessageBox.Show(
            $"BOARDING HOUSE - OFFICIAL RECEIPT\n\nReceipt #: {id:D5}\nDate: {r["PaidOn"]}\nReceived from: {r["Name"]}\nRoom: {r["RoomNo"]}\n" +
            $"For month: {r["ForMonth"]}\nAmount: {Convert.ToDecimal(r["Amount"]):N2}\nNotes: {r["Notes"]}",
            "Receipt", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
