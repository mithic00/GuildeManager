using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Premiers dialogues selon InfoJeu/Discussion.txt.</summary>
public partial class MainMenu
{
    private Window _fenetreDiscussion;
    private RichTextLabel _resultatDiscussion;
    private TextureRect _portraitDiscussionCible, _portraitDiscussionJoueur;
    private ItemList _sujetsDiscussion, _listeReponsesDiscussion;
    private Control _reponsesDiscussion;
    private enum SujetDiscussion { FaireConnaissance, ConversationAmicale, RemonterMoral, DemanderMission, Fermer }
    private readonly List<SujetDiscussion> _sujetsDisponibles = new();
    private MembreGuilde _cibleDiscussion;
    private bool _connaissanceAContinuer;
    private bool _discussionEnCours;
    // L'échange de soutien n'est résolu qu'après un choix explicite du joueur.
    private bool _soutienMoralEnAttente;

    private void InitialiserDiscussions()
    {
        const string baseDialogue = "FenetreDiscussion/LayoutConversation/Layout/";
        const string interfaceDialogue = baseDialogue + "InterfaceInteractive/";
        _fenetreDiscussion = GetNode<Window>("FenetreDiscussion");
        _resultatDiscussion = GetNode<RichTextLabel>(baseDialogue + "RésultatDiscussion");
        _portraitDiscussionCible = GetNode<TextureRect>(interfaceDialogue + "PortraitCible");
        _portraitDiscussionJoueur = GetNode<TextureRect>(interfaceDialogue + "PortraitJoueur");
        _sujetsDiscussion = GetNode<ItemList>(interfaceDialogue + "ListeSujetDiscussion");
        _listeReponsesDiscussion = GetNode<ItemList>(interfaceDialogue + "ListeReponsesConversation");
        _reponsesDiscussion = _listeReponsesDiscussion;
        _sujetsDiscussion.ItemSelected += index =>
        {
            if (_filRougeDialogueActif || _dialogueReclamationPolitiqueActif ||
                _discussionEnCours) return;
            int choix = (int)index;
            _sujetsDiscussion.DeselectAll();
            if (choix < 0 || choix >= _sujetsDisponibles.Count) return;

            switch (_sujetsDisponibles[choix])
            {
                case SujetDiscussion.FaireConnaissance:
                    ResoudreDiscussion(false, false);
                    break;
                case SujetDiscussion.ConversationAmicale:
                    ResoudreDiscussion(true, false);
                    break;
                case SujetDiscussion.RemonterMoral:
                    ProposerSoutienMoral();
                    break;
                case SujetDiscussion.DemanderMission:
                    ProposerDialogueDemandeMission();
                    break;
                case SujetDiscussion.Fermer:
                    FermerDiscussion();
                    break;
            }
        };
        _listeReponsesDiscussion.ItemSelected += index =>
        {
            if (_filRougeDialogueActif || _dialogueReclamationPolitiqueActif)
                return; // Chaque dialogue imposé gère ses propres réponses.
            int choix = (int)index;
            _listeReponsesDiscussion.DeselectAll();
            if (_choixMissionEnAttente || _confirmationMissionEnAttente)
            {
                TraiterChoixDialogueDemandeMission(choix);
                return;
            }
            if (_soutienMoralEnAttente)
            {
                _soutienMoralEnAttente = false;
                if (choix == 0)
                    ResoudreSoutienMoral();
                else
                    FermerDiscussion();
                return;
            }
            if (choix == 0 && _connaissanceAContinuer)
                ResoudreDiscussion(false, true);
            else
                FermerDiscussion();
        };
        _fenetreDiscussion.CloseRequested += () =>
        {
            if (_filRougeDialogueActif)
            {
                // Ne pas fermer ni perdre un dialogue scénarisé en cours.
                CallDeferred(nameof(ReprendreDialogueFilRouge));
                return;
            }
            if (_dialogueReclamationPolitiqueActif)
            {
                // Le joueur doit répondre : la fermeture native n'annule pas l'entretien.
                CallDeferred(nameof(ReafficherReclamationPolitique));
                return;
            }
            FermerDiscussion();
        };
        _fenetreDiscussion.Hide();
        _reponsesDiscussion.Hide();
    }

    private static bool JetCommunication(MembreGuilde membre, int cible,
        out int de, out int modificateur)
    {
        de = Random.Shared.Next(1, 21);
        int stat = membre.Statistiques?.GetValueOrDefault("Communication", 10) ?? 10;
        modificateur = ReglesCaracteristiques.Modificateur(Math.Clamp(stat, 1, 20));
        return de + modificateur >= cible;
    }

    // Seule l'heure de DÉBUT est limitée : de 08h00 à 23h59.
    // Une discussion commencée avant minuit peut se terminer après.
    private static bool HoraireDiscussionDisponible(DateTime instant) =>
        instant.Hour >= 8;

    private bool DialogueAccessible(MembreGuilde cible, bool afficherErreur,
        bool verifierHoraire = true)
    {
        if (verifierHoraire && _horloge != null &&
            !HoraireDiscussionDisponible(_horloge.Instant))
        {
            if (afficherErreur)
                AfficherInformation(
                    "Vous ne pouvez pas entamer une conversation entre 00h00 et 07h59.");
            return false;
        }
        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        if (cible != null && joueur != null && cible.Id != joueur.Id &&
            _registreMembres.Membres.Contains(cible) && !EstAbsent(cible) && _horloge != null)
        {
            if (DateTime.TryParseExact(cible.DiscussionBloqueeJusqua,
                "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime fin) && _horloge.Instant < fin)
            {
                if (afficherErreur)
                    AfficherInformation("Ce membre ne souhaite plus discuter avant le " +
                        HorlogeJeu.Formater(fin) + ".");
                return false;
            }
            return true;
        }
        if (afficherErreur)
            AfficherInformation("Ce membre est absent ou indisponible pour discuter.");
        return false;
    }

    private void OuvrirDiscussion()
    {
        if (_tempsEnCours || _traitementTempsDiscussion)
        {
            AfficherInformation("Arrêtez l'avancée du temps avant de discuter.");
            return;
        }
        if (!DialogueAccessible(_membreConsulte, true)) return;
        _cibleDiscussion = _membreConsulte;
        ReinitialiserDialogueDemandeMission();
        _soutienMoralEnAttente = false;
        _connaissanceAContinuer = false;
        _discussionEnCours = false;
        _resultatDiscussion.Text = "Choisissez un sujet pour discuter avec " +
            _cibleDiscussion.NomComplet + ".";
        const string portraitDefaut = "res://Images/Portraits/GrandFormat/Portait_Defaut.png";
        _portraitDiscussionCible.Texture = ResourceLoader.Load<Texture2D>(portraitDefaut);
        MembreGuilde joueur = _registreMembres.Membres.FirstOrDefault(m => m.EstJoueur);
        string[] races = { "Humain", "ElfeGris", "Grumol", "HumainDesMonts",
            "HautElfe", "Nain", "Tanneur", "Khajiit" };
        string race = joueur != null && joueur.ApparenceId > 0 &&
            joueur.ApparenceId <= races.Length ? races[joueur.ApparenceId - 1] : "";
        string chemin = "res://Images/Portraits/GrandFormat/" + race +
            (joueur?.Sexe == "F" ? "_Femme.png" : "_Homme.png");
        _portraitDiscussionJoueur.Texture = ResourceLoader.Load<Texture2D>(
            ResourceLoader.Exists(chemin) ? chemin : portraitDefaut);
        _portraitDiscussionCible.MouseFilter = Control.MouseFilterEnum.Stop;
        ActualiserInfobulleTraitsDiscussion();
        ActualiserBoutonsSujetsDiscussion();
        _sujetsDiscussion.Show();
        _reponsesDiscussion.Hide();
        _fenetreFiche.Hide();
        _fenetreDiscussion.PopupCentered();
    }

    private static string DateJourDiscussion(DateTime instant) =>
        instant.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Les mêmes caractéristiques servent à décider si « Faire connaissance »
    /// doit apparaître et à sélectionner celles que le dialogue peut révéler.
    /// </summary>
    private static IEnumerable<string> CaracteristiquesInconnuesDiscussion(MembreGuilde membre)
    {
        if (membre == null) return Enumerable.Empty<string>();
        return GenerateurCandidats.Stats
            .Concat(new[] { "Motivation" })
            .Distinct(StringComparer.Ordinal)
            .Where(s => !membre.ConnaitStatistique(s) &&
                (s == "Motivation" ? membre.Motivation.HasValue :
                    membre.Statistiques?.ContainsKey(s) == true));
    }

    private void AjouterSujetDiscussion(string texte, SujetDiscussion sujet,
        bool desactive = false, string infobulle = "")
    {
        int index = _sujetsDiscussion.ItemCount;
        _sujetsDiscussion.AddItem(texte);
        _sujetsDisponibles.Add(sujet);
        _sujetsDiscussion.SetItemDisabled(index, desactive);
        if (!string.IsNullOrEmpty(infobulle))
            _sujetsDiscussion.SetItemTooltip(index, infobulle);
    }

    /// <summary>
    /// Reconstruit l'ItemList à chaque ouverture et après les échanges :
    /// seuls les sujets autorisés sont proposés, avec leur état actualisé.
    /// </summary>
    private void ActualiserBoutonsSujetsDiscussion()
    {
        _sujetsDiscussion.Clear();
        _sujetsDisponibles.Clear();

        if (CaracteristiquesInconnuesDiscussion(_cibleDiscussion).Any())
            AjouterSujetDiscussion("Faire connaissance", SujetDiscussion.FaireConnaissance);

        AjouterSujetDiscussion("Conversation amicale", SujetDiscussion.ConversationAmicale);

        if (_cibleDiscussion != null && _cibleDiscussion.Moral <= 30)
        {
            bool dejaTente = _horloge == null ||
                _cibleDiscussion.DerniereTentativeRemonterMoral ==
                    DateJourDiscussion(_horloge.Instant);
            AjouterSujetDiscussion("Remonter le moral", SujetDiscussion.RemonterMoral,
                dejaTente,
                dejaTente
                    ? "Vous avez déjà tenté de remonter le moral de ce membre aujourd'hui."
                    : "D20 Empathie, 20 minutes, une fois par jour.");
        }

        if (PeutDemanderMission())
        {
            bool bloquee = DemandeMissionTemporairementBloquee(
                _cibleDiscussion, out DateTime disponibleLe);
            AjouterSujetDiscussion("Demander une mission", SujetDiscussion.DemanderMission,
                bloquee,
                bloquee ? "Ce membre a refusé une mission. Nouvelle demande possible le " +
                    HorlogeJeu.Formater(disponibleLe) + "." :
                    "Demander à ce membre de prendre une mission (D20 Leadership).");
        }

        AjouterSujetDiscussion("Fermer", SujetDiscussion.Fermer);
    }

    private void ProposerSoutienMoral()
    {
        if (_discussionEnCours || _cibleDiscussion == null || _horloge == null)
            return;
        if (!DialogueAccessible(_cibleDiscussion, true, verifierHoraire: false))
        {
            FermerDiscussion();
            return;
        }
        if (_cibleDiscussion.DerniereTentativeRemonterMoral ==
                DateJourDiscussion(_horloge.Instant) || _cibleDiscussion.Moral > 30)
        {
            ActualiserBoutonsSujetsDiscussion();
            return;
        }

        _connaissanceAContinuer = false;
        _soutienMoralEnAttente = true;
        _resultatDiscussion.Text =
            "Vous : Tu n'as pas l'air dans ton assiette en ce moment. Quelque chose ne va pas ?" +
            "\n" + _cibleDiscussion.Prenom +
            " : C'est vrai que ces derniers temps, ce n'est pas facile...";

        // Pas de jet ni de dépense de temps à ce stade : le joueur choisit.
        _sujetsDiscussion.Hide();
        _reponsesDiscussion.Show();
        _listeReponsesDiscussion.Clear();
        _listeReponsesDiscussion.AddItem(
            "Si tu as besoin de parler, je suis là. On est une équipe après tout. (D20 Empathie)");
        _listeReponsesDiscussion.AddItem("Mettre fin à la conversation");
    }

    private void ResoudreSoutienMoral()
    {
        if (_discussionEnCours || _cibleDiscussion == null ||
            _registreMembres == null || _horloge == null)
            return;
        if (!DialogueAccessible(_cibleDiscussion, true, verifierHoraire: false))
        {
            FermerDiscussion();
            return;
        }

        MembreGuilde cible = _cibleDiscussion;
        if (cible.DerniereTentativeRemonterMoral ==
            DateJourDiscussion(_horloge.Instant))
        {
            ActualiserBoutonsSujetsDiscussion();
            return;
        }
        if (cible.Moral > 30)
        {
            ActualiserBoutonsSujetsDiscussion();
            return;
        }
        MembreGuilde joueur = _registreMembres.Membres.FirstOrDefault(m => m.EstJoueur);
        if (joueur == null) return;

        _discussionEnCours = true;
        _connaissanceAContinuer = false;

        // Consommer la tentative dès le lancement du jet et enregistrer
        // le jour initial, même si les 20 minutes dépassent minuit.
        cible.DerniereTentativeRemonterMoral = DateJourDiscussion(_horloge.Instant);
        int moralAvant = cible.Moral;
        int relationAvant = RelationsMembres.Lire(joueur, cible);
        int empathie = joueur.Statistiques?.GetValueOrDefault("Empathie", 10) ?? 10;
        int bonus = ReglesCaracteristiques.Modificateur(Math.Clamp(empathie, 1, 20));
        int de = Random.Shared.Next(1, 21);
        int total = de + bonus;
        bool critique = de == 20;
        bool reussite = critique || total >= 12;
        int gain = critique ? 20 : reussite ? 10 : 0;
        cible.Moral = Math.Clamp(cible.Moral + gain, 0, 100);
        int gainReel = cible.Moral - moralAvant;
        // La relation progresse uniquement si le moral était inférieur à 20
        // AVANT l'échange et si le jet d'Empathie a réussi (20 naturel compris).
        bool bonusRelation = reussite && moralAvant < 20;
        int relationApres = bonusRelation
            ? RelationsMembres.Ajuster(joueur, cible, 5) : relationAvant;
        int gainRelationReel = relationApres - relationAvant;

        string signe = bonus >= 0 ? "+" : "-";
        string jet = "D20 " + de + " " + signe + " " + Math.Abs(bonus) +
            " = " + total + " (" + (reussite ? "réussite" : "échec") + ")";
        string replique = reussite
            ? "Merci, ça fait du bien d'entendre ça. Je vais essayer de reprendre du poil de la bête !"
            : "Merci... Mais j'ai surtout besoin d'un peu de temps pour moi.";

        _resultatDiscussion.Text =
            "Vous : Si tu as besoin de parler, je suis là. On est une équipe après tout." +
            "\n" + cible.Prenom + " : " + replique +
            "\n\nJet d'Empathie : " + jet +
            (critique ? "\nRéussite critique !" : "") +
            "\nMoral : " + moralAvant + " → " + cible.Moral +
            (gainReel > 0 ? " (+" + gainReel + ")" : " (inchangé)") +
            (bonusRelation ? "\nRelation : " + relationAvant + " → " +
                relationApres + (gainRelationReel > 0 ?
                    " (+" + gainRelationReel + ")" : " (plafond atteint)") : "") +
            "\nTemps écoulé : 20 minutes.";

        AjouterEntreeJournal("Discussion de soutien moral avec " + cible.NomComplet +
            " : " + (reussite ? "réussite" : "échec") +
            ", moral " + moralAvant + " → " + cible.Moral +
            (bonusRelation ? ", relation " + relationAvant + " → " + relationApres : "") +
            ".", "discussion");
        _historiqueMembres?.Ajouter(cible, _horloge.Instant,
            "SoutienMoral", "Soutien moral du maître de guilde : " +
            (reussite ? "réussite" : "échec") + " (" + jet + ").");
        if (gainReel > 0)
            _historiqueMembres?.Ajouter(cible, _horloge.Instant,
                "Moral", "Moral : " + moralAvant + " → " + cible.Moral +
                " (soutien du maître de guilde).",
                avant: moralAvant, apres: cible.Moral);
        if (gainRelationReel > 0)
        {
            _historiqueMembres?.Ajouter(cible, _horloge.Instant,
                "Relation", "Relation avec " + joueur.NomComplet + " : " +
                relationAvant + " → " + relationApres +
                " (soutien moral réussi).",
                avant: relationAvant, apres: relationApres);
            _historiqueMembres?.Ajouter(joueur, _horloge.Instant,
                "Relation", "Relation avec " + cible.NomComplet + " : " +
                relationAvant + " → " + relationApres +
                " (soutien moral réussi).",
                avant: relationAvant, apres: relationApres);
        }

        bool tempsComplet = AvancerTempsPourDiscussion(20);
        AfficherReponsesDiscussion(false);
        ActualiserBoutonsSujetsDiscussion();
        _discussionEnCours = false;
        if (!tempsComplet || EstAbsent(cible))
            FermerDiscussion();
    }

    private void ActualiserInfobulleTraitsDiscussion()
    {
        if (_cibleDiscussion == null) return;
        var connus = ReglesTraitsPersonnage.Evaluer(_cibleDiscussion, uniquementConnus: true);
        _portraitDiscussionCible.TooltipText = "Traits connus de " +
            _cibleDiscussion.NomComplet + " :\n" +
            (connus.Count == 0 ? "Aucun trait découvert." :
                string.Join(", ", connus.Select(t => t.Nom)));
    }

    private void AfficherReponsesDiscussion(bool peutContinuer)
    {
        _sujetsDiscussion.Hide();
        _reponsesDiscussion.Show();
        _listeReponsesDiscussion.Clear();
        if (peutContinuer)
            _listeReponsesDiscussion.AddItem("Continuer la conversation");
        _listeReponsesDiscussion.AddItem("Mettre fin à la conversation");
    }

    private void ResoudreDiscussion(bool amicale, bool continuation)
    {
        if (_discussionEnCours || _cibleDiscussion == null ||
            (continuation && !_connaissanceAContinuer) ||
            (!amicale && !CaracteristiquesInconnuesDiscussion(_cibleDiscussion).Any()))
            return;
        // Le dialogue a déjà été ouvert pendant les heures autorisées :
        // ses échanges et sa continuation sont permis au-delà de minuit.
        if (!DialogueAccessible(_cibleDiscussion, true, verifierHoraire: false))
        {
            FermerDiscussion();
            return;
        }
        _discussionEnCours = true;
        MembreGuilde joueur = _registreMembres.Membres.First(m => m.EstJoueur);
        MembreGuilde cible = _cibleDiscussion;
        bool basMoral = cible.Moral < 20;
        int relationAvant = RelationsMembres.Lire(joueur, cible);
        int bonheurAvant = cible.Bonheur;
        int moralAvant = cible.Moral;
        bool bonneRelation = relationAvant >= 10;

        int seuil = amicale
            ? (basMoral ? 18 : bonneRelation ? 4 : 10)
            : (basMoral ? (continuation ? 20 : 18) : 6);
        bool succesJoueur = JetCommunication(joueur, seuil, out int deJoueur,
            out int bonusJoueur);
        bool succesCible = JetCommunication(cible, seuil, out int deCible,
            out int bonusCible);
        bool critique = deJoueur == 20 || deCible == 20;
        bool reussite = critique || (succesJoueur && succesCible);
        int relationDelta = 0, bonheurDelta = 0, moralDelta = 0, stats = 0, minutes = 10;
        string replique;
        if (!amicale)
        {
            if (reussite)
            {
                if (basMoral)
                {
                    replique = "Ok, rapide s'il vous plait. J'ai pas le moral.";
                    relationDelta = continuation ? 1 : 3;
                    stats = 1;
                    minutes = 15;
                }
                else
                {
                    replique = "Avec plaisir.";
                    relationDelta = 3;
                    stats = 3;
                    minutes = 30;
                }
            }
            else
            {
                replique = basMoral ? "C'est pas le moment." :
                    continuation ? "Je vais devoir y aller désolé." :
                        "Je suis occupé pour le moment désolé.";
                relationDelta = basMoral ? (continuation ? -10 : -3) : -1;
            }
        }
        else
        {
            if (reussite)
            {
                replique = basMoral
                    ? "Bonjour. Oh bah c'est pas trop la forme en ce moment..."
                    : bonneRelation ? "Oh bah la routine et vous ?" : "Avec plaisir.";
                minutes = basMoral ? 15 : 30;
                if (bonneRelation)
                {
                    relationDelta = 3;
                    bonheurDelta = 1;
                    moralDelta = 10;
                }
                else
                {
                    relationDelta = basMoral ? 1 : 5;
                    moralDelta = 5;
                }
            }
            else
            {
                replique = basMoral ? "C'est pas le moment." :
                    "Je suis occupé pour le moment désolé.";
                relationDelta = basMoral ? -5 : bonneRelation ? -1 : -3;
                bonheurDelta = basMoral ? (bonneRelation ? 0 : -2) :
                    (bonneRelation ? 0 : -1);
                moralDelta = basMoral && !bonneRelation ? -5 : 0;
            }
        }

        // Bonus de Communication du maître de guilde, prévu pour les
        // dialogues réussis de 30 minutes et les échanges amicaux réussis
        // avec une relation >= 10 (même lorsque le moral est bas).
        // Tirage indépendant à 5 %, plafonné à 20.
        bool progressionCommunicationPossible = reussite &&
            (!amicale && !basMoral ||
             amicale && bonneRelation);
        bool progressionCommunication = progressionCommunicationPossible &&
            Random.Shared.Next(100) < 5 &&
            joueur.Statistiques.GetValueOrDefault("Communication", 10) < 20;
        if (progressionCommunication)
        {
            int avant = joueur.Statistiques.GetValueOrDefault("Communication", 10);
            joueur.Statistiques["Communication"] = avant + 1;
            _historiqueMembres?.Ajouter(joueur, _horloge.Instant,
                "Statistique", "Communication : " + avant + " → " +
                (avant + 1) + " (discussion réussie, bonus de 5 %).",
                avant: avant, apres: avant + 1);
            AjouterEntreeJournal(joueur.NomComplet +
                " gagne +1 en Communication après une discussion réussie.",
                "discussion");
        }

        int relationApres = RelationsMembres.Ajuster(joueur, cible, relationDelta);
        cible.Bonheur = Math.Clamp(cible.Bonheur + bonheurDelta, 0, 100);
        cible.Moral = Math.Clamp(cible.Moral + moralDelta, 0, 100);
        int decouvertes = RevelerCaracteristiquesDiscussion(cible, stats);
        string debut = continuation ? "Vous : Continuons notre conversation." :
            amicale ? "Vous : Bonjour ! Quoi de neuf ?" :
                "Vous : Bonjour ! Apprenons un peu à se connaitre.";
        _resultatDiscussion.Text = debut + "\n" + cible.Prenom + " : " + replique +
            "\n\nJets de Communication :\nVous : " +
            ReglesCaracteristiques.FormaterJet(deJoueur, bonusJoueur, seuil) +
            "\n" + cible.Prenom + " : " +
            ReglesCaracteristiques.FormaterJet(deCible, bonusCible, seuil) +
            (critique ? "\nSuccès critique !" : "") +
            "\nRelation : " + (relationDelta >= 0 ? "+" : "") + relationDelta +
            " (" + relationApres + "/100)." +
            (decouvertes > 0 ? "\nCaractéristiques découvertes : " + decouvertes + "." : "") +
            (progressionCommunication ? "\nCommunication du maître de guilde : +1." : "") +
            "\nTemps écoulé : " + minutes + " minutes.";
        // La suite prévue dans Discussion.txt est le deuxième et dernier échange
        // de ce dialogue. Une nouvelle conversation peut ensuite être entamée.
        _connaissanceAContinuer = reussite && !amicale && !continuation &&
            CaracteristiquesInconnuesDiscussion(cible).Any();
        bool tempsComplet = AvancerTempsPourDiscussion(minutes);
        // Une conversation entamée avant minuit peut continuer après minuit.
        AfficherReponsesDiscussion(_connaissanceAContinuer);
        if (!reussite)
            cible.DiscussionBloqueeJusqua = ReglesMissions.DateIso(_horloge.Instant.AddHours(24));
        ActualiserInfobulleTraitsDiscussion();
        ActualiserBoutonsSujetsDiscussion();
        _historiqueMembres?.Ajouter(cible, _horloge.Instant,
            reussite ? "DiscussionReussie" : "DiscussionEchouee",
            (amicale ? "Discussion amicale" : "Faire connaissance") +
            " avec le maître de guilde : " + (reussite ? "réussite" : "échec") +
            ", relation " + relationDelta + ".");
        if (relationApres != relationAvant)
        {
            string relationDetail = "Relation avec " + joueur.NomComplet +
                " : " + relationAvant + " → " + relationApres + ".";
            _historiqueMembres?.Ajouter(cible, _horloge.Instant, "Relation",
                relationDetail, avant: relationAvant, apres: relationApres);
            _historiqueMembres?.Ajouter(joueur, _horloge.Instant, "Relation",
                "Relation avec " + cible.NomComplet + " : " + relationAvant +
                " → " + relationApres + ".", avant: relationAvant, apres: relationApres);
        }
        if (cible.Bonheur != bonheurAvant)
            _historiqueMembres?.Ajouter(cible, _horloge.Instant, "Bonheur",
                "Bonheur : " + bonheurAvant + " → " + cible.Bonheur + ".",
                avant: bonheurAvant, apres: cible.Bonheur);
        if (cible.Moral != moralAvant)
            _historiqueMembres?.Ajouter(cible, _horloge.Instant, "Moral",
                "Moral : " + moralAvant + " → " + cible.Moral + ".",
                avant: moralAvant, apres: cible.Moral);
        if (decouvertes > 0)
            _historiqueMembres?.Ajouter(cible, _horloge.Instant, "Connaissance",
                decouvertes + " caractéristique(s) révélée(s) lors d'une conversation.");
        _discussionEnCours = false;
        if (!tempsComplet || EstAbsent(cible))
            FermerDiscussion();
    }

    private int RevelerCaracteristiquesDiscussion(MembreGuilde membre, int quantite)
    {
        if (quantite <= 0) return 0;
        membre.StatistiquesConnues ??= new List<string>();
        var possibles = CaracteristiquesInconnuesDiscussion(membre)
            .OrderBy(_ => Random.Shared.Next()).Take(quantite).ToList();
        foreach (string cle in possibles)
            membre.StatistiquesConnues.Add(cle);
        return possibles.Count;
    }

    private void FermerDiscussionSansRetour()
    {
        _fenetreDiscussion?.Hide();
        _cibleDiscussion = null;
        ReinitialiserDialogueDemandeMission();
        _connaissanceAContinuer = false;
        _soutienMoralEnAttente = false;
        _discussionEnCours = false;
    }

    private void FermerDiscussion()
    {
        FermerDiscussionSansRetour();
        if (_sortieEnCours) return;

        // Une protestation mise en attente pendant un autre entretien doit
        // être présentée à la fin de celui-ci, avant de revenir aux menus.
        if (_reclamationsPolitique?.EnAttente.Count > 0)
        {
            CallDeferred(nameof(VerifierReclamationsPolitique));
        }

        // Les événements qui ont eu lieu pendant le temps de discussion
        // reprennent la priorité dès que le joueur termine l'échange.
        if (VerifierRendezVousAuberge())
            return;
        if (_etatMissions?.Proposition?.Etat == "Proposee")
        {
            AfficherPropositionMission();
            return;
        }

        if (_fenetreFiche != null && !_fenetreFiche.Visible)
        {
            // Les nouvelles statistiques découvertes sont immédiatement
            // reflétées dans la fiche lorsque le joueur y retourne.
            if (_membreConsulte != null)
                RemplirFiche(_membreConsulte);
            _fenetreFiche.PopupCentered();
            AfficherSocial();
        }
    }
}
