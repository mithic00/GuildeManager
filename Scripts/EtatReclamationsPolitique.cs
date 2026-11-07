using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Une protestation est produite uniquement par une règle de politique
/// explicite (à définir ultérieurement), jamais automatiquement ici.
/// </summary>
public sealed class ReclamationPolitique
{
    public const string PrelevementMissions = "PourcentageGuildeMissions";
    public const string EcartRangMissions = "EcartNiveauMission";

    public string MembreId { get; set; } = "";
    public string Motif { get; set; } = "";
    public string DateSignalement { get; set; } = "";
    /// <summary>Identifie le paramètre de politique contesté.</summary>
    public string ClePolitique { get; set; } = "";
    /// <summary>
    /// Valeur confirmée immédiatement avant le changement à l'origine
    /// de cette réclamation. Null pour les réclamations antérieures.
    /// </summary>
    public int? ValeurAvantChangement { get; set; }
}

/// <summary>
/// File persistante de discussions imposées. L'absence (mission, concours)
/// retarde l'entretien sans perdre le signalement.
/// </summary>
public sealed class EtatReclamationsPolitique
{
    public const string CleSauvegarde = "reclamations_politique";
    public int Version { get; set; } = 1;
    public List<ReclamationPolitique> EnAttente { get; set; } = new();

    public void Normaliser()
    {
        EnAttente = (EnAttente ?? new())
            .Where(r => r != null &&
                !string.IsNullOrWhiteSpace(r.MembreId) &&
                !string.IsNullOrWhiteSpace(r.Motif))
            .GroupBy(r => (r.MembreId, r.Motif))
            .Select(g => g.First()).ToList();
    }

    public static EtatReclamationsPolitique Charger()
    {
        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
        {
            GD.PushWarning("Réclamations de politique : " + SaveManager.LastError);
            return new EtatReclamationsPolitique();
        }
        if (string.IsNullOrWhiteSpace(json))
            return new EtatReclamationsPolitique();
        try
        {
            EtatReclamationsPolitique etat =
                JsonSerializer.Deserialize<EtatReclamationsPolitique>(json);
            if (etat == null || etat.Version != 1)
                throw new InvalidOperationException("Version du registre de réclamations inconnue.");
            etat.Normaliser();
            return etat;
        }
        catch (Exception e) when (e is JsonException || e is InvalidOperationException)
        {
            GD.PushWarning("Réclamations de politique illisibles : " + e.Message);
            return new EtatReclamationsPolitique();
        }
    }

    public string Serialiser()
    {
        Normaliser();
        return JsonSerializer.Serialize(this);
    }
}
