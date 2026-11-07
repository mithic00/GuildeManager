using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Branche uniquement l'interface de politique sur un état mémorisé/sauvegardé.
/// Les effets en jeu (répartition des écus, autorisation de missions, etc.)
/// seront implémentés séparément lorsque leurs règles seront définies.
/// </summary>
public partial class MainMenu
{
    private EtatPolitiqueGuilde _politiqueGuilde;
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
            if (_rafraichissementPolitique || _politiqueGuilde == null)
                return;
            if (active)
                _politiqueGuilde.RetablirDefauts();
            else
                _politiqueGuilde.PolitiqueParDefaut = false;
            RafraichirInterfacePolitique();
        };

        _sliderPartGuilde.ValueChanged += valeur =>
        {
            if (_rafraichissementPolitique || _politiqueGuilde == null) return;
            _politiqueGuilde.PourcentageGuildeMissions = (int)Math.Round(valeur);
            SignalerPolitiquePersonnalisee();
            ActualiserLibellesPolitique();
        };
        _sliderEcartMissions.ValueChanged += valeur =>
        {
            if (_rafraichissementPolitique || _politiqueGuilde == null) return;
            _politiqueGuilde.EcartNiveauMission = (int)Math.Round(valeur);
            SignalerPolitiquePersonnalisee();
            ActualiserLibellesPolitique();
        };
        _sliderContrainteMissions.ValueChanged += valeur =>
        {
            if (_rafraichissementPolitique || _politiqueGuilde == null) return;
            _politiqueGuilde.ContrainteMission = (int)Math.Round(valeur);
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

        GetNode<Button>("FenetrePolitique/Marge/LayoutPrincipal/ButtonFermer")
            .Pressed += FermerPolitiqueGuilde;
        _fenetrePolitique.CloseRequested += FermerPolitiqueGuilde;
        RafraichirInterfacePolitique();
        _fenetrePolitique.Hide();
    }

    private void OuvrirPolitiqueGuilde()
    {
        if (_tempsEnCours || _fenetrePolitique == null || _politiqueGuilde == null)
            return;
        RafraichirInterfacePolitique();
        _fenetrePolitique.PopupCentered();
    }

    private void FermerPolitiqueGuilde()
    {
        if (_fenetrePolitique == null) return;
        ValiderNiveauRequisPolitique(normaliserTexte: true);
        _fenetrePolitique.Hide();
    }

    private void SignalerPolitiquePersonnalisee()
    {
        if (_politiqueGuilde == null) return;
        _politiqueGuilde.PolitiqueParDefaut = false;
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
        if (_politiqueGuilde == null) return;
        _labelPartGuilde.Text = _politiqueGuilde.PourcentageGuildeMissions + " %";
        _labelEcartMissions.Text =
            TexteEcartMissionPolitique(_politiqueGuilde.EcartNiveauMission);
        _labelContrainteMissions.Text =
            TexteContrainteMissionPolitique(_politiqueGuilde.ContrainteMission);
        _labelNiveauRequisPolitique.Text =
            _politiqueGuilde.NiveauMagiqueRequis == 0
                ? "Aucun" : "Niveau " + _politiqueGuilde.NiveauMagiqueRequis;
    }

    private void ValiderNiveauRequisPolitique(bool normaliserTexte)
    {
        if (_rafraichissementPolitique || _politiqueGuilde == null ||
            _champNiveauRequisPolitique == null)
            return;

        string texte = _champNiveauRequisPolitique.Text.Trim();
        bool estValide = int.TryParse(texte, NumberStyles.None,
            CultureInfo.InvariantCulture, out int valeur);
        if (!estValide)
        {
            if (normaliserTexte)
                _champNiveauRequisPolitique.Text =
                    _politiqueGuilde.NiveauMagiqueRequis.ToString(CultureInfo.InvariantCulture);
            else
                _labelNiveauRequisPolitique.Text = "Entrez un niveau entier positif";
            return;
        }

        if (valeur != _politiqueGuilde.NiveauMagiqueRequis)
        {
            _politiqueGuilde.NiveauMagiqueRequis = valeur;
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
        if (_rafraichissementPolitique || _politiqueGuilde == null) return;
        List<string> choix = ValeursSelectionneesPolitique(
            interdites ? _listeMissionsInterdites : _listeMissionsPreferees);
        if (interdites)
        {
            _politiqueGuilde.MissionsInterdites = choix;
            _politiqueGuilde.MissionsPreferees.RemoveAll(x => choix.Contains(x));
        }
        else
        {
            _politiqueGuilde.MissionsPreferees = choix;
            _politiqueGuilde.MissionsInterdites.RemoveAll(x => choix.Contains(x));
        }

        SignalerPolitiquePersonnalisee();
        RafraichirInterfacePolitique();
    }

    private void ModifierSelectionClientsInterditsPolitique()
    {
        if (_rafraichissementPolitique || _politiqueGuilde == null) return;
        List<string> choix = ValeursSelectionneesPolitique(_listeClientsInterdits);
        _politiqueGuilde.ClientsInterdits = choix;
        if (choix.Contains(_politiqueGuilde.ClientPrefere))
            _politiqueGuilde.ClientPrefere = "";

        SignalerPolitiquePersonnalisee();
        RafraichirInterfacePolitique();
    }

    private void ModifierClientPreferePolitique(int index)
    {
        if (_rafraichissementPolitique || _politiqueGuilde == null ||
            index < 0 || index >= _listeClientsPreferes.ItemCount)
            return;

        string choix = _listeClientsPreferes.GetItemText(index);
        // Recliquer sur un client préféré l'annule (sinon une liste en
        // sélection unique ne permettrait jamais de revenir à « aucun »).
        _politiqueGuilde.ClientPrefere =
            _politiqueGuilde.ClientPrefere == choix ? "" : choix;
        _politiqueGuilde.ClientsInterdits.Remove(choix);
        SignalerPolitiquePersonnalisee();
        RafraichirInterfacePolitique();
    }

    private void RafraichirInterfacePolitique()
    {
        if (_politiqueGuilde == null || _fenetrePolitique == null) return;

        _rafraichissementPolitique = true;
        try
        {
            _politiqueDefaut.SetPressedNoSignal(_politiqueGuilde.PolitiqueParDefaut);
            _sliderPartGuilde.SetValueNoSignal(_politiqueGuilde.PourcentageGuildeMissions);
            _sliderEcartMissions.SetValueNoSignal(_politiqueGuilde.EcartNiveauMission);
            _sliderContrainteMissions.SetValueNoSignal(_politiqueGuilde.ContrainteMission);
            _champNiveauRequisPolitique.Text =
                _politiqueGuilde.NiveauMagiqueRequis.ToString(CultureInfo.InvariantCulture);

            AppliquerSelectionListePolitique(_listeMissionsInterdites,
                _politiqueGuilde.MissionsInterdites, multiple: true);
            AppliquerSelectionListePolitique(_listeMissionsPreferees,
                _politiqueGuilde.MissionsPreferees, multiple: true);
            AppliquerSelectionListePolitique(_listeClientsInterdits,
                _politiqueGuilde.ClientsInterdits, multiple: true);
            AppliquerSelectionListePolitique(_listeClientsPreferes,
                string.IsNullOrEmpty(_politiqueGuilde.ClientPrefere)
                    ? Array.Empty<string>() : new[] { _politiqueGuilde.ClientPrefere },
                multiple: false);
            ActualiserLibellesPolitique();
        }
        finally
        {
            _rafraichissementPolitique = false;
        }
    }
}
