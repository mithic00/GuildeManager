using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Historique individuel durable, indépendant du registre des membres actifs :
/// les événements restent consultables après un décès ou un départ.
/// Écriture uniquement lors de la sauvegarde explicite du joueur.
/// </summary>
public sealed class EvenementHistoriqueMembre
{
    public string MembreId { get; set; } = "";
    public string NomMembre { get; set; } = "";
    public string Date { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string MissionId { get; set; } = "";
    public string CategorieMission { get; set; } = "";
    public string RangMission { get; set; } = "";
    public double? AncienneValeur { get; set; }
    public double? NouvelleValeur { get; set; }
}

public sealed class HistoriqueMembresGuilde
{
    public const string CleSauvegarde = "historique_membres_guilde";
    public List<EvenementHistoriqueMembre> Evenements { get; set; } = new();

    public static HistoriqueMembresGuilde Charger()
    {
        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
            throw new InvalidOperationException("Chargement historique : " + SaveManager.LastError);
        if (string.IsNullOrWhiteSpace(json))
            return new HistoriqueMembresGuilde();

        HistoriqueMembresGuilde historique = JsonSerializer.Deserialize<HistoriqueMembresGuilde>(json);
        if (historique == null || historique.Evenements == null)
            throw new InvalidOperationException("Historique des membres illisible.");
        return historique;
    }

    public void Ajouter(MembreGuilde membre, DateTime date, string type,
        string description, MissionGuilde mission = null,
        double? avant = null, double? apres = null)
    {
        if (membre == null) return;
        Evenements.Add(new EvenementHistoriqueMembre
        {
            MembreId = membre.Id,
            NomMembre = membre.NomComplet,
            Date = ReglesMissions.DateIso(date),
            Type = type,
            Description = description,
            MissionId = mission?.Id ?? "",
            CategorieMission = mission?.Categorie ?? "",
            RangMission = mission?.Rang ?? "",
            AncienneValeur = avant,
            NouvelleValeur = apres
        });
    }

    /// <summary>Accès prévu pour la future fenêtre d'historique d'un membre.</summary>
    public IReadOnlyList<EvenementHistoriqueMembre> PourMembre(string id) =>
        Evenements.Where(e => e.MembreId == id).ToList();

    public string Serialiser() => JsonSerializer.Serialize(this);
}
