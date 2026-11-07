using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Infrastructure des protestations liées aux décisions de politique.
/// AUCUNE règle ne choisit encore les mécontents : un futur système devra
/// appeler SignalerMecontentementsPolitique avec les membres et les motifs.
/// </summary>
public partial class MainMenu
{
    private EtatReclamationsPolitique _reclamationsPolitique;
    private ReclamationPolitique _reclamationCourante;
    private bool _dialogueReclamationPolitiqueActif;
    private bool _reclamationPolitiqueResultatAffiche;

    private void InitialiserReclamationsPolitique()
    {
        _reclamationsPolitique = EtatReclamationsPolitique.Charger();

        // Le moteur normal des conversations est déjà abonné à cet ItemList.
        // Il ignore les clics tant qu'une protestation imposée est active.
        _listeReponsesDiscussion.ItemSelected += index =>
        {
            if (!_dialogueReclamationPolitiqueActif) return;
            int choix = (int)index;
            _listeReponsesDiscussion.DeselectAll();
            if (choix != 0) return;
            if (!_reclamationPolitiqueResultatAffiche)
                ResoudreReclamationPolitique();
            else
                TerminerReclamationPolitique();
        };

        // Les protestations encore en attente dans une sauvegarde reprennent
        // après les autres initialisations, sans en créer de nouvelles.
        CallDeferred(nameof(VerifierReclamationsPolitique));
    }

    /// <summary>
    /// Point d'entrée réservé aux futures règles de mécontentement.
    /// Aucun appel automatique depuis ConfirmerPolitiqueGuilde.
    /// </summary>
    private void SignalerMecontentementsPolitique(
        IEnumerable<(MembreGuilde Membre, string Politique)> mecontentements)
    {
        if (_reclamationsPolitique == null || _registreMembres == null ||
            _horloge == null || mecontentements == null)
            return;

        int ajoutes = 0;
        int absents = 0;
        foreach (var (membre, politique) in mecontentements)
        {
            if (membre == null || membre.EstJoueur || membre.EstMaitreDeGuilde ||
                !_registreMembres.Membres.Contains(membre) ||
                string.IsNullOrWhiteSpace(politique))
                continue;
            string motif = politique.Trim();
            if (_reclamationsPolitique.EnAttente.Any(r =>
                r.MembreId == membre.Id && r.Motif == motif))
                continue;

            _reclamationsPolitique.EnAttente.Add(new ReclamationPolitique
            {
                MembreId = membre.Id,
                Motif = motif,
                DateSignalement = ReglesMissions.DateIso(_horloge.Instant)
            });
            ajoutes++;
            if (EstAbsent(membre)) absents++;
        }

        if (ajoutes == 0) return;
        AjouterEntreeJournal(ajoutes > 1
            ? "Politique de guilde : " + ajoutes +
              " membres sont mécontents. Leurs protestations seront entendues l'une après l'autre."
            : "Politique de guilde : un membre souhaite exprimer son mécontentement.",
            "politique");
        if (absents > 0)
            AjouterEntreeJournal("Politique de guilde : " + absents +
                " membre(s) absent(s) présenteront leur protestation à leur retour.",
                "politique");

        CallDeferred(nameof(VerifierReclamationsPolitique));
    }

    private bool ReclamationPolitiquePourMembre(string membreId) =>
        _reclamationsPolitique?.EnAttente.Any(r => r.MembreId == membreId) == true;

    private bool ReclamationPolitiqueDisponible() =>
        _reclamationsPolitique?.EnAttente.Any(r =>
            _registreMembres?.Membres.Any(m =>
                m.Id == r.MembreId && !EstAbsent(m)) == true) == true;

    /// <summary>
    /// La file peut contenir des absents : les autres membres parlent
    /// immédiatement, puis les absents à leur retour. Aucun dialogue n'est
    /// perdu si une fenêtre prioritaire (fil rouge, auberge...) est ouverte.
    /// </summary>
    private void VerifierReclamationsPolitique()
    {
        if (_reclamationsPolitique == null || _registreMembres == null ||
            _horloge == null || _fenetreDiscussion == null ||
            _dialogueReclamationPolitiqueActif || _filRougeDialogueActif ||
            _discussionEnCours || _traitementTempsDiscussion ||
            _fenetreDiscussion.Visible ||
            _fenetreNouvelleMission?.Visible == true ||
            _choixRecrutement?.Visible == true ||
            _fenetrePolitique?.Visible == true ||
            _fenetreFiche?.Visible == true ||
            _fenetreQuitter?.Visible == true ||
            _fenetreInfo?.Visible == true ||
            _fenetreBudget?.Visible == true ||
            _fenetreRelationBureau?.Visible == true ||
            _fenetreInventaireBureau?.Visible == true ||
            _fenetreCalendrierBureau?.Visible == true ||
            _fenetreAchatMagasin?.Visible == true)
            return;

        // Un membre décédé ou ayant quitté la guilde ne protestera jamais.
        _reclamationsPolitique.EnAttente.RemoveAll(r =>
            !_registreMembres.Membres.Any(m => m.Id == r.MembreId));

        ReclamationPolitique prochaine = _reclamationsPolitique.EnAttente
            .FirstOrDefault(r => _registreMembres.Membres.Any(m =>
                m.Id == r.MembreId && !EstAbsent(m)));
        if (prochaine == null) return;

        if (_tempsEnCours)
            InterrompreAvanceeTemps("Un membre souhaite discuter de la politique de la guilde.");

        _reclamationCourante = prochaine;
        _dialogueReclamationPolitiqueActif = true;
        _reclamationPolitiqueResultatAffiche = false;
        ReafficherReclamationPolitique();
    }

    private void ReafficherReclamationPolitique()
    {
        if (!_dialogueReclamationPolitiqueActif || _reclamationCourante == null)
            return;
        MembreGuilde membre = _registreMembres?.Membres.FirstOrDefault(
            m => m.Id == _reclamationCourante.MembreId);
        if (membre == null)
        {
            TerminerReclamationPolitique();
            return;
        }

        // Le joueur retrouve le bureau d'accueil, où le journal est visible.
        AfficherPanneau("accueil");
        _connaissanceAContinuer = false;
        _soutienMoralEnAttente = false;
        ReinitialiserDialogueDemandeMission();
        _sujetsDiscussion.Hide();
        _reponsesDiscussion.Show();

        const string portraitDefaut =
            "res://Images/Portraits/GrandFormat/Portait_Defaut.png";
        _portraitDiscussionCible.Texture =
            ResourceLoader.Load<Texture2D>(portraitDefaut);
        _portraitDiscussionJoueur.Texture =
            ResourceLoader.Load<Texture2D>(portraitDefaut);

        if (!_reclamationPolitiqueResultatAffiche)
            _resultatDiscussion.Text = membre.NomComplet +
                " : Je ne suis pas satisfait du changement de politique de la guilde (" +
                _reclamationCourante.Motif + "). Je voudrais comprendre pourquoi " +
                "vous avez pris cette décision. Je crains qu'elle ne nuise à nos membres." +
                "\n\nVous devez répondre à cette contestation.";
        _listeReponsesDiscussion.Clear();
        _listeReponsesDiscussion.AddItem(_reclamationPolitiqueResultatAffiche
            ? "Terminer cet échange"
            : "Expliquer votre décision (D20 Charisme + Leadership)");
        _fenetreDiscussion.PopupCentered();
    }

    private void ResoudreReclamationPolitique()
    {
        if (!_dialogueReclamationPolitiqueActif ||
            _reclamationCourante == null || _horloge == null)
            return;
        MembreGuilde membre = _registreMembres?.Membres.FirstOrDefault(
            m => m.Id == _reclamationCourante.MembreId);
        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        if (membre == null || joueur == null)
        {
            TerminerReclamationPolitique();
            return;
        }

        int calme = membre.Statistiques?.GetValueOrDefault("Calme", 10) ?? 10;
        int charisme = joueur.Statistiques?.GetValueOrDefault("Charisme", 10) ?? 10;
        int leadership = joueur.Statistiques?.GetValueOrDefault("Leadership", 10) ?? 10;
        int bonusCalme = ReglesCaracteristiques.Modificateur(Math.Clamp(calme, 1, 20));
        int bonusCharisme =
            ReglesCaracteristiques.Modificateur(Math.Clamp(charisme, 1, 20));
        int bonusLeadership =
            ReglesCaracteristiques.Modificateur(Math.Clamp(leadership, 1, 20));

        // Un membre plus calme est plus ouvert à une explication.
        // Un seul D20 et les deux modificateurs du maître de guilde.
        int cible = Math.Clamp(12 - bonusCalme, 5, 20);
        int de = Random.Shared.Next(1, 21);
        int total = de + bonusCharisme + bonusLeadership;
        bool succes = total >= cible;
        _reclamationPolitiqueResultatAffiche = true;

        _resultatDiscussion.Text =
            membre.NomComplet + " : Je ne suis pas satisfait du changement de politique de " +
            "la guilde (" + _reclamationCourante.Motif + ")." +
            "\n\nVous : Cette décision a été prise dans l'intérêt de la guilde. " +
            "Laissez-moi vous expliquer notre choix." +
            "\n\nJet de Charisme + Leadership : D20 " + de +
            " + (" + bonusCharisme + ") + (" + bonusLeadership + ") = " + total +
            " / cible " + cible + " (Calme du membre : " + calme + ")." +
            "\n\n" + membre.Prenom + " : " +
            (succes
                ? "Je ne suis pas forcément d'accord, mais je comprends mieux vos raisons. " +
                  "Je vais vous laisser une chance."
                : "Je vous ai entendu, mais vos explications ne me convainquent pas. " +
                  "Je reste opposé à ce changement.") +
            "\n\n" + (succes
                ? "Vous avez réussi à apaiser cette contestation."
                : "La contestation n'est pas apaisée.");

        string resultat = succes ? "apaisée" : "non apaisée";
        AjouterEntreeJournal("Politique de guilde : contestation de " +
            membre.NomComplet + " (" + _reclamationCourante.Motif + ") " +
            resultat + " (D20 " + total + "/" + cible + ").", "politique");
        _historiqueMembres?.Ajouter(membre, _horloge.Instant, "ContestationPolitique",
            "Contestation de la politique (" + _reclamationCourante.Motif +
            ") : " + resultat + " (D20 " + total + "/" + cible + ").");

        // Aucun effet chiffré sur la relation/le bonheur n'a été défini :
        // le résultat est historisé mais ces ressources restent inchangées.
        _listeReponsesDiscussion.Clear();
        _listeReponsesDiscussion.AddItem("Terminer cet échange");
    }

    private void TerminerReclamationPolitique()
    {
        if (!_dialogueReclamationPolitiqueActif) return;
        if (_reclamationCourante != null)
            _reclamationsPolitique?.EnAttente.Remove(_reclamationCourante);
        _reclamationCourante = null;
        _dialogueReclamationPolitiqueActif = false;
        _reclamationPolitiqueResultatAffiche = false;
        _fenetreDiscussion?.Hide();
        _reponsesDiscussion?.Hide();
        _sujetsDiscussion?.Show();

        // Chaque contestation suivante s'ouvre automatiquement, sans
        // permettre au joueur de court-circuiter la file de dialogues.
        CallDeferred(nameof(VerifierReclamationsPolitique));
    }

    /// <summary>
    /// Un membre déjà signalé, mais en mission, ne parle qu'une fois revenu.
    /// La fin de mission est le seul moment où ce rappel est déclenché ici.
    /// </summary>
    private void ReclamationPolitiqueAuRetourMission(IEnumerable<MembreGuilde> rentrants)
    {
        if (rentrants == null || _reclamationsPolitique == null ||
            !rentrants.Any(m => ReclamationPolitiquePourMembre(m.Id)))
            return;

        AjouterEntreeJournal("Politique de guilde : un membre revenu de mission " +
            "demande à être entendu au sujet des décisions de la guilde.", "politique");
        if (!_traitementTempsDiscussion && _tempsEnCours)
            InterrompreAvanceeTemps("Un membre revient de mission et souhaite une discussion.");
        CallDeferred(nameof(VerifierReclamationsPolitique));
    }
}
