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
            if (_reclamationPolitiqueResultatAffiche)
            {
                if (choix == 0) TerminerReclamationPolitique();
                return;
            }
            if (choix == 0)
                ResoudreReclamationPolitique();
            else if (choix == 1 && PeutRetablirPolitiqueConteste(_reclamationCourante))
                RetablirPolitiqueContestee();
        };

        // Les protestations encore en attente dans une sauvegarde reprennent
        // après les autres initialisations, sans en créer de nouvelles.
        CallDeferred(nameof(VerifierReclamationsPolitique));
    }

    /// <summary>
    /// Place les protestations décidées par les règles de politique.
    /// Mémorise pour chacune la valeur à rétablir si le joueur se ravise.
    /// </summary>
    private void SignalerMecontentementsPolitique(
        IEnumerable<(MembreGuilde Membre, string Politique)> mecontentements,
        string clePolitique, int valeurAvantChangement)
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
                DateSignalement = ReglesMissions.DateIso(_horloge.Instant),
                ClePolitique = clePolitique,
                ValeurAvantChangement = valeurAvantChangement
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

    /// <summary>
    /// Chance de tolérer un durcissement de politique selon la relation avec
    /// le maître de guilde. Les membres neutres ou hostiles protestent toujours.
    /// </summary>
    private static bool PasseOutrePolitiqueSelonRelation(MembreGuilde joueur,
        MembreGuilde membre)
    {
        int chance = RelationsMembres.Qualifier(
            RelationsMembres.Lire(joueur, membre)) switch
        {
            "Connaissance" => 10,
            "Ami" => 30,
            "Proche" => 75,
            _ => 0
        };
        return chance > 0 && Random.Shared.Next(100) < chance;
    }

    /// <summary>
    /// Première règle de mécontentement : une hausse du pourcentage que la
    /// guilde prélève sur les missions. Le seuil est propre à chaque membre
    /// et n'est jamais révélé à l'interface.
    /// Seule une politique CONFIRMÉE appelle cette méthode.
    /// </summary>
    private void EvaluerProtestationsPourcentageMissions(int ancienPourcentage,
        int nouveauPourcentage)
    {
        if (nouveauPourcentage <= ancienPourcentage ||
            _registreMembres?.Membres == null || _reclamationsPolitique == null)
            return;

        MembreGuilde joueur = _registreMembres.Membres.FirstOrDefault(m => m.EstJoueur);
        if (joueur == null) return;

        const string prefixeMotif =
            "Pourcentage récupéré par la guilde sur les missions : ";
        string motifActuel = prefixeMotif + nouveauPourcentage + " %";
        List<(MembreGuilde Membre, string Politique)> protestataires = new();
        foreach (MembreGuilde membre in _registreMembres.Membres)
        {
            if (membre == null || membre.EstJoueur || membre.EstMaitreDeGuilde ||
                nouveauPourcentage <= membre.SeuilPourcentageGuildeMissions)
                continue;

            if (PasseOutrePolitiqueSelonRelation(joueur, membre))
            {
                // Le membre accepte définitivement ce nouveau taux et tout
                // taux inférieur. Une hausse ultérieure peut être contestée.
                membre.SeuilPourcentageGuildeMissions = nouveauPourcentage;

                // Si le membre était encore absent lors d'une précédente
                // hausse, son acceptation de ce taux annule l'ancienne
                // protestation devenue sans objet.
                _reclamationsPolitique.EnAttente.RemoveAll(r =>
                    r.MembreId == membre.Id &&
                    r.Motif.StartsWith(prefixeMotif, StringComparison.Ordinal));
                continue;
            }

            // L'ancien seuil est une caractéristique cachée : ne jamais
            // l'insérer dans le motif montré au joueur ou dans le journal.
            // Une hausse successive met à jour la protestation déjà en
            // attente pour ce membre plutôt que de lui en ajouter une autre.
            ReclamationPolitique dejaEnAttente =
                _reclamationsPolitique.EnAttente.FirstOrDefault(r =>
                    r.MembreId == membre.Id &&
                    r.Motif.StartsWith(prefixeMotif, StringComparison.Ordinal));
            if (dejaEnAttente != null)
                dejaEnAttente.Motif = motifActuel;
            else
                protestataires.Add((membre, motifActuel));
        }

        SignalerMecontentementsPolitique(protestataires,
            ReclamationPolitique.PrelevementMissions, ancienPourcentage);
        if (ReclamationPolitiqueDisponible())
            CallDeferred(nameof(VerifierReclamationsPolitique));
    }

    /// <summary>
    /// Deuxième politique : écart MAXIMAL autorisé entre le rang des membres
    /// et celui des missions INFÉRIEURES. Les catégories de tolérance sont :
    /// 11 (libre), 1..10 (limité), 0 (strictement le même rang).
    /// Seul le passage à une catégorie plus contraignante peut créer une
    /// nouvelle réclamation. Un réglage moins strict peut annuler une
    /// réclamation devenue sans objet.
    /// </summary>
    private void EvaluerProtestationsEcartMissions(int ancienEcart, int nouvelEcart)
    {
        if (_registreMembres?.Membres == null || _reclamationsPolitique == null)
            return;

        const string prefixeMotif = "Écart de rang inférieur autorisé : ";
        string motifActuel = prefixeMotif + TexteEcartMissionPolitique(nouvelEcart);
        bool durcissement =
            EtatPolitiqueGuilde.IntensiteContrainteEcartMission(nouvelEcart) >
            EtatPolitiqueGuilde.IntensiteContrainteEcartMission(ancienEcart);

        MembreGuilde joueur = _registreMembres.Membres.FirstOrDefault(m => m.EstJoueur);
        if (joueur == null) return;

        List<(MembreGuilde Membre, string Politique)> protestataires = new();
        foreach (MembreGuilde membre in _registreMembres.Membres)
        {
            if (membre == null || membre.EstJoueur || membre.EstMaitreDeGuilde)
                continue;

            ReclamationPolitique enAttente =
                _reclamationsPolitique.EnAttente.FirstOrDefault(r =>
                    r.MembreId == membre.Id &&
                    r.Motif.StartsWith(prefixeMotif, StringComparison.Ordinal));

            // Les membres qui acceptent la nouvelle politique n'ont plus de
            // raison de garder une protestation différée (mission, concours).
            if (EtatPolitiqueGuilde.MembreAccepteEcartMission(
                    membre.SeuilEcartNiveauMission, nouvelEcart))
            {
                if (enAttente != null)
                    _reclamationsPolitique.EnAttente.Remove(enAttente);
                continue;
            }

            if (durcissement && PasseOutrePolitiqueSelonRelation(joueur, membre))
            {
                // Accepter l'égalité revient à accepter tous les écarts.
                // Accepter la catégorie 1..10 vaut pour chaque valeur de 1 à 10.
                membre.SeuilEcartNiveauMission = nouvelEcart == 0 ? 0 : 1;
                if (enAttente != null)
                    _reclamationsPolitique.EnAttente.Remove(enAttente);
                continue;
            }

            // Si le membre est déjà en mission, ne pas empiler plusieurs
            // plaintes de rang. Son entretien portera sur la politique
            // réellement en vigueur à son retour.
            if (enAttente != null)
                enAttente.Motif = motifActuel;
            else if (durcissement)
                protestataires.Add((membre, motifActuel));
        }

        SignalerMecontentementsPolitique(protestataires,
            ReclamationPolitique.EcartRangMissions, ancienEcart);
        if (ReclamationPolitiqueDisponible())
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

    /// <summary>
    /// Revenir sur sa décision signifie rétablir la valeur confirmée AVANT
    /// le changement ayant créé cette protestation. Les autres paramètres
    /// de politique ne sont jamais affectés.
    /// </summary>
    private bool PeutRetablirPolitiqueConteste(ReclamationPolitique reclamation)
    {
        if (_politiqueGuilde == null ||
            reclamation?.ValeurAvantChangement is not int valeur)
            return false;

        return reclamation.ClePolitique switch
        {
            ReclamationPolitique.PrelevementMissions =>
                valeur >= 0 && valeur <= 50 &&
                _politiqueGuilde.PourcentageGuildeMissions != valeur,
            ReclamationPolitique.EcartRangMissions =>
                valeur >= 0 && valeur <= 11 &&
                _politiqueGuilde.EcartNiveauMission != valeur,
            _ => false
        };
    }

    private void RetablirPolitiqueContestee()
    {
        if (!_dialogueReclamationPolitiqueActif ||
            !PeutRetablirPolitiqueConteste(_reclamationCourante))
            return;

        ReclamationPolitique reclamation = _reclamationCourante;
        int precedent = reclamation.ValeurAvantChangement.Value;
        string nouveauLibelle;
        switch (reclamation.ClePolitique)
        {
            case ReclamationPolitique.PrelevementMissions:
                _politiqueGuilde.PourcentageGuildeMissions = precedent;
                nouveauLibelle = "Part de la guilde sur les missions : " + precedent + " %";
                break;
            case ReclamationPolitique.EcartRangMissions:
                _politiqueGuilde.EcartNiveauMission = precedent;
                nouveauLibelle = "Écart de rang inférieur autorisé : " +
                    TexteEcartMissionPolitique(precedent);
                break;
            default:
                return;
        }

        _politiqueGuilde.Normaliser();
        _politiqueBrouillon = CopierPolitiqueGuilde(_politiqueGuilde);
        // On ne réapplique pas les évaluations de mécontentement : la mesure
        // est assouplie et ne doit pas générer de nouvelles protestations.
        int annulees = MettreAJourReclamationsApresRetrait(reclamation);

        _reclamationPolitiqueResultatAffiche = true;
        _resultatDiscussion.Text =
            "Vous : Après réflexion, je reviens sur ce changement de politique." +
            "\n\n" + nouveauLibelle + "." +
            "\n\nLa décision précédente est rétablie. Aucun jet n'est nécessaire." +
            (annulees > 0
                ? "\n" + annulees + " autre(s) contestation(s) sont devenues sans objet."
                : "");

        AjouterEntreeJournal("Politique de guilde : retour sur la décision (" +
            nouveauLibelle + "), à la suite de la contestation de " +
            (_registreMembres?.Membres.FirstOrDefault(m =>
                m.Id == reclamation.MembreId)?.NomComplet ?? "un membre") + ".", "politique");
        _listeReponsesDiscussion.Clear();
        _listeReponsesDiscussion.AddItem("Terminer cet échange");
    }

    /// <summary>
    /// Après un retour en arrière, les membres qui tolèrent le taux restauré
    /// ne contestent plus. Les autres restent dans la file, avec le motif
    /// correspondant à la politique effectivement en vigueur.
    /// </summary>
    private int MettreAJourReclamationsApresRetrait(ReclamationPolitique courante)
    {
        if (_reclamationsPolitique?.EnAttente == null ||
            _registreMembres?.Membres == null)
            return 0;

        int annulees = 0;
        foreach (ReclamationPolitique demande in
            _reclamationsPolitique.EnAttente.ToList())
        {
            if (ReferenceEquals(demande, courante) ||
                demande.ClePolitique != courante.ClePolitique)
                continue;

            MembreGuilde membre = _registreMembres.Membres.FirstOrDefault(
                m => m.Id == demande.MembreId);
            bool accepte = membre == null ||
                (demande.ClePolitique == ReclamationPolitique.PrelevementMissions
                    ? _politiqueGuilde.PourcentageGuildeMissions <=
                        membre.SeuilPourcentageGuildeMissions
                    : EtatPolitiqueGuilde.MembreAccepteEcartMission(
                        membre.SeuilEcartNiveauMission,
                        _politiqueGuilde.EcartNiveauMission));

            if (accepte)
            {
                _reclamationsPolitique.EnAttente.Remove(demande);
                annulees++;
            }
            else
            {
                demande.Motif = demande.ClePolitique ==
                    ReclamationPolitique.PrelevementMissions
                    ? "Pourcentage récupéré par la guilde sur les missions : " +
                        _politiqueGuilde.PourcentageGuildeMissions + " %"
                    : "Écart de rang inférieur autorisé : " +
                        TexteEcartMissionPolitique(_politiqueGuilde.EcartNiveauMission);
            }
        }
        return annulees;
    }

    private void ReafficherReclamationPolitique()
    {
        if (!_dialogueReclamationPolitiqueActif || _reclamationCourante == null)
            return;
        MembreGuilde membre = _registreMembres?.Membres.FirstOrDefault(
            m => m.Id == _reclamationCourante.MembreId);
        if (membre == null && !_reclamationPolitiqueResultatAffiche)
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

        if (!_reclamationPolitiqueResultatAffiche && membre != null)
            _resultatDiscussion.Text = membre.NomComplet +
                " : Je ne suis pas satisfait du changement de politique de la guilde (" +
                _reclamationCourante.Motif + "). Je voudrais comprendre pourquoi " +
                "vous avez pris cette décision. Je crains qu'elle ne nuise à nos membres." +
                "\n\nVous devez répondre à cette contestation.";
        _listeReponsesDiscussion.Clear();
        if (_reclamationPolitiqueResultatAffiche)
            _listeReponsesDiscussion.AddItem("Terminer cet échange");
        else
        {
            _listeReponsesDiscussion.AddItem(
                "Défendre votre décision (D20 Charisme et D20 Leadership)");
            if (PeutRetablirPolitiqueConteste(_reclamationCourante))
                _listeReponsesDiscussion.AddItem("Revenir sur le changement de politique");
        }
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
        int bonusCalme =
            ReglesCaracteristiques.Modificateur(Math.Clamp(calme, 1, 20));
        int bonusCharisme =
            ReglesCaracteristiques.Modificateur(Math.Clamp(charisme, 1, 20));
        int bonusLeadership =
            ReglesCaracteristiques.Modificateur(Math.Clamp(leadership, 1, 20));

        // Deux D20 indépendants, contre UNE même cible déterminée par le
        // Calme de la cible. Les deux jets doivent réussir pour convaincre.
        int cible = Math.Clamp(12 - bonusCalme, 5, 20);
        int deCharisme = Random.Shared.Next(1, 21);
        int deLeadership = Random.Shared.Next(1, 21);
        int totalCharisme = deCharisme + bonusCharisme;
        int totalLeadership = deLeadership + bonusLeadership;
        bool reussiteCharisme = totalCharisme >= cible;
        bool reussiteLeadership = totalLeadership >= cible;
        bool succes = reussiteCharisme && reussiteLeadership;
        _reclamationPolitiqueResultatAffiche = true;

        int bonheurAvant = membre.Bonheur;
        bool depart = false;
        if (!succes)
        {
            membre.Bonheur = Math.Clamp(membre.Bonheur - 60, 0, 100);
            _historiqueMembres?.Ajouter(membre, _horloge.Instant, "Bonheur",
                "Contestation de politique : bonheur " + bonheurAvant +
                " → " + membre.Bonheur + " (-60).",
                avant: bonheurAvant, apres: membre.Bonheur);
            depart = membre.Bonheur <= 0;
        }

        _resultatDiscussion.Text =
            membre.NomComplet + " : Je ne suis pas satisfait du changement de politique de " +
            "la guilde (" + _reclamationCourante.Motif + ")." +
            "\n\nVous : Cette décision a été prise dans l'intérêt de la guilde. " +
            "Laissez-moi vous expliquer notre choix." +
            "\n\nJet de Charisme : D20 " + deCharisme + " + (" + bonusCharisme +
            ") = " + totalCharisme + " / cible " + cible +
            " — " + (reussiteCharisme ? "réussite" : "échec") + "." +
            "\nJet de Leadership : D20 " + deLeadership + " + (" + bonusLeadership +
            ") = " + totalLeadership + " / cible " + cible +
            " — " + (reussiteLeadership ? "réussite" : "échec") + "." +
            "\n\n" + membre.Prenom + " : " +
            (succes
                ? "Je ne suis pas forcément d'accord, mais je comprends mieux vos raisons. " +
                  "Je vais vous laisser une chance."
                : "Je vous ai entendu, mais vos explications ne me convainquent pas. " +
                  "Je reste opposé à ce changement.") +
            "\n\n" + (succes
                ? "Vous avez réussi à apaiser cette contestation."
                : "La contestation n'est pas apaisée. Bonheur : " +
                  bonheurAvant + " → " + membre.Bonheur + " (-60).") +
            (depart
                ? "\n" + membre.NomComplet +
                  " est à bout et décide de quitter définitivement la guilde."
                : "");

        string resultat = succes ? "apaisée" : "non apaisée";
        AjouterEntreeJournal("Politique de guilde : contestation de " +
            membre.NomComplet + " (" + _reclamationCourante.Motif + ") " +
            resultat + " (Charisme " + totalCharisme + "/" + cible +
            ", Leadership " + totalLeadership + "/" + cible + ")." +
            (succes ? "" : " Bonheur : " + bonheurAvant + " → " + membre.Bonheur + "."),
            "politique");
        _historiqueMembres?.Ajouter(membre, _horloge.Instant, "ContestationPolitique",
            "Contestation de la politique (" + _reclamationCourante.Motif +
            ") : " + resultat + " (Charisme " + totalCharisme + "/" + cible +
            ", Leadership " + totalLeadership + "/" + cible + ").");

        if (depart)
            FaireQuitterGuildeApresContestation(membre);

        _listeReponsesDiscussion.Clear();
        _listeReponsesDiscussion.AddItem("Terminer cet échange");
    }

    /// <summary>
    /// Départ immédiat après une contestation ratée ayant amené le bonheur
    /// à zéro. Met à jour les statistiques globales et libère les équipes
    /// dont il ne reste qu'un membre.
    /// </summary>
    private void FaireQuitterGuildeApresContestation(MembreGuilde membre)
    {
        if (membre == null || membre.EstJoueur || membre.EstMaitreDeGuilde ||
            _registreMembres?.Membres.Remove(membre) != true)
            return;

        _historiqueMembres?.Ajouter(membre, _horloge.Instant, "DepartGuilde",
            "A quitté la guilde après une contestation de politique non apaisée.");
        AjouterEntreeJournal(membre.NomComplet +
            " quitte la guilde après une contestation de sa politique.", "politique");

        _reclamationsPolitique?.EnAttente.RemoveAll(r =>
            r.MembreId == membre.Id && !ReferenceEquals(r, _reclamationCourante));
        if (_membreConsulte?.Id == membre.Id)
            _membreConsulte = null;
        if (_cibleDiscussion?.Id == membre.Id)
            _cibleDiscussion = null;

        // Les membres survivants d'une équipe réduite à un seul personnage
        // retrouvent leur statut de membres indépendants.
        foreach (MembreGuilde solo in _registreMembres.Membres
            .Where(m => !string.IsNullOrWhiteSpace(m.EquipeId))
            .GroupBy(m => m.EquipeId)
            .Where(g => g.Count() == 1)
            .Select(g => g.First()))
            solo.EquipeId = "";

        _registreMembres.RecalculerClassement();
        _etat.Membres = _registreMembres.Membres.Count;
        _etat.Pouvoir = _registreMembres.Membres.Sum(m => (long)m.NiveauMagique);
        _etat.Savoir = _registreMembres.Membres.Sum(m => (long)m.ConnaissanceMagique);
        AfficherValeur(_membres, _etat.Membres);
        AfficherValeur(_pouvoir, _etat.Pouvoir);
        AfficherValeur(_savoir, _etat.Savoir);
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
