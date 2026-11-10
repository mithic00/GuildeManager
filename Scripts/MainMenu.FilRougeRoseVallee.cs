using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Déclenchement, UI, persistance et conséquences du fil rouge Rose-Vallée.</summary>
public partial class MainMenu
{
    private EtatFilRougeRoseVallee _filRouge;
    private bool _filRougeDialogueActif;
    private ItemList _listeChoixFilRouge;
    private readonly List<(string Texte, string Action)> _optionsFilRouge = new();
    private readonly List<string> _repliquesFilRouge = new();
    private int _indexRepliqueFilRouge;
    // Animation brève du texte : aucune attente imposée sur les réponses.
    private Tween _animationTexteFilRouge;
    private const double DureeAnimationMinFilRouge = 0.2;
    private const double DureeAnimationMaxFilRouge = 0.7;

    private bool FilRougeEstRoseVallee =>
        string.Equals(_villeDepart, "Rose-Vallée", StringComparison.OrdinalIgnoreCase);

    private void InitialiserFilRougeRoseVallee()
    {
        _filRouge = EtatFilRougeRoseVallee.Charger();
        if (!FilRougeEstRoseVallee) return;

        _listeChoixFilRouge = GetNode<ItemList>(
            "FenetreDiscussion/LayoutConversation/Layout/InterfaceInteractive/ListeReponsesConversation");
        _listeChoixFilRouge.ItemSelected += index =>
        {
            if (!_filRougeDialogueActif || !_listeChoixFilRouge.Visible) return;
            _listeChoixFilRouge.DeselectAll();
            if (_indexRepliqueFilRouge < _repliquesFilRouge.Count - 1)
            {
                // « Continuer » est une action d'affichage, pas un choix narratif.
                if ((int)index == 0)
                {
                    _indexRepliqueFilRouge++;
                    PresenterRepliqueFilRouge();
                }
                return;
            }
            int i = (int)index;
            if (i < 0 || i >= _optionsFilRouge.Count) return;
            string action = _optionsFilRouge[i].Action;
            string choix = _optionsFilRouge[i].Texte;
            if (action != "filrouge-fermer-bilan")
                AjouterChoixFilRouge(choix);
            TraiterChoixFilRouge(action);
        };
        _listeChoixFilRouge.Hide();
        // Toute conversation / action interrompue se reprend depuis le dernier
        // noeud enregistré dans l'état en mémoire à la prochaine ouverture.
        if (_filRouge.ActeEnCours > 0 && !_filRouge.AttendRue4)
            CallDeferred(nameof(ReprendreDialogueFilRouge));
        else
            PreparerProchainEvenementFilRouge();
        CallDeferred(nameof(VerifierEvenementFilRougeAuChargement));
    }

    private static DateTime? LireDateFilRouge(string date)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;
        try { return ReglesMissions.LireDate(date); }
        catch { return null; }
    }

    private static bool DansPlageFilRouge(DateTime instant) =>
        instant.Hour >= 10 && (instant.Hour < 14 || instant.Hour == 14 && instant.Minute == 0);

    private static DateTime AjusterPlageFilRouge(DateTime date)
    {
        if (date.Hour < 10) return date.Date.AddHours(10);
        if (!DansPlageFilRouge(date)) return date.Date.AddDays(1).AddHours(10);
        return date;
    }

    private void PreparerProchainEvenementFilRouge()
    {
        if (_filRouge == null || !FilRougeEstRoseVallee ||
            _horloge == null || _filRouge.Fin != "" || _filRouge.AttendRue4 ||
            _filRouge.ActeEnCours > 0 || !string.IsNullOrEmpty(_filRouge.DateEvenement))
            return;
        int[] jours = { 10, 30, 50, 80, 120, 150 };
        int index = _filRouge.ActeTermine;
        if (index < 0 || index >= jours.Length) return;
        DateTime premierJour = HorlogeJeu.DateInitiale.Date.AddDays(jours[index] - 1).AddHours(10);
        DateTime date = AjusterPlageFilRouge(
            _horloge.Instant >= premierJour ? _horloge.Instant : premierJour);
        _filRouge.DateEvenement = ReglesMissions.DateIso(date);
    }

    private void ReporterEvenementFilRouge(DateTime maintenant)
    {
        if (_filRouge == null || _filRouge.ActeEnCours > 0 || !FilRougeEstRoseVallee) return;
        DateTime? date = LireDateFilRouge(_filRouge.DateEvenement);
        if (date == null || date > maintenant || !DansPlageFilRouge(maintenant)) return;
        DateTime report = AjusterPlageFilRouge(maintenant.AddHours(1));
        _filRouge.DateEvenement = ReglesMissions.DateIso(report);
        AjouterEntreeJournal("Fil rouge Rose-Vallée : un événement simultané reporte la rencontre au " +
            HorlogeJeu.Formater(report) + ".", "fil_rouge");
    }

    private bool VerifierMinuteFilRouge(bool conflitAutreEvenement = false)
    {
        if (!FilRougeEstRoseVallee || _filRouge == null || _horloge == null) return false;
        DateTime maintenant = _horloge.Instant;
        if (_filRouge.BatailleCharlesPrevue && !_filRouge.BatailleCharlesSignalee &&
            LireDateFilRouge(_filRouge.DateBatailleCharles) is DateTime duel &&
            maintenant >= duel)
        {
            _filRouge.BatailleCharlesSignalee = true;
            AjouterEntreeJournal("Rose-Vallée : Charles Suriname réclame une bataille ! " +
                "Événement de confrontation à développer ultérieurement.", "fil_rouge");
        }
        if (_filRouge.AttendRue4 &&
            LireDateFilRouge(_filRouge.DateLimiteRue4) is DateTime limite && maintenant >= limite)
        {
            _filRouge.AttendRue4 = false;
            _filRouge.DateLimiteRue4 = "";
            _filRouge.NotificationAbandon = true;
            AjouterEntreeJournal("Rose-Vallée : délai de quatre heures dépassé pour retrouver les saboteurs.", "fil_rouge");
            AppliquerAbandonSerres(-40);
            TerminerActeFilRouge(4, fermerFenetre: false);
            _filRouge.DateEvenement = ReglesMissions.DateIso(AjusterPlageFilRouge(maintenant));
        }
        if (_filRougeDialogueActif || _filRouge.AttendRue4 || _filRouge.ActeEnCours > 0)
            return false;
        PreparerProchainEvenementFilRouge();
        // Les délais et événements passifs continuent de progresser, mais
        // aucun dialogue avec un choix ne doit interrompre le joueur absent.
        if (JoueurEnMission)
            return false;
        DateTime? prevu = LireDateFilRouge(_filRouge.DateEvenement);
        if (prevu == null || prevu > maintenant || !DansPlageFilRouge(maintenant))
            return false;
        if (conflitAutreEvenement)
        {
            ReporterEvenementFilRouge(maintenant);
            return false;
        }
        if (_traitementTempsDiscussion || _fenetreDiscussion?.Visible == true ||
            _fenetreNouvelleMission?.Visible == true ||
            _choixRecrutement?.Visible == true)
            return false;

        if (_tempsEnCours)
            InterrompreAvanceeTemps("Un événement du fil rouge de Rose-Vallée commence.");
        _filRouge.DateEvenement = "";
        if (_filRouge.NotificationAbandon)
        {
            _filRouge.NotificationAbandon = false;
            _filRouge.ActeEnCours = 4;
            _filRouge.Noeud = "iv-abandon-notification";
        }
        else
        {
            _filRouge.ActeEnCours = _filRouge.ActeTermine + 1;
            _filRouge.Noeud = "debut";
        }
        AjouterEntreeJournal("Fil rouge Rose-Vallée : ouverture de l'acte " +
            _filRouge.ActeEnCours + ".", "fil_rouge");
        ReprendreDialogueFilRouge();
        return true;
    }

    private void VerifierEvenementFilRougeAuChargement() =>
        VerifierMinuteFilRouge();

    private void ReprendreDialogueFilRouge()
    {
        if (!FilRougeEstRoseVallee || _filRouge == null ||
            _filRouge.ActeEnCours == 0 || _filRouge.AttendRue4)
            return;
        if (_fenetreDiscussion == null || _listeChoixFilRouge == null) return;
        _filRougeDialogueActif = true;
        _sujetsDiscussion.Hide();
        _reponsesDiscussion.Hide();
        _fenetreDiscussion.PopupCentered();
        if (!string.IsNullOrEmpty(_filRouge.BilanTexte))
            AfficherBilanFilRouge(_filRouge.BilanTexte);
        else
            AfficherNoeudFilRouge();
    }

    private void AfficherFilRouge(string portrait, string interlocuteur, string texte,
        params (string Texte, string Action)[] options) =>
        AfficherFilRougeProgressif(portrait, interlocuteur, texte, false, options);

    // Les transcriptions contiennent déjà les noms de leurs interlocuteurs.
    private void AfficherTranscriptionFilRouge(string portrait, string interlocuteur,
        string transcription, params (string Texte, string Action)[] options) =>
        AfficherFilRougeProgressif(portrait, interlocuteur, transcription, true, options);

    private void AfficherFilRougeProgressif(string portrait, string interlocuteur,
        string texte, bool transcription, (string Texte, string Action)[] options)
    {
        if (!_filRougeDialogueActif || _listeChoixFilRouge == null) return;
        _animationTexteFilRouge?.Kill();
        _optionsFilRouge.Clear();
        _repliquesFilRouge.Clear();
        _listeChoixFilRouge.Clear();
        _listeChoixFilRouge.Hide();
        _indexRepliqueFilRouge = 0;

        string nomGuilde = _nomGuilde?.Text ?? "";
        foreach (var option in options)
            _optionsFilRouge.Add((option.Texte.Replace("{Nom de la guilde}", nomGuilde),
                option.Action));

        // Les bilans sont présentés en une seule page pour voir ensemble
        // le résultat narratif et toutes les conséquences.
        if (transcription && interlocuteur == "Conséquences de la rencontre")
        {
            _repliquesFilRouge.Add(texte.Replace("{Nom de la guilde}", nomGuilde));
        }
        else
        {
            foreach (string fragment in texte.Replace("\r\n", "\n").Split('\n'))
            {
                string ligne = fragment.Trim();
                if (ligne.Length == 0) continue;
                ligne = ligne.Replace("{Nom de la guilde}", nomGuilde);
                if (!transcription && !EstLigneAttribueeFilRouge(ligne))
                    ligne = interlocuteur + " : " + ligne;
                _repliquesFilRouge.Add(ligne);
            }
        }
        if (_repliquesFilRouge.Count == 0)
            _repliquesFilRouge.Add(interlocuteur + " : ...");

        string dossier = "res://Images/Portraits/FilRougeRoseVallee/";
        string fichier = string.IsNullOrWhiteSpace(portrait) ? "" :
            dossier + portrait + ".png";
        if (string.IsNullOrEmpty(fichier) || !ResourceLoader.Exists(fichier))
        {
            if (!string.IsNullOrEmpty(fichier))
                GD.PushWarning("Portrait du fil rouge absent : " + fichier);
            fichier = "res://Images/Portraits/GrandFormat/Portait_Defaut.png";
        }
        _portraitDiscussionCible.Texture = GD.Load<Texture2D>(fichier);
        _portraitDiscussionCible.TooltipText = interlocuteur;
        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        if (joueur != null)
        {
            string[] races = { "Humain", "ElfeGris", "Grumol", "HumainDesMonts",
                "HautElfe", "Nain", "Tanneur", "Khajiit" };
            string race = joueur.ApparenceId > 0 && joueur.ApparenceId <= races.Length ?
                races[joueur.ApparenceId - 1] : "Humain";
            string portraitJoueur = "res://Images/Portraits/GrandFormat/" + race +
                (joueur.Sexe == "F" ? "_Femme.png" : "_Homme.png");
            if (!ResourceLoader.Exists(portraitJoueur))
                portraitJoueur = "res://Images/Portraits/GrandFormat/Portait_Defaut.png";
            _portraitDiscussionJoueur.Texture = GD.Load<Texture2D>(portraitJoueur);
        }
        PresenterRepliqueFilRouge();
    }

    /// <summary>Une ligne déjà attribuée (joueur/PNJ) garde son auteur.</summary>
    private static bool EstLigneAttribueeFilRouge(string ligne)
    {
        string[] auteurs = {
            "Vous", "Joueur", "Charles", "Charles Suriname", "Léonie", "Étienne",
            "Perrine", "Mireille", "Saboteur", "Seigneur", "Préfet", "Narration"
        };
        return auteurs.Any(auteur => ligne.StartsWith(auteur + " :", StringComparison.OrdinalIgnoreCase));
    }

    private void PresenterRepliqueFilRouge()
    {
        if (!_filRougeDialogueActif ||
            _indexRepliqueFilRouge < 0 || _indexRepliqueFilRouge >= _repliquesFilRouge.Count)
            return;

        _animationTexteFilRouge?.Kill();
        _listeChoixFilRouge.Clear();
        _resultatDiscussion.VisibleRatio = 1f;
        _resultatDiscussion.Text = _repliquesFilRouge[_indexRepliqueFilRouge] +
            (_dernierJetFilRouge.Length > 0 ? "\n\n" + _dernierJetFilRouge : "");
        _dernierJetFilRouge = "";

        // Les réponses sont visibles IMMÉDIATEMENT. Seule la phrase apparaît
        // progressivement, et un clic peut passer à la suite sans délai.
        DebloquerSuiteFilRouge();
        _resultatDiscussion.VisibleRatio = 0f;
        double duree = Math.Clamp(_resultatDiscussion.Text.Length / 240.0,
            DureeAnimationMinFilRouge, DureeAnimationMaxFilRouge);
        _animationTexteFilRouge = CreateTween();
        _animationTexteFilRouge.TweenProperty(_resultatDiscussion,
            "visible_ratio", 1.0f, duree);
    }

    private void DebloquerSuiteFilRouge()
    {
        if (!_filRougeDialogueActif || _listeChoixFilRouge == null) return;
        _listeChoixFilRouge.Clear();
        if (_indexRepliqueFilRouge < _repliquesFilRouge.Count - 1)
            _listeChoixFilRouge.AddItem("Continuer");
        else
            foreach (var option in _optionsFilRouge)
                _listeChoixFilRouge.AddItem(option.Texte);

        _listeChoixFilRouge.Show();
    }

    /// <summary>
    /// Affiche le résultat narratif et les effets dans la fenêtre de dialogue.
    /// Le bouton de fermeture est immédiatement accessible et le bilan est
    /// persisté avant le clic : les conséquences ne sont jamais rejouées.
    /// </summary>
    private void AfficherBilanFilRouge(string texte)
    {
        if (_filRouge == null || !_filRougeDialogueActif) return;
        _filRouge.BilanTexte = texte;
        AfficherTranscriptionFilRouge("",
            "Conséquences de la rencontre", texte,
            ("Fermer la conversation", "filrouge-fermer-bilan"));
    }

    private void AjouterChoixFilRouge(string texte)
    {
        if (_filRouge == null || _horloge == null) return;
        string ligne = HorlogeJeu.Formater(_horloge.Instant) + " | Acte " +
            _filRouge.ActeEnCours + " : " + texte;
        _filRouge.Choix ??= new();
        _filRouge.Choix.Add(ligne);
        AjouterEntreeJournal("Rose-Vallée — choix : " + texte, "fil_rouge");
    }

    private void VersNoeudFilRouge(string noeud)
    {
        _filRouge.Noeud = noeud;
        AfficherNoeudFilRouge();
    }

    private void FermerInterfaceFilRouge()
    {
        _filRougeDialogueActif = false;
        _animationTexteFilRouge?.Kill();
        if (_resultatDiscussion != null)
            _resultatDiscussion.VisibleRatio = 1f;
        _fenetreDiscussion?.Hide();
        _listeChoixFilRouge?.Hide();
        _optionsFilRouge.Clear();
        _repliquesFilRouge.Clear();
        _indexRepliqueFilRouge = 0;
        _reponsesDiscussion?.Hide();
        _sujetsDiscussion?.Show();
        if (ReclamationPolitiqueDisponible())
            CallDeferred(nameof(VerifierReclamationsPolitique));
    }

    private void TerminerActeFilRouge(int numero, bool fermerFenetre = true)
    {
        if (_filRouge == null) return;
        _filRouge.ActeTermine = Math.Max(_filRouge.ActeTermine, numero);
        _filRouge.ActeEnCours = 0;
        _filRouge.Noeud = "";
        _filRouge.DateEvenement = "";
        if (fermerFenetre) FermerInterfaceFilRouge();
        _filRouge.BilanTexte = "";
        PreparerProchainEvenementFilRouge();
    }

    /// <summary>
    /// Les réputations du fil rouge ont pour origine soit l'état narratif Facon,
    /// soit l'état des relations générales. Les effets sont bornés à [-100, 100]
    /// et enregistrés uniquement en mémoire jusqu'à la sauvegarde manuelle.
    /// Retourne la variation réelle (après plafonnement) pour le bilan.
    /// </summary>
    private int ModifierReputationFilRouge(string cible, int valeur,
        string raison, bool fixerValeur = false)
    {
        if (!FilRougeEstRoseVallee || _filRouge == null) return 0;

        int avant;
        int apres;
        string nom;
        switch (cible)
        {
            case "Facon":
                avant = Math.Clamp(_filRouge.ReputationFacon, -100, 100);
                apres = fixerValeur
                    ? Math.Clamp(valeur, -100, 100)
                    : (int)Math.Clamp((long)avant + valeur, -100L, 100L);
                _filRouge.ReputationFacon = apres;
                nom = "Facon";
                break;
            case "Commercants":
            case "Ville":
                if (_relationsGuilde == null) return 0;
                string cle = cible == "Commercants"
                    ? EtatRelationsGuilde.Commercants
                    : EtatRelationsGuilde.CleVille(_villeDepart);
                avant = _relationsGuilde.Lire(cle);
                apres = fixerValeur
                    ? _relationsGuilde.Ajuster(cle, Math.Clamp(valeur, -100, 100) - avant)
                    : _relationsGuilde.Ajuster(cle, valeur);
                nom = cible == "Commercants" ? "Commerçants" : (_villeDepart ?? "Ville");
                break;
            default:
                GD.PushWarning("Réputation du fil rouge non reconnue : " + cible);
                return 0;
        }

        RafraichirRelationsBureau();
        int variation = apres - avant;
        if (variation != 0 || fixerValeur)
            AjouterEntreeJournal("Rose-Vallée : réputation auprès de " + nom +
                (fixerValeur ? " fixée à " + apres :
                    " " + (variation > 0 ? "+" : "") + variation) +
                " (" + raison + ").", "fil_rouge");
        return variation;
    }

    private void ModifierPrestigeFilRouge(int valeur, string raison)
    {
        if (_etat == null) return;
        long avant = _etat.Prestige;
        _etat.Prestige = Math.Max(0, avant + valeur);
        AfficherValeur(_prestige, _etat.Prestige);
        AjouterEntreeJournal("Rose-Vallée : " + raison + " ; prestige " +
            (valeur >= 0 ? "+" : "") + valeur + " (réel : " +
            (_etat.Prestige - avant) + ").", "fil_rouge");
    }

    private void ModifierEcusFilRouge(int valeur, string raison)
    {
        if (_etat == null) return;
        long soldeAvant = _etat.Ecus;
        _etat.Ecus = Math.Max(0, _etat.Ecus + valeur);
        // Enregistrer la variation réellement versée ou payée, après le
        // plafonnement à zéro de la trésorerie.
        EnregistrerMouvementBudget(valeur >= 0 ? RegistreBudget.Recompenses :
            RegistreBudget.Autres, _etat.Ecus - soldeAvant,
            "Fil rouge Rose-Vallée : " + raison);
        AfficherValeur(_ecus, _etat.Ecus);
        AjouterEntreeJournal("Rose-Vallée : " + raison + " ; " +
            (valeur >= 0 ? "+" : "") + valeur + " écus.", "fil_rouge");
    }

    private void ModifierBonheurTousFilRouge(int valeur, string raison)
    {
        if (_registreMembres == null) return;
        foreach (MembreGuilde membre in _registreMembres.Membres.ToList())
        {
            int avant = membre.Bonheur;
            membre.Bonheur = Math.Clamp(avant + valeur, 0, 100);
            AjouterEntreeJournal("Rose-Vallée : " + membre.NomComplet +
                " — bonheur " + avant + " → " + membre.Bonheur +
                " (" + raison + ").", "fil_rouge");
            _historiqueMembres?.Ajouter(membre, _horloge.Instant,
                "Bonheur", "Fil rouge Rose-Vallée : " + raison +
                " (" + avant + " → " + membre.Bonheur + ").",
                avant: avant, apres: membre.Bonheur);
        }
        // La règle de départ des membres à bonheur nul demeure gérée par
        // le système de membres/missions existant.
    }

    private void AjouterEffetSpawnFilRouge(string id, double facteur, string demandeur = "")
    {
        _filRouge.AjouterMultiplicateur(id, facteur, demandeur);
        AjouterEntreeJournal("Rose-Vallée : modulation des apparitions de missions " +
            id + " (×" + facteur.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) +
            (demandeur == "" ? "" : ", demandeur " + demandeur) + ").", "fil_rouge");
    }

    // Le seuil sert uniquement à décider de la réussite : il ne doit jamais
    // apparaître dans FenetreDiscussion ni dans le journal du fil rouge.
    private static string FormaterJetFilRouge(int de, int modificateur, int cible)
    {
        int total = de + modificateur;
        string signe = modificateur >= 0 ? "+" : "-";
        return "D20 " + de + " " + signe + " " + Math.Abs(modificateur) +
            " = " + total + " (" + (total >= cible ? "réussite" : "échec") + ")";
    }

    private bool JetFilRouge(string statistique, int cible)
    {
        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        int stat = joueur?.Statistiques?.GetValueOrDefault(statistique, 10) ?? 10;
        int de = Random.Shared.Next(1, 21);
        int modificateur = ReglesCaracteristiques.Modificateur(Math.Clamp(stat, 1, 20));
        bool reussi = de + modificateur >= cible;
        string resultat = FormaterJetFilRouge(de, modificateur, cible);
        AjouterEntreeJournal("Rose-Vallée : jet de " + statistique + " : " +
            resultat + ".", "fil_rouge");
        // Le jet est consultable immédiatement dans le dialogue, avant la
        // prochaine réplique. Le résultat montré est celui utilisé par le jeu.
        _dernierJetFilRouge = "Jet de " + statistique + " : " + resultat;
        return reussi;
    }

    private string _dernierJetFilRouge = "";

    private int StatJoueurFilRouge(string nom) =>
        _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur)?
            .Statistiques?.GetValueOrDefault(nom, 10) ?? 10;

    private void CloreFinFilRouge(string fin, int prestige, int ecus = 0)
    {
        if (!string.IsNullOrEmpty(_filRouge.Fin)) return;
        // Les fins A/B/C n'impliquent pas toujours les mêmes réputations :
        // on utilise le noeud exact d'audience, sauvegardé avant ce résultat.
        // Les effets sont appliqués une seule fois, grâce au garde sur Fin.
        (int facon, int ville, int commercants) = _filRouge.Noeud switch
        {
            "vi-a" => (-10, 0, 0),
            "vi-a-accusation" => (-40, 10, 10),
            "vi-b-defaut" => (-10, 0, 0),
            "vi-b-cooperation" => (30, -10, -10),
            "vi-c-defaut" => (-50, 0, 0),
            "vi-c-cooperation" => (-10, 0, 0),
            "vi-c-accusation" => (-40, 0, 0),
            "vi-c-soumission" => (30, -20, -20),
            _ => (0, 0, 0)
        };
        _filRouge.Fin = fin;
        int effetFacon = facon != 0
            ? ModifierReputationFilRouge("Facon", facon, "Jugement du Comté, fin " + fin) : 0;
        int effetVille = ville != 0
            ? ModifierReputationFilRouge("Ville", ville, "Jugement du Comté, fin " + fin) : 0;
        int effetCommercants = commercants != 0
            ? ModifierReputationFilRouge("Commercants", commercants,
                "Jugement du Comté, fin " + fin) : 0;
        _filRouge.ReinitialiserMultiplicateurs(
            fin == "A" ? 1.5 : fin == "C" ? 0.5 : 1.0, "conclusion_" + fin);
        if (prestige != 0) ModifierPrestigeFilRouge(prestige, "Jugement du Comté, fin " + fin);
        if (ecus != 0) ModifierEcusFilRouge(ecus, "Jugement du Comté, fin " + fin);
        if (fin == "A" && _filRouge.RancuneCharles >= 2)
        {
            _filRouge.BatailleCharlesPrevue = true;
            _filRouge.DateBatailleCharles =
                ReglesMissions.DateIso(_horloge.Instant.AddDays(10));
            AjouterEntreeJournal("Rose-Vallée : la rancune de Charles annonce un conflit " +
                "dans dix jours (combat à intégrer ultérieurement).", "fil_rouge");
        }
        string libelle = fin == "A" ? "L'indépendance" :
            fin == "B" ? "Le compromis" : "Sous l'ombre de Facon";
        AjouterEntreeJournal("Rose-Vallée : décision du Comté — " + libelle + ".", "fil_rouge");
        string narration = fin == "A"
            ? "Le Seigneur reconnaît votre guilde comme pleinement indépendante. " +
              "Les pressions organisées de Facon cessent et votre position à Rose-Vallée se renforce."
            : fin == "B"
            ? "Un accord de non-ingérence est conclu. Les deux guildes devront " +
              "désormais coexister sans empiéter sur leurs activités respectives."
            : "Le Comté maintient la position dominante de Facon. Votre guilde " +
              "reste ouverte, mais les contrats seront plus difficiles à obtenir.";
        string bilanRelations =
            (facon != 0 ? "\nRéputation Facon : " + (effetFacon > 0 ? "+" : "") + effetFacon + "." : "") +
            (ville != 0 ? "\nRéputation Rose-Vallée : " + (effetVille > 0 ? "+" : "") + effetVille + "." : "") +
            (commercants != 0 ? "\nRéputation Commerçants : " +
                (effetCommercants > 0 ? "+" : "") + effetCommercants + "." : "");
        AfficherBilanFilRouge(narration + bilanRelations +
            (prestige != 0 ? "\nPrestige : +" + prestige + "." : "") +
            (ecus != 0 ? "\nTrésorerie : +" + ecus + " écus." : "") +
            (fin == "C" && prestige == 0 && ecus == 0 && _filRouge.Choix.Any(
                c => c.Contains("Confirmer la dépendance à Facon"))
                ? "\nAccord volontaire : +2 000 écus, prestige -20." : "") +
            "\nApparition des missions : taux de base ×" +
            (fin == "A" ? "1,5" : fin == "B" ? "1" : "0,5") + "." +
            (fin == "A" && _filRouge.BatailleCharlesPrevue
                ? "\nCharles garde rancune et pourrait provoquer une confrontation." : ""));
    }

    private void VerifierRueFilRouge(string nomRue)
    {
        if (_filRouge?.AttendRue4 != true || !FilRougeEstRoseVallee ||
            _horloge == null || !string.Equals(nomRue,
                "RoseValleeRue4", StringComparison.Ordinal))
            return;
        if (LireDateFilRouge(_filRouge.DateLimiteRue4) is DateTime limite &&
            _horloge.Instant >= limite) return;
        _filRouge.AttendRue4 = false;
        _filRouge.DateLimiteRue4 = "";
        AjouterEntreeJournal("Rose-Vallée : les saboteurs ont été retrouvés.", "fil_rouge");
        _filRouge.Noeud = "iv-rue4";
        ReprendreDialogueFilRouge();
    }

    private int AppliquerAbandonSerres(int reputationCommercants)
    {
        ModifierPrestigeFilRouge(-30, "Abandon des serres de Perrine");
        ModifierBonheurTousFilRouge(-10, "Abandon des serres");
        AjouterEffetSpawnFilRouge("habitants_doutent", 0.5);
        return ModifierReputationFilRouge("Commercants", reputationCommercants,
            "Abandon des serres de Perrine");
    }

    private void ProposerMissionSpecialeFilRouge(string id, string categorie, string rang)
    {
        if (_etatMissions == null || _filRouge == null ||
            _filRouge.MissionsCreees.Contains(id)) return;
        // Les missions de Facon sont confiées à la guilde entière ; leur prix
        // est divisé par deux. Elles restent sur le tableau sans expiration.
        int remuneration = ReglesMissions.GenererRecompense(rang, categorie, 0.5);
        var mission = new MissionGuilde
        {
            FilRougeId = id,
            Titre = id == "enquete_facon" ? "Les signatures de Facon" : "Protéger les serres",
            Categorie = categorie,
            Rang = rang,
            Demandeur = "Facon",
            Recompense = remuneration,
            Etat = "Disponible"
        };
        _etatMissions.Missions.Add(mission);
        _filRouge.MissionsCreees.Add(id);
        AjouterEntreeJournal("Rose-Vallée : mission spéciale donnée par Facon « " +
            (id == "enquete_facon" ? "Les signatures de Facon" : "Protéger les serres") +
            " », rang " + ReglesMissions.FormaterRang(rang) +
            ", récompense diminuée de moitié, sans date d'expiration.", "fil_rouge");
        RaffraichirTableauMissions();
    }

    private void TraiterFinMissionFilRouge(MissionGuilde mission, IssueMission issue,
        DateTime instant)
    {
        if (_filRouge == null || !FilRougeEstRoseVallee ||
            string.IsNullOrEmpty(mission.FilRougeId)) return;
        bool victoire = issue == IssueMission.Reussite;
        bool echec = issue == IssueMission.Echec;
        if (mission.FilRougeId == "enquete_facon")
        {
            if (victoire)
            {
                _filRouge.PreuveFalsification = true;
                AjouterEntreeJournal("Rose-Vallée : preuve de falsification découverte " +
                    "grâce à la mission de Facon.", "fil_rouge");
                ModifierReputationFilRouge("Ville", 5,
                    "Enquête des signatures de Facon réussie");
            }
            else if (echec)
            {
                AjouterEntreeJournal("Rose-Vallée : l'intermédiaire de Facon a disparu. " +
                    "L'enquête a échoué.", "fil_rouge");
                ModifierReputationFilRouge("Ville", -5,
                    "Enquête des signatures de Facon échouée");
            }
        }
        else if (mission.FilRougeId == "serres_facon")
        {
            if (victoire)
            {
                // Identification effective du coupable après la protection.
                var equipiers = _registreMembres.Membres
                    .Where(m => mission.ParticipantIds.Contains(m.Id)).ToList();
                int intelligence = equipiers.Select(m => m.Statistiques.GetValueOrDefault(
                    "Intelligence", 10)).DefaultIfEmpty(10).Max();
                int de = Random.Shared.Next(1, 21);
                int bonus = ReglesCaracteristiques.Modificateur(
                    Math.Clamp(intelligence, 1, 20));
                bool identifie = de + bonus >= 12;
                _filRouge.PreuveSabotage |= identifie;
                ModifierReputationFilRouge("Commercants", 5,
                    "Mission de protection des serres réussie");
                AjouterEntreeJournal("Rose-Vallée : serres protégées ; " +
                    (identifie ? "un saboteur a été identifié, preuve obtenue." :
                    "les saboteurs ont fui sans être identifiés.") +
                    " (" + FormaterJetFilRouge(de, bonus, 12) +
                    ").", "fil_rouge");
            }
            else if (echec)
                AjouterEntreeJournal("Rose-Vallée : nouvelle dégradation des serres " +
                    "de Perrine, coût subi par les habitants : 600 écus.", "fil_rouge");
        }
    }
}
