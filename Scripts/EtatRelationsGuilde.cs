using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Réputations diplomatiques de la guilde, comprises entre -100 et +100.
/// Les valeurs absentes sont neutres (0). Les réputations propres aux quêtes,
/// comme celle de Facon, restent dans leur état narratif d'origine.
/// </summary>
public sealed class EtatRelationsGuilde
{
    public const string CleSauvegarde = "relations_guilde";
    public const string AcademieMagique = "academie_magique";
    public const string Commercants = "commercants";
    private const string PrefixeVille = "ville:";

    public int Version { get; set; } = 1;
    public Dictionary<string, int> Reputations { get; set; } = new();

    public static string CleVille(string ville) => PrefixeVille + (ville ?? "").Trim();

    public int Lire(string cle)
    {
        if (string.IsNullOrWhiteSpace(cle) || Reputations == null)
            return 0;
        return Reputations.TryGetValue(cle, out int valeur)
            ? Math.Clamp(valeur, -100, 100) : 0;
    }

    /// <summary>
    /// Point d'entrée pour les futurs effets de mission et de dialogue.
    /// Retourne la valeur effectivement obtenue après plafonnement.
    /// </summary>
    public int Ajuster(string cle, int delta)
    {
        if (string.IsNullOrWhiteSpace(cle))
            throw new ArgumentException("Une réputation doit avoir un identifiant.", nameof(cle));
        Reputations ??= new();
        int nouvelleValeur = (int)Math.Clamp((long)Lire(cle) + delta, -100L, 100L);
        Reputations[cle] = nouvelleValeur;
        return nouvelleValeur;
    }

    public static EtatRelationsGuilde ChargerOuCreer()
    {
        if (!SaveManager.HasCurrentSave)
            return null;

        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
        {
            GD.PushError("EtatRelationsGuilde : " + SaveManager.LastError);
            return null;
        }

        // Compatibilité des anciennes parties : toutes les relations
        // démarrent à 0 sans écriture avant la prochaine sauvegarde.
        if (string.IsNullOrWhiteSpace(json))
            return new EtatRelationsGuilde();

        try
        {
            EtatRelationsGuilde etat = JsonSerializer.Deserialize<EtatRelationsGuilde>(json);
            if (etat == null || etat.Version != 1 || etat.Reputations == null)
                throw new InvalidOperationException("État des réputations illisible.");

            // Les éventuelles valeurs hors bornes des anciennes sauvegardes
            // ne sont pas propagées dans les jauges.
            foreach (string cle in new List<string>(etat.Reputations.Keys))
                etat.Reputations[cle] = Math.Clamp(etat.Reputations[cle], -100, 100);
            return etat;
        }
        catch (Exception exception)
        {
            GD.PushError("EtatRelationsGuilde : échec du chargement : " + exception);
            return null;
        }
    }

    public string Serialiser() => JsonSerializer.Serialize(this);
}
