namespace BoardingHouse;

class RoomsPanel : UserControl, IRefreshable
{
    readonly DataGridView grid = UI.Grid();

    public RoomsPanel()
    {
        Controls.Add(grid);
        Controls.Add(UI.Bar(UI.Btn("Add", (_, _) => Edit(null)),
            UI.Btn("Edit", (_, _) => { if (UI.SelectedId(grid) is long id) Edit(id); }),
            UI.Btn("Delete", (_, _) => Delete())));
        RefreshData();
    }

    public void RefreshData()
    {
        grid.DataSource = Db.Query(@"
SELECT r.Id, r.RoomNo AS Room, r.Capacity, r.MonthlyRate AS Rate,
  (SELECT COUNT(*) FROM Tenants t WHERE t.RoomId=r.Id AND t.Active=1) AS Occupied,
  CASE WHEN (SELECT COUNT(*) FROM Tenants t WHERE t.RoomId=r.Id AND t.Active=1) >= r.Capacity THEN 'Full' ELSE 'Available' END AS Status
FROM Rooms r ORDER BY r.RoomNo");
        grid.Columns["Id"]!.Visible = false;
    }

    void Edit(long? id)
    {
        var row = id == null ? null : Db.Query("SELECT * FROM Rooms WHERE Id=@i", ("@i", id)).Rows[0];
        var no = new TextBox { Text = row?["RoomNo"].ToString() };
        var cap = new NumericUpDown { Minimum = 1, Maximum = 50, Value = row == null ? 2 : Convert.ToInt32(row["Capacity"]) };
        var rate = new NumericUpDown { Minimum = 0, Maximum = 1000000, DecimalPlaces = 2, Value = row == null ? 1500 : Convert.ToDecimal(row["MonthlyRate"]) };
        bool ok = UI.Dialog(id == null ? "Add Room" : "Edit Room",
            new[] { ("Room no.", (Control)no), ("Capacity (beds)", cap), ("Monthly rate", rate) },
            () =>
            {
                if (string.IsNullOrWhiteSpace(no.Text)) return "Room number is required.";
                var dup = Db.Scalar<long>("SELECT COUNT(*) FROM Rooms WHERE RoomNo=@n AND Id<>@i", ("@n", no.Text.Trim()), ("@i", id ?? -1));
                if (dup > 0) return "Room number already exists.";
                if (id != null)
                {
                    var occ = Db.Scalar<long>("SELECT COUNT(*) FROM Tenants WHERE RoomId=@i AND Active=1", ("@i", id));
                    if (cap.Value < occ) return $"Room has {occ} tenants; capacity can't be lower.";
                }
                return null;
            });
        if (!ok) return;
        if (id == null)
            Db.Exec("INSERT INTO Rooms(RoomNo,Capacity,MonthlyRate) VALUES(@n,@c,@r)", ("@n", no.Text.Trim()), ("@c", (int)cap.Value), ("@r", (double)rate.Value));
        else
            Db.Exec("UPDATE Rooms SET RoomNo=@n,Capacity=@c,MonthlyRate=@r WHERE Id=@i", ("@n", no.Text.Trim()), ("@c", (int)cap.Value), ("@r", (double)rate.Value), ("@i", id));
        RefreshData();
    }

    void Delete()
    {
        if (UI.SelectedId(grid) is not long id) return;
        if (Db.Scalar<long>("SELECT COUNT(*) FROM Tenants WHERE RoomId=@i", ("@i", id)) > 0)
        { UI.Error("Room has tenant records; reassign or remove them first."); return; }
        if (!UI.Confirm("Delete this room?")) return;
        Db.Exec("DELETE FROM Rooms WHERE Id=@i", ("@i", id));
        RefreshData();
    }
}
