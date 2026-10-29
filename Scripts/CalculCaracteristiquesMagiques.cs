using System;
using System.Collections.Generic;

/// <summary>
/// Dés de points de vie et de magie, reproduisant InfoJeu/Regle_Stats.txt.
/// Les résultats doivent être conservés dans le membre sauvegardé :
/// relire une fiche ne relance jamais les dés.
/// </summary>
public static class CalculCaracteristiquesMagiques
{
    private readonly struct ProfilPouvoir
    {
        public int DeMagie { get; }
        public int BonusVie { get; }

        public ProfilPouvoir(int deMagie, int bonusVie)
        {
            DeMagie = deMagie;
            BonusVie = bonusVie;
        }
    }

    private static readonly Dictionary<string, ProfilPouvoir> Profils =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Magiciens : bonus de PV +1.
            ["Mage"] = new(10, 1),
            ["Sorcier"] = new(10, 1),
            ["Clerc"] = new(8, 1),
            ["Démoniste"] = new(8, 1),
            ["Invocateur"] = new(12, 1),
            ["Archimage"] = new(10, 1),
            ["Ensorceleur"] = new(8, 1),
            ["Enchanteur"] = new(8, 1),
            ["Métamorphe"] = new(6, 1),
            ["Purificateur"] = new(8, 1),
            ["Scribtum"] = new(8, 1),
            ["Schistite"] = new(8, 1),
            ["Aquiste"] = new(8, 1),
            ["Végenae"] = new(8, 1),
            ["Follet"] = new(8, 1),
            ["Storma"] = new(8, 1),
            ["Druide"] = new(8, 1),
            ["Chamane"] = new(8, 1),

            // Combattants : bonus de PV +4.
            ["Guerrier"] = new(8, 4),
            ["Assassin de l'ombre"] = new(6, 4),
            ["Archer Magique"] = new(6, 4),
            ["Proctor"] = new(6, 4),
            ["Poistodon"] = new(6, 4),
            ["Berserker"] = new(6, 4),
            ["Moine"] = new(6, 4),
            ["Invocateur d'arme"] = new(10, 4),

            // Élémentaires : bonus de PV +2.
            ["Élémentaire d'eau"] = new(8, 2),
            ["Élémentaire de plante"] = new(8, 2),
            ["Élémentaire de feu"] = new(8, 2),
            ["Élémentaire de Roche"] = new(8, 2),
            ["Élémentaire de vent"] = new(8, 2),
            ["Élémentaire d'Alcool"] = new(8, 2),
            ["Élémentaire de glace"] = new(8, 2),
            ["Élémentaire de vapeur"] = new(8, 2),
            ["Élémentaire de sang"] = new(8, 2),
            ["Élémentaire de froid"] = new(8, 2),
            ["Élémentaire de lave"] = new(8, 2),
            ["Élémentaire de chaleur"] = new(8, 2),
            ["Élémentaire de bois"] = new(8, 2),
            ["Élémentaire fongique"] = new(8, 2),
            ["Élémentaire de spore"] = new(8, 2),
            ["Élémentaire de diamant"] = new(8, 2),
            ["Élémentaire de métal"] = new(8, 2),
            ["Élémentaire de pierre précieuse"] = new(8, 2),
            ["Élémentaire de foudre"] = new(8, 2),
            ["Élémentaire de lumière"] = new(8, 2),
            ["Élémentaire d'ombre"] = new(8, 2),
            ["Élémentaire de fumée"] = new(8, 2),
            ["Élémentaire Supérieur"] = new(10, 2),
            ["Élémentaire Temporel"] = new(8, 2),

            // Psychistes : bonus de PV +2.
            ["Télékinésiste"] = new(8, 2),
            ["Télékinésite"] = new(8, 2), // Orthographe de Regle_Stats.txt.
            ["Animiste"] = new(8, 2),
            ["Devin"] = new(8, 2),
            ["Crieur"] = new(8, 2),
            ["Illusionniste"] = new(8, 2),
            ["Télépathe"] = new(4, 2)
        };

    /// <summary>
    /// Calcule les gains cumulés. Chaque niveau relance un dé de PV
    /// et un seul dé de PM (celui ayant le plus de faces parmi les pouvoirs).
    /// Aucun pouvoir : 0 bonus de catégorie, 0 PM par niveau.
    /// </summary>
    public static (int PointsDeVie, int PointsDeMagie) TirerPourNiveaux(
        string race, IEnumerable<string> pouvoirs, int nombreDeNiveaux)
    {
        if (nombreDeNiveaux < 1)
            throw new ArgumentOutOfRangeException(nameof(nombreDeNiveaux));

        int facesVie = race switch
        {
            "Humain" => 6,
            "Nain" => 8,
            "Elfe gris" => 6,
            "Haut-Elfe" => 2,
            "Grumol" => 6,
            "Humain des monts" => 4,
            "Tanneur" => 10,
            "Khajiit" => 4,
            _ => throw new ArgumentException("Race inconnue pour le calcul des PV : " + race, nameof(race))
        };

        int bonusVie = 0;
        int facesMagie = 0;
        if (pouvoirs != null)
        {
            foreach (string pouvoir in pouvoirs)
            {
                if (!Profils.TryGetValue(pouvoir ?? "", out ProfilPouvoir profil))
                    throw new ArgumentException("Pouvoir inconnu pour le calcul des PM : " + pouvoir, nameof(pouvoirs));
                bonusVie = Math.Max(bonusVie, profil.BonusVie);
                facesMagie = Math.Max(facesMagie, profil.DeMagie);
            }
        }

        // Les Hauts-Elfes doublent le bonus de catégorie, pas le dé.
        if (race == "Haut-Elfe") bonusVie *= 2;

        int vie = 0, magie = 0;
        for (int i = 0; i < nombreDeNiveaux; i++)
        {
            vie = checked(vie + Random.Shared.Next(1, facesVie + 1) + bonusVie);
            if (facesMagie > 0)
                magie = checked(magie + Random.Shared.Next(1, facesMagie + 1));
        }

        return (vie, magie);
    }
}
