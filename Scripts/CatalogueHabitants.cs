using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Lien institutionnel d'un habitant, utilisable lors des futurs événements.
/// Un commerçant est rattaché à la réputation globale des commerçants,
/// un représentant de la ville à la réputation de sa cité.
/// </summary>
[Flags]
public enum AffiliationHabitant
{
    Aucune = 0,
    Commercants = 1,
    Ville = 2
}

/// <summary>
/// Personnage non joueur préétabli : ne fait pas partie du registre des membres
/// et ne déclenche aucune rencontre tant qu'un événement ne le référence pas.
/// </summary>
public sealed class HabitantCatalogue
{
    public string Id { get; }
    public string Prenom { get; }
    public string Nom { get; }
    /// <summary>Sexe du CSV de référence ("M" ou "F").</summary>
    public string Sexe { get; }
    public string Race { get; }
    public int Age { get; }
    public string Metier { get; }
    public string Ville { get; }
    public AffiliationHabitant Affiliation { get; }

    public string NomComplet => string.IsNullOrWhiteSpace(Nom) ? Prenom : Prenom + " " + Nom;
    public bool EstCommercant => (Affiliation & AffiliationHabitant.Commercants) != 0;
    public bool EstLieALaVille => (Affiliation & AffiliationHabitant.Ville) != 0;

    /// <summary>
    /// Clé de réputation que pourra utiliser un futur événement.
    /// Une affiliation neutre n'entraîne aucune réputation.
    /// Aucune modification de relation n'est effectuée par le catalogue.
    /// </summary>
    public string CleReputation => EstLieALaVille
        ? EtatRelationsGuilde.CleVille(Ville)
        : EstCommercant ? EtatRelationsGuilde.Commercants : "";

    public HabitantCatalogue(
        string id, string prenom, string nom, string sexe, string race, int age,
        string metier, AffiliationHabitant affiliation, string ville = "Rose-Vallée")
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(prenom) ||
            string.IsNullOrWhiteSpace(ville) || age <= 0)
            throw new ArgumentException("Identifiant, prénom, ville et âge doivent être valides.");

        Id = id;
        Prenom = prenom;
        Nom = nom ?? "";
        Sexe = sexe;
        Race = race;
        Age = age;
        Metier = metier;
        Ville = ville;
        Affiliation = affiliation;
    }
}

/// <summary>
/// Catalogue statique des habitants qui pourront intervenir dans des événements.
/// Les 50 entrées proviennent de la liste actualisée de Rose-Vallée.
/// Les ID RV-001 à RV-050 sont stables : ne pas les recycler lors des futures
/// modifications, pour permettre de les référencer dans les événements/sauvegardes.
/// Aucun PNJ n'est automatiquement créé ou affiché au démarrage.
/// </summary>
public static class CatalogueHabitants
{
    public const string RoseVallee = "Rose-Vallée";

    public static IReadOnlyList<HabitantCatalogue> Tous { get; } =
        Array.AsReadOnly(new HabitantCatalogue[]
        {
        new("RV-001", "Lucien", "Boucher", "M", "Humain", 51, "Horticulteur", AffiliationHabitant.Commercants),
        new("RV-002", "Élise", "Gauthier", "F", "Humain", 33, "Fleuriste", AffiliationHabitant.Commercants),
        new("RV-003", "Antoine", "Marchand", "M", "Humain", 42, "Producteur de fleurs", AffiliationHabitant.Commercants),
        new("RV-004", "Jeanne", "Lefèvre", "F", "Humain", 39, "Garde de la ville", AffiliationHabitant.Ville),
        new("RV-005", "Martin", "Dubois", "M", "Humain", 56, "Horticulteur", AffiliationHabitant.Commercants),
        new("RV-006", "Agathe", "Pelletier", "F", "Humain", 31, "Producteur de fleurs", AffiliationHabitant.Commercants),
        new("RV-007", "Louis", "Garnier", "M", "Humain", 43, "Forgeron", AffiliationHabitant.Commercants),
        new("RV-008", "Claire", "Perrin", "F", "Humain", 37, "Herboriste", AffiliationHabitant.Commercants),
        new("RV-009", "Camille", "Fontaine", "F", "Humain", 26, "Préparatrice d'essences florales", AffiliationHabitant.Commercants),
        new("RV-010", "Simon", "Rousseau", "M", "Humain", 45, "Marchand en fleurs", AffiliationHabitant.Commercants),
        new("RV-011", "Adèle", "Lambert", "F", "Humain", 29, "Parfumeuse", AffiliationHabitant.Commercants),
        new("RV-012", "Gabriel", "Roux", "M", "Humain", 38, "Producteur de fleurs", AffiliationHabitant.Commercants),
        new("RV-013", "Mathilde", "Chevalier", "F", "Humain", 34, "Confectionneuse de couronnes florales", AffiliationHabitant.Commercants),
        new("RV-014", "Jules", "Meunier", "M", "Humain", 28, "Producteur de fleurs", AffiliationHabitant.Commercants),
        new("RV-015", "Hélène", "Poirier", "F", "Humain", 49, "Horticultrice", AffiliationHabitant.Commercants),
        new("RV-016", "Marcel", "Bonnet", "M", "Humain", 58, "Fabricant de caisses de transport", AffiliationHabitant.Commercants),
        new("RV-017", "Lucie", "Vasseur", "F", "Humain", 32, "Garde de la ville", AffiliationHabitant.Ville),
        new("RV-018", "Olivier", "Renault", "M", "Humain", 41, "Aubergiste", AffiliationHabitant.Commercants),
        new("RV-019", "Émilie", "Leblanc", "F", "Humain", 30, "Serveuse d'auberge", AffiliationHabitant.Aucune),
        new("RV-020", "René", "Girard", "M", "Humain", 60, "Vannier", AffiliationHabitant.Commercants),
        new("RV-021", "Louise", "Lemoine", "F", "Humain", 35, "Productrice de graines de fleurs", AffiliationHabitant.Commercants),
        new("RV-022", "Nicolas", "Bouvier", "M", "Humain", 40, "Charretier de fleurs", AffiliationHabitant.Aucune),
        new("RV-023", "Fanny", "Roche", "F", "Humain", 27, "Fleuriste", AffiliationHabitant.Commercants),
        new("RV-024", "Pierre", "Colin", "M", "Humain", 54, "Maçon", AffiliationHabitant.Commercants),
        new("RV-025", "Pauline", "Giraud", "F", "Humain", 28, "Garde de la ville", AffiliationHabitant.Ville),
        new("RV-026", "Julien", "Masson", "M", "Humain", 36, "Garde de la ville", AffiliationHabitant.Ville),
        new("RV-027", "Cécile", "Brunet", "F", "Humain", 52, "Parfumeuse", AffiliationHabitant.Commercants),
        new("RV-028", "Robert", "Bernard", "M", "Humain", 61, "Manutentionnaire au marché", AffiliationHabitant.Aucune),
        new("RV-029", "Inès", "Petit", "F", "Humain", 24, "Vendeuse de fleurs", AffiliationHabitant.Commercants),
        new("RV-030", "Victor", "Laurent", "M", "Humain", 34, "Jardinier", AffiliationHabitant.Aucune),
        new("RV-031", "Manon", "Delorme", "F", "Humain", 25, "Potière", AffiliationHabitant.Commercants),
        new("RV-032", "Maxime", "Lacroix", "M", "Humain", 29, "Cuisinier", AffiliationHabitant.Aucune),
        new("RV-033", "Margot", "Dufour", "F", "Humain", 46, "Fleuriste", AffiliationHabitant.Commercants),
        new("RV-034", "Benoît", "Legrand", "M", "Humain", 48, "Horticulteur", AffiliationHabitant.Commercants),
        new("RV-035", "Loïc", "Roche", "M", "Humain des monts", 38, "Tailleur de pierre", AffiliationHabitant.Commercants),
        new("RV-036", "Maëlle", "Brun", "F", "Humain des monts", 29, "Manutentionnaire au marché", AffiliationHabitant.Aucune),
        new("RV-037", "Baldor", "Forge-Fer", "M", "Nain", 74, "Forgeron", AffiliationHabitant.Commercants),
        new("RV-038", "Brannia", "Enclume-Rouge", "F", "Nain", 64, "Serrurière", AffiliationHabitant.Commercants),
        new("RV-039", "Dornar", "Kromdahl", "M", "Nain", 92, "Mineur", AffiliationHabitant.Aucune),
        new("RV-040", "Gunnia", "Brondrak", "F", "Nain", 78, "Sculpteuse", AffiliationHabitant.Commercants),
        new("RV-041", "Khorik", "Marteau-Bronze", "M", "Nain", 81, "Fabricant d'outils horticoles", AffiliationHabitant.Commercants),
        new("RV-042", "Elarielle", "Aelundriel", "F", "Elfe gris", 114, "Cultivatrice de fleurs rares", AffiliationHabitant.Commercants),
        new("RV-043", "Caelion", "Galathenriel", "M", "Elfe gris", 126, "Cultivateur de fleurs rares", AffiliationHabitant.Commercants),
        new("RV-044", "Silwen", "Silavellind", "F", "Elfe gris", 103, "Fleuriste", AffiliationHabitant.Commercants),
        new("RV-045", "Vaelion", "Vaerilren", "M", "Haut elfe", 142, "Alchimiste de parfums floraux", AffiliationHabitant.Commercants),
        new("RV-046", "Ardelwen", "Arandilriel", "F", "Haut elfe", 134, "Herboriste", AffiliationHabitant.Commercants),
        new("RV-047", "Glu'Garak", "", "M", "Grumol", 39, "Manutentionnaire au marché", AffiliationHabitant.Aucune),
        new("RV-048", "Gaska'Goulou", "", "F", "Grumol", 36, "Cuisinière", AffiliationHabitant.Aucune),
        new("RV-049", "Marvoc", "", "M", "Tanneur", 33, "Marchand d'onguents", AffiliationHabitant.Commercants),
        new("RV-050", "Jo'Beshtar", "", "M", "Khajiit", 31, "Fleuriste de fleurs rares", AffiliationHabitant.Commercants),
        });

    public static HabitantCatalogue TrouverParId(string id) =>
        Tous.FirstOrDefault(h => string.Equals(h.Id, id, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<HabitantCatalogue> ParVille(string ville) =>
        Tous.Where(h => string.Equals(h.Ville, ville, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public static IReadOnlyList<HabitantCatalogue> CommercantsDe(string ville) =>
        Tous.Where(h => h.EstCommercant &&
                        string.Equals(h.Ville, ville, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public static IReadOnlyList<HabitantCatalogue> RepresentantsDeLaVille(string ville) =>
        Tous.Where(h => h.EstLieALaVille &&
                        string.Equals(h.Ville, ville, StringComparison.OrdinalIgnoreCase))
            .ToArray();
}
