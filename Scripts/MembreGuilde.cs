using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Un membre de guilde. L'âge et le potentiel non connu restent indéfinis.
/// PV/PM sont tirés aux dés à la création, puis persistés.
/// </summary>
public sealed class MembreGuilde
{
    public string Id { get; set; } = "";
    public bool EstJoueur { get; set; }
    /// <summary>Statut de direction, distinct du rang de missions du membre.</summary>
    public bool EstMaitreDeGuilde { get; set; }
    public string Prenom { get; set; } = "";
    public string Nom { get; set; } = "";
    public string Race { get; set; } = "";
    /// <summary>Identifiant persistant de l’équipe de recrutement. Vide pour un membre solo.</summary>
    public string EquipeId { get; set; } = "";
    public string Metier { get; set; } = "";
    public List<string> Competences { get; set; } = new();
    public string Sexe { get; set; } = "H";
    public int ApparenceId { get; set; }
    public string Rang { get; set; } = "*";
    public int Classement { get; set; } = 1;
    public int NiveauMagique { get; set; } = 1;
    public int? PotentielMagique { get; set; }
    /// <summary>Le potentiel réel existe, mais reste caché tant qu'il n'est pas révélé.</summary>
    public bool PotentielMagiqueRevele { get; set; }
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

    /// <summary>Affichage de la direction, sans perdre le rang de missions du joueur.</summary>
    [JsonIgnore]
    public string RangAffiche => EstMaitreDeGuilde ? "MG" : Rang;

    /// <summary>
    /// Les positions -1 et 0 sont réservées au maître de guilde et au rang U.
    /// Les autres membres utilisent la position numérique sauvegardée (1er, 2e...),
    /// qui sera déterminée par leur niveau magique puis modifiable par le joueur.
    /// Aucun reclassement automatique des membres ordinaires n'est effectué ici.
    /// </summary>
    public bool NormaliserClassementReserve()
    {
        if (EstMaitreDeGuilde && Classement != -1)
        {
            Classement = -1;
            return true;
        }

        if (!EstMaitreDeGuilde
            && string.Equals(Rang?.Trim(), "U", StringComparison.OrdinalIgnoreCase)
            && Classement != 0)
        {
            Classement = 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Complète uniquement les champs initiaux absents d'une ancienne sauvegarde.
    /// Les tirages PV/PM ne sont effectués qu'en l'absence de valeur persistée.
    /// Les valeurs déjà existantes (XP, niveaux, PV, PM...) sont conservées.
    /// </summary>
    public bool CompleterInitialisationJoueur()
    {
        if (!EstJoueur)
            return false;

        bool modifie = false;
        // Compatibilité des sauvegardes créées avant la distinction maître/membre.
        if (!EstMaitreDeGuilde) { EstMaitreDeGuilde = true; modifie = true; }
        if (NormaliserClassementReserve()) modifie = true;
        if (!Motivation.HasValue) { Motivation = 20; modifie = true; }
        if (!Fatigue.HasValue) { Fatigue = 0; modifie = true; }
        if (!Experience.HasValue) { Experience = 0; modifie = true; }
        if (!Niveau.HasValue) { Niveau = 1; modifie = true; }

        // Le potentiel du joueur n'est jamais déterminé ou communiqué.
        if (PotentielMagique.HasValue) { PotentielMagique = null; modifie = true; }

        // Certaines anciennes parties peuvent ne contenir que l'identifiant
        // du portrait, pas le libellé de race.
        if (string.IsNullOrWhiteSpace(Race) && ApparenceId >= 1 && ApparenceId <= 8)
        {
            string[] races = {
                "Humain", "Elfe gris", "Grumol", "Humain des monts",
                "Haut-Elfe", "Nain", "Tanneur", "Khajiit"
            };
            Race = races[ApparenceId - 1];
            modifie = true;
        }

        if (!PointsDeVie.HasValue || !PointsDeMagie.HasValue)
        {
            var tirages = CalculCaracteristiquesMagiques.TirerPourNiveaux(
                Race, Pouvoirs, Math.Max(1, Niveau.Value));

            if (!PointsDeVie.HasValue) { PointsDeVie = tirages.PointsDeVie; modifie = true; }
            if (!PointsDeMagie.HasValue) { PointsDeMagie = tirages.PointsDeMagie; modifie = true; }
        }

        return modifie;
    }

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

        MembreGuilde joueur = new MembreGuilde
        {
            Id = "joueur",
            EstJoueur = true,
            EstMaitreDeGuilde = true,
            Prenom = prenom,
            Nom = nom,
            Race = creation.RacePersonnage,
            Sexe = creation.SexePersonnage,
            ApparenceId = creation.ApparenceId,
            Rang = "*",
            Classement = -1,
            NiveauMagique = 1,
            PotentielMagique = null,
            Pouvoirs = creation.Pouvoirs == null ? new List<string>() : new List<string>(creation.Pouvoirs),
            Statistiques = creation.Statistiques == null
                ? new Dictionary<string, int>()
                : new Dictionary<string, int>(creation.Statistiques),
            Bonheur = 20,
            Moral = 100,
            ConnaissanceMagique = 0,
            Motivation = 20,
            Fatigue = 0,
            Experience = 0,
            Niveau = 1
        };

        // Un dé par niveau et par type de ressource, une seule fois.
        joueur.CompleterInitialisationJoueur();
        return joueur;
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

                bool migrationNecessaire = false;
                foreach (MembreGuilde membre in registre.Membres)
                {
                    if (membre == null)
                        throw new InvalidOperationException("Le registre contient un membre invalide.");
                    if (membre.CompleterInitialisationJoueur())
                        migrationNecessaire = true;
                    if (membre.NormaliserClassementReserve())
                        migrationNecessaire = true;
                }

                // Migration persistée une seule fois. Au chargement suivant,
                // les mêmes PV/PM seront relus sans aucun nouveau tirage.
                if (migrationNecessaire && !registre.Sauvegarder())
                    return null;

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
