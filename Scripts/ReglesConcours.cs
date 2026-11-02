using System;
using System.Collections.Generic;

/// <summary>
/// Conditions de classement existantes et durées/prix de InfoJeu/Concours.txt.
/// Les résultats des concours sont garantis pour le moment.
/// </summary>
public static class ReglesConcours
{
    public const int PrixS = 5000;
    public const int PrixP = 20000;

    /// <summary>Renvoie le concours S ou P, ou vide si le membre est inéligible.</summary>
    public static string ConcoursEligible(MembreGuilde membre)
    {
        if (membre == null || membre.EstMaitreDeGuilde || membre.EstJoueur ||
            !string.IsNullOrWhiteSpace(membre.ConcoursEnCours) ||
            membre.ResultatConcoursEnAttente)
            return "";
        int victoires = 0;
        if (membre.Rang == "*****")
        {
            membre.MissionsReussiesParRang?.TryGetValue("*****", out victoires);
            return victoires >= 10 && membre.NiveauMagique >= 151 ? "S" : "";
        }
        if (membre.Rang == "S*****")
        {
            membre.MissionsReussiesParRang?.TryGetValue("S*****", out victoires);
            return victoires >= 10 && membre.NiveauMagique >= 601 ? "P" : "";
        }
        return "";
    }

    public static int Prix(string concours) => concours == "S" ? PrixS :
        concours == "P" ? PrixP : throw new ArgumentException("Concours inconnu.", nameof(concours));

    public static DateTime DateRetour(string concours, DateTime depart) => concours switch
    {
        "S" => depart.AddDays(7),
        "P" => depart.AddMonths(1),
        _ => throw new ArgumentException("Concours inconnu.", nameof(concours))
    };
}
