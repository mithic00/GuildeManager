using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Fenêtre de budget : historique des mouvements réels de trésorerie.
/// Les périodes 1-12 correspondent aux mois calendaires, mois actuel inclus.
/// Le cran 13 affiche tout le suivi depuis sa création.
/// </summary>
public partial class MainMenu
{
    private RegistreBudget _budget;
    private Window _fenetreBudget;
    private Window _fenetreRelationBureau;
    private Window _fenetreInventaireBureau;
    private Window _fenetreCalendrierBureau;
    private HSlider _sliderPeriodeBudget;
    private Label _labelPeriodeBudget;
    private RichTextLabel _entreesBudget, _sortiesBudget;

    private void InitialiserBudgetEtBureau()
    {
        const string baseBudget = "FenetreBudget/Marge/Layout/";
        _fenetreBudget = GetNode<Window>("FenetreBudget");
        _labelPeriodeBudget = GetNode<Label>(baseBudget + "HBoxContainer/Label");
        _sliderPeriodeBudget = GetNode<HSlider>(baseBudget + "HBoxContainer/HSliderPeriode");
        _entreesBudget = GetNode<RichTextLabel>(baseBudget + "InfoRentreArgent");
        _sortiesBudget = GetNode<RichTextLabel>(baseBudget + "InfoSortieArgent");
        _sliderPeriodeBudget.MinValue = 1;
        _sliderPeriodeBudget.MaxValue = 13;
        _sliderPeriodeBudget.Step = 1;
        _sliderPeriodeBudget.SetValueNoSignal(1);
        _sliderPeriodeBudget.ValueChanged += _ => RafraichirBudget();
        GetNode<Button>(baseBudget + "ButtonFermer").Pressed += FermerBudget;
        _fenetreBudget.CloseRequested += FermerBudget;
        _fenetreBudget.Hide();

        const string baseBureau = "LayoutsPanneauPrincipal/Bureau/";
        GetNode<Button>(baseBureau + "BoutonBudget").Pressed += OuvrirBudget;
        GetNode<Button>(baseBureau + "BoutonRelation").Pressed += OuvrirRelationBureau;
        GetNode<Button>(baseBureau + "BoutonInventaire").Pressed += OuvrirInventaireBureau;
        GetNode<Button>(baseBureau + "BoutonCalendrier").Pressed += OuvrirCalendrier;

        // Ces fenêtres sont encore vides côté jeu, mais leurs boutons et
        // demandes de fermeture utilisent désormais les nouveaux nœuds.
        _fenetreRelationBureau = GetNode<Window>("FenetreRelation");
        _fenetreInventaireBureau = GetNode<Window>("FenetreInventaire");
        _fenetreCalendrierBureau = GetNode<Window>("FenetreCalendrier");
        _fenetreRelationBureau.CloseRequested += () => _fenetreRelationBureau.Hide();
        _fenetreInventaireBureau.CloseRequested += () => _fenetreInventaireBureau.Hide();
        _fenetreCalendrierBureau.CloseRequested += () => _fenetreCalendrierBureau.Hide();
        _fenetreRelationBureau.Hide();
        _fenetreInventaireBureau.Hide();
        _fenetreCalendrierBureau.Hide();

        if (_etat != null && _horloge != null)
            _budget = RegistreBudget.ChargerOuInitialiser(_etat.Ecus, _horloge.Instant);
        if (_budget == null)
            GD.PushError("MainMenu : registre du budget indisponible.");
        RafraichirBudget();
    }

    private void OuvrirBudget()
    {
        if (_tempsEnCours) return;
        if (_budget == null)
        {
            AfficherInformation("Impossible d'afficher le budget : registre comptable indisponible.");
            return;
        }
        RafraichirBudget();
        _fenetreBudget.PopupCentered();
    }

    private void FermerBudget() => _fenetreBudget.Hide();

    private void OuvrirRelationBureau()
    {
        if (!_tempsEnCours) _fenetreRelationBureau?.PopupCentered();
    }

    private void OuvrirInventaireBureau()
    {
        if (!_tempsEnCours) _fenetreInventaireBureau?.PopupCentered();
    }

    private void OuvrirCalendrier()
    {
        if (!_tempsEnCours) _fenetreCalendrierBureau?.PopupCentered();
    }

    /// <summary>
    /// Appelé exclusivement après une opération effective sur _etat.Ecus,
    /// en enregistrant la variation réelle, pas seulement le montant demandé.
    /// Aucun nouveau mécanisme de dépense automatique n'est créé ici.
    /// </summary>
    private void EnregistrerMouvementBudget(string categorie, long variation, string detail)
    {
        if (variation == 0) return;
        if (_budget == null || _horloge == null)
        {
            GD.PushError("Un mouvement financier n'a pas été historisé : " +
                categorie + ", " + variation + " écus.");
            return;
        }
        _budget.Ajouter(_horloge.Instant, categorie, variation, detail);
        if (_fenetreBudget != null && _fenetreBudget.Visible)
            RafraichirBudget();
    }

    private void RafraichirBudget()
    {
        if (_labelPeriodeBudget == null || _entreesBudget == null || _sortiesBudget == null)
            return;
        if (_budget == null || _horloge == null)
        {
            _labelPeriodeBudget.Text = "Budget indisponible";
            _entreesBudget.Text = "Registre indisponible.";
            _sortiesBudget.Text = "Registre indisponible.";
            return;
        }

        int periode = Math.Clamp((int)Math.Round(_sliderPeriodeBudget.Value), 1, 13);
        DateTime maintenant = _horloge.Instant;
        DateTime debut;
        if (periode == 13)
            debut = RegistreBudget.LireDate(_budget.DateDebutSuivi, out DateTime d)
                ? d : HorlogeJeu.DateInitiale;
        else
            debut = new DateTime(maintenant.Year, maintenant.Month, 1).AddMonths(1 - periode);

        var mouvements = _budget.Mouvements.Where(m =>
            RegistreBudget.LireDate(m.Date, out DateTime date) &&
            date >= debut && date <= maintenant).ToList();

        string format = "dd MMM yyyy";
        string debutLisible = debut.ToString(format, Francais);
        string finLisible = maintenant.ToString(format, Francais);
        bool suiviDepuisOuverture = RegistreBudget.LireDate(
            _budget.DateDebutSuivi, out DateTime debutRegistre) &&
            debutRegistre <= HorlogeJeu.DateInitiale;
        string titre = periode == 13
            ? (suiviDepuisOuverture ? "Depuis l'ouverture de la guilde" :
                "Tout l'historique comptable disponible")
            : periode == 1 ? "1 mois (mois en cours)" :
                periode + " mois (mois en cours inclus)";
        _labelPeriodeBudget.Text = titre + "\nDu " + debutLisible + " au " + finLisible;

        long recettes = mouvements.Where(m => m.Montant > 0).Sum(m => m.Montant);
        long depenses = -mouvements.Where(m => m.Montant < 0).Sum(m => m.Montant);

        var revenus = new List<string> { "RECETTES" };
        foreach (string categorie in RegistreBudget.CategoriesRecettes)
        {
            long somme = mouvements.Where(m => m.Montant > 0 && m.Categorie == categorie)
                .Sum(m => m.Montant);
            if (categorie == RegistreBudget.Ajustements && somme == 0) continue;
            revenus.Add(categorie + " : +" + FormaterEcusBudget(somme));
        }
        revenus.Add("");
        revenus.Add("TOTAL DES RECETTES : +" + FormaterEcusBudget(recettes));
        _entreesBudget.Text = string.Join("\n", revenus);

        var charges = new List<string> { "DÉPENSES" };
        foreach (string categorie in RegistreBudget.CategoriesDepenses)
        {
            long somme = -mouvements.Where(m => m.Montant < 0 && m.Categorie == categorie)
                .Sum(m => m.Montant);
            if (categorie == RegistreBudget.Ajustements && somme == 0) continue;
            charges.Add(categorie + " : -" + FormaterEcusBudget(somme));
        }
        charges.Add("");
        charges.Add("TOTAL DES DÉPENSES : -" + FormaterEcusBudget(depenses));
        long bilan = recettes - depenses;
        charges.Add("BILAN SUR LA PÉRIODE : " + (bilan >= 0 ? "+" : "") +
            FormaterEcusBudget(bilan));
        charges.Add("TRÉSORERIE ACTUELLE : " + FormaterEcusBudget(_etat?.Ecus ?? 0));
        if (periode == 13)
        {
            charges.Add("Solde au début du suivi : " +
                FormaterEcusBudget(_budget.SoldeOuverture));
            if (RegistreBudget.LireDate(_budget.DateDebutSuivi, out DateTime dateDebut) &&
                dateDebut > HorlogeJeu.DateInitiale)
                charges.Add("Ancienne partie : seules les opérations depuis " +
                    dateDebut.ToString(format, Francais) + " sont disponibles.");
        }
        _sortiesBudget.Text = string.Join("\n", charges);
    }

    private static string FormaterEcusBudget(long montant) =>
        montant.ToString("N0", Francais) + " écus";
}
