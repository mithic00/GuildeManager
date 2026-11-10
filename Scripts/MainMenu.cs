using Godot;
using System;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Affiche l'état de la guilde issu de la sauvegarde active.
/// Les sections de jeu non encore développées ne sont pas simulées.
/// </summary>
public partial class MainMenu : Control
{
    private const string SceneDemarrage = "res://Scenes/MenuDemarrer.tscn";
    private const string CleCreation = "creation_initiale";
    private const string CleDate = "date_jeu";
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");

    private Label _nomGuilde;
    private Label _membres;
    private Label _ecus;
    private Label _savoir;
    private Label _pouvoir;
    private Label _prestige;
    private Label _date;
    private Window _fenetreQuitter;
    private Window _fenetreOptions;
    private HSlider _sliderVolumeOptions;
    private Label _labelVolumeOptions;
    private Window _fenetreInfo;
    private RichTextLabel _messageFenetreInfo;
    private Window _fenetreAReouvrirApresInfo;
    private EtatGuilde _etat;

    public override void _Ready()
    {
        _nomGuilde = GetNode<Label>("LabelNomGuilde");
        _membres = GetNode<Label>("LabelNbMembre");
        _ecus = GetNode<Label>("LabelNbEcus");
        _savoir = GetNode<Label>("LabelNbSavoir");
        _pouvoir = GetNode<Label>("LabelNbPouvoir");
        _prestige = GetNode<Label>("LabelNbPrestige");
        _date = GetNode<Label>("MargeChoixMenu/BoutonsMenu/LabelDateEtHeure");
        _fenetreQuitter = GetNode<Window>("FenetreMenuQuitter");
        InitialiserOptions();
        InitialiserFenetreInfo();

        GetNode<Button>("MargeChoixMenu/BoutonsMenu/BoutonQuitter").Pressed += () => _fenetreQuitter.PopupCentered();
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonSave").Pressed += SauvegarderSansQuitter;
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonSaveEtQuitter").Pressed += () => Quitter(true);
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonQuitterSansSave").Pressed += () => Quitter(false);
        _fenetreQuitter.CloseRequested += () => _fenetreQuitter.Hide();
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonFermer").Pressed +=
            () => _fenetreQuitter.Hide();
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonOption").Pressed += OuvrirOptions;

        // Les pages Tableau des missions, Membres, Magasins, Recrutement,
        // Budget et Événements seront reliées quand leur contenu existera.
        // MainBouton est également réservé à une future navigation.
        ChargerAffichage();
        // Une guilde déjà transmise est une partie terminée : ne pas
        // permettre de poursuivre la direction en rechargeant la sauvegarde.
        if (_etat?.PartieTerminee == true)
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, SceneDemarrage);
            return;
        }
        InitialiserNavigationEtMembres();
        InitialiserTempsEtJournal();
        // Le registre financier doit exister avant les autres systèmes.
        // Une erreur de nœud dans le bureau ou l'inventaire n'empêche
        // ainsi plus l'historisation des récompenses de mission.
        InitialiserRegistreBudget();
        InitialiserRecrutementAuberge();
        InitialiserNavigationMagasins();
        InitialiserMissions();
        InitialiserRapportsMissions();
        InitialiserBudgetEtBureau();
        InitialiserGestionMembres();
        InitialiserDiscussions();
        InitialiserFilRougeRoseVallee();
        InitialiserRelationsBureau();
        InitialiserReclamationsPolitique();
        ActualiserAnniversaires();
        if (_horloge != null)
            TraiterRetoursConcours(_horloge.Instant);
    }

    private void ChargerAffichage()
    {
        if (!SaveManager.HasCurrentSave)
        {
            GD.PushWarning("MainMenu : aucune partie active. Chargez une sauvegarde depuis le menu de démarrage.");
            _nomGuilde.Text = "Aucune guilde chargée";
            _nomGuilde.TooltipText = "Chargez ou créez une partie.";
            _membres.Text = _ecus.Text = _savoir.Text = _pouvoir.Text = _prestige.Text = "—";
            return;
        }

        string creationJson = SaveManager.GetValue(CleCreation, "");
        if (string.IsNullOrWhiteSpace(creationJson) || !string.IsNullOrEmpty(SaveManager.LastError))
        {
            GD.PushWarning("MainMenu : informations de création absentes ou impossibles à lire.");
            _nomGuilde.Text = SaveManager.CurrentSaveName;
        }
        else
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(creationJson);
                JsonElement root = document.RootElement;
                _nomGuilde.Text = root.TryGetProperty("NomGuilde", out JsonElement nom)
                    && nom.ValueKind == JsonValueKind.String
                    ? nom.GetString() ?? SaveManager.CurrentSaveName
                    : SaveManager.CurrentSaveName;
            }
            catch (JsonException exception)
            {
                GD.PushWarning("MainMenu : données initiales illisibles : " + exception.Message);
                _nomGuilde.Text = SaveManager.CurrentSaveName;
            }
        }

        _nomGuilde.TooltipText = _nomGuilde.Text;
        _etat = EtatGuilde.ChargerOuInitialiser();
        if (_etat == null)
        {
            GD.PushError("MainMenu : " + EtatGuilde.DerniereErreur);
            _membres.Text = _ecus.Text = _savoir.Text = _pouvoir.Text = _prestige.Text = "—";
            return;
        }

        AfficherValeur(_membres, _etat.Membres);
        AfficherValeur(_ecus, _etat.Ecus);
        AfficherValeur(_savoir, _etat.Savoir);
        AfficherValeur(_pouvoir, _etat.Pouvoir);
        AfficherValeur(_prestige, _etat.Prestige);

        // MainMenu.Temps.cs charge et affiche la date sauvegardée.
        // Ne pas afficher la représentation ISO dans le label.
    }

    private static void AfficherValeur(Label label, long valeur)
    {
        label.Text = FormaterValeur(valeur);
        label.TooltipText = valeur.ToString("N0", Francais);
        label.MouseFilter = Control.MouseFilterEnum.Stop;
    }

    /// <summary>
    /// 999 -> 999 ; 1100 -> 1,1k ; 9999 -> 10k ;
    /// 102400 -> 102k. Au-delà, arrondi au millier entier.
    /// </summary>
    public static string FormaterValeur(long valeur)
    {
        if (valeur > -1000 && valeur < 1000)
            return valeur.ToString(Francais);

        bool negatif = valeur < 0;
        double absolue = Math.Abs((double)valeur);
        string texte;

        if (absolue < 10000)
        {
            // Arrondit au dixième de millier, sans dépasser 9,9k.
            long dixiemes = (long)Math.Floor(absolue / 100.0 + 0.5);
            if (dixiemes >= 100)
                texte = "10k";
            else
                texte = (dixiemes / 10.0).ToString("0.0", Francais) + "k";
        }
        else
        {
            long milliers = (long)Math.Floor(absolue / 1000.0 + 0.5);
            texte = milliers.ToString(Francais) + "k";
        }

        return negatif ? "-" + texte : texte;
    }

    private void InitialiserOptions()
    {
        const string racine = "FenetreOption/Marge/ConteneurOptions/";
        _fenetreOptions = GetNode<Window>("FenetreOption");
        _sliderVolumeOptions = GetNode<HSlider>(racine + "HSliderVolume");
        _labelVolumeOptions = GetNode<Label>(racine + "LabelVolume");

        // Même échelle et mêmes paramètres que dans MenuDemarrer.
        _sliderVolumeOptions.MinValue = 0.0;
        _sliderVolumeOptions.MaxValue = 1.0;
        _sliderVolumeOptions.Step = 0.01;
        ActualiserVolumeOptions(MusicManager.GetSavedMasterVolume());

        _sliderVolumeOptions.ValueChanged += valeur =>
        {
            MusicManager.SetMasterVolume((float)valeur);
            _labelVolumeOptions.Text =
                "Volume du son : " + Math.Round(valeur * 100.0) + " %";
        };

        GetNode<Button>(racine + "BoutonFermer").Pressed += FermerOptions;
        _fenetreOptions.CloseRequested += FermerOptions;
        _fenetreOptions.Hide();
    }

    private void ActualiserVolumeOptions(float volume)
    {
        _sliderVolumeOptions.SetValueNoSignal(volume);
        _labelVolumeOptions.Text =
            "Volume du son : " + Math.Round(volume * 100.0) + " %";
    }

    private void OuvrirOptions()
    {
        // FenetreMenuQuitter et FenetreOption sont des fenêtres exclusives :
        // ne pas les laisser ouvertes simultanément.
        ActualiserVolumeOptions(MusicManager.GetSavedMasterVolume());
        _fenetreQuitter.Hide();
        _fenetreOptions.PopupCentered();
    }

    private void FermerOptions()
    {
        _fenetreOptions.Hide();
        if (!_sortieEnCours)
            _fenetreQuitter.PopupCentered();
    }

    private bool _sortieEnCours;

    /// <summary>
    /// Un seul point d'entrée pour les deux boutons de sauvegarde :
    /// ressources, horloge, journal, membres, recrutement, missions
    /// et historique sont enregistrés dans une transaction atomique.
    /// Le temps en session n'est jamais réinitialisé par une sauvegarde.
    /// </summary>
    private bool SauvegarderPartie()
    {
        if (!SaveManager.HasCurrentSave || _etat == null || _horloge == null ||
            _journalPartie == null || _registreMembres == null ||
            _aubergeEtat == null || _etatMissions == null ||
            _historiqueMembres == null || _budget == null ||
            _inventaireGuilde == null || _relationsGuilde == null ||
            _politiqueGuilde == null || _reclamationsPolitique == null)
        {
            AfficherInformation("Impossible de sauvegarder : certaines données de la partie sont indisponibles.");
            return false;
        }

        try
        {
            SaveManager.TracerSauvegarde("Début : préparation des données en mémoire.");
            // Toute écriture historique antérieure qui aurait échappé au
            // registre est visible comme ajustement, jamais inventée comme
            // don, loyer ou recette de mission.
            long ecartBudget = _etat.Ecus - _budget.SoldeReconstitue;
            if (ecartBudget != 0)
            {
                GD.PushWarning("MainMenu : écart comptable non ventilé : " +
                    ecartBudget + " écus.");
                EnregistrerMouvementBudget(RegistreBudget.Ajustements, ecartBudget,
                    "Régularisation d'une variation de trésorerie sans catégorie connue.");
            }
            var donnees = new System.Collections.Generic.Dictionary<string, string>
            {
                [EtatGuilde.CleSauvegarde] = JsonSerializer.Serialize(_etat),
                [HorlogeJeu.CleSauvegarde] = _horloge.ValeurSauvegardee,
                [JournalPartie.CleSauvegarde] = _journalPartie.ValeurSauvegardee,
                [RegistreMembresGuilde.CleSauvegarde] = JsonSerializer.Serialize(_registreMembres),
                [RecrutementAubergeEtat.CleSauvegarde] = _aubergeEtat.Serialiser(),
                [EtatMissions.CleSauvegarde] = _etatMissions.Serialiser(),
                [HistoriqueMembresGuilde.CleSauvegarde] = _historiqueMembres.Serialiser(),
                [RegistreBudget.CleSauvegarde] = _budget.Serialiser(),
                [InventaireGuilde.CleSauvegarde] = _inventaireGuilde.Serialiser(),
                [EtatFilRougeRoseVallee.CleSauvegarde] = (_filRouge ?? new EtatFilRougeRoseVallee()).Serialiser(),
                [EtatRelationsGuilde.CleSauvegarde] = _relationsGuilde.Serialiser(),
                [EtatPolitiqueGuilde.CleSauvegarde] = _politiqueGuilde.Serialiser(),
                [EtatReclamationsPolitique.CleSauvegarde] = _reclamationsPolitique.Serialiser()
            };
            SaveManager.TracerSauvegarde("Toutes les données sont sérialisées.");

            if (!SaveManager.SetValuesAtomically(donnees))
            {
                AfficherInformation("Impossible de sauvegarder la partie : " + SaveManager.LastError);
                return false;
            }

            SaveManager.TracerSauvegarde("Sauvegarde complète effectuée.");
            return true;
        }
        catch (Exception exception)
        {
            GD.PushError("MainMenu : échec de la sauvegarde : " + exception);
            SaveManager.TracerSauvegarde("EXCEPTION : " + exception);
            AfficherInformation("La sauvegarde a échoué : " + exception.Message +
                "\nLa partie reste ouverte. Consulte le fichier user://diagnostic_sauvegarde.txt.");
            return false;
        }
    }

    private void SauvegarderSansQuitter()
    {
        if (_sortieEnCours || !SauvegarderPartie())
            return;

        // On reste sur la partie après confirmation ; ne pas rouvrir
        // automatiquement la fenêtre de sortie à la fermeture du message.
        _fenetreQuitter.Hide();
        AfficherInformation("Partie sauvegardée avec succès.");
    }

    private void Quitter(bool sauvegarder)
    {
        if (_sortieEnCours) return;
        if (sauvegarder && !SauvegarderPartie())
            return;

        _sortieEnCours = true;
        ArreterHorlogeSansJournal();
        _fenetreQuitter.Hide();
        if (ResourceLoader.Exists(SceneDemarrage))
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, SceneDemarrage);
        else
            GetTree().Quit();
    }

    private void InitialiserFenetreInfo()
    {
        _fenetreInfo = GetNode<Window>("FenetreInfo");
        _messageFenetreInfo = GetNode<RichTextLabel>(
            "FenetreInfo/MarginContainer/VBoxContainer/RichTextLabel");
        GetNode<Button>("FenetreInfo/MarginContainer/VBoxContainer/Button").Pressed += FermerFenetreInfo;
        _fenetreInfo.CloseRequested += FermerFenetreInfo;
        _fenetreInfo.Hide();
    }

    /// <summary>
    /// Affiche les informations dans la fenêtre partagée de MainMenu.
    /// Une seule fenêtre exclusive peut être ouverte à la fois : si nécessaire,
    /// celle qui était visible est masquée puis restaurée à la fermeture.
    /// </summary>
    private void AfficherInformation(string message)
    {
        _messageFenetreInfo.Text = message;

        if (!_fenetreInfo.Visible)
        {
            _fenetreAReouvrirApresInfo = null;
            foreach (Window fenetre in new[] { _fenetreQuitter, _fenetreFiche, _choixRecrutement, _fenetreAchatMagasin })
            {
                if (fenetre == null || !fenetre.Visible)
                    continue;

                _fenetreAReouvrirApresInfo = fenetre;
                fenetre.Hide();
                break;
            }
        }

        _fenetreInfo.PopupCentered();
    }

    private void FermerFenetreInfo()
    {
        _fenetreInfo.Hide();
        Window precedente = _fenetreAReouvrirApresInfo;
        _fenetreAReouvrirApresInfo = null;
        if (precedente != null && GodotObject.IsInstanceValid(precedente))
            precedente.PopupCentered();
    }
}
