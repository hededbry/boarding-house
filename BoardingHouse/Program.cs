namespace BoardingHouse;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.ToString(), "Unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        Db.Init();
        using var login = new LoginForm();
        if (login.ShowDialog() != DialogResult.OK) return;
        Application.Run(new MainForm(login.Username));
    }
}
