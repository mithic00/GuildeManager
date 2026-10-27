using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Branche les contrôles déjà créés dans CreationPerso.tscn.
/// Les informations de ville sont intégrées au C# et ne dépendent pas du dossier InfoJeu.
/// </summary>
public partial class CreationPerso : Control
{
    private const string SceneMenuDemarrer = "res://Scenes/MenuDemarrer.tscn";
    private const string ScenePrincipale = "res://Scenes/MainMenu.tscn";
    private const string CleSauvegardeCreation = "creation_initiale";
    private const int NombrePointsBonus = 5;
    private const int StatInitiale = 10;
    private const int MinStat = 0;
    private const int MaxStat = 20;

    private static readonly string[] NomsStatsModifiables =
    {
        "Communication", "Leadership", "Charisme", "Empathie",
        "Intelligence", "Memoire", "Volonte", "Calme",
        "Force", "Agilite", "Constitution", "Vitesse", "Endurance"
    };

    // Noms des pouvoirs et autres libellés restent ceux placés dans l'ItemList Godot.
    // Les particularités cachées des villes ne sont volontairement PAS exposées au joueur.
    private static readonly VilleDepart[] Villes =
    {
        new VilleDepart("Rose-Vallée", "BoutonRoseVallee", "- Comté de Rose Vallée, Royaume Humain.\n- 25 000 habitants\n- 90% Humain, 5% Nain, 5% Autre.\n- Taux de criminalité : faible\n- Académie magique : Non\n- Guilde de mercenaire : Non\n- Richesse de la population : 5/10\n- Biome : Vallée\n- Biomes à proximité : Vallée, Rivière, Montagne rocheuse.", "Préfecture de son comté, la petite commune de Rose-Vallée cherche un maitre de guilde depuis longtemps pour ne plus dépendre des comtés voisin en matière de guilde magique. Au cœur des grandes vallées du royaume Humain, Rose-Vallée est une petite commune paisible installée au bord de la rivière Tangue. Elle vit principalement du commerce de fleurs.", "res://Images/Fonds/Fond_Rose-Vallee.png"),
        new VilleDepart("Agrar", "BoutonAgrar", "- Montagnes Sud, Royaume Nain.\n- 40 000 habitants\n- 80% Nain, 15% Humain, 5% Autre.\n- Taux de criminalité : faible\n- Académie magique : Non\n- Guilde de mercenaire : Oui (\"Le Roc Rouge\")\n- Richesse de la population : 4/10\n- Biome : Montagne rocheuse\n- Biomes à proximité : Montagne rocheuse, Glacier, mer.", "Préfecture de la région des Montagnes Sud, Agrar est une petite ville construite à flan de falaise. On y voit la mer depuis les modestes remparts de pierre. La région des montagnes sud la plus basse du royaume Nain. Elle est donc beaucoup moins peuplé et apprécié par les siens. Ce qui fait d'Agrar la ville la plus grande de toute la moitié sud du Royaume Nain. Une guilde dans cette ville serait très apprécié pour couvrir cette partie du Royaume.", "res://Images/Fonds/Fond_Agrar.png"),
        new VilleDepart("Shaelis", "BoutonShaelis", "- Région Nahamis, Royaume Elfe Gris.\n- 20 000 habitants\n- 90% Elfe Gris, 5% Humain, 5% Autre.\n- Taux de criminalité : faible\n- Académie magique : Non\n- Guilde de mercenaire : Non\n- Richesse de la population : 7/10\n- Biome : Mer\n- Biomes à proximité : Plaine, Vallée, Montagne Boisée", "Chef lieu de la région de Nahamis, Shaelis est une petite cité portuaire réputé pour ses plages de sables fin et sa cuisine de la mer. L'idée de faire construire une guilde magique a émergée depuis l'arrivé du nouveau Seigneur de la cité. Cette décision est très partagé au sein de la cité.", "res://Images/Fonds/Fond_Shaelis.png"),
        new VilleDepart("Alturia", "BoutonAlturia", "- Région de l'écriture, Royaume Haut-Elfe.\n- 25 000 habitants\n- 98% Haut-Elfe, 2% Autre.\n- Taux de criminalité : faible\n- Académie magique : Non\n- Guilde de mercenaire : Oui\n- Richesse de la population : 6/10\n- Biome : Plaine\n- Biomes à proximité : Vallée, Rivière, Lac.", "Préfecture de sa région, Alturia est vue comme la capitale de l'écriture et du théâtre au sein du royaume Haut-Elfe. La ville est également connu pour son miel. En matière de guilde magique, Alturia est depuis toujours dépendant de Bahiri, la capitale du royaume. C'est pourquoi le duc de la ville souhaite faire construire une guilde magique avec l'objectif de voir grandir la notoriété de sa ville.", "res://Images/Fonds/Fond_Alturia.png"),
        new VilleDepart("Gori Nirog", "BoutonGoriNirog", "- Marais de la Presqu'Ile, Royaume Grumol.\n- 30 000 habitants\n- 80% Grumol, 10% Elfe Gris, 5% Nain, 5% autre.\n- Taux de criminalité : faible\n- Académie magique : Non\n- Guilde de mercenaire : Oui\n- Richesse de la population : 3/10\n- Biome : Marais\n- Biomes à proximité : Mer", "Gori Nirog est une grande cité au cœur de la Presqu'Ile, au nord du Royaume Grumol. La région ne dispose pas de guilde magique, et son chef lieu, Gamak Naos, ne souhaite pas en avoir une. Le choix se tourne naturellement vers Gori Nirog. Connu pour ses fleurs de sucre, Gori Nirog est une cité paisible et sans histoire. La cité ce situe sur la route commerciale principale entre le royaume des Elfes Gris et celui des Grumols.", "res://Images/Fonds/Fond_GoriNirog.png"),
        new VilleDepart("Niclass", "BoutonNiclass", "- Comté de Niclass, Royaume Humain des monts.\n- 20 000 habitants\n- 90% Humains des monts, 8% Tanneurs, 2% autre.\n- Taux de criminalité : faible\n- Académie magique : Non\n- Guilde de mercenaire : Non\n- Richesse de la population : 5/10\n- Biome : Haute Montagne\n- Biomes à proximité : Glacier, Lac, Vallée", "Niclass est la commune de plus de 10 000 hab. la plus haute du royaume humain des monts. Niché à plus de 6500m, le chef lieu du Comté trône sur la vallée des 3 dents. Spécialisé dans l'élevage et l'ornithologie, cette commune est a la recherche d'un maitre de guilde pour répondre aux demandes de tout le comté, déserté de guilde magique.", "res://Images/Fonds/Fond_Niclass.png"),
        new VilleDepart("Thorm", "BoutonThorm", "- Région d'Agfar, Royaume Tanneur.\n- 25 000 habitants\n- 90% Tanneurs, 5% Humains des monts, 5% autre.\n- Taux de criminalité : moyenne\n- Académie magique : Non\n- Guilde de mercenaire : Non\n- Richesse de la population : 4/10\n- Biome : Mer\n- Biomes à proximité : Vallée, Rivière, Montagne boisée", "La cité de Thorm est désespérément à la recherche d'un maitre de guilde depuis des années. Boudé par tout le royaume, cette région est régulièrement en proie aux attaques et pillages des pirates. C'est pourquoi l'arrivé d'une guilde magique serait une bénédiction pour les habitants. La cité de Throm est peu connu, mais est historiquement une grand cité constructrice de navire.", "res://Images/Fonds/Fond_Thorm.png")
    };

    private LineEdit _saisieNomPersonnage;
    private LineEdit _saisieNomGuilde;
    private ItemList _listeExperience;
    private ItemList _listePouvoirs;
    private readonly Button[] _cadresPersonnage = new Button[8];
    private readonly Button[] _cadresEmbleme = new Button[9];
    private RichTextLabel _texteStatsVille;
    private RichTextLabel _texteDescriptionVille;
    private TextureRect _dessinVille;
    private Window _fenetreEtapes;
    private VBoxContainer _layoutStat;
    private VBoxContainer _layoutGuilde;
    private Label _labelSocial;
    private AcceptDialog _dialoguePrincipal;
    private AcceptDialog _dialogueEtapes;
    private readonly Dictionary<string, HSlider> _curseurs = new();
    private readonly Dictionary<string, Button> _boutonsMoins = new();
    private readonly Dictionary<string, Button> _boutonsPlus = new();
    private readonly Dictionary<string, int> _stats = new();

    private int _apparenceSelectionnee = -1;
    private int _emblemeSelectionne = -1;
    private int _villeSelectionnee = 0;
    private bool _limitationPouvoirEnCours;
    private bool _creationEnCours;

    public override void _Ready()
    {
        _saisieNomPersonnage = GetNode<LineEdit>("MargePersonnage/Layout/LineEditNomPersonnage");
        _listeExperience = GetNode<ItemList>("MargePersonnage/Layout/ItemListExperence");
        _listePouvoirs = GetNode<ItemList>("MargePersonnage/Layout/ItemListPouvoir");
        _texteStatsVille = GetNode<RichTextLabel>("MargeInfoVille/Layout/HLayout/RLabelStatVille");
        _texteDescriptionVille = GetNode<RichTextLabel>("MargeInfoVille/Layout/RlabelDescriptionVille");
        _dessinVille = GetNode<TextureRect>("MargeInfoVille/Layout/HLayout/DessinVille");
        _fenetreEtapes = GetNode<Window>("FenetreStatEtConfGuilde");
        _layoutStat = GetNode<VBoxContainer>("FenetreStatEtConfGuilde/MargeStat/LayoutStat");
        _layoutGuilde = GetNode<VBoxContainer>("FenetreStatEtConfGuilde/MargeStat/LayoutGuilde");
        _saisieNomGuilde = GetNode<LineEdit>("FenetreStatEtConfGuilde/MargeStat/LayoutGuilde/LineEditNomGuilde");
        _labelSocial = GetNode<Label>("FenetreStatEtConfGuilde/MargeStat/LayoutStat/LabelSocial");

        InitialiserExperience();
        InitialiserPouvoirs();
        InitialiserApparences();
        InitialiserVilles();
        InitialiserStatistiques();
        InitialiserEmblemes();

        GetNode<Button>("BoutonsUI/BoutonRetour").Pressed += RetourMenu;
        GetNode<Button>("BoutonsUI/BoutonSuivant").Pressed += AfficherEtapeStatistiques;
        GetNode<Button>("FenetreStatEtConfGuilde/MargeStat/LayoutStat/BoutonsUI/BoutonRetour").Pressed += RetourPersonnage;
        GetNode<Button>("FenetreStatEtConfGuilde/MargeStat/LayoutStat/BoutonsUI/BoutonSuivant").Pressed += AfficherEtapeGuilde;
        GetNode<Button>("FenetreStatEtConfGuilde/MargeStat/LayoutGuilde/BoutonsUI/BoutonRetour").Pressed += RetourStatistiques;
        GetNode<Button>("FenetreStatEtConfGuilde/MargeStat/LayoutGuilde/BoutonsUI/BoutonCommencer").Pressed += CreerGuilde;
        _fenetreEtapes.CloseRequested += RetourPersonnage;

        _layoutStat.Show();
        _layoutGuilde.Hide();
    }

    private void InitialiserExperience()
    {
        // Les entrées 1 à 3 sont visibles mais grisées tant que le joueur
        // ne les a pas débloquées sur son profil, indépendamment d'une sauvegarde.
        int niveauDebloque = ProgressionNouvellePartie.ExperienceMaximumDebloquee;
        for (int i = 0; i < _listeExperience.ItemCount; i++)
        {
            _listeExperience.SetItemDisabled(i, i > niveauDebloque);
            _listeExperience.SetItemTooltipEnabled(i, true);
        }

        if (_listeExperience.ItemCount > 0)
            _listeExperience.Select(0);
    }

    private void InitialiserPouvoirs()
    {
        // La scène possède select_mode = 2 : sélection multiple par bascule.
        // Le signal couvre les clics individuels sans imposer Ctrl/Maj.
        _listePouvoirs.MultiSelected += (index, selected) =>
        {
            if (_limitationPouvoirEnCours || !selected)
                return;

            if (_listePouvoirs.GetSelectedItems().Length <= 2)
                return;

            _limitationPouvoirEnCours = true;
            _listePouvoirs.Deselect((int)index);
            _limitationPouvoirEnCours = false;
            AfficherInformation("Le personnage ne peut posséder que deux pouvoirs au maximum.");
        };
    }

    private void InitialiserApparences()
    {
        for (int i = 0; i < _cadresPersonnage.Length; i++)
        {
            int numero = i;
            Button bouton = GetNode<Button>("MargePersonnage/Layout/BouttonsFondPerso/BoutonPerso" + (i + 1));
            bouton.ToggleMode = true;
            bouton.TooltipText = "Apparence " + (i + 1);
            bouton.Pressed += () => SelectionnerApparence(numero);
            _cadresPersonnage[i] = bouton;
        }
    }

    private void SelectionnerApparence(int index)
    {
        _apparenceSelectionnee = index;
        ActualiserCadres(_cadresPersonnage, index);
    }

    private void InitialiserVilles()
    {
        for (int i = 0; i < Villes.Length; i++)
        {
            int index = i;
            Button bouton = GetNode<Button>(
                "MargeChoixVilleDepart/LayoutListeBoutonVilles/" + Villes[i].NomBouton);
            bouton.Pressed += () => SelectionnerVille(index);
        }

        SelectionnerVille(0);
    }

    private void SelectionnerVille(int index)
    {
        _villeSelectionnee = index;
        VilleDepart ville = Villes[index];

        // Conserve exactement la disposition de la scène : les stats à droite,
        // la description en dessous. Aucun accès au .txt n'a lieu en jeu.
        _texteStatsVille.Text = ville.Statistiques;
        _texteDescriptionVille.Text = ville.Description;

        if (ResourceLoader.Exists(ville.ImagePath))
        {
            _dessinVille.Texture = ResourceLoader.Load<Texture2D>(ville.ImagePath);
            _dessinVille.CustomMinimumSize = Vector2.Zero;
            _dessinVille.TooltipText = ville.Nom;
        }
        else
        {
            // Évite de laisser l'illustration de Rose-Vallée sur une autre ville.
            _dessinVille.Texture = null;
            _dessinVille.CustomMinimumSize = new Vector2(320, 200);
            _dessinVille.TooltipText = "Illustration de " + ville.Nom + " à venir.";
        }
    }

    private void InitialiserStatistiques()
    {
        const string racine = "FenetreStatEtConfGuilde/MargeStat/LayoutStat/";
        foreach (string nom in NomsStatsModifiables)
        {
            string cle = nom;
            string prefixe = racine + "Stat" + nom + "/";
            HSlider slider = GetNode<HSlider>(prefixe + "HSlider" + nom);
            Button moins = GetNode<Button>(prefixe + "Bouton" + nom + "Moins");
            Button plus = GetNode<Button>(prefixe + "Bouton" + nom + "Plus");

            _stats[nom] = StatInitiale;
            _curseurs[nom] = slider;
            _boutonsMoins[nom] = moins;
            _boutonsPlus[nom] = plus;

            slider.MinValue = MinStat;
            slider.MaxValue = MaxStat;
            slider.Step = 1.0;
            slider.SetValueNoSignal(StatInitiale);

            slider.ValueChanged += valeur => ModifierStat(cle, (int)Math.Round(valeur));
            moins.Pressed += () => ModifierStat(cle, _stats[cle] - 1);
            plus.Pressed += () => ModifierStat(cle, _stats[cle] + 1);
        }

        ActualiserPointsDisponibles();
    }

    private int PointsDisponibles
    {
        get
        {
            int pointsDepenses = _stats.Values.Sum(valeur => valeur - StatInitiale);
            return NombrePointsBonus - pointsDepenses;
        }
    }

    private void ModifierStat(string nom, int valeurDemandee)
    {
        int ancienneValeur = _stats[nom];
        int valeur = Math.Clamp(valeurDemandee, MinStat, MaxStat);

        if (valeur > ancienneValeur)
            valeur = Math.Min(valeur, ancienneValeur + PointsDisponibles);

        if (valeur != ancienneValeur)
            _stats[nom] = valeur;

        // Corrige également un déplacement direct du curseur qui coûterait
        // plus de points que le solde disponible.
        _curseurs[nom].SetValueNoSignal(valeur);
        ActualiserPointsDisponibles();
    }

    private void ActualiserPointsDisponibles()
    {
        int points = PointsDisponibles;
        _labelSocial.Text = "Social — Points disponibles : " + points;

        foreach (string nom in NomsStatsModifiables)
        {
            _boutonsMoins[nom].Disabled = _stats[nom] <= MinStat;
            _boutonsPlus[nom].Disabled = _stats[nom] >= MaxStat || points <= 0;
        }
    }

    private void InitialiserEmblemes()
    {
        for (int i = 0; i < _cadresEmbleme.Length; i++)
        {
            int numero = i;
            Button bouton = GetNode<Button>(
                "FenetreStatEtConfGuilde/MargeStat/LayoutGuilde/GridContainerEmblemeGuilde/BoutonEmblemeGuilde" + (i + 1));
            bouton.ToggleMode = true;
            bouton.TooltipText = "Emblème " + (i + 1);
            bouton.Pressed += () => SelectionnerEmbleme(numero);
            _cadresEmbleme[i] = bouton;
        }
    }

    private void SelectionnerEmbleme(int index)
    {
        _emblemeSelectionne = index;
        ActualiserCadres(_cadresEmbleme, index);
    }

    private static void ActualiserCadres(Button[] cadres, int indexSelectionne)
    {
        for (int i = 0; i < cadres.Length; i++)
        {
            bool selectionne = i == indexSelectionne;
            cadres[i].ButtonPressed = selectionne;
            // Les boutons vides sont plats dans la scène. Le bouton choisi
            // est rendu non plat pour matérialiser la sélection sans image.
            cadres[i].Flat = !selectionne;
            cadres[i].Modulate = selectionne ? new Color(1f, 0.94f, 0.72f) : Colors.White;
        }
    }

    private void RetourMenu()
    {
        if (!ResourceLoader.Exists(SceneMenuDemarrer))
        {
            AfficherInformation("MenuDemarrer.tscn est introuvable.");
            return;
        }

        Error erreur = GetTree().ChangeSceneToFile(SceneMenuDemarrer);
        if (erreur != Error.Ok)
            AfficherInformation("Impossible de revenir au menu (" + erreur + ").");
    }

    private void AfficherEtapeStatistiques()
    {
        if (string.IsNullOrWhiteSpace(_saisieNomPersonnage.Text))
        {
            AfficherInformation("Veuillez renseigner le nom de votre personnage.");
            return;
        }

        if (_apparenceSelectionnee < 0)
        {
            AfficherInformation("Veuillez sélectionner une apparence pour votre personnage.");
            return;
        }

        // De 0 à 2 pouvoirs autorisés : seule la limite maximum est spécifiée.
        // Sans sélection explicite, l'expérience est toujours « Sans Expérience ».
        _layoutGuilde.Hide();
        _layoutStat.Show();
        _fenetreEtapes.PopupCentered();
    }

    private void RetourPersonnage()
    {
        _fenetreEtapes.Hide();
    }

    private void AfficherEtapeGuilde()
    {
        if (PointsDisponibles != 0)
        {
            AfficherInformation("Il vous reste " + PointsDisponibles +
                " point(s) de statistiques à répartir avant de poursuivre.");
            return;
        }

        _layoutStat.Hide();
        _layoutGuilde.Show();
    }

    private void RetourStatistiques()
    {
        _layoutGuilde.Hide();
        _layoutStat.Show();
    }

    private void CreerGuilde()
    {
        if (_creationEnCours)
            return;

        string nomPersonnage = _saisieNomPersonnage.Text.Trim();
        string nomGuilde = _saisieNomGuilde.Text.Trim();

        if (string.IsNullOrWhiteSpace(nomGuilde))
        {
            AfficherInformation("Veuillez renseigner un nom pour votre guilde.");
            return;
        }

        if (PointsDisponibles != 0)
        {
            AfficherInformation("Vous devez répartir tous les points de statistiques.");
            return;
        }

        if (!ResourceLoader.Exists(ScenePrincipale))
        {
            AfficherInformation("La scène principale est introuvable : " + ScenePrincipale);
            return;
        }

        var statistiques = new Dictionary<string, int>(_stats)
        {
            ["Gentillesse"] = StatInitiale,
            ["Frustration"] = StatInitiale
        };

        int[] pouvoirsSelectionnes = _listePouvoirs.GetSelectedItems();
        List<string> pouvoirs = pouvoirsSelectionnes
            .Select(index => _listePouvoirs.GetItemText(index))
            .ToList();

        int[] experiences = _listeExperience.GetSelectedItems();
        int experienceIndex = experiences.Length > 0 ? experiences[0] : 0;
        if (experienceIndex > ProgressionNouvellePartie.ExperienceMaximumDebloquee)
        {
            AfficherInformation("Ce niveau d'expérience de départ n'est pas encore débloqué.");
            return;
        }

        DonneesCreationInitiale donnees = new()
        {
            NomPersonnage = nomPersonnage,
            ApparenceId = _apparenceSelectionnee + 1,
            ExperienceDepartIndex = experienceIndex,
            ExperienceDepartLibelle = _listeExperience.GetItemText(experienceIndex),
            Pouvoirs = pouvoirs,
            VilleDepart = Villes[_villeSelectionnee].Nom,
            NomGuilde = nomGuilde,
            EmblemeId = _emblemeSelectionne + 1, // 0 tant qu'aucun emblème n'est choisi.
            Statistiques = statistiques
        };

        _creationEnCours = true;
        try
        {
            SaveManager.SaveInfo sauvegarde = SaveManager.CreateSave(nomGuilde + " — " + nomPersonnage);
            if (sauvegarde == null)
            {
                AfficherInformation("Impossible de créer la sauvegarde : " + SaveManager.LastError);
                return;
            }

            // Enregistrement groupé des 15 caractéristiques et des choix initiaux
            // dans un seul enregistrement JSON SQLite.
            string json = JsonSerializer.Serialize(donnees);
            if (!SaveManager.SetValue(CleSauvegardeCreation, json))
            {
                string erreur = SaveManager.LastError;
                SaveManager.DeleteSave(sauvegarde.Id); // pas de partie vide en cas d'échec.
                AfficherInformation("Impossible d'enregistrer les informations de départ : " + erreur);
                return;
            }

            _fenetreEtapes.Hide();
            Error transition = GetTree().ChangeSceneToFile(ScenePrincipale);
            if (transition != Error.Ok)
                AfficherInformation("La guilde a été sauvegardée, mais le chargement du jeu a échoué (" + transition + ").");
        }
        catch (Exception exception)
        {
            GD.PushError("CreationPerso : " + exception);
            AfficherInformation("Une erreur est survenue pendant la création : " + exception.Message);
        }
        finally
        {
            _creationEnCours = false;
        }
    }

    private void AfficherInformation(string message)
    {
        // Un AcceptDialog enfant de la fenêtre exclusive évite les conflits
        // « parent window already has another exclusive child ».
        bool dansFenetre = _fenetreEtapes.Visible;
        AcceptDialog dialogue = dansFenetre ? _dialogueEtapes : _dialoguePrincipal;
        if (dialogue == null)
        {
            dialogue = new AcceptDialog
            {
                Title = "Information",
                Exclusive = true
            };

            if (dansFenetre)
            {
                _fenetreEtapes.AddChild(dialogue);
                _dialogueEtapes = dialogue;
            }
            else
            {
                AddChild(dialogue);
                _dialoguePrincipal = dialogue;
            }
        }

        dialogue.DialogText = message;
        dialogue.PopupCentered();
    }

    private sealed class VilleDepart
    {
        public string Nom { get; }
        public string NomBouton { get; }
        public string Statistiques { get; }
        public string Description { get; }
        public string ImagePath { get; }

        public VilleDepart(string nom, string nomBouton, string statistiques, string description, string imagePath)
        {
            Nom = nom;
            NomBouton = nomBouton;
            Statistiques = statistiques;
            Description = description;
            ImagePath = imagePath;
        }
    }

    /// <summary>
    /// Format lu ultérieurement via :
    /// JsonSerializer.Deserialize<DonneesCreationInitiale>(SaveManager.GetValue("creation_initiale")).
    /// </summary>
    public sealed class DonneesCreationInitiale
    {
        public int Version { get; set; } = 1;
        public string NomPersonnage { get; set; } = "";
        public int ApparenceId { get; set; }
        public int ExperienceDepartIndex { get; set; }
        public string ExperienceDepartLibelle { get; set; } = "";
        public List<string> Pouvoirs { get; set; } = new();
        public string VilleDepart { get; set; } = "";
        public string NomGuilde { get; set; } = "";
        public int EmblemeId { get; set; }
        public Dictionary<string, int> Statistiques { get; set; } = new();
    }
}
