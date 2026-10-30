using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>Journal durable des événements de la partie, du plus ancien au plus récent.</summary>
public sealed class JournalPartie
{
    public const string CleSauvegarde = "journal_joueur";
    public int Version { get; set; } = 1;
    public List<EntreeJournal> Entrees { get; set; } = new();

    public static string DerniereErreur { get; private set; } = "";

    public static JournalPartie ChargerOuCreer()
    {
        DerniereErreur = "";
        if (!SaveManager.HasCurrentSave)
        {
            DerniereErreur = "Aucune sauvegarde active.";
            return null;
        }

        string texte = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
        {
            DerniereErreur = SaveManager.LastError;
            return null;
        }

        if (string.IsNullOrWhiteSpace(texte))
        {
            JournalPartie journal = new();
            return journal.Enregistrer() ? journal : null;
        }

        try
        {
            JournalPartie journal = JsonSerializer.Deserialize<JournalPartie>(texte);
            if (journal == null || journal.Version != 1 || journal.Entrees == null)
                throw new InvalidOperationException("Format du journal inconnu.");
            return journal;
        }
        catch (Exception e)
        {
            DerniereErreur = "Journal illisible : " + e.Message;
            GD.PushError(DerniereErreur);
            return null;
        }
    }

    /// <summary>Ne modifie le journal en mémoire que si l'écriture SQLite réussit.</summary>
    public bool Ajouter(DateTime instant, string description, string type = "temps")
    {
        EntreeJournal entree = new()
        {
            DateJeu = instant.ToString("yyyy-MM-dd'T'HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture),
            Type = type,
            Description = description
        };
        Entrees.Add(entree);
        if (Enregistrer())
            return true;

        Entrees.RemoveAt(Entrees.Count - 1);
        return false;
    }

    public bool Enregistrer()
    {
        DerniereErreur = "";
        if (!SaveManager.HasCurrentSave)
        {
            DerniereErreur = "Aucune sauvegarde active.";
            return false;
        }

        if (SaveManager.SetValue(CleSauvegarde, JsonSerializer.Serialize(this)))
            return true;
        DerniereErreur = SaveManager.LastError;
        return false;
    }
}

public sealed class EntreeJournal
{
    public string DateJeu { get; set; } = "";
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
}
