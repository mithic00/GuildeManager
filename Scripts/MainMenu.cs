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

        GetNode<Button>("MargeChoixMenu/BoutonsMenu/BoutonQuitter").Pressed += () => _fenetreQuitter.PopupCentered();
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonSaveEtQuitter").Pressed += () => Quitter(true);
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonQuitterSansSave").Pressed += () => Quitter(false);
        _fenetreQuitter.CloseRequested += () => _fenetreQuitter.Hide();
        GetNode<Button>("FenetreMenuQuitter/Marge/Layout/BoutonOption").Pressed += OuvrirOptions;

        // Les pages Tableau des missions, Membres, Magasins, Recrutement,
        // Budget et Événements seront reliées quand leur contenu existera.
        // MainBouton est également réservé à une future navigation.
        ChargerAffichage();
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

        string date = SaveManager.GetValue(CleDate, "");
        _date.Text = string.IsNullOrWhiteSpace(date) ? "An 600 - 1er Janvier - 08h00" : date;
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

    private void Quitter(bool sauvegarder)
    {
        if (sauvegarder)
        {
            if (_etat == null || !_etat.Sauvegarder())
            {
                AfficherInformation("Impossible de sauvegarder la guilde : " + EtatGuilde.DerniereErreur);
                return;
            }
        }

        _fenetreQuitter.Hide();
        if (ResourceLoader.Exists(SceneDemarrage))
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, SceneDemarrage);
        else
            GetTree().Quit();
    }

    private void AfficherInformation(string message)
    {
        AcceptDialog dialogue = new()
        {
            Title = "Information",
            DialogText = message
        };
        _fenetreQuitter.AddChild(dialogue);
        dialogue.Confirmed += () => dialogue.QueueFree();
        dialogue.Canceled += () => dialogue.QueueFree();
        dialogue.PopupCentered();
    }
}
