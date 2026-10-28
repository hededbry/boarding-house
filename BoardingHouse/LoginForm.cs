namespace BoardingHouse;

class LoginForm : Form
{
    readonly TextBox user = new() { Left = 100, Top = 20, Width = 170 };
    readonly TextBox pass = new() { Left = 100, Top = 55, Width = 170, UseSystemPasswordChar = true };
    public string Username => user.Text.Trim();

    public LoginForm()
    {
        Text = "Boarding House - Login";
        ClientSize = new Size(300, 140);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        var ok = new Button { Text = "Login", Left = 100, Top = 90, Width = 80 };
        var cancel = new Button { Text = "Cancel", Left = 190, Top = 90, Width = 80, DialogResult = DialogResult.Cancel };
        ok.Click += (_, _) => TryLogin();
        Controls.AddRange(new Control[] {
            new Label { Text = "Username", Left = 20, Top = 24, Width = 75 }, user,
            new Label { Text = "Password", Left = 20, Top = 59, Width = 75 }, pass, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
    }

    void TryLogin()
    {
        var n = Db.Scalar<long>("SELECT COUNT(*) FROM Users WHERE Username=@u AND PasswordHash=@p",
            ("@u", Username), ("@p", Db.Hash(pass.Text)));
        if (n > 0) { DialogResult = DialogResult.OK; return; }
        MessageBox.Show("Invalid username or password.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        pass.Clear();
    }
}
