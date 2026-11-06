using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Inventaire commun de la guilde. Les achats restent en mémoire jusqu'à la
/// sauvegarde atomique de MainMenu, comme les écus et le registre du budget.
/// </summary>
public sealed class InventaireGuilde
{
    public const string CleSauvegarde = "inventaire_guilde";

    public int Version { get; set; } = 1;
    public Dictionary<string, int> Quantites { get; set; } = new();

    public static InventaireGuilde ChargerOuCreer()
    {
        if (!SaveManager.HasCurrentSave)
            return null;

        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
        {
            GD.PushError("InventaireGuilde : " + SaveManager.LastError);
            return null;
        }

        // Les anciennes parties n'ont pas encore d'inventaire : il est vide,
        // sans créer d'objets fictifs ni écrire de données avant sauvegarde.
        if (string.IsNullOrWhiteSpace(json))
            return new InventaireGuilde();

        try
        {
            InventaireGuilde inventaire = JsonSerializer.Deserialize<InventaireGuilde>(json);
            if (inventaire == null || inventaire.Version != 1 || inventaire.Quantites == null)
                throw new InvalidOperationException("Inventaire de guilde illisible.");
            // Les anciens inventaires utilisent encore les noms génériques
            // des trois armes, renommées dans le catalogue documentaire.
            // Migrer en mémoire : la sauvegarde normale persistera ces noms.
            inventaire.MigrerNomsAnciens();
            return inventaire;
        }
        catch (Exception exception)
        {
            GD.PushError("InventaireGuilde : chargement impossible : " + exception);
            return null;
        }
    }

    private void MigrerNomsAnciens()
    {
        foreach ((string ancien, string nouveau) in new[]
        {
            ("Dague", "Dague en fer"),
            ("Epée", "Epée en fer"),
            ("Massue", "Massue en fer")
        })
        {
            if (!Quantites.TryGetValue(ancien, out int nombre) || nombre <= 0)
                continue;
            int dejaPresent = Quantite(nouveau);
            if (dejaPresent > int.MaxValue - nombre)
            {
                GD.PushError("InventaireGuilde : impossible de fusionner " + ancien +
                    " avec " + nouveau + " (quantité excessive).");
                continue;
            }
            Quantites[nouveau] = dejaPresent + nombre;
            Quantites.Remove(ancien);
        }
    }

    public int Quantite(string nom) =>
        !string.IsNullOrWhiteSpace(nom) && Quantites.TryGetValue(nom, out int nombre)
            ? nombre : 0;

    public bool Ajouter(string nom, int nombre = 1)
    {
        if (string.IsNullOrWhiteSpace(nom) || nombre <= 0 ||
            Quantite(nom) > int.MaxValue - nombre)
            return false;
        Quantites[nom] = Quantite(nom) + nombre;
        return true;
    }

    public bool Retirer(string nom, int nombre = 1)
    {
        if (nombre <= 0 || Quantite(nom) < nombre)
            return false;
        int restant = Quantite(nom) - nombre;
        if (restant == 0) Quantites.Remove(nom);
        else Quantites[nom] = restant;
        return true;
    }

    public string Serialiser() => JsonSerializer.Serialize(this);
}
