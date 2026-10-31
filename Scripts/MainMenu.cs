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
        InitialiserFenetreInfo();

        GetNode<Button>("MargeChoixMenu/BoutonsMenu/BoutonQuitter").Pressed += () => _fenetreQuitter.PopupCentered();
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonSaveEtQuitter").Pressed += () => Quitter(true);
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonQuitterSansSave").Pressed += () => Quitter(false);
        _fenetreQuitter.CloseRequested += () => _fenetreQuitter.Hide();
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonOption").Pressed += OuvrirOptions;

        // Les pages Tableau des missions, Membres, Magasins, Recrutement,
        // Budget et Événements seront reliées quand leur contenu existera.
        // MainBouton est également réservé à une future navigation.
        ChargerAffichage();
        InitialiserNavigationEtMembres();
        InitialiserTempsEtJournal();
        InitialiserRecrutementAuberge();
        InitialiserMissions();
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

    private void OuvrirOptions()
    {
        // Pas encore de fenêtre d'options dans MainMenu :
        // conserve la valeur enregistrée depuis MenuDemarrer et informe le joueur.
        AfficherInformation("Les options supplémentaires seront disponibles prochainement.\nLe volume reste enregistré depuis le menu de démarrage.");
    }

    private bool _sortieEnCours;

    private void Quitter(bool sauvegarder)
    {
        if (_sortieEnCours) return;
        ArreterHorlogeSansJournal();

        if (sauvegarder)
        {
            if (_etat == null || _horloge == null || _journalPartie == null ||
                _registreMembres == null || _aubergeEtat == null || _etatMissions == null)
            {
                AfficherInformation("Impossible de sauvegarder : certaines données de la partie sont indisponibles.");
                return;
            }

            try
            {
                SaveManager.TracerSauvegarde("Début : préparation des données en mémoire.");
                var donnees = new System.Collections.Generic.Dictionary<string, string>();
                donnees[EtatGuilde.CleSauvegarde] = JsonSerializer.Serialize(_etat);
                SaveManager.TracerSauvegarde("Ressources sérialisées.");
                donnees[HorlogeJeu.CleSauvegarde] = _horloge.ValeurSauvegardee;
                donnees[JournalPartie.CleSauvegarde] = _journalPartie.ValeurSauvegardee;
                SaveManager.TracerSauvegarde("Horloge et journal sérialisés.");
                donnees[RegistreMembresGuilde.CleSauvegarde] = JsonSerializer.Serialize(_registreMembres);
                SaveManager.TracerSauvegarde("Membres sérialisés.");
                donnees[RecrutementAubergeEtat.CleSauvegarde] = _aubergeEtat.Serialiser();
                donnees[EtatMissions.CleSauvegarde] = _etatMissions.Serialiser();
                SaveManager.TracerSauvegarde("Recrutement et missions sérialisés.");

                if (!SaveManager.SetValuesAtomically(donnees))
                {
                    AfficherInformation("Impossible de sauvegarder la partie : " + SaveManager.LastError);
                    return;
                }
                SaveManager.TracerSauvegarde("Toutes les données ont été sauvegardées. Retour au menu.");
            }
            catch (Exception exception)
            {
                GD.PushError("MainMenu : échec de la sauvegarde : " + exception);
                SaveManager.TracerSauvegarde("EXCEPTION : " + exception);
                AfficherInformation("La sauvegarde a échoué : " + exception.Message +
                    "\nLa partie reste ouverte. Consulte le fichier user://diagnostic_sauvegarde.txt.");
                return;
            }
        }

        _sortieEnCours = true;
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
            foreach (Window fenetre in new[] { _fenetreQuitter, _fenetreFiche, _choixRecrutement })
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
