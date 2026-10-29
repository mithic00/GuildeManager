using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Un membre de guilde. Les valeurs non encore définies par les règles
/// (âge, niveau ordinaire, potentiel connu, PV, PM...) restent nulles.
/// </summary>
public sealed class MembreGuilde
{
    public string Id { get; set; } = "";
    public bool EstJoueur { get; set; }
    public string Prenom { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Race { get; set; } = "";
    public string Sexe { get; set; } = "H";
    public int ApparenceId { get; set; }
    public string Rang { get; set; } = "*";
    public int Classement { get; set; } = 1;
    public int NiveauMagique { get; set; } = 1;
    public int? PotentielMagique { get; set; }
    public List<string> Pouvoirs { get; set; } = new();
    public Dictionary<string, int> Statistiques { get; set; } = new();
    public int Bonheur { get; set; } = 20;
    public int Moral { get; set; } = 100;
    public int ConnaissanceMagique { get; set; } = 0;
    public int? Age { get; set; }
    public int? Motivation { get; set; }
    public int? Fatigue { get; set; }
    public int? Experience { get; set; }
    public int? Niveau { get; set; }
    public int? PointsDeVie { get; set; }
    public int? PointsDeMagie { get; set; }

    public string NomComplet => (Prenom + " " + Nom).Trim();

    /// <summary>Découpe uniquement sur le caractère espace ' '.</summary>
    public static void SeparerNom(string texte, out string prenom, out string nom)
    {
        string nettoye = (texte ?? "").Trim(' ');
        int position = nettoye.IndexOf(' ');
        prenom = position < 0 ? nettoye : nettoye.Substring(0, position);
        nom = position < 0 ? "" : nettoye.Substring(position + 1).Trim(' ');
    }

    public static MembreGuilde DepuisCreation(CreationPerso.DonneesCreationInitiale creation)
    {
        if (creation == null) throw new ArgumentNullException(nameof(creation));
        string prenom = creation.PrenomPersonnage;
        string nom = creation.NomFamillePersonnage;
        if (string.IsNullOrWhiteSpace(prenom))
            SeparerNom(creation.NomPersonnage, out prenom, out nom);

        return new MembreGuilde
        {
            Id = "joueur",
            EstJoueur = true,
            Prenom = prenom,
            Nom = nom,
            Race = creation.RacePersonnage,
            Sexe = creation.SexePersonnage,
            ApparenceId = creation.ApparenceId,
            Rang = "*",
            Classement = 1,
            NiveauMagique = 1,
            PotentielMagique = null,
            Pouvoirs = creation.Pouvoirs == null ? new List<string>() : new List<string>(creation.Pouvoirs),
            Statistiques = creation.Statistiques == null
                ? new Dictionary<string, int>()
                : new Dictionary<string, int>(creation.Statistiques),
            Bonheur = 20,
            Moral = 100,
            ConnaissanceMagique = 0
        };
    }
}

/// <summary>Ensemble des membres, extensible au futur recrutement.</summary>
public sealed class RegistreMembresGuilde
{
    public const string CleSauvegarde = "membres_guilde";
    public int Version { get; set; } = 1;
    public List<MembreGuilde> Membres { get; set; } = new();

    public static string DerniereErreur { get; private set; } = "";

    public static RegistreMembresGuilde Nouveau(CreationPerso.DonneesCreationInitiale creation)
    {
        return new RegistreMembresGuilde
        {
            Membres = new List<MembreGuilde> { MembreGuilde.DepuisCreation(creation) }
        };
    }

    public static RegistreMembresGuilde ChargerOuInitialiser()
    {
        DerniereErreur = "";
        if (!SaveManager.HasCurrentSave)
        {
            DerniereErreur = "Aucune sauvegarde active.";
            return null;
        }

        string json = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrWhiteSpace(SaveManager.LastError))
        {
            DerniereErreur = SaveManager.LastError;
            return null;
        }

        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                RegistreMembresGuilde registre = JsonSerializer.Deserialize<RegistreMembresGuilde>(json);
                if (registre == null || registre.Version != 1 || registre.Membres == null)
                    throw new InvalidOperationException("Registre des membres invalide.");
                return registre;
            }
            catch (Exception ex)
            {
                DerniereErreur = "Registre des membres illisible : " + ex.Message;
                GD.PushError(DerniereErreur);
                return null; // Ne jamais écraser des données existantes illisibles.
            }
        }

        // Migration transparente des parties antérieures à l'ajout du registre.
        string creationJson = SaveManager.GetValue("creation_initiale", "");
        if (string.IsNullOrWhiteSpace(creationJson) || !string.IsNullOrWhiteSpace(SaveManager.LastError))
        {
            DerniereErreur = "Impossible de reconstruire le joueur depuis la sauvegarde d'origine.";
            return null;
        }

        try
        {
            var creation = JsonSerializer.Deserialize<CreationPerso.DonneesCreationInitiale>(creationJson);
            if (creation == null)
                throw new InvalidOperationException("Données de création absentes.");
            RegistreMembresGuilde nouveau = Nouveau(creation);
            return nouveau.Sauvegarder() ? nouveau : null;
        }
        catch (Exception ex)
        {
            DerniereErreur = "Migration des membres impossible : " + ex.Message;
            GD.PushError(DerniereErreur);
            return null;
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
            if (SaveManager.SetValue(CleSauvegarde, JsonSerializer.Serialize(this)))
                return true;
            DerniereErreur = SaveManager.LastError;
        }
        catch (Exception ex)
        {
            DerniereErreur = ex.Message;
        }

        GD.PushError("RegistreMembresGuilde : " + DerniereErreur);
        return false;
    }
}
