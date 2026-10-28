using System.Data;

namespace BoardingHouse;

class TenantsPanel : UserControl, IRefreshable
{
    readonly DataGridView grid = UI.Grid();

    public TenantsPanel()
    {
        Controls.Add(grid);
        Controls.Add(UI.Bar(UI.Btn("Add / Check-in", (_, _) => Edit(null)),
            UI.Btn("Edit", (_, _) => { if (UI.SelectedId(grid) is long id) Edit(id); }),
            UI.Btn("Check-out", (_, _) => CheckOut()),
            UI.Btn("Delete", (_, _) => Delete())));
        RefreshData();
    }

    public void RefreshData()
    {
        grid.DataSource = Db.Query(@"
SELECT t.Id, t.Name, t.Contact, r.RoomNo AS Room, t.CheckIn AS 'Check-in', t.CheckOut AS 'Check-out',
  CASE t.Active WHEN 1 THEN 'Active' ELSE 'Moved out' END AS Status
FROM Tenants t LEFT JOIN Rooms r ON r.Id=t.RoomId ORDER BY t.Active DESC, t.Name");
        grid.Columns["Id"]!.Visible = false;
    }

    void Edit(long? id)
    {
        var row = id == null ? null : Db.Query("SELECT * FROM Tenants WHERE Id=@i", ("@i", id)).Rows[0];
        bool active = row == null || Convert.ToInt32(row["Active"]) == 1;
        var name = new TextBox { Text = row?["Name"].ToString() };
        var contact = new TextBox { Text = row?["Contact"].ToString() };
        var room = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Label", ValueMember = "Id" };
        room.DataSource = Db.Query(@"
SELECT r.Id, r.RoomNo || ' (' || (SELECT COUNT(*) FROM Tenants t WHERE t.RoomId=r.Id AND t.Active=1) || '/' || r.Capacity || ')' AS Label
FROM Rooms r ORDER BY r.RoomNo");
        if (row != null && row["RoomId"] != DBNull.Value)
        {
            // SelectedValue can't be set before the dialog is shown, so select by index
            var rid = Convert.ToInt64(row["RoomId"]);
            for (int i = 0; i < room.Items.Count; i++)
                if (Convert.ToInt64(((DataRowView)room.Items[i]!)["Id"]) == rid) room.SelectedIndex = i;
        }
        var checkIn = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = row == null ? DateTime.Today : DateTime.Parse(row["CheckIn"].ToString()!) };

        bool ok = UI.Dialog(id == null ? "Add Tenant" : "Edit Tenant",
            new[] { ("Name", (Control)name), ("Contact", contact), ("Room", room), ("Check-in date", checkIn) },
            () =>
            {
                if (string.IsNullOrWhiteSpace(name.Text)) return "Name is required.";
                if (room.SelectedValue == null) return "Add a room first, then pick one.";
                if (active)
                {
                    var rid = Convert.ToInt64(room.SelectedValue);
                    var occ = Db.Scalar<long>("SELECT COUNT(*) FROM Tenants WHERE RoomId=@r AND Active=1 AND Id<>@i", ("@r", rid), ("@i", id ?? -1));
                    var cap = Db.Scalar<long>("SELECT Capacity FROM Rooms WHERE Id=@r", ("@r", rid));
                    if (occ >= cap) return "That room is full.";
                }
                return null;
            });
        if (!ok) return;
        var args = new (string, object?)[] {
            ("@n", name.Text.Trim()), ("@c", contact.Text.Trim()), ("@r", Convert.ToInt64(room.SelectedValue)),
            ("@d", checkIn.Value.ToString("yyyy-MM-dd")), ("@i", id) };
        if (id == null)
            Db.Exec("INSERT INTO Tenants(Name,Contact,RoomId,CheckIn) VALUES(@n,@c,@r,@d)", args[..4]);
        else
            Db.Exec("UPDATE Tenants SET Name=@n,Contact=@c,RoomId=@r,CheckIn=@d WHERE Id=@i", args);
        RefreshData();
    }

    void CheckOut()
    {
        if (UI.SelectedId(grid) is not long id) return;
        if (Db.Scalar<long>("SELECT Active FROM Tenants WHERE Id=@i", ("@i", id)) == 0)
        { UI.Error("Tenant already moved out."); return; }
        if (!UI.Confirm("Check out this tenant today?")) return;
        Db.Exec("UPDATE Tenants SET Active=0, CheckOut=@d WHERE Id=@i", ("@d", DateTime.Today.ToString("yyyy-MM-dd")), ("@i", id));
        RefreshData();
    }

    void Delete()
    {
        if (UI.SelectedId(grid) is not long id) return;
        if (Db.Scalar<long>("SELECT COUNT(*) FROM Payments WHERE TenantId=@i", ("@i", id)) > 0)
        { UI.Error("Tenant has payment records; use Check-out instead."); return; }
        if (!UI.Confirm("Delete this tenant?")) return;
        Db.Exec("DELETE FROM Tenants WHERE Id=@i", ("@i", id));
        RefreshData();
    }
}
