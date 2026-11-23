using System.Globalization;
using OkRegis.Core.Localization;
using OkRegis.Core.Settings;

namespace OkRegis.App;

/// <summary>Petite fenêtre du tout premier lancement : choisir la langue (interface et reconnaissance vocale).</summary>
public static class LanguagePicker
{
    /// <summary>La langue de Windows si on la gère (fr, es), sinon l'anglais.</summary>
    public static Language FromSystem() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
    {
        "fr" => Language.French,
        "es" => Language.Spanish,
        _ => Language.English,
    };

    /// <summary>Affiche le choix. Renvoie null si la fenêtre est fermée sans choisir.</summary>
    public static Language? Ask()
    {
        var suggested = FromSystem();
        Loc.Current = suggested;
        using var form = new Form
        {
            Text = "OK Régis", ClientSize = new Size(420, 230), StartPosition = FormStartPosition.CenterScreen,
            FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
        };
        form.Controls.Add(new Label
        {
            Text = Loc.T("Choisis ta langue · Choose your language · Elige tu idioma"), Font = Theme.CardTitleFont,
            AutoSize = false, Location = new Point(20, 16), Size = new Size(380, 50),
        });
        form.Controls.Add(new Label
        {
            Text = Loc.T("Langue de l'interface et de la reconnaissance vocale"), Tag = "muted",
            AutoSize = false, Location = new Point(20, 66), Size = new Size(380, 20),
        });

        Language? chosen = null;
        var x = 20;
        foreach (var language in new[] { Language.French, Language.English, Language.Spanish })
        {
            var button = new Button
            {
                Text = Loc.NameOf(language), Location = new Point(x, 110), Size = new Size(120, 70),
                Font = new Font("Segoe UI Semibold", 11f), Tag = language == suggested ? "primary" : null,
            };
            button.Click += (_, _) => { chosen = language; form.Close(); };
            form.Controls.Add(button);
            x += 130;
        }
        Theme.Apply(form);
        form.ShowDialog();
        return chosen;
    }
}
