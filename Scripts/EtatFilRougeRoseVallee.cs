using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>Progression exclusivement en mémoire jusqu'à la sauvegarde manuelle.</summary>
public sealed class EtatFilRougeRoseVallee
{
    public const string CleSauvegarde = "fil_rouge_rose_vallee";
    public int Version { get; set; } = 1;
    public int ActeTermine { get; set; }
    public int ActeEnCours { get; set; }
    public string Noeud { get; set; } = "";
    public string DateEvenement { get; set; } = "";
    public string DateLimiteRue4 { get; set; } = "";
    public bool AttendRue4 { get; set; }
    public bool NotificationAbandon { get; set; }
    public bool PreuveCommerciale { get; set; }
    public bool TemoignageFavorable { get; set; }
    public bool PreuveFalsification { get; set; }
    public bool PreuveSabotage { get; set; }
    public int RancuneCharles { get; set; }
    public int ReputationFacon { get; set; }
    public bool VoieDiplomatique { get; set; }
    public bool AccordDependance { get; set; }
    public bool EnquetePossibleDiscussion { get; set; }
    public bool BatailleCharlesPrevue { get; set; }
    public string DateBatailleCharles { get; set; } = "";
    public bool BatailleCharlesSignalee { get; set; }
    public string Fin { get; set; } = "";
    public List<ModificateurSpawnFilRouge> Modificateurs { get; set; } = new();
    public List<string> Choix { get; set; } = new();
    public List<string> MissionsCreees { get; set; } = new();

    public double MultiplicateurMissions(string demandeur)
    {
        double facteur = 1;
        foreach (var modificateur in Modificateurs ?? new())
            if (string.Equals(demandeur, modificateur.Demandeur, StringComparison.Ordinal))
                facteur *= modificateur.Facteur;
        return Math.Clamp(facteur, 0, 100);
    }

    public void AjouterMultiplicateur(string id, double facteur, string demandeur = "")
    {
        Modificateurs ??= new();
        // Un effet narratif ne peut pas être cumulé deux fois après un rechargement.
        if (Modificateurs.Exists(m => m.Id == id)) return;
        Modificateurs.Add(new() { Id = id, Facteur = facteur, Demandeur = demandeur });
    }

    public void ReinitialiserMultiplicateurs(double facteur, string id)
    {
        Modificateurs = new();
        if (Math.Abs(facteur - 1) > 0.000001)
            AjouterMultiplicateur(id, facteur);
    }

    public static EtatFilRougeRoseVallee Charger()
    {
        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<EtatFilRougeRoseVallee>(json) ?? new(); }
        catch (JsonException e)
        {
            Godot.GD.PushWarning("État du fil rouge Rose-Vallée illisible : " + e.Message);
            return new();
        }
    }

    public string Serialiser() => JsonSerializer.Serialize(this);
}

public sealed class ModificateurSpawnFilRouge
{
    public string Id { get; set; } = "";
    public string Demandeur { get; set; } = "";
    public double Facteur { get; set; } = 1;
}
