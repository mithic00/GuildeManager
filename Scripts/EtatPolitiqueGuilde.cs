using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Choix de politique de la guilde. Ces préférences sont purement déclaratives
/// pour le moment : aucune mécanique de missions/recrutement ne les consulte.
/// Elles sont enregistrées lors de la sauvegarde normale de la partie.
/// </summary>
public sealed class EtatPolitiqueGuilde
{
    public const string CleSauvegarde = "politique_guilde";
    public int Version { get; set; } = 1;

    public bool PolitiqueParDefaut { get; set; } = true;
    public int PourcentageGuildeMissions { get; set; } = 20;
    // 0 : niveaux identiques ; 1..10 : écart autorisé ; 11 : aucune contrainte.
    public int EcartNiveauMission { get; set; } = 11;
    // 0 : libre ; 1 : légère pression ; 2 : grosse pression ; 3 : résultat attendu.
    public int ContrainteMission { get; set; } = 0;
    // Zéro : aucun niveau magique minimal pour rejoindre la guilde.
    public int NiveauMagiqueRequis { get; set; } = 0;

    public List<string> MissionsInterdites { get; set; } = new();
    public List<string> MissionsPreferees { get; set; } = new();
    public string ClientPrefere { get; set; } = "";
    public List<string> ClientsInterdits { get; set; } = new();

    public void RetablirDefauts()
    {
        PolitiqueParDefaut = true;
        PourcentageGuildeMissions = 20;
        EcartNiveauMission = 11;
        ContrainteMission = 0;
        NiveauMagiqueRequis = 0;
        MissionsInterdites = new();
        MissionsPreferees = new();
        ClientPrefere = "";
        ClientsInterdits = new();
    }

    /// <summary>Évite qu'un état ancien/corrompu perturbe les contrôles.</summary>
    public void Normaliser()
    {
        PourcentageGuildeMissions = Math.Clamp(PourcentageGuildeMissions, 0, 50);
        EcartNiveauMission = Math.Clamp(EcartNiveauMission, 0, 11);
        ContrainteMission = Math.Clamp(ContrainteMission, 0, 3);
        NiveauMagiqueRequis = Math.Max(0, NiveauMagiqueRequis);

        MissionsInterdites = (MissionsInterdites ?? new())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal).ToList();
        MissionsPreferees = (MissionsPreferees ?? new())
            .Where(s => !string.IsNullOrWhiteSpace(s) &&
                !MissionsInterdites.Contains(s, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal).ToList();

        ClientsInterdits = (ClientsInterdits ?? new())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal).ToList();
        ClientPrefere ??= "";
        if (ClientsInterdits.Contains(ClientPrefere, StringComparer.Ordinal))
            ClientPrefere = "";

        if (PolitiqueParDefaut)
        {
            // Une ancienne sauvegarde incohérente ne doit pas afficher le mode
            // par défaut alors qu'elle contient des réglages personnalisés.
            PolitiqueParDefaut = PourcentageGuildeMissions == 20 &&
                EcartNiveauMission == 11 && ContrainteMission == 0 &&
                NiveauMagiqueRequis == 0 && MissionsInterdites.Count == 0 &&
                MissionsPreferees.Count == 0 && ClientPrefere == "" &&
                ClientsInterdits.Count == 0;
        }
    }

    public static EtatPolitiqueGuilde Charger()
    {
        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (string.IsNullOrWhiteSpace(json))
            return new EtatPolitiqueGuilde();

        try
        {
            EtatPolitiqueGuilde etat = JsonSerializer.Deserialize<EtatPolitiqueGuilde>(json);
            if (etat == null || etat.Version != 1)
                throw new InvalidOperationException("Version de politique inconnue.");
            etat.Normaliser();
            return etat;
        }
        catch (Exception exception) when (exception is JsonException ||
                                           exception is InvalidOperationException)
        {
            GD.PushWarning("Politique de guilde illisible : " + exception.Message);
            return new EtatPolitiqueGuilde();
        }
    }

    public string Serialiser() => JsonSerializer.Serialize(this);
}
