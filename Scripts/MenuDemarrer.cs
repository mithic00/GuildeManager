using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Branche les contrôles de la scène existante du menu de démarrage.
/// La scène de jeu n'étant pas encore présente dans le dépôt, l'ouverture d'une partie
/// sélectionne la sauvegarde et indique clairement que le jeu pourra être lancé ensuite.
/// </summary>
public partial class MenuDemarrer : Control
{
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
        _boutonQuitter.Pressed += QuitterJeu;

        _sliderVolume.ValueChanged += VolumeModifie;
        GetNode<Button>("FenetreOption/Marge/ConteneurOptions/BoutonFermer").Pressed += () => _fenetreOptions.Hide();

        _boutonSelectionner.Pressed += SelectionnerPartie;
        _boutonSupprimer.Pressed += DemanderSuppression;
        GetNode<Button>("FenetreChargerPartie/Marge/Conteneur/Boutons/BoutonFermer").Pressed += () => _fenetreCharger.Hide();

        _confirmationSuppression.Confirmed += ConfirmerSuppression;
        _confirmationSuppression.Canceled += () => _idASupprimer = string.Empty;

        _boutonContinuer.Disabled = SaveManager.GetLatestSave() == null;
    }

    private void NouvellePartie()
    {
        SaveManager.SaveInfo save = SaveManager.CreateSave();
        if (save == null)
        {
            AfficherInformation("Impossible de créer la sauvegarde.\n" + SaveManager.LastError);
            return;
        }

        AfficherInformation(
            "La partie « " + save.Name + " » a été créée.\n\n" +
            "Le système de sauvegarde est prêt. La scène de jeu n'est pas encore intégrée au projet.");
        ActualiserDisponibiliteContinuer();
    }

    private void ContinuerPartie()
    {
        SaveManager.SaveInfo latest = SaveManager.GetLatestSave();
        if (latest == null)
        {
            AfficherInformation("Aucune sauvegarde n'a été trouvée. Utilisez « Nouvelle Partie » pour commencer.");
            ActualiserDisponibiliteContinuer();
            return;
        }

        OuvrirSauvegarde(latest.Id);
    }

    private void OuvrirChargement()
    {
        ActualiserListeSauvegardes();
        _fenetreCharger.PopupCentered();
    }

    private void ActualiserListeSauvegardes()
    {
        _listeParties.Clear();
        _sauvegardesAffichees.Clear();
        _sauvegardesAffichees.AddRange(SaveManager.ListSaves());

        foreach (SaveManager.SaveInfo save in _sauvegardesAffichees)
        {
            string date = FormaterDate(save.LastPlayedAtUtc);
            _listeParties.AddItem(save.Name + "  —  " + date);
            _listeParties.SetItemMetadata(_listeParties.ItemCount - 1, save.Id);
        }

        if (_listeParties.ItemCount > 0)
            _listeParties.Select(0);

        _boutonSelectionner.Disabled = _listeParties.ItemCount == 0;
        _boutonSupprimer.Disabled = _listeParties.ItemCount == 0;
        ActualiserDisponibiliteContinuer();
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
        OuvrirSauvegarde(id);
    }

    private void OuvrirSauvegarde(string id)
    {
        if (!SaveManager.OpenSave(id))
        {
            AfficherInformation("Impossible d'ouvrir cette sauvegarde.\n" + SaveManager.LastError);
            ActualiserDisponibiliteContinuer();
            return;
        }

        AfficherInformation(
            "La sauvegarde « " + SaveManager.CurrentSaveName + " » est sélectionnée.\n\n" +
            "La scène de jeu n'est pas encore présente dans le projet ; aucune progression n'a été chargée dans une interface de jeu.");
        ActualiserDisponibiliteContinuer();
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
        SaveManager.SaveInfo save = _sauvegardesAffichees.Find(item => item.Id == id);
        if (save == null)
        {
            ActualiserListeSauvegardes();
            return;
        }

        _idASupprimer = id;
        _confirmationSuppression.DialogText =
            "Voulez-vous vraiment supprimer la sauvegarde « " + save.Name + " » ?\nCette action est irréversible.";
        _confirmationSuppression.PopupCentered();
    }

    private void ConfirmerSuppression()
    {
        if (string.IsNullOrEmpty(_idASupprimer))
            return;

        string id = _idASupprimer;
        _idASupprimer = string.Empty;

        if (!SaveManager.DeleteSave(id))
        {
            AfficherInformation("La suppression a échoué.\n" + SaveManager.LastError);
            return;
        }

        ActualiserListeSauvegardes();
    }

    private void OuvrirOptions()
    {
        _sliderVolume.Value = MusicManager.GetSavedMasterVolume();
        _fenetreOptions.PopupCentered();
    }

    private void VolumeModifie(double valeur)
    {
        MusicManager.SetMasterVolume((float)valeur);
    }

    private void QuitterJeu()
    {
        GetTree().Quit();
    }

    private void ActualiserDisponibiliteContinuer()
    {
        _boutonContinuer.Disabled = SaveManager.GetLatestSave() == null;
    }

    private void AfficherInformation(string message)
    {
        _fenetreInformation.DialogText = message;
        _fenetreInformation.PopupCentered();
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
