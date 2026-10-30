using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Contrôleur des boutons et fenêtres déjà présents dans MenuDemarrer.tscn.
/// Les scènes Godot restent entièrement gérées dans l'éditeur.
/// </summary>
public partial class MenuDemarrer : Control
{
    private const string SceneCreationPersonnage = "res://Scenes/CreationPerso.tscn";
    private const string SceneJeu = "res://Scenes/MainMenu.tscn";

    private Button _boutonContinuer;
    private Button _boutonNouvellePartie;
    private Button _boutonChargerPartie;
    private Button _boutonOptions;
    private Button _boutonQuitter;

    private Window _fenetreOptions;
    private HSlider _sliderVolume;
    private Window _fenetreCharger;
    private ItemList _listeParties;
    private Button _boutonSelectionner;
    private Button _boutonSupprimer;
    private ConfirmationDialog _confirmationSuppression;
    private AcceptDialog _fenetreInformation;

    private readonly List<SaveManager.SaveInfo> _sauvegardesAffichees = new();
    private string _idASupprimer = string.Empty;
    private bool _reouvrirChargementApresInformation;

    public override void _Ready()
    {
        _boutonContinuer = GetNode<Button>("Marge/Boutons/BoutonContinuer");
        _boutonNouvellePartie = GetNode<Button>("Marge/Boutons/BoutonNouvellePartie");
        _boutonChargerPartie = GetNode<Button>("Marge/Boutons/BoutonChargerPartie");
        _boutonOptions = GetNode<Button>("Marge/Boutons/BoutonOption");
        _boutonQuitter = GetNode<Button>("Marge/Boutons/BoutonQuitter");

        _fenetreOptions = GetNode<Window>("FenetreOption");
        _sliderVolume = GetNode<HSlider>("FenetreOption/Marge/ConteneurOptions/HSliderVolume");

        _fenetreCharger = GetNode<Window>("FenetreChargerPartie");
        _listeParties = GetNode<ItemList>("FenetreChargerPartie/Marge/Conteneur/ItemListParties");
        _boutonSelectionner = GetNode<Button>("FenetreChargerPartie/Marge/Conteneur/Boutons/BoutonSelectionner");
        _boutonSupprimer = GetNode<Button>("FenetreChargerPartie/Marge/Conteneur/Boutons/BoutonSupprimer");

        _confirmationSuppression = GetNode<ConfirmationDialog>("ConfirmationSuppression");
        _fenetreInformation = GetNode<AcceptDialog>("FenetreInformation");

        MusicManager.Initialize(GetTree());

        _sliderVolume.MinValue = 0.0;
        _sliderVolume.MaxValue = 1.0;
        _sliderVolume.Step = 0.01;
        _sliderVolume.Value = MusicManager.GetSavedMasterVolume();

        _boutonContinuer.Pressed += ContinuerPartie;
        _boutonNouvellePartie.Pressed += NouvellePartie;
        _boutonChargerPartie.Pressed += OuvrirChargement;
        _boutonOptions.Pressed += OuvrirOptions;
        _boutonQuitter.Pressed += () => GetTree().Quit();

        _sliderVolume.ValueChanged += valeur => MusicManager.SetMasterVolume((float)valeur);

        GetNode<Button>("FenetreOption/Marge/ConteneurOptions/BoutonFermer").Pressed += FermerOptions;
        _fenetreOptions.CloseRequested += FermerOptions;

        _listeParties.ItemSelected += _ => ActualiserBoutonsChargement();
        _listeParties.ItemActivated += _ => SelectionnerPartie();
        _boutonSelectionner.Pressed += SelectionnerPartie;
        _boutonSupprimer.Pressed += DemanderSuppression;
        GetNode<Button>("FenetreChargerPartie/Marge/Conteneur/Boutons/BoutonFermer").Pressed += FermerChargement;
        _fenetreCharger.CloseRequested += FermerChargement;

        _confirmationSuppression.Confirmed += ConfirmerSuppression;
        _confirmationSuppression.Canceled += AnnulerSuppression;
        _confirmationSuppression.CloseRequested += AnnulerSuppression;

        _fenetreInformation.Confirmed += InformationFermee;
        _fenetreInformation.Canceled += InformationFermee;
        _fenetreInformation.CloseRequested += InformationFermee;

        ActualiserDisponibiliteContinuer();
    }

    private void NouvellePartie()
    {
        // Ne pas créer de sauvegarde avant la validation de CreationPerso.
        // La création de la base sera effectuée à l'étape « Créer ma guilde ».
        if (!ResourceLoader.Exists(SceneCreationPersonnage))
        {
            AfficherInformation("La scène de création de personnage est introuvable :\n" + SceneCreationPersonnage);
            return;
        }

        // Ne pas retirer le menu pendant que son bouton traite encore le clic.
        SaveManager.ResetCurrentSave();
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, SceneCreationPersonnage);
    }

    private void ContinuerPartie()
    {
        SaveManager.SaveInfo derniere = SaveManager.GetLatestSave();
        if (derniere == null)
        {
            AfficherInformation("Aucune sauvegarde disponible. Utilisez « Nouvelle Partie » pour commencer.");
            ActualiserDisponibiliteContinuer();
            return;
        }

        OuvrirSauvegarde(derniere.Id, false);
    }

    private void OuvrirChargement()
    {
        _reouvrirChargementApresInformation = false;
        ActualiserListeSauvegardes();
        _fenetreCharger.PopupCentered();
    }

    private void FermerChargement()
    {
        _fenetreCharger.Hide();
        _reouvrirChargementApresInformation = false;
    }

    private void ActualiserListeSauvegardes()
    {
        _listeParties.Clear();
        _sauvegardesAffichees.Clear();
        _sauvegardesAffichees.AddRange(SaveManager.ListSaves());

        foreach (SaveManager.SaveInfo sauvegarde in _sauvegardesAffichees)
        {
            string date = FormaterDate(sauvegarde.LastPlayedAtUtc);
            _listeParties.AddItem(sauvegarde.Name + "  —  " + date);
            _listeParties.SetItemMetadata(_listeParties.ItemCount - 1, sauvegarde.Id);
        }

        if (_listeParties.ItemCount > 0)
            _listeParties.Select(0);

        ActualiserBoutonsChargement();
        ActualiserDisponibiliteContinuer();
    }

    private void ActualiserBoutonsChargement()
    {
        bool selectionValide = _listeParties.GetSelectedItems().Length > 0;
        _boutonSelectionner.Disabled = !selectionValide;
        _boutonSupprimer.Disabled = !selectionValide;
    }

    private void SelectionnerPartie()
    {
        int[] selection = _listeParties.GetSelectedItems();
        if (selection.Length == 0)
        {
            AfficherInformation("Sélectionnez une sauvegarde dans la liste.");
            return;
        }

        string id = _listeParties.GetItemMetadata(selection[0]).AsString();
        _fenetreCharger.Hide();
        OuvrirSauvegarde(id, true);
    }

    private void OuvrirSauvegarde(string id, bool depuisChargement)
    {
        if (!ResourceLoader.Exists(SceneJeu))
        {
            AfficherInformation("La scène principale est introuvable :\n" + SceneJeu, depuisChargement);
            return;
        }

        if (!SaveManager.OpenSave(id))
        {
            AfficherInformation("Impossible d'ouvrir la sauvegarde.\n" + SaveManager.LastError, depuisChargement);
            ActualiserDisponibiliteContinuer();
            return;
        }

        // La sauvegarde active est prête. Le changement de scène est différé
        // afin de terminer l'événement souris (et la fermeture éventuelle de
        // FenetreChargerPartie) avant de détacher l'ancien Viewport.
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, SceneJeu);
    }

    private void DemanderSuppression()
    {
        int[] selection = _listeParties.GetSelectedItems();
        if (selection.Length == 0)
        {
            AfficherInformation("Sélectionnez d'abord la sauvegarde à supprimer.");
            return;
        }

        string id = _listeParties.GetItemMetadata(selection[0]).AsString();
        SaveManager.SaveInfo sauvegarde = _sauvegardesAffichees.Find(element => element.Id == id);
        if (sauvegarde == null)
        {
            ActualiserListeSauvegardes();
            return;
        }

        _idASupprimer = id;
        _confirmationSuppression.DialogText =
            "Voulez-vous vraiment supprimer la sauvegarde « " + sauvegarde.Name + " » ?\nCette action est irréversible.";

        // Ces fenêtres sont des enfants exclusifs de la même fenêtre racine.
        // On ferme d'abord la liste, puis on ouvre la confirmation en différé.
        _fenetreCharger.Hide();
        _confirmationSuppression.CallDeferred("popup_centered");
    }

    private void AnnulerSuppression()
    {
        // Évite une seconde réouverture si plusieurs signaux de fermeture arrivent.
        if (string.IsNullOrEmpty(_idASupprimer))
            return;

        _idASupprimer = string.Empty;
        _confirmationSuppression.Hide();
        _fenetreCharger.CallDeferred("popup_centered");
    }

    private void ConfirmerSuppression()
    {
        if (string.IsNullOrEmpty(_idASupprimer))
            return;

        string id = _idASupprimer;
        _idASupprimer = string.Empty;
        _confirmationSuppression.Hide();

        if (!SaveManager.DeleteSave(id))
        {
            AfficherInformation("La suppression a échoué.\n" + SaveManager.LastError, true);
            return;
        }

        ActualiserListeSauvegardes();
        _fenetreCharger.CallDeferred("popup_centered");
    }

    private void OuvrirOptions()
    {
        _sliderVolume.Value = MusicManager.GetSavedMasterVolume();
        _fenetreOptions.PopupCentered();
    }

    private void FermerOptions()
    {
        _fenetreOptions.Hide();
    }

    private void ActualiserDisponibiliteContinuer()
    {
        _boutonContinuer.Disabled = SaveManager.GetLatestSave() == null;
    }

    private void AfficherInformation(string message, bool reouvrirChargement = false)
    {
        if (_fenetreCharger.Visible)
        {
            _fenetreCharger.Hide();
            reouvrirChargement = true;
        }

        if (_confirmationSuppression.Visible)
            _confirmationSuppression.Hide();

        _reouvrirChargementApresInformation = reouvrirChargement;
        _fenetreInformation.DialogText = message;
        _fenetreInformation.CallDeferred("popup_centered");
    }

    private void InformationFermee()
    {
        _fenetreInformation.Hide();
        if (!_reouvrirChargementApresInformation)
            return;

        _reouvrirChargementApresInformation = false;
        _fenetreCharger.CallDeferred("popup_centered");
    }

    private static string FormaterDate(string valeurUtc)
    {
        if (DateTime.TryParse(
            valeurUtc,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTime dateUtc))
        {
            return dateUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("fr-FR"));
        }

        return valeurUtc;
    }
}
