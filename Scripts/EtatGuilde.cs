using Godot;
using System;
using System.Text.Json;

/// <summary>
/// Ressources de guilde conservées dans la base SQLite de la partie active.
/// À ne pas confondre avec les statistiques du maître de guilde (« creation_initiale »).
/// </summary>
public sealed class EtatGuilde
{
    public const string CleSauvegarde = "etat_guilde";

    public int Version { get; set; } = 1;
    public long Membres { get; set; } = 1;
    public long Ecus { get; set; } = 60000;
    public long Savoir { get; set; } = 0;
    public long Pouvoir { get; set; } = 1;
    public long Prestige { get; set; } = 0;

    public static string DerniereErreur { get; private set; } = "";

    public static EtatGuilde CreerEtatInitial() => new EtatGuilde();

    /// <summary>
    /// Lit l'état persistant. Pour les parties créées avant l'introduction
    /// des ressources, initialise et enregistre une seule fois les valeurs de départ.
    /// Ne réinitialise jamais une partie contenant déjà un état valide.
    /// </summary>
    public static EtatGuilde ChargerOuInitialiser()
    {
        DerniereErreur = "";
        if (!SaveManager.HasCurrentSave)
        {
            DerniereErreur = "Aucune sauvegarde active.";
            return null;
        }

        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
        {
            DerniereErreur = SaveManager.LastError;
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            EtatGuilde nouvelEtat = CreerEtatInitial();
            return nouvelEtat.Sauvegarder() ? nouvelEtat : null;
        }

        try
        {
            EtatGuilde etat = JsonSerializer.Deserialize<EtatGuilde>(json);
            if (etat == null || etat.Version != 1)
                throw new InvalidOperationException("Version des ressources de guilde inconnue.");
            return etat;
        }
        catch (Exception exception)
        {
            DerniereErreur = "Ressources de guilde illisibles : " + exception.Message;
            GD.PushError("EtatGuilde : " + DerniereErreur);
            return null; // Ne pas écraser une sauvegarde corrompue.
        }
    }

    public bool Sauvegarder()
    {
        DerniereErreur = "";
        if (!SaveManager.HasCurrentSave)
        {
            DerniereErreur = "Aucune sauvegarde active.";
            return false;
        }

        try
        {
            string json = JsonSerializer.Serialize(this);
            if (SaveManager.SetValue(CleSauvegarde, json))
                return true;

            DerniereErreur = SaveManager.LastError;
            return false;
        }
        catch (Exception exception)
        {
            DerniereErreur = exception.Message;
            GD.PushError("EtatGuilde : " + DerniereErreur);
            return false;
        }
    }
}
