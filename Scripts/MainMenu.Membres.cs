using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

public partial class MainMenu
{
    private TextureRect _fondBureau, _fondMissions, _fondMembres, _fondMagasin;
    private Control _panneauMissions, _panneauMembres;
    private ItemList _listeDesMembres;
    private Window _fenetreFiche;
    private Control _contenuFiche, _contenuGestion;
    private Control _layoutStatsFiche;
    private RichTextLabel _autresInfosFiche;
    private Button _boutonAutreInfoFiche;
    private Label _titreSectionFiche;
    private RegistreMembresGuilde _registreMembres;
    private readonly List<MembreGuilde> _membresAffiches = new();
    private MembreGuilde _membreConsulte;
    // Panneau actuellement sélectionné dans la navigation principale.
    private string _panneauActif = "bureau";

    private void InitialiserNavigationEtMembres()
    {
        const string racine = "MargeChoixMenu/BoutonsMenu/";
        _fondBureau = GetNode<TextureRect>("AffichagePanneauPrincipal/FondBureau");
        _fondMissions = GetNode<TextureRect>("AffichagePanneauPrincipal/TableauMission");
        _fondMembres = GetNode<TextureRect>("AffichagePanneauPrincipal/ListeMembre");
        _fondMagasin = GetNode<TextureRect>("AffichagePanneauPrincipal/Magasin");
        _panneauMissions = GetNode<Control>("LayoutsPanneauPrincipal/TableauDesMissions");
        _panneauMembres = GetNode<Control>("LayoutsPanneauPrincipal/ListeDesMembres");
        _listeDesMembres = GetNode<ItemList>("LayoutsPanneauPrincipal/ListeDesMembres/ItemListMembres");
        _fenetreFiche = GetNode<Window>("FenetreFichePerso");
        _contenuFiche = GetNode<Control>("FenetreFichePerso/MargeFichePerso");
        _contenuGestion = GetNode<Control>("FenetreFichePerso/LayoutGestion");
        const string layoutFiche = "FenetreFichePerso/MargeFichePerso/Layout/";
        _layoutStatsFiche = GetNode<Control>(layoutFiche + "LayoutStats");
        _autresInfosFiche = GetNode<RichTextLabel>(layoutFiche + "AutresInfos");
        _titreSectionFiche = GetNode<Label>(layoutFiche + "LabelStatistiques");
        _autresInfosFiche.Hide();
        _boutonAutreInfoFiche = GetNode<Button>(
            layoutFiche + "Boutons/GridContainer/BoutonAutreInfo");
        _boutonAutreInfoFiche.Pressed += BasculerAutresInfosFiche;
        // La fenêtre de discussion est indépendante de la fiche personnage.

        GetNode<Button>(racine + "BoutonMembreGuilde").Pressed += AfficherListeMembres;
        GetNode<Button>(racine + "BoutonTableauMission").Pressed += () => BasculerPanneau("missions");
        GetNode<Button>(racine + "BoutonMagasins").Pressed += OuvrirMagasins;
        GetNode<Button>(racine + "BoutonRecrutement").Pressed += OuvrirChoixRecrutement;
        GetNode<Button>(racine + "BoutonBudgetInventaire").Pressed +=
            () => BasculerPanneau("budget_inventaire");
        GetNode<Button>(racine + "BoutonCalendrier").Pressed +=
            () => BasculerPanneau("calendrier");

        _listeDesMembres.ItemSelected += i => OuvrirFicheMembre((int)i);
        _listeDesMembres.ItemActivated += i => OuvrirFicheMembre((int)i);
        _fenetreFiche.CloseRequested += FermerFiche;
        const string actions = "FenetreFichePerso/MargeFichePerso/Layout/Boutons/GridContainer/";
        GetNode<Button>(actions + "BoutonFermer").Pressed += FermerFiche;
        GetNode<Button>(actions + "BoutonGestion").Pressed += AfficherGestion;
        // Le bouton Social et ses sous-pages sont reliés par MainMenu.GestionMembres.cs.
        const string gestion = "FenetreFichePerso/LayoutGestion/Boutons/";
        GetNode<Button>(gestion + "BoutonRetour").Pressed += RetourFiche;
        // Actions des trois boutons connectées par InitialiserGestionMembres().

        _registreMembres = RegistreMembresGuilde.ChargerOuInitialiser();
        if (_registreMembres == null && SaveManager.HasCurrentSave)
            GD.PushWarning("MainMenu : " + RegistreMembresGuilde.DerniereErreur);
        if (_registreMembres != null)
            AfficherValeur(_membres, _registreMembres.Membres.Count);
        AfficherPanneau("bureau");
    }

    // À utiliser pour tous les boutons de menu, y compris ceux ajoutés plus tard :
    // clic sur le panneau déjà ouvert => retour au bureau.
    private bool BasculerPanneau(string nom)
    {
        bool ouverture = _panneauActif != nom;
        AfficherPanneau(ouverture ? nom : "bureau");
        return ouverture;
    }

    private void AfficherPanneau(string nom)
    {
        _panneauActif = nom;
        _fondBureau.Visible = nom == "bureau" || nom == "budget_inventaire" ||
            nom == "recrutement" || nom == "calendrier";
        _fondMissions.Visible = nom == "missions";
        _fondMembres.Visible = nom == "membres";
        _fondMagasin.Visible = nom == "magasin";
        _panneauMissions.Visible = nom == "missions";
        _panneauMembres.Visible = nom == "membres";
        if (_zoneMagasins != null)
            _zoneMagasins.Visible = nom == "magasin";
        GetNode<Control>("JournalJoueur").Visible = nom == "bureau";
        if (_contenuAuberge != null) _contenuAuberge.Visible = nom == "auberge";
        if (_fondAuberge != null) _fondAuberge.Visible = nom == "auberge";
        if (nom == "magasin" || nom == "budget_inventaire" ||
            nom == "recrutement" || nom == "calendrier")
            GD.Print("MainMenu : section " + nom + " à développer.");
    }

    private void AfficherListeMembres()
    {
        if (BasculerPanneau("membres"))
            RafraichirListeMembres();
    }

    private void RafraichirListeMembres()
    {
        _listeDesMembres.Clear();
        _membresAffiches.Clear();
        if (_registreMembres == null) return;
        // Le rang prime sur la magie ; recalculer aussi à l'ouverture de la liste,
        // notamment après chargement, sans migration ni écriture automatique.
        _registreMembres.RecalculerClassement();
        _membresAffiches.AddRange(_registreMembres.Membres
            .OrderBy(m => m.Classement)
            .ThenByDescending(m => m.NiveauMagique)
            .ThenBy(m => m.NomComplet, StringComparer.CurrentCultureIgnoreCase));
        _listeDesMembres.FixedIconSize = new Vector2I(52, 28);
        foreach (MembreGuilde membre in _membresAffiches)
        {
            string nom = membre.NomComplet;
            string potentiel = membre.PotentielMagiqueRevele && membre.PotentielMagique.HasValue ? membre.PotentielMagique.Value.ToString() : "?";
            string rang = string.IsNullOrWhiteSpace(membre.RangAffiche) ? "?" : membre.RangAffiche;
            string classement = membre.EstMaitreDeGuilde ? "MG" :
                membre.Classement == 0 ? "U" :
                membre.Classement == 1 ? "1er" : membre.Classement + "e";
            bool enMission = _etatMissions?.Missions.Any(mission =>
                mission.Etat == "EnCours" &&
                mission.ParticipantIds != null &&
                mission.ParticipantIds.Contains(membre.Id)) == true;
            bool enConcours = !string.IsNullOrWhiteSpace(membre.ConcoursEnCours);
            string texte = classement + "  |  " + rang + "  |  " + nom + "  |  " +
                           membre.Race + "  |  Magie : " + membre.NiveauMagique +
                           "  |  Potentiel : " + potentiel +
                           (enMission ? "  |  EN MISSION" : "") +
                           (enConcours ? "  |  EN CONCOURS " + membre.ConcoursEnCours : "");
            Texture2D icone = ConstruireIconePouvoirs(membre.Pouvoirs);
            int index = _listeDesMembres.AddItem(texte, icone);
            if (!string.IsNullOrWhiteSpace(membre.EquipeId))
            {
                // Même couleur stable pour chaque équipe, même après chargement.
                uint h = 2166136261;
                foreach (char lettre in membre.EquipeId)
                    h = (h ^ lettre) * 16777619;
                float nuance = (h % 360) / 360f;
                Color fond = Color.FromHsv(nuance, 0.23f, 0.30f, 0.72f);
                _listeDesMembres.SetItemCustomBgColor(index, fond);
            }
            _listeDesMembres.SetItemTooltip(index, "Pouvoir(s) : " +
                (membre.Pouvoirs.Count == 0 ? "Aucun" : string.Join(", ", membre.Pouvoirs)) +
                "\nRang : " + rang + "\nNiveau magique : " + membre.NiveauMagique +
                (enMission ? "\nStatut : En mission (absent)" : "") +
                (enConcours ? "\nStatut : En concours " + membre.ConcoursEnCours +
                    "\nRetour prévu : " + membre.DateRetourConcours : ""));
        }
    }

    private static readonly string[] PouvoirsConnus =
    {
        "mage", "sorcier", "clerc", "demoniste", "invocateur", "archimage",
        "ensorceleur", "enchanteur", "metamorphe", "purificateur", "scribtum", "schistite",
        "aquiste", "vegenae", "follet", "storma", "druide", "chamane",
        "guerrier", "assassin_de_l_ombre", "archer_magique", "proctor", "poistodon",
        "berserker", "moine", "invocateur_d_arme", "elementaire_d_eau",
        "elementaire_de_plante", "elementaire_de_feu", "elementaire_de_roche",
        "elementaire_de_vent", "elementaire_d_alcool", "elementaire_de_glace",
        "elementaire_de_vapeur", "elementaire_de_sang", "elementaire_de_froid",
        "elementaire_de_lave", "elementaire_de_chaleur", "elementaire_de_bois",
        "elementaire_fongique", "elementaire_de_spore", "elementaire_de_diamant",
        "elementaire_de_metal", "elementaire_de_pierre_precieuse",
        "elementaire_de_foudre", "elementaire_de_lumiere", "elementaire_d_ombre",
        "elementaire_de_fumee", "elementaire_superieur", "telekinesite",
        "animiste", "devin", "crieur", "illusionniste", "telepathe"
    };

    private static string NormaliserPouvoir(string valeur)
    {
        string decomposed = (valeur ?? "").Normalize(NormalizationForm.FormD);
        StringBuilder sb = new();
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            char min = char.ToLowerInvariant(c);
            if (char.IsLetterOrDigit(min)) sb.Append(min);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }

    private static string CheminIconePouvoir(string pouvoir)
    {
        string identifiant = NormaliserPouvoir(pouvoir);
        if (identifiant == "telekinesiste") identifiant = "telekinesite";
        for (int i = 0; i < PouvoirsConnus.Length; i++)
        {
            if (PouvoirsConnus[i] != identifiant) continue;
            string categorie = i < 18 ? "magicien" : i < 26 ? "combattant" : i < 49 ? "elementaire" : "psychiste";
            return "res://Images/Icone_EM/" + categorie + "__" + identifiant + ".png";
        }
        return "";
    }

    private static Texture2D ConstruireIconePouvoirs(List<string> pouvoirs)
    {
        if (pouvoirs == null || pouvoirs.Count == 0) return null;
        List<Image> images = new();
        foreach (string pouvoir in pouvoirs.Take(2))
        {
            string chemin = CheminIconePouvoir(pouvoir);
            if (!ResourceLoader.Exists(chemin)) continue;
            Texture2D texture = ResourceLoader.Load<Texture2D>(chemin);
            if (texture == null) continue;
            Image image = texture.GetImage();
            image.Convert(Image.Format.Rgba8);
            image.Resize(26, 26);
            images.Add(image);
        }
        if (images.Count == 0) return null;
        Image resultat = Image.CreateEmpty(52, 28, false, Image.Format.Rgba8);
        resultat.Fill(Colors.Transparent);
        for (int i = 0; i < images.Count; i++)
            resultat.BlendRect(images[i], new Rect2I(0, 0, 26, 26), new Vector2I(i * 26, 1));
        return ImageTexture.CreateFromImage(resultat);
    }

    private void OuvrirFicheMembre(int index)
    {
        if (index < 0 || index >= _membresAffiches.Count) return;
        _membreConsulte = _membresAffiches[index];
        ReinitialiserVueFiche();
        RemplirFiche(_membreConsulte);
        RetourFiche();
        _fenetreFiche.PopupCentered();
    }

    private void RemplirFiche(MembreGuilde m)
    {
        const string baseFiche = "FenetreFichePerso/MargeFichePerso/Layout/";
        const string g1 = baseFiche + "StatsGeneral/Layout1/";
        const string g2 = baseFiche + "StatsGeneral/Layout2/";
        const string sg = baseFiche + "LayoutStats/";
        void Remplir(string chemin, string libelle, string valeur) => GetNode<Label>(chemin).Text = libelle + " : " + valeur;
        string inconnu = "Inconnu";
        GetNode<Label>(baseFiche + "LabelNomPrenom").Text = m.NomComplet;
        string membresEquipe = "Aucune";
        if (!string.IsNullOrWhiteSpace(m.EquipeId) && _registreMembres != null)
        {
            var equipiers = _registreMembres.Membres
                .Where(autre => autre.EquipeId == m.EquipeId)
                .Select(autre => autre.NomComplet).ToList();
            if (equipiers.Count > 1)
                membresEquipe = string.Join(", ", equipiers);
        }
        GetNode<Label>(g2 + "LabelConnaissanceMagique2").Text = "Équipe : " + membresEquipe;
        Remplir(g1 + "LabelPrenom", "Prénom", m.Prenom);
        Remplir(g1 + "LabelNom", "Nom", string.IsNullOrEmpty(m.Nom) ? "—" : m.Nom);
        if (_horloge != null)
            m.ActualiserAge(_horloge.Instant);
        Remplir(g1 + "LabelAge", "Âge", m.Age.HasValue ? m.Age.Value + " ans" : inconnu);
        Label labelAge = GetNode<Label>(g1 + "LabelAge");
        DateTime? naissance = m.LireDateNaissance();
        labelAge.TooltipText = naissance.HasValue
            ? "Date de naissance : " +
              naissance.Value.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR")) +
              " (an " + naissance.Value.Year + ")"
            : "Date de naissance inconnue.";
        labelAge.MouseFilter = Control.MouseFilterEnum.Stop;
        Remplir(g1 + "LabelRang", "Rang", m.RangAffiche);
        // Le statut d'adjoint est affiché sans changer le classement de rang.
        if (m.EstAdjoint)
            GetNode<Label>(g1 + "LabelRang").Text += " (Adjoint)";
        if (!string.IsNullOrWhiteSpace(m.ConcoursEnCours))
            GetNode<Label>(g1 + "LabelRang").Text += " — en concours " + m.ConcoursEnCours;
        Remplir(sg + "Layout2/LabelMotivation", "Motivation",
            m.ConnaitStatistique("Motivation") ? (m.Motivation?.ToString() ?? inconnu) : inconnu);
        Remplir(g1 + "LabelFatigue", "Fatigue", m.Fatigue?.ToString() ?? inconnu);
        Remplir(g1 + "LabelExpérience", "Expérience", m.Experience?.ToString() ?? inconnu);
        Remplir(g1 + "LabelNiveau", "Niveau", m.Niveau?.ToString() ?? inconnu);
        Remplir(g2 + "LabelPouvoirs", "Pouvoirs", m.Pouvoirs.Count == 0 ? "Aucun" : string.Join(", ", m.Pouvoirs));
        Remplir(g2 + "LabelNiveauMagique", "Niveau magique", m.NiveauMagique.ToString());
        Remplir(g2 + "LabelPotentielMagique", "Potentiel magique", m.PotentielMagiqueRevele ? (m.PotentielMagique?.ToString() ?? inconnu) : inconnu);
        Remplir(g2 + "LabelPV", "Points de vie", m.PointsDeVie?.ToString() ?? inconnu);
        Remplir(g2 + "LabelPM", "Points de magie", m.PointsDeMagie?.ToString() ?? inconnu);
        Remplir(g2 + "LabelConnaissanceMagique", "Connaissance magique", m.ConnaissanceMagique.ToString());
        var traits = ReglesTraitsPersonnage.Evaluer(m);
        Remplir(g1 + "LabelTraitsPositifs", "Traits positifs",
            ReglesTraitsPersonnage.Afficher(traits, false));
        Remplir(g2 + "LabelTraitsNegatifs", "Traits négatifs",
            ReglesTraitsPersonnage.Afficher(traits, true));
        Remplir(sg + "Layout1/LabelBonheur", "Bonheur", m.Bonheur + "/100");
        Remplir(sg + "Layout1/LabelMoral", "Moral", m.Moral + "/100");

        string[][] stats =
        {
            new [] { "Communication", "Leadership", "Charisme", "Empathie", "Gentillesse" },
            new [] { "Intelligence", "Memoire", "Volonte", "Calme", "Frustration" },
            new [] { "Force", "Agilite", "Constitution", "Vitesse", "Endurance" }
        };
        string[][] labels =
        {
            new [] { "Communication", "Leadership", "Charisme", "Empathie", "Gentillesse" },
            new [] { "Intelligence", "Mémoire", "Volonte", "Calme", "Frustration" },
            new [] { "Force", "Agilite", "Constitution", "Vitesse", "Endurance" }
        };
        string[] chemins = { "Layout1", "Layout2", "Layout3" };
        for (int c = 0; c < 3; c++)
            for (int i = 0; i < stats[c].Length; i++)
            {
                string cle = stats[c][i];
                string etiquette = labels[c][i];
                string valeur = m.ConnaitStatistique(cle) &&
                    m.Statistiques.TryGetValue(cle, out int nombre) ? nombre + "/20" : inconnu;
                Remplir(sg + chemins[c] + "/Label" + etiquette, etiquette, valeur);
            }

        TextureRect portrait = GetNode<TextureRect>(baseFiche + "StatsGeneral/Portait");
        string[] races = { "Humain", "ElfeGris", "Grumol", "HumainDesMonts", "HautElfe", "Nain", "Tanneur", "Khajiit" };
        string race = m.ApparenceId >= 1 && m.ApparenceId <= races.Length ? races[m.ApparenceId - 1] : "";
        string sexe = m.Sexe == "F" ? "Femme" : "Homme";
        string cheminPortrait = "res://Images/Portraits/GrandFormat/" + race + "_" + sexe + ".png";
        string cheminDefaut = "res://Images/Portraits/GrandFormat/Portait_Defaut.png";

        // Seul le maître de guilde affiche son portrait personnalisé.
        // Tous les autres membres utilisent systématiquement le portrait par défaut.
        // Si le portrait grand format du maître est absent, utiliser aussi le défaut.
        string cheminUtilise = m.EstMaitreDeGuilde && ResourceLoader.Exists(cheminPortrait)
            ? cheminPortrait : cheminDefaut;
        portrait.Texture = ResourceLoader.Load<Texture2D>(cheminUtilise);
        RemplirAutresInfosFiche(m);
    }

    /// <summary>
    /// Bascule entre les caractéristiques et les autres renseignements.
    /// La partie supérieure (portrait, identité, niveau) reste affichée.
    /// </summary>
    private void BasculerAutresInfosFiche()
    {
        if (_membreConsulte == null) return;
        bool afficherAutresInfos = !_autresInfosFiche.Visible;
        _layoutStatsFiche.Visible = !afficherAutresInfos;
        _autresInfosFiche.Visible = afficherAutresInfos;
        _boutonAutreInfoFiche.Text = afficherAutresInfos
            ? "Statistiques" : "Autre Info";
        _titreSectionFiche.Text = afficherAutresInfos
            ? "Autres informations" : "Statistiques";
        if (afficherAutresInfos)
            RemplirAutresInfosFiche(_membreConsulte);
    }

    private void ReinitialiserVueFiche()
    {
        _layoutStatsFiche.Show();
        _autresInfosFiche.Hide();
        _boutonAutreInfoFiche.Text = "Autre Info";
        _titreSectionFiche.Text = "Statistiques";
    }

    private void RemplirAutresInfosFiche(MembreGuilde membre)
    {
        if (membre == null || _autresInfosFiche == null) return;
        var texte = new StringBuilder();

        texte.AppendLine("IDENTITÉ ET PARCOURS");
        texte.AppendLine("Race : " + (string.IsNullOrWhiteSpace(membre.Race) ? "Inconnue" : membre.Race));
        texte.AppendLine("Sexe : " + (membre.Sexe == "F" ? "Femme" :
            membre.Sexe == "H" ? "Homme" : "Non renseigné"));
        DateTime? naissance = membre.LireDateNaissance();
        texte.AppendLine("Date de naissance : " +
            (naissance.HasValue ? naissance.Value.ToString("d MMMM yyyy", Francais) :
                "Inconnue"));
        texte.AppendLine("Métier : " +
            (string.IsNullOrWhiteSpace(membre.Metier) ? "Aucun renseigné" : membre.Metier));
        texte.AppendLine("Rôle dans la guilde : " +
            (membre.EstMaitreDeGuilde ? "Maître de guilde" :
                membre.EstAdjoint ? "Adjoint" : "Membre"));
        texte.AppendLine();

        texte.AppendLine("COMPÉTENCES");
        var competences = membre.Competences == null
            ? new List<string>()
            : membre.Competences.Where(nom => !string.IsNullOrWhiteSpace(nom))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(nom => nom, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (competences.Count == 0)
            texte.AppendLine("Aucune compétence renseignée.");
        else
            foreach (string competence in competences)
                texte.AppendLine("• " + competence);
        texte.AppendLine();

        texte.AppendLine("CARRIÈRE ET FINANCES");
        texte.AppendLine("Écus personnels : " +
            membre.EcusPersonnels.ToString("N0", Francais) + " écus");
        var succes = membre.MissionsReussiesParRang;
        if (succes == null || succes.Count == 0 || succes.All(x => x.Value <= 0))
        {
            texte.AppendLine("Missions réussies : aucune");
        }
        else
        {
            texte.AppendLine("Missions réussies : " +
                succes.Values.Where(nombre => nombre > 0).Sum() + " au total");
            foreach (var rang in succes.Where(x => x.Value > 0)
                .OrderBy(x =>
                {
                    int index = Array.IndexOf(ReglesMissions.Rangs, x.Key);
                    return index < 0 ? int.MaxValue : index;
                }))
                texte.AppendLine("• Rang " + ReglesMissions.FormaterRang(rang.Key) +
                    " : " + rang.Value);
        }
        texte.AppendLine();

        texte.AppendLine("SITUATION ACTUELLE");
        var missions = _etatMissions?.Missions?.Where(mission =>
            mission.Etat == "EnCours" &&
            mission.ParticipantIds != null &&
            mission.ParticipantIds.Contains(membre.Id)).ToList();
        if (missions?.Count > 0)
        {
            foreach (MissionGuilde mission in missions)
                texte.AppendLine("En mission : " +
                    (string.IsNullOrWhiteSpace(mission.Titre) ?
                        mission.Categorie : mission.Titre));
        }
        else if (!string.IsNullOrWhiteSpace(membre.ConcoursEnCours))
        {
            texte.AppendLine("Participe au concours " + membre.ConcoursEnCours);
            if (!string.IsNullOrWhiteSpace(membre.DateRetourConcours) &&
                DateTime.TryParseExact(membre.DateRetourConcours,
                    "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime retour))
                texte.AppendLine("Retour prévu : " + HorlogeJeu.Formater(retour));
        }
        else
            texte.AppendLine(membre.Absent ? "Absent de la guilde" :
                "Présent à la guilde");

        _autresInfosFiche.Text = texte.ToString().TrimEnd();
    }

    private void AfficherGestion()
    {
        MasquerOngletsFiche();
        ActualiserBoutonsGestion();
        _contenuGestion.Show();
    }

    private void RetourFiche()
    {
        MasquerOngletsFiche();
        _contenuFiche.Show();
    }

    private void FermerFiche() => _fenetreFiche.Hide();

    private void AfficherInformationFiche(string message)
    {
        AfficherInformation(message);
    }
}
