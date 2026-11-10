using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// État des missions de la partie : aucune écriture sur disque avant la
/// sauvegarde explicite du joueur.
/// </summary>
public sealed class EtatMissions
{
    public const string CleSauvegarde = "missions_guilde";
    public int Version { get; set; } = 1;
    // Conservé pour pouvoir relire les sauvegardes de l'ancien tirage à minuit.
    public string DernierMinuitTraite { get; set; } = "";
    // Date ET heure du dernier jet, afin de ne pas le rejouer après chargement.
    public string DernierJalonApparitionTraite { get; set; } = "";
    public string DateAnnoncePrevue { get; set; } = "";
    public MissionGuilde Proposition { get; set; }
    public List<MissionGuilde> Missions { get; set; } = new();

    public static EtatMissions Charger()
    {
        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
            throw new InvalidOperationException(SaveManager.LastError);
        if (string.IsNullOrWhiteSpace(json)) return new EtatMissions();
        EtatMissions etat = JsonSerializer.Deserialize<EtatMissions>(json);
        if (etat == null || etat.Version != 1 || etat.Missions == null)
            throw new InvalidOperationException("État des missions invalide.");
        return etat;
    }

    public string Serialiser() => JsonSerializer.Serialize(this);
}

public sealed class MissionGuilde
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilRougeId { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Rang { get; set; } = "*";
    public string Categorie { get; set; } = "";
    public string Demandeur { get; set; } = "";
    public int Recompense { get; set; }
    public string Etat { get; set; } = "Proposee"; // Proposee, Disponible, EnCours, Terminee
    public string DateDepart { get; set; } = "";
    public string DateRetour { get; set; } = "";
    public List<string> ParticipantIds { get; set; } = new();
    public bool? Reussie { get; set; }
    public int JoursEcoules { get; set; }
    public int DureeFixee { get; set; }
    public int EchecsEnqueteConsecutifs { get; set; }
    public bool SansIncident { get; set; }
    public bool SansGainSecondaire { get; set; }
    public string DetailResultat { get; set; } = "";
    // Effet des traits figé au départ de mission ; un avantage maximum par
    // mission, utilisable sur un D20 raté OU sur une table de résolution magique.
    public bool AvantageTraitsDisponible { get; set; }
    public bool AvantageTraitsConsomme { get; set; }
    public int MalusVolonteTraits { get; set; }
    public string MembreDefavorableId { get; set; } = "";
    public string MembreDefavorableNom { get; set; } = "";
    // Coordonnées normalisées (0 à 1) du parchemin sur le tableau.
    // Null : mission jamais placée ; la position est créée au premier affichage.
    // Ces valeurs sont sauvegardées avec l'état des missions.
    public float? PositionTableauX { get; set; }
    public float? PositionTableauY { get; set; }
}

public static class ReglesMissions
{
    // Correspondance stricte avec InfoJeu/Missions&Rang.txt.
    public static readonly string[] Rangs =
    {
        "*", "**", "***", "****", "*****",
        "S", "S*", "S**", "S***", "S****", "S*****",
        "P", "P*", "P**", "P***", "P****", "P*****", "U"
    };
    private static readonly int[] MinEcus =
    {
        50, 100, 225, 350, 500, 1000, 2000, 3000, 4000, 5000, 6000,
        10000, 20000, 30000, 40000, 50000, 60000, 100000
    };
    private static readonly int[] MaxEcus =
    {
        100, 225, 300, 500, 1000, 3000, 4000, 5000, 6000, 7000, 10000,
        100000, 100000, 100000, 100000, 100000, 100000, 100000
    };
    private static readonly int[] PointsExperience =
    {
        50, 100, 200, 400, 800, 2000, 2500, 3000, 3500, 4000, 5000,
        10000, 12000, 14000, 16000, 18000, 20000, 1000000
    };
    private static readonly int[] PrestigeVariation =
    {
        1, 2, 3, 4, 5, 10, 11, 12, 13, 14, 15, 30, 32, 34, 36, 38, 50, 500
    };
    private static readonly double[] PoidsApparition =
    {
        10, 18, 18.5, 16, 15, 3, 2.8, 2.6, 2.4, 2.2, 2,
        1.5, 1.4, 1.3, 1.2, 1.1, 1, 0
    };
    private static readonly int[] MissionsPourPromotion =
    {
        1, 2, 4, 7, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 0
    };
    // Niveau magique minimal du rang que l'on souhaite atteindre.
    // InfoJeu/Missions&Rang.txt : ** = 21, *** = 41, etc.
    private static readonly int[] NiveauMagiqueMinimumParRang =
    {
        0, 21, 41, 61, 81, 151, 201, 251, 301, 351, 401,
        601, 701, 781, 851, 911, 961, 1000
    };
    private static readonly (string Nom, int PositionPrix)[] Categories =
    {
        ("Enquête", 0), ("Escorte", 1), ("Élimination de danger", 2),
        ("Recherche mort ou vif", 2), ("Capture vif", 1),
        ("Protection d'un lieu", 0), ("Recherche d'un objet", 1),
        ("Recherche d'un ingrédient", 0), ("Recherche d'un animal", 1),
        ("Service", 0), ("Renfort pour Assaut", 2)
    };

    private static readonly string[] Demandeurs =
    {
        "Cité", "Autre Cité", "Particulier", "Marchand",
        "Commerçant de la ville", "Guilde de Mercenaire"
    };

    /// <summary>
    /// Les rangs conservent leurs identifiants internes (*, S*, P*, etc.)
    /// pour les calculs et les sauvegardes. Seul l'affichage utilise ★.
    /// </summary>
    public static string FormaterRang(string rang) => (rang ?? "").Replace('*', '★');

    /// <summary>
    /// Adapte les anciens messages du journal contenant des rangs en étoiles
    /// ASCII (ex. **, S***, P****) sans altérer les sauvegardes ni les noms.
    /// </summary>
    public static string FormaterRangsDansTexte(string texte)
    {
        if (string.IsNullOrEmpty(texte)) return texte ?? "";
        return Regex.Replace(texte,
            @"(?<![\p{L}\p{N}★*])(?:S|P)?\*{1,5}(?![\p{L}\p{N}★*])",
            match => FormaterRang(match.Value));
    }

    public static int IndexRang(string rang)
    {
        int index = Array.IndexOf(Rangs, rang ?? "");
        return index < 0 ? 0 : index;
    }

    public static int ExperiencePourMission(string rang) => PointsExperience[IndexRang(rang)];

    public static int VariationPrestige(string rang, bool reussite)
    {
        int valeur = PrestigeVariation[IndexRang(rang)];
        return reussite ? valeur : -valeur;
    }

    // Formule telle que définie dans InfoJeu/Missions&Rang.txt.
    public static double ProbabiliteApparition(long prestige)
    {
        double x = Math.Max(0.0, prestige) / 5000.0;
        return 0.1 + 0.4 * Math.Tanh(Math.Pow(x, 1.3));
    }

    public static string DateIso(DateTime date) =>
        date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    public static DateTime LireDate(string date) =>
        DateTime.ParseExact(date, "yyyy-MM-dd'T'HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None);

    public static int GenererRecompense(string rang, string categorie, double multiplicateur = 1.0)
    {
        int index = IndexRang(rang);
        int debut = MinEcus[index];
        int fin = MaxEcus[index];
        int position = Categories.FirstOrDefault(c => c.Nom == categorie).PositionPrix;
        int amplitude = fin - debut;
        int borneMin = debut + amplitude * position / 3;
        int borneMax = position == 2 ? fin :
            debut + amplitude * (position + 1) / 3;
        return Math.Max(0, (int)Math.Round(
            Random.Shared.Next(borneMin, borneMax + 1) * multiplicateur,
            MidpointRounding.AwayFromZero));
    }

    public static MissionGuilde Generer(IEnumerable<MembreGuilde> membres,
        double facteurCommercants = 1.0,
        IEnumerable<string> categoriesPreferees = null)
    {
        // Les missions U ne peuvent jamais apparaître naturellement.
        // Une mission n'apparaît que si un membre de la guilde possède le rang demandé.
        int rangMax = membres.Where(m => m != null)
            .Select(m => IndexRang(m.Rang)).DefaultIfEmpty(0).Max();
        var possibles = Enumerable.Range(0, Rangs.Length)
            .Where(i => i <= rangMax && PoidsApparition[i] > 0 &&
                membres.Any(m => m != null && IndexRang(m.Rang) >= i))
            .Select(i => (Index: i, Poids: PoidsApparition[i])).ToArray();
        if (possibles.Length == 0) return null;

        double tirage = Random.Shared.NextDouble() * possibles.Sum(p => p.Poids);
        int indexRang = possibles[^1].Index;
        foreach (var candidat in possibles)
        {
            tirage -= candidat.Poids;
            if (tirage < 0) { indexRang = candidat.Index; break; }
        }

        // Spécialisation : chaque catégorie non préférée cède 30 % de
        // SON poids d'apparition. Le poids ainsi libéré est redistribué
        // équitablement entre toutes les catégories préférées.
        //
        // Exemple avec 2 préférées parmi 11 : chaque non préférée conserve
        // 0,7 / 11 de probabilité et chaque préférée obtient
        // (1 + 0,3 * 9 / 2) / 11. Le poids total reste égal à 11 :
        // cela ne change pas la fréquence générale des missions.
        // Aucun effet si aucune catégorie, ou toutes, sont préférées.
        HashSet<string> preferees = new(categoriesPreferees ??
            Enumerable.Empty<string>(), StringComparer.Ordinal);
        int nombrePreferees = Categories.Count(c => preferees.Contains(c.Nom));
        int nombreNonPreferees = Categories.Length - nombrePreferees;

        var categorie = Categories[Random.Shared.Next(Categories.Length)];
        if (nombrePreferees > 0 && nombreNonPreferees > 0)
        {
            double poidsPreferee = 1.0 +
                0.30 * nombreNonPreferees / nombrePreferees;
            double tirageCategorie = Random.Shared.NextDouble() * Categories.Length;
            foreach (var candidate in Categories)
            {
                double poids = preferees.Contains(candidate.Nom)
                    ? poidsPreferee : 0.70;
                tirageCategorie -= poids;
                if (tirageCategorie < 0)
                {
                    categorie = candidate;
                    break;
                }
            }
        }
        string[] demandeursValides = Demandeurs
            .Where(d => DemandeurAccepte(d, categorie.Nom, indexRang)).ToArray();
        double[] poidsDemandeurs = demandeursValides.Select(d =>
            d == "Commerçant de la ville" ? Math.Max(0, facteurCommercants) : 1.0).ToArray();
        double tirageDemandeur = Random.Shared.NextDouble() * poidsDemandeurs.Sum();
        string demandeur = demandeursValides[^1];
        for (int i = 0; i < demandeursValides.Length; i++)
        {
            tirageDemandeur -= poidsDemandeurs[i];
            if (tirageDemandeur < 0) { demandeur = demandeursValides[i]; break; }
        }

        int debut = MinEcus[indexRang];
        int fin = MaxEcus[indexRang];
        int amplitude = fin - debut;
        // Bas/milieu/haut : trois tiers de la fourchette.
        int borneMin = debut + amplitude * categorie.PositionPrix / 3;
        int borneMax = categorie.PositionPrix == 2
            ? fin : debut + amplitude * (categorie.PositionPrix + 1) / 3;
        int recompense = Random.Shared.Next(borneMin, borneMax + 1);

        return new MissionGuilde
        {
            Rang = Rangs[indexRang],
            Categorie = categorie.Nom,
            Demandeur = demandeur,
            Recompense = recompense
        };
    }

    private static bool DemandeurAccepte(string demandeur, string categorie, int rang)
    {
        return demandeur switch
        {
            "Cité" => true,
            "Autre Cité" => rang >= IndexRang("S"),
            "Particulier" => categorie != "Renfort pour Assaut",
            "Marchand" => categorie == "Enquête" || categorie == "Escorte" ||
                categorie == "Recherche d'un objet" || categorie == "Service",
            "Commerçant de la ville" => categorie != "Renfort pour Assaut" &&
                categorie != "Recherche mort ou vif" && categorie != "Capture vif",
            // La règle particulière « Renfort pour Assaut » autorise également
            // les guildes de mercenaires malgré l'exclusion générale.
            "Guilde de Mercenaire" => rang >= IndexRang("***") &&
                categorie != "Enquête" && categorie != "Service",
            _ => false
        };
    }

    public static bool RangSuffisant(MembreGuilde membre, string rangMission) =>
        IndexRang(membre.Rang) >= IndexRang(rangMission);

    public static void AjouterExperience(MembreGuilde membre, int experience)
    {
        if (experience <= 0) return;
        long reste = (long)(membre.Experience ?? 0) + experience;
        int niveau = Math.Max(1, membre.Niveau ?? 1);
        while (niveau < 10000)
        {
            double seuilExact = Math.Ceiling(75.0 * Math.Pow(1.1, niveau - 1));
            if (seuilExact >= int.MaxValue || reste < (long)seuilExact) break;
            reste -= (long)seuilExact;
            niveau++;
            // Un dé supplémentaire de PV/PM par niveau, comme à la création.
            var gain = CalculCaracteristiquesMagiques.TirerPourNiveaux(
                membre.Race, membre.Pouvoirs, 1);
            membre.PointsDeVie = (membre.PointsDeVie ?? 0) + gain.PointsDeVie;
            membre.PointsDeMagie = (membre.PointsDeMagie ?? 0) + gain.PointsDeMagie;
        }
        membre.Experience = (int)Math.Min(int.MaxValue, reste);
        membre.Niveau = niveau;
    }

    /// <summary>
    /// Comptabilise toute mission réussie, y compris une mission sans incident.
    /// Une promotion exige le nombre de victoires au rang actuel ET le niveau
    /// magique minimum du rang suivant.
    /// </summary>
    public static bool EnregistrerReussiteEtPromouvoir(MembreGuilde membre, string rangMission)
    {
        membre.MissionsReussiesParRang ??= new Dictionary<string, int>();
        membre.MissionsReussiesParRang.TryGetValue(rangMission, out int reussites);
        membre.MissionsReussiesParRang[rangMission] = reussites + 1;
        return AppliquerPromotionsAcquises(membre);
    }

    /// <summary>
    /// Vérifie les conditions de promotion après une mission ou un gain
    /// de niveau magique, jamais lors du chargement d'une sauvegarde.
    /// Les passages ***** vers S et S***** vers P attendent les concours.
    /// Le rang U exige uniquement 1000 de niveau magique.
    /// </summary>
    public static bool AppliquerPromotionsAcquises(MembreGuilde membre)
    {
        if (membre == null || membre.EstMaitreDeGuilde)
            return false;

        if (membre.NiveauMagique >= 1000 && membre.Rang != "U")
        {
            membre.Rang = "U";
            membre.Classement = 0;
            return true;
        }

        int rangActuel = IndexRang(membre.Rang);
        if (rangActuel >= Rangs.Length - 1)
            return false;

        bool progression = false;
        while (rangActuel < Rangs.Length - 2)
        {
            // Les deux promotions par concours seront gérées ultérieurement.
            if (rangActuel == IndexRang("*****") ||
                rangActuel == IndexRang("S*****"))
                break;

            int victoires = 0;
            membre.MissionsReussiesParRang?.TryGetValue(Rangs[rangActuel], out victoires);
            if (victoires < MissionsPourPromotion[rangActuel] ||
                membre.NiveauMagique < NiveauMagiqueMinimumParRang[rangActuel + 1])
                break;

            rangActuel++;
            membre.Rang = Rangs[rangActuel];
            progression = true;
        }

        return progression;
    }

}
