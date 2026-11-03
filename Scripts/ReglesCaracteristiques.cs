using System;

/// <summary>
/// Modificateurs universels des caractéristiques notées de 1 à 20.
/// Barème : InfoJeu/Regle_Stats.txt. Réutilisable pour tous les jets du jeu.
/// </summary>
public static class ReglesCaracteristiques
{
    /// <summary>
    /// Affiche le résultat effectif d'un jet (dé, bonus/malus, total et seuil).
    /// Le même format sert aux conversations et aux événements scénarisés.
    /// </summary>
    public static string FormaterJet(int de, int modificateur, int cible)
    {
        int total = de + modificateur;
        string signe = modificateur >= 0 ? "+" : "-";
        return "D20 " + de + " " + signe + " " + Math.Abs(modificateur) +
            " = " + total + " / cible " + cible +
            " (" + (total >= cible ? "réussite" : "échec") + ")";
    }

    /// <summary>Retourne le bonus ou malus correspondant à une caractéristique sur 20.</summary>
    public static int Modificateur(int valeur)
    {
        if (valeur < 1 || valeur > 20)
            throw new ArgumentOutOfRangeException(nameof(valeur),
                "Une caractéristique doit être comprise entre 1 et 20.");

        return valeur switch
        {
            1 => -5,
            2 or 3 => -4,
            4 or 5 => -3,
            6 or 7 => -2,
            8 or 9 => -1,
            10 => 0,
            11 or 12 => 1,
            13 or 14 => 2,
            15 or 16 => 3,
            17 or 18 or 19 => 4,
            20 => 5,
            _ => throw new InvalidOperationException("Caractéristique invalide.")
        };
    }
}
