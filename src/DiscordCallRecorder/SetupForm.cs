namespace DiscordCallRecorder;

internal sealed class SetupForm : Form
{
    private readonly TextBox folder = new() { ReadOnly = true, Dock = DockStyle.Fill };
    public string SelectedFolder => folder.Text;
    public string SelectedLanguage => Localization.Language;
    public SetupForm(string? current = null)
    {
        Text = "Discord Recorder";
        var originalLanguage = Localization.Language;
        ClientSize = new Size(540, 215);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, AccessibleName = "Idioma / Language" };
        language.Items.AddRange(["Português (Brasil)", "English"]);
        language.SelectedIndex = Localization.Language == "en" ? 1 : 0;
        layout.Controls.Add(language);
        var instruction = new Label { Text = Localization.Text("Escolha onde deseja salvar suas gravações."), AutoSize = true };
        layout.Controls.Add(instruction);
        folder.Text = current ?? "";
        layout.Controls.Add(folder);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var confirm = new Button { Text = Localization.Text("Confirmar"), AutoSize = true, Enabled = folder.Text.Length > 0 };
        var browse = new Button { Text = Localization.Text("Escolher pasta"), AutoSize = true };
        language.SelectedIndexChanged += (_, _) =>
        {
            Localization.Language = language.SelectedIndex == 1 ? "en" : "pt-BR";
            instruction.Text = Localization.Text("Escolha onde deseja salvar suas gravações.");
            confirm.Text = Localization.Text("Confirmar");
            browse.Text = Localization.Text("Escolher pasta");
        };
        FormClosed += (_, _) => { if (DialogResult != DialogResult.OK) Localization.Language = originalLanguage; };
        browse.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { Description = Localization.Text("Escolha onde deseja salvar suas gravações."), UseDescriptionForTitle = true, SelectedPath = folder.Text };
            if (picker.ShowDialog(this) == DialogResult.OK) { folder.Text = picker.SelectedPath; confirm.Enabled = true; }
        };
        confirm.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(folder.Text);
                var probe = Path.Combine(folder.Text, $".recorder-{Guid.NewGuid():N}.tmp");
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
                DialogResult = DialogResult.OK;
            }
            catch (Exception e) { MessageBox.Show(this, Localization.Text("Não foi possível usar essa pasta.\n") + e.Message, Text); }
        };
        buttons.Controls.Add(confirm); buttons.Controls.Add(browse);
        layout.Controls.Add(buttons); Controls.Add(layout); AcceptButton = confirm;
    }
}
