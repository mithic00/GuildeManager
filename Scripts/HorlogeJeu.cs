using Godot;
using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Horloge de la partie, en temps du jeu (indépendante de la date réelle).
/// Reprend les anciennes sauvegardes "An 600 - 1er Janvier - 08h00".
/// La clé date_jeu contient désormais une date ISO 8601 stable et lisible.
/// </summary>
public sealed class HorlogeJeu
{
    public const string CleSauvegarde = "date_jeu";
    public const int MinutesParSecondeReelle = 10;
    public static readonly DateTime DateInitiale = new(600, 1, 1, 8, 0, 0);
    private const string FormatSauvegarde = "yyyy-MM-dd'T'HH:mm:ss";
    private static readonly CultureInfo CultureFr = CultureInfo.GetCultureInfo("fr-FR");

    public DateTime Instant { get; private set; }

    public static string DerniereErreur { get; private set; } = "";

    private HorlogeJeu(DateTime instant)
    {
        Instant = instant;
    }

    public static HorlogeJeu Charger()
    {
        DerniereErreur = "";
        if (!SaveManager.HasCurrentSave)
        {
            DerniereErreur = "Aucune sauvegarde active.";
            return null;
        }

        string texte = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
        {
            DerniereErreur = "Impossible de lire l'horloge : " + SaveManager.LastError;
            return null;
        }

        if (string.IsNullOrWhiteSpace(texte))
        {
            // Partie plus ancienne ne possédant pas encore de date enregistrée.
            HorlogeJeu horlogeInitiale = new(DateInitiale);
            return horlogeInitiale.Enregistrer() ? horlogeInitiale : null;
        }

        if (DateTime.TryParseExact(texte, FormatSauvegarde, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime dateIso))
            return new HorlogeJeu(dateIso);

        if (TryLireAncienFormat(texte, out DateTime dateAncienne))
        {
            HorlogeJeu horloge = new(dateAncienne);
            // Conversion durable de l'ancien affichage textuel en format stable.
            return horloge.Enregistrer() ? horloge : null;
        }

        // Une date inconnue n'est pas remplacée silencieusement par An 600.
        DerniereErreur = "Date de jeu enregistrée non reconnue : " + texte;
        GD.PushError("HorlogeJeu : " + DerniereErreur);
        return null;
    }

    public bool AvancerUneMinute()
    {
        if (!SaveManager.HasCurrentSave)
        {
            DerniereErreur = "Aucune sauvegarde active.";
            return false;
        }

        DateTime suivante;
        try
        {
            suivante = Instant.AddMinutes(1);
        }
        catch (ArgumentOutOfRangeException)
        {
            DerniereErreur = "La date maximale prise en charge a été atteinte.";
            return false;
        }

        // La nouvelle minute n'est validée en mémoire que si la sauvegarde réussit.
        string texte = suivante.ToString(FormatSauvegarde, CultureInfo.InvariantCulture);
        if (!SaveManager.SetValue(CleSauvegarde, texte))
        {
            DerniereErreur = SaveManager.LastError;
            return false;
        }

        Instant = suivante;
        DerniereErreur = "";
        return true;
    }

    public bool Enregistrer()
    {
        if (!SaveManager.HasCurrentSave)
        {
            DerniereErreur = "Aucune sauvegarde active.";
            return false;
        }

        if (!SaveManager.SetValue(CleSauvegarde, Instant.ToString(FormatSauvegarde, CultureInfo.InvariantCulture)))
        {
            DerniereErreur = SaveManager.LastError;
            return false;
        }

        DerniereErreur = "";
        return true;
    }

    public bool EstJalon()
    {
        if (Instant.Minute != 0)
            return false;

        return Instant.Hour == 0 || Instant.Hour == 8 || Instant.Hour == 12 ||
               Instant.Hour == 16 || Instant.Hour == 20;
    }

    public static string Formater(DateTime instant)
    {
        string jour = instant.Day == 1 ? "1er" : instant.Day.ToString(CultureFr);
        string mois = CultureFr.DateTimeFormat.GetMonthName(instant.Month);
        mois = char.ToUpper(mois[0], CultureFr) + mois.Substring(1);
        return "An " + instant.Year + " - " + jour + " " + mois + " - " +
               instant.ToString("HH'h'mm", CultureInfo.InvariantCulture);
    }

    private static bool TryLireAncienFormat(string texte, out DateTime date)
    {
        date = default;
        Match correspondance = Regex.Match(texte.Trim(),
            @"^An\s+(\d{1,4})\s*-\s*(\d{1,2})(?:er)?\s+([\p{L}]+)\s*-\s*(\d{1,2})h(\d{2})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!correspondance.Success)
            return false;

        if (!int.TryParse(correspondance.Groups[1].Value, out int annee) ||
            !int.TryParse(correspondance.Groups[2].Value, out int jour) ||
            !int.TryParse(correspondance.Groups[4].Value, out int heure) ||
            !int.TryParse(correspondance.Groups[5].Value, out int minute))
            return false;

        string moisLitteral = SupprimerAccents(correspondance.Groups[3].Value).ToLowerInvariant();
        int mois = 0;
        for (int i = 1; i <= 12; i++)
        {
            if (SupprimerAccents(CultureFr.DateTimeFormat.GetMonthName(i)).ToLowerInvariant() != moisLitteral)
                continue;
            mois = i;
            break;
        }

        if (mois == 0) return false;
        try
        {
            date = new DateTime(annee, mois, jour, heure, minute, 0);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string SupprimerAccents(string texte)
    {
        string normalise = texte.Normalize(NormalizationForm.FormD);
        StringBuilder resultat = new();
        foreach (char caractere in normalise)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                resultat.Append(caractere);
        }

        return resultat.ToString();
    }
}
