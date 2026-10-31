using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

/// <summary>
/// État des missions de la partie : aucune écriture sur disque avant la
/// sauvegarde explicite du joueur.
/// </summary>
public sealed class EtatMissions
{
    public const string CleSauvegarde = "missions_guilde";
    public int Version { get; set; } = 1;
    public string DernierMinuitTraite { get; set; } = "";
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
    public string Rang { get; set; } = "*";
    public string Categorie { get; set; } = "";
    public string Demandeur { get; set; } = "";
    public int Recompense { get; set; }
    public string Etat { get; set; } = "Proposee"; // Proposee, Disponible, EnCours, Terminee
    public string DateDepart { get; set; } = "";
    public string DateRetour { get; set; } = "";
    public List<string> ParticipantIds { get; set; } = new();
    public bool? Reussie { get; set; }
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
    private static readonly int[] PrestigeGagne =
    {
        1, 2, 4, 8, 20, 50, 55, 60, 65, 70, 100, 300, 400, 500, 600, 700, 800, 10000
    };
    private static readonly int[] PrestigePerdu =
    {
        1, 2, 3, 5, 15, 30, 30, 40, 40, 50, 80, 200, 300, 400, 500, 600, 700, 0
    };
    private static readonly int[] MissionsPourPromotion =
    {
        1, 2, 4, 7, 10, 10, 10, 10, 10, 10, 10,
        10, 10, 10, 10, 10, 10, 0
    };
    private static readonly int[] NiveauMagiquePourRang =
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

    public static int IndexRang(string rang)
    {
        int index = Array.IndexOf(Rangs, rang ?? "");
        return index < 0 ? 0 : index;
    }

    public static int ExperiencePourMission(string rang) => PointsExperience[IndexRang(rang)];
    public static int VariationPrestige(string rang, bool reussite) =>
        reussite ? PrestigeGagne[IndexRang(rang)] : -PrestigePerdu[IndexRang(rang)];

    public static string DateIso(DateTime date) =>
        date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

    public static DateTime LireDate(string date) =>
        DateTime.ParseExact(date, "yyyy-MM-dd'T'HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None);

    public static MissionGuilde Generer(IEnumerable<MembreGuilde> membres)
    {
        int plusHaut = membres.Where(m => m != null)
            .Select(m => IndexRang(m.Rang)).DefaultIfEmpty(0).Max();

        // Tirage uniforme provisoire parmi les rangs accessibles.
        int indexRang = Random.Shared.Next(plusHaut + 1);
        var categorie = Categories[Random.Shared.Next(Categories.Length)];
        string[] demandeursValides = Demandeurs
            .Where(d => DemandeurAccepte(d, categorie.Nom, indexRang)).ToArray();
        string demandeur = demandeursValides[Random.Shared.Next(demandeursValides.Length)];

        int debut = MinEcus[indexRang];
        int fin = MaxEcus[indexRang];
        int amplitude = fin - debut;
        // Traduction provisoire de « bas / moyenne / haut de fourchette »
        // par tiers égaux de la fourchette disponible.
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
            "Guilde de Mercenaire" => rang >= IndexRang("***") &&
                categorie != "Renfort pour Assaut" && categorie != "Enquête" &&
                categorie != "Service",
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

    public static void EnregistrerReussiteEtPromouvoir(MembreGuilde membre, string rangMission)
    {
        membre.MissionsReussiesParRang ??= new Dictionary<string, int>();
        membre.MissionsReussiesParRang.TryGetValue(rangMission, out int ancien);
        membre.MissionsReussiesParRang[rangMission] = ancien + 1;

        int rangActuel = IndexRang(membre.Rang);
        if (rangActuel >= Rangs.Length - 1 || rangActuel != IndexRang(rangMission))
            return;

        // S et P requièrent un concours spécifique : ne pas les attribuer automatiquement.
        if (rangActuel == IndexRang("*****") ||
            rangActuel == IndexRang("S*****"))
            return;

        if (ancien + 1 >= MissionsPourPromotion[rangActuel] &&
            membre.NiveauMagique >= NiveauMagiquePourRang[rangActuel + 1])
            membre.Rang = Rangs[rangActuel + 1];
    }
}
