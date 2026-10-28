using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace BoardingHouse;

static class Db
{
    static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "boardinghouse.db");
    static string ConnStr => $"Data Source={DbPath}";

    public static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    public static void Init()
    {
        Exec(@"
CREATE TABLE IF NOT EXISTS Users(Id INTEGER PRIMARY KEY, Username TEXT UNIQUE NOT NULL, PasswordHash TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS Rooms(Id INTEGER PRIMARY KEY, RoomNo TEXT UNIQUE NOT NULL, Capacity INTEGER NOT NULL, MonthlyRate REAL NOT NULL);
CREATE TABLE IF NOT EXISTS Tenants(Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Contact TEXT, RoomId INTEGER REFERENCES Rooms(Id),
    CheckIn TEXT NOT NULL, CheckOut TEXT, Active INTEGER NOT NULL DEFAULT 1);
CREATE TABLE IF NOT EXISTS Payments(Id INTEGER PRIMARY KEY, TenantId INTEGER NOT NULL REFERENCES Tenants(Id),
    PaidOn TEXT NOT NULL, ForMonth TEXT NOT NULL, Amount REAL NOT NULL, Notes TEXT);");
        if (Scalar<long>("SELECT COUNT(*) FROM Users") == 0)
            Exec("INSERT INTO Users(Username,PasswordHash) VALUES(@u,@p)", ("@u", "admin"), ("@p", Hash("admin123")));
    }

    static SqliteConnection Open()
    {
        var c = new SqliteConnection(ConnStr);
        c.Open();
        using var p = c.CreateCommand();
        p.CommandText = "PRAGMA foreign_keys=ON";
        p.ExecuteNonQuery();
        return c;
    }

    static SqliteCommand Cmd(SqliteConnection c, string sql, (string, object?)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd;
    }

    public static void Exec(string sql, params (string, object?)[] ps)
    {
        using var c = Open();
        using var cmd = Cmd(c, sql, ps);
        cmd.ExecuteNonQuery();
    }

    public static T Scalar<T>(string sql, params (string, object?)[] ps)
    {
        using var c = Open();
        using var cmd = Cmd(c, sql, ps);
        var r = cmd.ExecuteScalar();
        return r == null || r is DBNull ? default! : (T)Convert.ChangeType(r, typeof(T));
    }

    public static DataTable Query(string sql, params (string, object?)[] ps)
    {
        using var c = Open();
        using var cmd = Cmd(c, sql, ps);
        using var rd = cmd.ExecuteReader();
        // Computed columns report as byte[] when the result is empty, which makes the grid draw red X
        // image cells; treat them as text/object instead.
        var dt = new DataTable();
        for (int i = 0; i < rd.FieldCount; i++)
        {
            var t = rd.GetFieldType(i);
            dt.Columns.Add(rd.GetName(i), t == typeof(byte[]) ? typeof(object) : t);
        }
        var vals = new object[rd.FieldCount];
        while (rd.Read())
        {
            rd.GetValues(vals);
            dt.Rows.Add(vals);
        }
        return dt;
    }
}
