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
    private Control _contenuFiche, _contenuGestion, _contenuConversation;
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
        _contenuConversation = GetNode<Control>("FenetreFichePerso/LayoutConversation");

        GetNode<Button>(racine + "BoutonMembreGuilde").Pressed += AfficherListeMembres;
        GetNode<Button>(racine + "BoutonTableauMission").Pressed += () => BasculerPanneau("missions");
        GetNode<Button>(racine + "BoutonMagasins").Pressed += () => BasculerPanneau("magasin");
        GetNode<Button>(racine + "BoutonRecrutement").Pressed += OuvrirChoixRecrutement;
        GetNode<Button>(racine + "BoutonBudget").Pressed += () => BasculerPanneau("budget");
        GetNode<Button>(racine + "BoutonEvenements").Pressed += () => BasculerPanneau("evenements");

        _listeDesMembres.ItemSelected += i => OuvrirFicheMembre((int)i);
        _listeDesMembres.ItemActivated += i => OuvrirFicheMembre((int)i);
        _fenetreFiche.CloseRequested += FermerFiche;
        const string actions = "FenetreFichePerso/MargeFichePerso/Layout/Boutons/GridContainer/";
        GetNode<Button>(actions + "BoutonFermer").Pressed += FermerFiche;
        GetNode<Button>(actions + "BoutonGestion").Pressed += AfficherGestion;
        GetNode<Button>(actions + "BoutonConversation").Pressed += () => AfficherInformationFiche("Les conversations seront développées ultérieurement.");
        GetNode<Button>(actions + "BoutonVirer").Disabled = true; // Le maître ne peut pas s'exclure lui-même.
        const string gestion = "FenetreFichePerso/LayoutGestion/Boutons/";
        GetNode<Button>(gestion + "BoutonRetour").Pressed += RetourFiche;
        foreach (string nom in new[] { "BoutonPromouvoir", "BoutonPromouvoirAdjoint", "BoutonConcours" })
            GetNode<Button>(gestion + nom).Disabled = true; // Aucun système de promotion implémenté.

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
        _fondBureau.Visible = nom == "bureau" || nom == "budget" || nom == "recrutement" || nom == "evenements";
        _fondMissions.Visible = nom == "missions";
        _fondMembres.Visible = nom == "membres";
        _fondMagasin.Visible = nom == "magasin";
        _panneauMissions.Visible = nom == "missions";
        _panneauMembres.Visible = nom == "membres";
        GetNode<Control>("JournalJoueur").Visible = nom == "bureau";
        if (_contenuAuberge != null) _contenuAuberge.Visible = nom == "auberge";
        if (_fondAuberge != null) _fondAuberge.Visible = nom == "auberge";
        if (nom == "magasin" || nom == "budget" || nom == "recrutement" || nom == "evenements")
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
        // Le classement sauvegardé est la seule position de tri :
        // -1 = maître de guilde, 0 = rang U, 1, 2, 3... = autres membres.
        _membresAffiches.AddRange(_registreMembres.Membres
            .OrderBy(m => m.Classement)
            .ThenBy(m => m.NiveauMagique * -1)
            .ThenBy(m => m.NomComplet, StringComparer.CurrentCultureIgnoreCase));
        _listeDesMembres.FixedIconSize = new Vector2I(52, 28);
        foreach (MembreGuilde membre in _membresAffiches)
        {
            string nom = membre.NomComplet;
            string potentiel = membre.PotentielMagique.HasValue ? membre.PotentielMagique.Value.ToString() : "?";
            string rang = string.IsNullOrWhiteSpace(membre.RangAffiche) ? "?" : membre.RangAffiche;
            string classement = membre.EstMaitreDeGuilde ? "MG" :
                membre.Classement == 0 ? "U" :
                membre.Classement == 1 ? "1er" : membre.Classement + "e";
            string texte = classement + "  |  " + rang + "  |  " + nom + "  |  " +
                           membre.Race + "  |  Magie : " + membre.NiveauMagique +
                           "  |  Potentiel : " + potentiel;
            Texture2D icone = ConstruireIconePouvoirs(membre.Pouvoirs);
            int index = _listeDesMembres.AddItem(texte, icone);
            _listeDesMembres.SetItemTooltip(index, "Pouvoir(s) : " +
                (membre.Pouvoirs.Count == 0 ? "Aucun" : string.Join(", ", membre.Pouvoirs)) +
                "\nRang : " + rang + "\nNiveau magique : " + membre.NiveauMagique);
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
        Remplir(g1 + "LabelPrenom", "Prénom", m.Prenom);
        Remplir(g1 + "LabelNom", "Nom", string.IsNullOrEmpty(m.Nom) ? "—" : m.Nom);
        Remplir(g1 + "LabelAge", "Âge", m.Age.HasValue ? m.Age.Value + " ans" : inconnu);
        Remplir(g1 + "LabelRang", "Rang", m.RangAffiche);
        Remplir(g1 + "LabelMotivation", "Motivation", m.Motivation?.ToString() ?? inconnu);
        Remplir(g1 + "LabelFatigue", "Fatigue", m.Fatigue?.ToString() ?? inconnu);
        Remplir(g1 + "LabelExpérience", "Expérience", m.Experience?.ToString() ?? inconnu);
        Remplir(g1 + "LabelNiveau", "Niveau", m.Niveau?.ToString() ?? inconnu);
        Remplir(g2 + "LabelPouvoirs", "Pouvoirs", m.Pouvoirs.Count == 0 ? "Aucun" : string.Join(", ", m.Pouvoirs));
        Remplir(g2 + "LabelNiveauMagique", "Niveau magique", m.NiveauMagique.ToString());
        Remplir(g2 + "LabelPotentielMagique", "Potentiel magique", m.PotentielMagique?.ToString() ?? inconnu);
        Remplir(g2 + "LabelPV", "Points de vie", m.PointsDeVie?.ToString() ?? inconnu);
        Remplir(g2 + "LabelPM", "Points de magie", m.PointsDeMagie?.ToString() ?? inconnu);
        Remplir(g2 + "LabelConnaissanceMagique", "Connaissance magique", m.ConnaissanceMagique.ToString());
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
                string valeur = m.Statistiques.TryGetValue(cle, out int nombre) ? nombre + "/20" : inconnu;
                Remplir(sg + chemins[c] + "/Label" + etiquette, etiquette, valeur);
            }

        TextureRect portrait = GetNode<TextureRect>(baseFiche + "StatsGeneral/Portait");
        string[] races = { "Humain", "ElfeGris", "Grumol", "HumainDesMonts", "HautElfe", "Nain", "Tanneur", "Khajiit" };
        string race = m.ApparenceId >= 1 && m.ApparenceId <= races.Length ? races[m.ApparenceId - 1] : "";
        string sexe = m.Sexe == "F" ? "Femme" : "Homme";
        string cheminPortrait = "res://Images/Portraits/GrandFormat/" + race + "_" + sexe + ".png";
        string cheminPetitPortrait = "res://Images/Portraits/" + race + "_" + sexe + ".png";
        string cheminDefaut = "res://Images/Portraits/GrandFormat/Portait_Defaut.png";

        // Le portrait du joueur ne doit jamais être agrandi depuis la vignette
        // de sélection : si son grand format manque, afficher le portrait par défaut.
        // Pour les autres membres, garder provisoirement le repli sur la petite image.
        string cheminUtilise = ResourceLoader.Exists(cheminPortrait) ? cheminPortrait
            : (!m.EstJoueur && ResourceLoader.Exists(cheminPetitPortrait))
                ? cheminPetitPortrait : cheminDefaut;
        portrait.Texture = ResourceLoader.Load<Texture2D>(cheminUtilise);
    }

    private void AfficherGestion()
    {
        _contenuFiche.Hide();
        _contenuConversation.Hide();
        _contenuGestion.Show();
    }

    private void RetourFiche()
    {
        _contenuGestion.Hide();
        _contenuConversation.Hide();
        _contenuFiche.Show();
    }

    private void FermerFiche() => _fenetreFiche.Hide();

    private void AfficherInformationFiche(string message)
    {
        AcceptDialog d = new() { Title = "Information", DialogText = message };
        _fenetreFiche.AddChild(d);
        d.Confirmed += () => d.QueueFree();
        d.PopupCentered();
    }
}
