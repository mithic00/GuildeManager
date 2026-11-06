using Godot;
using System;

/// <summary>
/// Relations affichées dans la fenêtre du bureau.
/// Les trois jauges générales sont persistées dans EtatRelationsGuilde.
/// Facon utilise exclusivement la réputation du fil rouge de Rose-Vallée.
/// </summary>
public partial class MainMenu
{
    private EtatRelationsGuilde _relationsGuilde;

    private Label _labelRelationAcademie;
    private Label _labelRelationCommercants;
    private Label _labelRelationVille;
    private Label _labelRelationFacon;

    private JaugeRelation _jaugeAcademie;
    private JaugeRelation _jaugeCommercants;
    private JaugeRelation _jaugeVille;
    private JaugeRelation _jaugeFacon;

    private Control _emplacementRelationFacon;
    private Control _labelAnnexe2;
    private Control _barreAnnexe2;
    private Control _labelAnnexe3;
    private Control _barreAnnexe3;

    private void InitialiserRelationsBureau()
    {
        const string baseRelation = "FenetreRelation/Marge/Layout/";
        _relationsGuilde = EtatRelationsGuilde.ChargerOuCreer();
        if (_relationsGuilde == null)
            GD.PushError("MainMenu : impossible de charger les réputations.");

        _labelRelationAcademie = GetNode<Label>(baseRelation + "LabelAcademie");
        _labelRelationCommercants = GetNode<Label>(baseRelation + "LabelCommercant");
        _labelRelationVille = GetNode<Label>(baseRelation + "LabelVille");
        _labelRelationFacon = GetNode<Label>(baseRelation + "LabelAnnexe");

        _jaugeAcademie = AjouterJaugeRelation(baseRelation + "BarreReputationAcademie");
        _jaugeCommercants = AjouterJaugeRelation(baseRelation + "BarreReputationComercant");
        _jaugeVille = AjouterJaugeRelation(baseRelation + "BarreReputationVille");

        _emplacementRelationFacon = GetNode<Control>(
            baseRelation + "BarreReputationAnnexe");
        _jaugeFacon = AjouterJaugeRelation(baseRelation + "BarreReputationAnnexe");

        _labelAnnexe2 = GetNode<Control>(baseRelation + "LabelAnnexe2");
        _barreAnnexe2 = GetNode<Control>(baseRelation + "BarreReputationAnnexe2");
        _labelAnnexe3 = GetNode<Control>(baseRelation + "LabelAnnexe3");
        _barreAnnexe3 = GetNode<Control>(baseRelation + "BarreReputationAnnexe3");

        // Emplacements réservés à de futures factions / quêtes.
        _labelAnnexe2.Hide();
        _barreAnnexe2.Hide();
        _labelAnnexe3.Hide();
        _barreAnnexe3.Hide();

        GetNode<Button>(baseRelation + "ButtonFermer").Pressed +=
            () => _fenetreRelationBureau?.Hide();
        RafraichirRelationsBureau();
    }

    private JaugeRelation AjouterJaugeRelation(string chemin)
    {
        Control emplacement = GetNode<Control>(chemin);
        JaugeRelation jauge = new()
        {
            Name = "JaugeRelationDessinee",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        emplacement.AddChild(jauge);
        jauge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return jauge;
    }

    private static string TexteReputation(int valeur)
    {
        valeur = Math.Clamp(valeur, -100, 100);
        string qualificatif = valeur == 0 ? "Neutre" :
            valeur > 0 ? "Positive" : "Négative";
        return (valeur > 0 ? "+" : "") + valeur + " / 100 — " + qualificatif;
    }

    private void RafraichirRelationsBureau()
    {
        if (_jaugeAcademie == null || _jaugeCommercants == null ||
            _jaugeVille == null || _jaugeFacon == null)
            return;

        int academie = _relationsGuilde?.Lire(EtatRelationsGuilde.AcademieMagique) ?? 0;
        int commercants = _relationsGuilde?.Lire(EtatRelationsGuilde.Commercants) ?? 0;
        int ville = _relationsGuilde?.Lire(EtatRelationsGuilde.CleVille(_villeDepart)) ?? 0;

        _jaugeAcademie.Relation = academie;
        _jaugeCommercants.Relation = commercants;
        _jaugeVille.Relation = ville;

        _labelRelationAcademie.Text = "Académie magique : " + TexteReputation(academie);
        _labelRelationCommercants.Text = "Commerçants : " + TexteReputation(commercants);
        _labelRelationVille.Text = (_villeDepart ?? "Ville") + " : " + TexteReputation(ville);

        bool roseVallee = string.Equals(_villeDepart, "Rose-Vallée",
            StringComparison.OrdinalIgnoreCase);
        _labelRelationFacon.Visible = roseVallee;
        _emplacementRelationFacon.Visible = roseVallee;
        if (roseVallee)
        {
            int facon = Math.Clamp(_filRouge?.ReputationFacon ?? 0, -100, 100);
            _jaugeFacon.Relation = facon;
            _labelRelationFacon.Text = "Facon : " + TexteReputation(facon);
        }
    }
}
