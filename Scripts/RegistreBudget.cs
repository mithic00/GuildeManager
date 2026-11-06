using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Une opération réelle de la trésorerie de la guilde, datée dans le jeu.</summary>
public sealed class MouvementBudget
{
    public string Date { get; set; } = "";
    public long Montant { get; set; }
    public string Categorie { get; set; } = "";
    public string Detail { get; set; } = "";
}

/// <summary>
/// Historique comptable séparé du solde. Les anciennes sauvegardes repartent
/// de leur solde connu, sans inventer les opérations antérieures.
/// </summary>
public sealed class RegistreBudget
{
    public const string CleSauvegarde = "budget_guilde";
    public const string Missions = "Missions";
    public const string Dons = "Dons";
    public const string Recompenses = "Récompenses";
    public const string Loyer = "Loyer";
    public const string Fonctionnement = "Frais de fonctionnement";
    public const string Taxes = "Taxes";
    public const string Amenagements = "Aménagements et investissements";
    public const string Recrutement = "Recrutement";
    public const string Concours = "Concours";
    public const string Autres = "Autres dépenses";
    public const string Ajustements = "Ajustements non ventilés";
    public const string AchatsMagasin = "Achats en magasin";
    public const string VentesMagasin = "Ventes en magasin";

    public static readonly string[] CategoriesRecettes =
        { Missions, Dons, Recompenses, VentesMagasin, Ajustements };
    public static readonly string[] CategoriesDepenses =
        { Loyer, Fonctionnement, Taxes, Amenagements, Recrutement, Concours,
          AchatsMagasin, Autres, Ajustements };

    public int Version { get; set; } = 1;
    public string DateDebutSuivi { get; set; } = "";
    public long SoldeOuverture { get; set; }
    public List<MouvementBudget> Mouvements { get; set; } = new();

    private static readonly string FormatDate = "yyyy-MM-dd'T'HH:mm:ss";

    public static string DateIso(DateTime instant) =>
        instant.ToString(FormatDate, CultureInfo.InvariantCulture);

    public static bool LireDate(string date, out DateTime instant) =>
        DateTime.TryParseExact(date, FormatDate, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out instant);

    public static RegistreBudget ChargerOuInitialiser(long soldeActuel, DateTime instant)
    {
        if (!SaveManager.HasCurrentSave) return null;
        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
        {
            GD.PushError("RegistreBudget : " + SaveManager.LastError);
            return null;
        }
        if (string.IsNullOrWhiteSpace(json))
        {
            // Un ancien solde n'est PAS une recette. Il devient le solde
            // d'ouverture du registre, sans mouvement fictif.
            return new RegistreBudget
            {
                DateDebutSuivi = DateIso(instant),
                SoldeOuverture = soldeActuel
            };
        }
        try
        {
            var registre = JsonSerializer.Deserialize<RegistreBudget>(json);
            if (registre == null || registre.Version != 1 ||
                registre.Mouvements == null ||
                !LireDate(registre.DateDebutSuivi, out _))
                throw new InvalidOperationException("Registre comptable illisible.");
            return registre;
        }
        catch (Exception ex)
        {
            GD.PushError("RegistreBudget : impossible de charger le budget : " + ex);
            return null;
        }
    }

    /// <summary>Enregistre une variation signée : positive pour recette, négative pour dépense.</summary>
    public void Ajouter(DateTime instant, string categorie, long variation, string detail)
    {
        if (variation == 0) return;
        if (string.IsNullOrWhiteSpace(categorie))
            throw new ArgumentException("Une catégorie financière est nécessaire.", nameof(categorie));
        Mouvements.Add(new MouvementBudget
        {
            Date = DateIso(instant),
            Categorie = categorie,
            Montant = variation,
            Detail = detail ?? ""
        });
    }

    [JsonIgnore]
    public long SoldeReconstitue => SoldeOuverture + Mouvements.Sum(m => m.Montant);

    public string Serialiser() => JsonSerializer.Serialize(this);
}
