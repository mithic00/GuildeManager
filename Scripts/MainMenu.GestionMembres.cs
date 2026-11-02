using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>Gestion de la guilde, relations sociales et concours des membres.</summary>
public partial class MainMenu
{
    private Control _contenuSocial, _contenuRelations, _contenuConcours;
    private RichTextLabel _bonnesRelations, _mauvaisesRelations, _texteConfirmation;
    private ItemList _listeConcours;
    private Window _fenetreConfirmation;
    private Button _lancerConcours, _promouvoir, _promouvoirAdjoint;
    private readonly List<MembreGuilde> _membresConcours = new();
    private Action _confirmationPrevue;

    private void InitialiserGestionMembres()
    {
        const string racine = "FenetreFichePerso/";
        _contenuSocial = GetNode<Control>(racine + "LayoutSocial");
        _contenuRelations = GetNode<Control>(racine + "LayoutRelation");
        _contenuConcours = GetNode<Control>(racine + "LayoutConcours");
        _bonnesRelations = GetNode<RichTextLabel>(racine +
            "LayoutRelation/Layout/InfosRelation/BonnesRelations");
        _mauvaisesRelations = GetNode<RichTextLabel>(racine +
            "LayoutRelation/Layout/InfosRelation/MauvaisesRelations");
        _listeConcours = GetNode<ItemList>(racine + "LayoutConcours/Layout/ListeMembreEligible");
        _lancerConcours = GetNode<Button>(racine +
            "LayoutConcours/Layout/Boutons/BoutonLancerConcours");
        _promouvoir = GetNode<Button>(racine + "LayoutGestion/Boutons/BoutonPromouvoir");
        _promouvoirAdjoint = GetNode<Button>(racine + "LayoutGestion/Boutons/BoutonPromouvoirAdjoint");

        GetNode<Button>(racine + "MargeFichePerso/Layout/Boutons/GridContainer/BoutonSocial")
            .Pressed += AfficherSocial;
        GetNode<Button>(racine + "LayoutSocial/VBoxContainer/BoutonRelation")
            .Pressed += AfficherRelations;
        GetNode<Button>(racine + "LayoutSocial/VBoxContainer/BoutonConversation")
            .Pressed += () => AfficherInformation("Les conversations seront développées ultérieurement.");
        GetNode<Button>(racine + "LayoutSocial/VBoxContainer/BoutonRetour")
            .Pressed += RetourFiche;
        GetNode<Button>(racine + "LayoutRelation/Layout/BoutonRetour")
            .Pressed += AfficherSocial;
        GetNode<Button>(racine + "LayoutGestion/Boutons/BoutonConcours")
            .Pressed += AfficherConcours;
        GetNode<Button>(racine + "LayoutConcours/Layout/Boutons/BoutonRetour")
            .Pressed += AfficherGestion;
        _listeConcours.ItemSelected += _ => ActualiserBoutonConcours();
        _lancerConcours.Pressed += ProposerEnvoiConcours;
        _promouvoir.Pressed += ProposerPassation;
        _promouvoirAdjoint.Pressed += ProposerAdjoint;

        _fenetreConfirmation = GetNode<Window>("FenetreConfirmation");
        _texteConfirmation = GetNode<RichTextLabel>("FenetreConfirmation/Marge/Layout/Info");
        GetNode<Button>("FenetreConfirmation/Marge/Layout/BoutonConfirmation")
            .Pressed += ConfirmerAction;
        GetNode<Button>("FenetreConfirmation/Marge/Layout/BoutonAnnuler")
            .Pressed += AnnulerConfirmation;
        _fenetreConfirmation.CloseRequested += AnnulerConfirmation;
        _fenetreConfirmation.Hide();
        _contenuSocial.Hide();
        _contenuRelations.Hide();
        _contenuConcours.Hide();
    }

    private void MasquerOngletsFiche()
    {
        _contenuFiche.Hide();
        _contenuGestion.Hide();
        _contenuConversation.Hide();
        _contenuSocial.Hide();
        _contenuRelations.Hide();
        _contenuConcours.Hide();
    }

    private void AfficherSocial()
    {
        MasquerOngletsFiche();
        _contenuSocial.Show();
    }

    private void AfficherRelations()
    {
        if (_membreConsulte == null || _registreMembres == null) return;
        var proches = new List<string>();
        var distants = new List<string>();
        foreach (MembreGuilde autre in _registreMembres.Membres
            .Where(m => m.Id != _membreConsulte.Id).OrderBy(m => m.NomComplet))
        {
            int valeur = RelationsMembres.Lire(_membreConsulte, autre);
            string lien = autre.NomComplet + " : " + RelationsMembres.Qualifier(valeur) +
                " (" + (valeur > 0 ? "+" : "") + valeur + ")";
            // Neutre couvre bien toute la plage -9 à +9.
            if (valeur >= -9) proches.Add(lien);
            else distants.Add(lien);
        }
        _bonnesRelations.Text = "Bonnes relations et relations neutres :\n" +
            (proches.Count == 0 ? "Aucune" : string.Join("\n", proches));
        _mauvaisesRelations.Text = "Mauvaises relations :\n" +
            (distants.Count == 0 ? "Aucune" : string.Join("\n", distants));
        MasquerOngletsFiche();
        _contenuRelations.Show();
    }

    private bool EstAbsent(MembreGuilde membre) =>
        membre == null || !string.IsNullOrWhiteSpace(membre.ConcoursEnCours) ||
        _etatMissions?.Missions.Any(q => q.Etat == "EnCours" &&
            q.ParticipantIds.Contains(membre.Id)) == true;

    private void ActualiserBoutonsGestion()
    {
        bool disponible = _membreConsulte != null && !_membreConsulte.EstJoueur &&
            !_membreConsulte.EstMaitreDeGuilde &&
            _registreMembres?.Membres.Contains(_membreConsulte) == true &&
            !EstAbsent(_membreConsulte);
        _promouvoir.Disabled = !disponible;
        _promouvoirAdjoint.Disabled = !disponible || _membreConsulte.EstAdjoint;
    }

    private void OuvrirConfirmation(string message, Action action)
    {
        if (action == null) return;
        _confirmationPrevue = action;
        _texteConfirmation.Text = message;
        _fenetreFiche.Hide();
        _fenetreConfirmation.PopupCentered();
    }

    private void AnnulerConfirmation()
    {
        _confirmationPrevue = null;
        _fenetreConfirmation.Hide();
        if (!_sortieEnCours) _fenetreFiche.PopupCentered();
    }

    private void ConfirmerAction()
    {
        Action action = _confirmationPrevue;
        _confirmationPrevue = null;
        _fenetreConfirmation.Hide();
        if (action == null) return;
        action();
    }

    private void ProposerAdjoint()
    {
        MembreGuilde cible = _membreConsulte;
        if (cible == null || cible.EstJoueur || cible.EstMaitreDeGuilde || EstAbsent(cible)) return;
        OuvrirConfirmation("Nommer " + cible.NomComplet + " adjoint de la guilde ?\n" +
            "Cette nomination remplace l'adjoint actuel et améliore votre relation de 20 points.",
            () => ConfirmerAdjoint(cible));
    }

    private void ConfirmerAdjoint(MembreGuilde cible)
    {
        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        if (joueur == null || !_registreMembres.Membres.Contains(cible) || EstAbsent(cible))
        { _fenetreFiche.PopupCentered(); return; }
        foreach (MembreGuilde membre in _registreMembres.Membres)
            membre.EstAdjoint = membre.Id == cible.Id;
        int relation = RelationsMembres.Ajuster(joueur, cible, 20);
        AjouterEntreeJournal(cible.NomComplet + " devient l'adjoint de la guilde.", "gestion");
        _historiqueMembres?.Ajouter(cible, _horloge.Instant, "NominationAdjoint",
            "Nommé adjoint. Relation avec le maître : " + relation + ".");
        _fenetreFiche.PopupCentered();
        AfficherGestion();
        AfficherInformation(cible.NomComplet + " devient votre adjoint. Relation : " +
            RelationsMembres.Qualifier(relation) + " (" + relation + "/100).");
    }

    private void ProposerPassation()
    {
        MembreGuilde cible = _membreConsulte;
        if (cible == null || cible.EstJoueur || cible.EstMaitreDeGuilde || EstAbsent(cible)) return;
        OuvrirConfirmation("Confier définitivement la direction de la guilde à " +
            cible.NomComplet + " ?\nVotre personnage quittera la guilde et votre partie sera terminée. " +
            "Cette décision sera sauvegardée.", () => ConfirmerPassation(cible));
    }

    private void ConfirmerPassation(MembreGuilde cible)
    {
        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        if (joueur == null || !_registreMembres.Membres.Contains(cible) ||
            EstAbsent(cible) || _etat == null || _horloge == null)
        { _fenetreFiche.PopupCentered(); return; }

        // Préparer la fin de partie en mémoire. Si l'écriture échoue,
        // restaurer l'ensemble des mutations et rester dans la partie.
        bool ancienStatut = cible.EstMaitreDeGuilde;
        var anciensAdjoints = _registreMembres.Membres.ToDictionary(m => m.Id, m => m.EstAdjoint);
        bool ancienneFin = _etat.PartieTerminee;
        string ancienSuccesseur = _etat.SuccesseurId;
        long anciensMembres = _etat.Membres, ancienPouvoir = _etat.Pouvoir;
        int nbJournal = _journalPartie?.Entrees.Count ?? 0;
        int nbHistorique = _historiqueMembres?.Evenements.Count ?? 0;
        _registreMembres.Membres.Remove(joueur);
        joueur.EstMaitreDeGuilde = false;
        cible.EstMaitreDeGuilde = true;
        foreach (var membre in _registreMembres.Membres) membre.EstAdjoint = false;
        _etat.PartieTerminee = true;
        _etat.SuccesseurId = cible.Id;
        _etat.Membres = _registreMembres.Membres.Count;
        _etat.Pouvoir = _registreMembres.Membres.Sum(m => (long)m.NiveauMagique);
        _registreMembres.RecalculerClassement();
        AjouterEntreeJournal(cible.NomComplet + " devient maître de guilde. " +
            joueur.NomComplet + " quitte la guilde.", "gestion");
        _historiqueMembres?.Ajouter(cible, _horloge.Instant, "Succession",
            "Devient maître de guilde après le départ de " + joueur.NomComplet + ".");
        _historiqueMembres?.Ajouter(joueur, _horloge.Instant, "DepartGuilde",
            "Quitte la guilde après avoir nommé " + cible.NomComplet + " maître.");
        if (!SauvegarderPartie())
        {
            _registreMembres.Membres.Add(joueur);
            joueur.EstMaitreDeGuilde = true;
            cible.EstMaitreDeGuilde = ancienStatut;
            foreach (var membre in _registreMembres.Membres)
                if (anciensAdjoints.TryGetValue(membre.Id, out bool etaitAdjoint))
                    membre.EstAdjoint = etaitAdjoint;
            _etat.PartieTerminee = ancienneFin;
            _etat.SuccesseurId = ancienSuccesseur;
            _etat.Membres = anciensMembres;
            _etat.Pouvoir = ancienPouvoir;
            while (_journalPartie != null && _journalPartie.Entrees.Count > nbJournal)
                _journalPartie.Entrees.RemoveAt(_journalPartie.Entrees.Count - 1);
            while (_historiqueMembres != null && _historiqueMembres.Evenements.Count > nbHistorique)
                _historiqueMembres.Evenements.RemoveAt(_historiqueMembres.Evenements.Count - 1);
            _registreMembres.RecalculerClassement();
            if (_fenetreInfo?.Visible == true)
                _fenetreAReouvrirApresInfo = _fenetreFiche;
            else
                _fenetreFiche.PopupCentered();
            return;
        }
        _sortieEnCours = true;
        ArreterHorlogeSansJournal();
        _fenetreFiche.Hide();
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, SceneDemarrage);
    }

    private void AfficherConcours()
    {
        if (_registreMembres == null) return;
        _listeConcours.Clear();
        _membresConcours.Clear();
        foreach (MembreGuilde membre in _registreMembres.Membres
            .Where(m => !EstAbsent(m)).OrderBy(m => m.NomComplet))
        {
            string niveau = ReglesConcours.ConcoursEligible(membre);
            if (niveau == "") continue;
            _membresConcours.Add(membre);
            int cout = ReglesConcours.Prix(niveau);
            string duree = niveau == "S" ? "7 jours" : "1 mois";
            _listeConcours.AddItem(membre.NomComplet + " — Concours " + niveau +
                " — " + cout.ToString("N0", Francais) + " écus — " + duree);
        }
        ActualiserBoutonConcours();
        MasquerOngletsFiche();
        _contenuConcours.Show();
    }

    private void ActualiserBoutonConcours()
    {
        int[] selection = _listeConcours.GetSelectedItems();
        if (selection.Length == 0 || selection[0] < 0 ||
            selection[0] >= _membresConcours.Count || _etat == null)
        {
            _lancerConcours.Disabled = true;
            return;
        }
        var candidat = _membresConcours[selection[0]];
        string concours = ReglesConcours.ConcoursEligible(candidat);
        _lancerConcours.Disabled = concours == "" || EstAbsent(candidat) ||
            _etat.Ecus < (concours == "" ? int.MaxValue : ReglesConcours.Prix(concours));
    }

    private void ProposerEnvoiConcours()
    {
        int[] selection = _listeConcours.GetSelectedItems();
        if (selection.Length == 0 || selection[0] < 0 ||
            selection[0] >= _membresConcours.Count) return;
        MembreGuilde cible = _membresConcours[selection[0]];
        string concours = ReglesConcours.ConcoursEligible(cible);
        if (concours == "" || EstAbsent(cible)) return;
        int prix = ReglesConcours.Prix(concours);
        if (_etat.Ecus < prix) { AfficherInformation("Fonds insuffisants."); return; }
        string duree = concours == "S" ? "7 jours" : "un mois";
        OuvrirConfirmation("Envoyer " + cible.NomComplet + " au concours " + concours +
            " ?\nPrix : " + prix.ToString("N0", Francais) +
            " écus. Absence : " + duree + ". Réussite garantie pour l'instant.",
            () => ConfirmerEnvoiConcours(cible, concours));
    }

    private void ConfirmerEnvoiConcours(MembreGuilde cible, string concours)
    {
        if (_etat == null || _horloge == null || EstAbsent(cible) ||
            ReglesConcours.ConcoursEligible(cible) != concours ||
            _etat.Ecus < ReglesConcours.Prix(concours))
        { _fenetreFiche.PopupCentered(); return; }

        int prix = ReglesConcours.Prix(concours);
        DateTime retour = ReglesConcours.DateRetour(concours, _horloge.Instant);
        _etat.Ecus -= prix;
        cible.ConcoursEnCours = concours;
        cible.DateRetourConcours = ReglesMissions.DateIso(retour);
        cible.ResultatConcoursEnAttente = false;
        _historiqueMembres?.Ajouter(cible, _horloge.Instant, "DepartConcours",
            "Départ au concours " + concours + " (coût : " + prix +
            " écus ; retour : " + HorlogeJeu.Formater(retour) + ").");
        AjouterEntreeJournal(cible.NomComplet + " part au concours " + concours +
            " pour " + prix + " écus. Retour prévu le " +
            HorlogeJeu.Formater(retour) + ".", "concours");
        AfficherValeur(_ecus, _etat.Ecus);
        _fenetreFiche.PopupCentered();
        AfficherConcours();
    }

    private bool TraiterRetoursConcours(DateTime instant)
    {
        if (_registreMembres == null) return false;
        bool retour = false;
        foreach (MembreGuilde membre in _registreMembres.Membres.Where(m =>
            !string.IsNullOrWhiteSpace(m.ConcoursEnCours) &&
            !string.IsNullOrWhiteSpace(m.DateRetourConcours) &&
            ReglesMissions.LireDate(m.DateRetourConcours) <= instant).ToList())
        {
            string concours = membre.ConcoursEnCours;
            string ancienRang = membre.Rang;
            // Pour cette première version, chaque concours est réussi.
            membre.Rang = concours;
            membre.ConcoursEnCours = "";
            membre.DateRetourConcours = "";
            membre.ResultatConcoursEnAttente = false;
            _historiqueMembres?.Ajouter(membre, instant, "ConcoursReussi",
                "Réussite du concours " + concours + " : " +
                ReglesMissions.FormaterRang(ancienRang) + " → " +
                ReglesMissions.FormaterRang(concours) + ".");
            AjouterEntreeJournal(membre.NomComplet + " réussit son concours et obtient le rang " +
                concours + " !", "concours");
            retour = true;
        }
        if (!retour) return false;
        _registreMembres.RecalculerClassement();
        if (_membreConsulte != null && _fenetreFiche.Visible)
            RemplirFiche(_membreConsulte);
        return true;
    }
}
