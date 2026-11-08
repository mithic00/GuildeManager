using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Branche uniquement l'interface de politique sur un état mémorisé/sauvegardé.
/// Les effets en jeu (répartition des écus, autorisation de missions, etc.)
/// seront implémentés séparément lorsque leurs règles seront définies.
/// </summary>
public partial class MainMenu
{
    private EtatPolitiqueGuilde _politiqueGuilde;
    // Les contrôles modifient un brouillon : rien n'est appliqué sans confirmation.
    private EtatPolitiqueGuilde _politiqueBrouillon;
    private Window _fenetrePolitique;
    private CheckButton _politiqueDefaut;
    private HSlider _sliderPartGuilde, _sliderEcartMissions, _sliderContrainteMissions;
    private Label _labelPartGuilde, _labelEcartMissions, _labelContrainteMissions;
    private Label _labelNiveauRequisPolitique;
    private TextEdit _champNiveauRequisPolitique;
    private ItemList _listeMissionsInterdites, _listeMissionsPreferees;
    private ItemList _listeClientsPreferes, _listeClientsInterdits;
    private bool _rafraichissementPolitique;

    private void InitialiserPolitiqueGuilde()
    {
        _politiqueGuilde = EtatPolitiqueGuilde.Charger();
        _politiqueBrouillon = CopierPolitiqueGuilde(_politiqueGuilde);
        _fenetrePolitique = GetNode<Window>("FenetrePolitique");

        const string baseGauche =
            "FenetrePolitique/Marge/LayoutPrincipal/ListeDesFonctions/Gauche/";
        const string baseDroite =
            "FenetrePolitique/Marge/LayoutPrincipal/ListeDesFonctions/Droite/";

        _politiqueDefaut = GetNode<CheckButton>(
            baseGauche + "FonctionDefaut/CheckButtonPolitiqueDefaut");
        _sliderPartGuilde = GetNode<HSlider>(
            baseGauche + "FonctionGainEcuMission/Layout/HSliderModifPourcentageGainEcuMission");
        _labelPartGuilde = GetNode<Label>(
            baseGauche + "FonctionGainEcuMission/Layout/LabelPourcentage");
        _sliderEcartMissions = GetNode<HSlider>(
            baseGauche + "FonctionAutoriserMission/Layout/HSliderNiveauEcart");
        _labelEcartMissions = GetNode<Label>(
            baseGauche + "FonctionAutoriserMission/Layout/LabelNiveauEcart");
        _sliderContrainteMissions = GetNode<HSlider>(
            baseGauche + "FonctionImposerMission/Layout/HSliderNiveauContrainteMission");
        _labelContrainteMissions = GetNode<Label>(
            baseGauche + "FonctionImposerMission/Layout/LabelNiveauContrainteMission");
        _champNiveauRequisPolitique = GetNode<TextEdit>(
            baseGauche + "FonctionNiveauRequisGuilde/Layout/TextEdit");
        _labelNiveauRequisPolitique = GetNode<Label>(
            baseGauche + "FonctionNiveauRequisGuilde/Layout/LabelNiveauRequis");
        _listeMissionsInterdites = GetNode<ItemList>(
            baseGauche + "FonctionMissionInterdites/ListeTypeMission");
        _listeMissionsPreferees = GetNode<ItemList>(
            baseDroite + "FonctionMissionPrefere/ListeTypeMission");
        _listeClientsPreferes = GetNode<ItemList>(
            baseDroite + "FonctionClientPrefere/ListeClients");
        _listeClientsInterdits = GetNode<ItemList>(
            baseDroite + "FonctionMissionInterdites3/ListeClients");

        _sliderPartGuilde.MinValue = 0;
        _sliderPartGuilde.MaxValue = 50;
        _sliderPartGuilde.Step = 1;
        _sliderEcartMissions.MinValue = 0;
        _sliderEcartMissions.MaxValue = 11;
        _sliderEcartMissions.Step = 1;
        _sliderContrainteMissions.MinValue = 0;
        _sliderContrainteMissions.MaxValue = 3;
        _sliderContrainteMissions.Step = 1;

        // Les ItemList en mode Toggle rendent la sélection multiple
        // utilisable sans touche Ctrl. Un seul client préféré est possible.
        _listeMissionsInterdites.SelectMode = ItemList.SelectModeEnum.Toggle;
        _listeMissionsPreferees.SelectMode = ItemList.SelectModeEnum.Toggle;
        _listeClientsInterdits.SelectMode = ItemList.SelectModeEnum.Toggle;
        _listeClientsPreferes.SelectMode = ItemList.SelectModeEnum.Single;
        _listeClientsPreferes.AllowReselect = true;

        _politiqueDefaut.Toggled += active =>
        {
            if (_rafraichissementPolitique || _politiqueBrouillon == null)
                return;
            if (active)
                _politiqueBrouillon.RetablirDefauts();
            else
                _politiqueBrouillon.PolitiqueParDefaut = false;
            RafraichirInterfacePolitique();
        };

        _sliderPartGuilde.ValueChanged += valeur =>
        {
            if (_rafraichissementPolitique || _politiqueBrouillon == null) return;
            _politiqueBrouillon.PourcentageGuildeMissions = (int)Math.Round(valeur);
            SignalerPolitiquePersonnalisee();
            ActualiserLibellesPolitique();
        };
        _sliderEcartMissions.ValueChanged += valeur =>
        {
            if (_rafraichissementPolitique || _politiqueBrouillon == null) return;
            _politiqueBrouillon.EcartNiveauMission = (int)Math.Round(valeur);
            SignalerPolitiquePersonnalisee();
            ActualiserLibellesPolitique();
        };
        _sliderContrainteMissions.ValueChanged += valeur =>
        {
            if (_rafraichissementPolitique || _politiqueBrouillon == null) return;
            _politiqueBrouillon.ContrainteMission = (int)Math.Round(valeur);
            SignalerPolitiquePersonnalisee();
            ActualiserLibellesPolitique();
        };

        _champNiveauRequisPolitique.TextChanged +=
            () => ValiderNiveauRequisPolitique(normaliserTexte: false);
        _champNiveauRequisPolitique.FocusExited +=
            () => ValiderNiveauRequisPolitique(normaliserTexte: true);

        _listeMissionsInterdites.MultiSelected += (index, selectionne) =>
            ModifierSelectionMissionsPolitique(interdites: true);
        _listeMissionsPreferees.MultiSelected += (index, selectionne) =>
            ModifierSelectionMissionsPolitique(interdites: false);
        _listeClientsInterdits.MultiSelected += (index, selectionne) =>
            ModifierSelectionClientsInterditsPolitique();
        _listeClientsPreferes.ItemSelected += index =>
            ModifierClientPreferePolitique((int)index);

        const string boutons = "FenetrePolitique/Marge/LayoutPrincipal/Boutons/";
        GetNode<Button>(boutons + "ButtonConfirmer").Pressed += ConfirmerPolitiqueGuilde;
        GetNode<Button>(boutons + "ButtonAnnuler").Pressed += AnnulerPolitiqueGuilde;
        GetNode<Button>(boutons + "ButtonFermer").Pressed += AnnulerPolitiqueGuilde;
        _fenetrePolitique.CloseRequested += AnnulerPolitiqueGuilde;
        RafraichirInterfacePolitique();
        _fenetrePolitique.Hide();
    }

    private static EtatPolitiqueGuilde CopierPolitiqueGuilde(EtatPolitiqueGuilde source) =>
        JsonSerializer.Deserialize<EtatPolitiqueGuilde>(source.Serialiser())
        ?? new EtatPolitiqueGuilde();

    private void OuvrirPolitiqueGuilde()
    {
        if (_tempsEnCours || _fenetrePolitique == null || _politiqueGuilde == null)
            return;
        _politiqueBrouillon = CopierPolitiqueGuilde(_politiqueGuilde);
        RafraichirInterfacePolitique();
        _fenetrePolitique.PopupCentered();
    }

    private void ConfirmerPolitiqueGuilde()
    {
        if (_fenetrePolitique == null || _politiqueBrouillon == null ||
            _politiqueGuilde == null) return;

        ValiderNiveauRequisPolitique(normaliserTexte: true);
        _politiqueBrouillon.Normaliser();
        EtatPolitiqueGuilde nouvellePolitique = CopierPolitiqueGuilde(_politiqueBrouillon);
        bool modifiee = _politiqueGuilde.Serialiser() != nouvellePolitique.Serialiser();
        int ancienPrelevement = _politiqueGuilde.PourcentageGuildeMissions;
        int ancienEcart = _politiqueGuilde.EcartNiveauMission;
        int anciennePression = _politiqueGuilde.ContrainteMission;
        _politiqueGuilde = nouvellePolitique;
        _fenetrePolitique.Hide();
        if (modifiee)
            AjouterEntreeJournal("La politique de la guilde a été modifiée.", "politique");

        // Première politique active : seul le prélèvement des récompenses
        // peut provoquer une protestation. Tous les autres réglages restent
        // purement déclaratifs en attendant leurs propres règles.
        if (nouvellePolitique.PourcentageGuildeMissions > ancienPrelevement)
            EvaluerProtestationsPourcentageMissions(ancienPrelevement,
                nouvellePolitique.PourcentageGuildeMissions);
        else if (nouvellePolitique.PourcentageGuildeMissions < ancienPrelevement)
            ActualiserReclamationsPrelevementApresBaisse(
                nouvellePolitique.PourcentageGuildeMissions);
        if (nouvellePolitique.EcartNiveauMission != ancienEcart)
            EvaluerProtestationsEcartMissions(ancienEcart,
                nouvellePolitique.EcartNiveauMission);
        if (nouvellePolitique.ContrainteMission != anciennePression)
        {
            // La série de trois échecs appartient uniquement à une période
            // durant laquelle la politique Résultat attendu est active.
            if (anciennePression == 3 || nouvellePolitique.ContrainteMission == 3)
                ReinitialiserEchecsConsecutifsSousPression();
            EvaluerProtestationsContrainteMissions(anciennePression,
                nouvellePolitique.ContrainteMission);
        }
    }

    private void AnnulerPolitiqueGuilde()
    {
        if (_fenetrePolitique == null || _politiqueGuilde == null) return;
        _politiqueBrouillon = CopierPolitiqueGuilde(_politiqueGuilde);
        RafraichirInterfacePolitique();
        _fenetrePolitique.Hide();
    }

    private void SignalerPolitiquePersonnalisee()
    {
        if (_politiqueBrouillon == null) return;
        _politiqueBrouillon.PolitiqueParDefaut = false;
        _politiqueDefaut.SetPressedNoSignal(false);
    }

    private static string TexteEcartMissionPolitique(int valeur) =>
        valeur switch
        {
            0 => "Niveau équivalent",
            11 => "Aucune contrainte",
            1 => "1 niveau d'écart",
            _ => valeur + " niveaux d'écart"
        };

    private static string TexteContrainteMissionPolitique(int valeur) =>
        valeur switch
        {
            0 => "Libre",
            1 => "Légère pression",
            2 => "Grosse pression",
            3 => "Résultat attendu",
            _ => "Libre"
        };

    private void ActualiserLibellesPolitique()
    {
        if (_politiqueBrouillon == null) return;
        _labelPartGuilde.Text = _politiqueBrouillon.PourcentageGuildeMissions + " %";
        _labelEcartMissions.Text =
            TexteEcartMissionPolitique(_politiqueBrouillon.EcartNiveauMission);
        _labelContrainteMissions.Text =
            TexteContrainteMissionPolitique(_politiqueBrouillon.ContrainteMission);
        _labelNiveauRequisPolitique.Text =
            _politiqueBrouillon.NiveauMagiqueRequis == 0
                ? "Aucun" : "Niveau " + _politiqueBrouillon.NiveauMagiqueRequis;
    }

    private void ValiderNiveauRequisPolitique(bool normaliserTexte)
    {
        if (_rafraichissementPolitique || _politiqueBrouillon == null ||
            _champNiveauRequisPolitique == null)
            return;

        string texte = _champNiveauRequisPolitique.Text.Trim();
        bool estValide = int.TryParse(texte, NumberStyles.None,
            CultureInfo.InvariantCulture, out int valeur);
        if (!estValide)
        {
            if (normaliserTexte)
                _champNiveauRequisPolitique.Text =
                    _politiqueBrouillon.NiveauMagiqueRequis.ToString(CultureInfo.InvariantCulture);
            else
                _labelNiveauRequisPolitique.Text = "Entrez un niveau entier positif";
            return;
        }

        if (valeur != _politiqueBrouillon.NiveauMagiqueRequis)
        {
            _politiqueBrouillon.NiveauMagiqueRequis = valeur;
            SignalerPolitiquePersonnalisee();
        }
        if (normaliserTexte)
            _champNiveauRequisPolitique.Text = valeur.ToString(CultureInfo.InvariantCulture);
        ActualiserLibellesPolitique();
    }

    private static List<string> ValeursSelectionneesPolitique(ItemList liste) =>
        liste.GetSelectedItems()
            .Select(index => liste.GetItemText(index))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static void AppliquerSelectionListePolitique(
        ItemList liste, IEnumerable<string> selections, bool multiple)
    {
        HashSet<string> valeurs = new(selections ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        liste.DeselectAll();
        for (int i = 0; i < liste.ItemCount; i++)
        {
            if (valeurs.Contains(liste.GetItemText(i)))
                liste.Select(i, !multiple);
        }
    }

    private void ModifierSelectionMissionsPolitique(bool interdites)
    {
        if (_rafraichissementPolitique || _politiqueBrouillon == null) return;
        List<string> choix = ValeursSelectionneesPolitique(
            interdites ? _listeMissionsInterdites : _listeMissionsPreferees);
        if (interdites)
        {
            _politiqueBrouillon.MissionsInterdites = choix;
            _politiqueBrouillon.MissionsPreferees.RemoveAll(x => choix.Contains(x));
        }
        else
        {
            _politiqueBrouillon.MissionsPreferees = choix;
            _politiqueBrouillon.MissionsInterdites.RemoveAll(x => choix.Contains(x));
        }

        SignalerPolitiquePersonnalisee();
        RafraichirInterfacePolitique();
    }

    private void ModifierSelectionClientsInterditsPolitique()
    {
        if (_rafraichissementPolitique || _politiqueBrouillon == null) return;
        List<string> choix = ValeursSelectionneesPolitique(_listeClientsInterdits);
        _politiqueBrouillon.ClientsInterdits = choix;
        if (choix.Contains(_politiqueBrouillon.ClientPrefere))
            _politiqueBrouillon.ClientPrefere = "";

        SignalerPolitiquePersonnalisee();
        RafraichirInterfacePolitique();
    }

    private void ModifierClientPreferePolitique(int index)
    {
        if (_rafraichissementPolitique || _politiqueBrouillon == null ||
            index < 0 || index >= _listeClientsPreferes.ItemCount)
            return;

        string choix = _listeClientsPreferes.GetItemText(index);
        // Recliquer sur un client préféré l'annule (sinon une liste en
        // sélection unique ne permettrait jamais de revenir à « aucun »).
        _politiqueBrouillon.ClientPrefere =
            _politiqueBrouillon.ClientPrefere == choix ? "" : choix;
        _politiqueBrouillon.ClientsInterdits.Remove(choix);
        SignalerPolitiquePersonnalisee();
        RafraichirInterfacePolitique();
    }

    private void RafraichirInterfacePolitique()
    {
        if (_politiqueBrouillon == null || _fenetrePolitique == null) return;

        _rafraichissementPolitique = true;
        try
        {
            _politiqueDefaut.SetPressedNoSignal(_politiqueBrouillon.PolitiqueParDefaut);
            _sliderPartGuilde.SetValueNoSignal(_politiqueBrouillon.PourcentageGuildeMissions);
            _sliderEcartMissions.SetValueNoSignal(_politiqueBrouillon.EcartNiveauMission);
            _sliderContrainteMissions.SetValueNoSignal(_politiqueBrouillon.ContrainteMission);
            _champNiveauRequisPolitique.Text =
                _politiqueBrouillon.NiveauMagiqueRequis.ToString(CultureInfo.InvariantCulture);

            AppliquerSelectionListePolitique(_listeMissionsInterdites,
                _politiqueBrouillon.MissionsInterdites, multiple: true);
            AppliquerSelectionListePolitique(_listeMissionsPreferees,
                _politiqueBrouillon.MissionsPreferees, multiple: true);
            AppliquerSelectionListePolitique(_listeClientsInterdits,
                _politiqueBrouillon.ClientsInterdits, multiple: true);
            AppliquerSelectionListePolitique(_listeClientsPreferes,
                string.IsNullOrEmpty(_politiqueBrouillon.ClientPrefere)
                    ? Array.Empty<string>() : new[] { _politiqueBrouillon.ClientPrefere },
                multiple: false);
            ActualiserLibellesPolitique();
        }
        finally
        {
            _rafraichissementPolitique = false;
        }
    }
}
