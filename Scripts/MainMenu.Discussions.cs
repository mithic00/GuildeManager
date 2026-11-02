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
    private Control _sujetsDiscussion, _reponsesDiscussion;
    private Button _faireConnaissance, _conversationAmicale;
    private Button[] _boutonsReponses;
    private MembreGuilde _cibleDiscussion;
    private bool _connaissanceAContinuer;
    private bool _discussionEnCours;

    private void InitialiserDiscussions()
    {
        const string baseDialogue = "FenetreDiscussion/LayoutConversation/Layout/";
        const string interfaceDialogue = baseDialogue + "InterfaceInteractive/";
        _fenetreDiscussion = GetNode<Window>("FenetreDiscussion");
        _resultatDiscussion = GetNode<RichTextLabel>(baseDialogue + "RésultatDiscussion");
        _portraitDiscussionCible = GetNode<TextureRect>(interfaceDialogue + "PortraitCible");
        _portraitDiscussionJoueur = GetNode<TextureRect>(interfaceDialogue + "PortraitJoueur");
        _sujetsDiscussion = GetNode<Control>(interfaceDialogue + "ListeSujetDiscussion");
        _reponsesDiscussion = GetNode<Control>(interfaceDialogue + "ListeReponsesConversation");
        _faireConnaissance = GetNode<Button>(interfaceDialogue +
            "ListeSujetDiscussion/BoutonFaireConnaissance");
        _conversationAmicale = GetNode<Button>(interfaceDialogue +
            "ListeSujetDiscussion/BoutonConversationAmicale");
        GetNode<Button>(interfaceDialogue + "ListeSujetDiscussion/BoutonFermer")
            .Pressed += FermerDiscussion;
        _faireConnaissance.Pressed += () => ResoudreDiscussion(false, false);
        _conversationAmicale.Pressed += () => ResoudreDiscussion(true, false);
        _boutonsReponses = Enumerable.Range(1, 4)
            .Select(i => GetNode<Button>(interfaceDialogue +
                "ListeReponsesConversation/BoutonReponse" + i)).ToArray();
        _boutonsReponses[0].Pressed += () => ResoudreDiscussion(false, true);
        _boutonsReponses[1].Pressed += FermerDiscussion;
        _boutonsReponses[2].Pressed += FermerDiscussion;
        _boutonsReponses[3].Pressed += FermerDiscussion;
        _fenetreDiscussion.CloseRequested += FermerDiscussion;
        _fenetreDiscussion.Hide();
        _reponsesDiscussion.Hide();
    }

    private static bool JetCommunication(MembreGuilde membre, int cible, out int de)
    {
        de = Random.Shared.Next(1, 21);
        int stat = membre.Statistiques?.GetValueOrDefault("Communication", 10) ?? 10;
        return de + ReglesCaracteristiques.Modificateur(Math.Clamp(stat, 1, 20)) >= cible;
    }

    private bool DialogueAccessible(MembreGuilde cible, bool afficherErreur)
    {
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
        if (!DialogueAccessible(_membreConsulte, true)) return;
        _cibleDiscussion = _membreConsulte;
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
        _sujetsDiscussion.Show();
        _reponsesDiscussion.Hide();
        _fenetreFiche.Hide();
        _fenetreDiscussion.PopupCentered();
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
        for (int i = 0; i < _boutonsReponses.Length; i++)
            _boutonsReponses[i].Hide();
        if (peutContinuer)
        {
            _boutonsReponses[0].Text = "Continuer la conversation";
            _boutonsReponses[0].Show();
        }
        _boutonsReponses[1].Text = "Mettre fin à la conversation";
        _boutonsReponses[1].Show();
    }

    private void ResoudreDiscussion(bool amicale, bool continuation)
    {
        if (_discussionEnCours || _cibleDiscussion == null ||
            (continuation && !_connaissanceAContinuer)) return;
        if (!DialogueAccessible(_cibleDiscussion, true))
        {
            FermerDiscussion();
            return;
        }
        _discussionEnCours = true;
        MembreGuilde joueur = _registreMembres.Membres.First(m => m.EstJoueur);
        MembreGuilde cible = _cibleDiscussion;
        bool basMoral = cible.Moral < 20;
        int relationAvant = RelationsMembres.Lire(joueur, cible);
        bool bonneRelation = relationAvant >= 10;

        int seuil = amicale
            ? (basMoral ? 18 : bonneRelation ? 4 : 10)
            : (basMoral ? (continuation ? 20 : 18) : 6);
        bool succesJoueur = JetCommunication(joueur, seuil, out int deJoueur);
        bool succesCible = JetCommunication(cible, seuil, out int deCible);
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
                replique = basMoral ? "Ok, rapide s'il vous plait. J'ai pas le moral." :
                    "Avec plaisir.";
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

        int relationApres = RelationsMembres.Ajuster(joueur, cible, relationDelta);
        cible.Bonheur = Math.Clamp(cible.Bonheur + bonheurDelta, 0, 100);
        cible.Moral = Math.Clamp(cible.Moral + moralDelta, 0, 100);
        int decouvertes = RevelerCaracteristiquesDiscussion(cible, stats);
        string debut = continuation ? "Vous : Continuons notre conversation." :
            amicale ? "Vous : Bonjour ! Quoi de neuf ?" :
                "Vous : Bonjour ! Apprenons un peu à se connaitre.";
        _resultatDiscussion.Text = debut + "\n" + cible.Prenom + " : " + replique +
            "\n\nJets de Communication : vous " + deJoueur + "/20, " + cible.Prenom +
            " " + deCible + "/20" + (critique ? " — Succès critique !" : "") +
            "\nRelation : " + (relationDelta >= 0 ? "+" : "") + relationDelta +
            " (" + relationApres + "/100)." +
            (decouvertes > 0 ? "\nCaractéristiques découvertes : " + decouvertes + "." : "") +
            "\nTemps écoulé : " + minutes + " minutes.";
        _connaissanceAContinuer = reussite && !amicale;
        AfficherReponsesDiscussion(_connaissanceAContinuer);
        bool tempsComplet = AvancerTempsPourDiscussion(minutes);
        if (!reussite)
            cible.DiscussionBloqueeJusqua = ReglesMissions.DateIso(_horloge.Instant.AddHours(24));
        ActualiserInfobulleTraitsDiscussion();
        _historiqueMembres?.Ajouter(cible, _horloge.Instant,
            reussite ? "DiscussionReussie" : "DiscussionEchouee",
            (amicale ? "Discussion amicale" : "Faire connaissance") +
            " avec le maître de guilde : " + (reussite ? "réussite" : "échec") +
            ", relation " + relationDelta + ".");
        _discussionEnCours = false;
        if (!tempsComplet || EstAbsent(cible))
            FermerDiscussionSansRetour();
    }

    private int RevelerCaracteristiquesDiscussion(MembreGuilde membre, int quantite)
    {
        if (quantite <= 0) return 0;
        membre.StatistiquesConnues ??= new List<string>();
        var possibles = GenerateurCandidats.Stats
            .Concat(new[] { "Motivation" })
            .Distinct(StringComparer.Ordinal)
            .Where(s => !membre.ConnaitStatistique(s) &&
                (s == "Motivation" ? membre.Motivation.HasValue :
                    membre.Statistiques.ContainsKey(s)))
            .OrderBy(_ => Random.Shared.Next()).Take(quantite).ToList();
        foreach (string cle in possibles)
            membre.StatistiquesConnues.Add(cle);
        return possibles.Count;
    }

    private void FermerDiscussionSansRetour()
    {
        _fenetreDiscussion?.Hide();
        _cibleDiscussion = null;
        _connaissanceAContinuer = false;
        _discussionEnCours = false;
    }

    private void FermerDiscussion()
    {
        FermerDiscussionSansRetour();
        _cibleDiscussion = null;
        _connaissanceAContinuer = false;
        _discussionEnCours = false;
        if (!_sortieEnCours && _fenetreFiche != null && !_fenetreFiche.Visible)
        {
            _fenetreFiche.PopupCentered();
            AfficherSocial();
        }
    }
}
