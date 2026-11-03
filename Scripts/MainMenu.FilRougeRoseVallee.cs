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
    private Timer _timerLectureFilRouge;
    // Les choix ne sont présentés qu'après un court temps de lecture.
    // Le joueur décide lui-même quand passer à la réplique suivante.
    private const double DelaiLectureFilRougeSecondes = 0.9;

    private bool FilRougeEstRoseVallee =>
        string.Equals(_villeDepart, "Rose-Vallée", StringComparison.OrdinalIgnoreCase);

    private void InitialiserFilRougeRoseVallee()
    {
        _filRouge = EtatFilRougeRoseVallee.Charger();
        if (!FilRougeEstRoseVallee) return;

        _listeChoixFilRouge = GetNode<ItemList>(
            "FenetreDiscussion/LayoutConversation/Layout/InterfaceInteractive/ListeReponsesConversation");
        _timerLectureFilRouge = new Timer
        {
            Name = "TimerLectureFilRouge",
            OneShot = true,
            WaitTime = DelaiLectureFilRougeSecondes
        };
        AddChild(_timerLectureFilRouge);
        _timerLectureFilRouge.Timeout += DebloquerSuiteFilRouge;

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
            AppliquerAbandonSerres();
            TerminerActeFilRouge(4, fermerFenetre: false);
            _filRouge.DateEvenement = ReglesMissions.DateIso(AjusterPlageFilRouge(maintenant));
        }
        if (_filRougeDialogueActif || _filRouge.AttendRue4 || _filRouge.ActeEnCours > 0)
            return false;
        PreparerProchainEvenementFilRouge();
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
        _timerLectureFilRouge?.Stop();
        _optionsFilRouge.Clear();
        _repliquesFilRouge.Clear();
        _listeChoixFilRouge.Clear();
        _listeChoixFilRouge.Hide();
        _indexRepliqueFilRouge = 0;

        string nomGuilde = _nomGuilde?.Text ?? "";
        foreach (var option in options)
            _optionsFilRouge.Add((option.Texte.Replace("{Nom de la guilde}", nomGuilde),
                option.Action));

        foreach (string fragment in texte.Replace("\r\n", "\n").Split('\n'))
        {
            string ligne = fragment.Trim();
            if (ligne.Length == 0) continue;
            ligne = ligne.Replace("{Nom de la guilde}", nomGuilde);
            if (!transcription && !EstLigneAttribueeFilRouge(ligne))
                ligne = interlocuteur + " : " + ligne;
            _repliquesFilRouge.Add(ligne);
        }
        if (_repliquesFilRouge.Count == 0)
            _repliquesFilRouge.Add(interlocuteur + " : ...");

        string dossier = "res://Images/Portraits/FilRougeRoseVallee/";
        string fichier = dossier + portrait + ".png";
        if (!ResourceLoader.Exists(fichier))
        {
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
        if (!_filRougeDialogueActif || _timerLectureFilRouge == null ||
            _indexRepliqueFilRouge < 0 || _indexRepliqueFilRouge >= _repliquesFilRouge.Count)
            return;

        _timerLectureFilRouge.Stop();
        _listeChoixFilRouge.Clear();
        _listeChoixFilRouge.Hide();
        _resultatDiscussion.Text = _repliquesFilRouge[_indexRepliqueFilRouge];
        _timerLectureFilRouge.Start();
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
        _timerLectureFilRouge?.Stop();
        _fenetreDiscussion?.Hide();
        _listeChoixFilRouge?.Hide();
        _optionsFilRouge.Clear();
        _repliquesFilRouge.Clear();
        _indexRepliqueFilRouge = 0;
        _reponsesDiscussion?.Hide();
        _sujetsDiscussion?.Show();
    }

    private void TerminerActeFilRouge(int numero, bool fermerFenetre = true)
    {
        if (_filRouge == null) return;
        _filRouge.ActeTermine = Math.Max(_filRouge.ActeTermine, numero);
        _filRouge.ActeEnCours = 0;
        _filRouge.Noeud = "";
        _filRouge.DateEvenement = "";
        if (fermerFenetre) FermerInterfaceFilRouge();
        AjouterEntreeJournal("Rose-Vallée : acte " + numero + " terminé.", "fil_rouge");
        PreparerProchainEvenementFilRouge();
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
        _etat.Ecus = Math.Max(0, _etat.Ecus + valeur);
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

    private bool JetFilRouge(string statistique, int cible)
    {
        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        int stat = joueur?.Statistiques?.GetValueOrDefault(statistique, 10) ?? 10;
        int de = Random.Shared.Next(1, 21);
        int modificateur = ReglesCaracteristiques.Modificateur(Math.Clamp(stat, 1, 20));
        bool reussi = de + modificateur >= cible;
        AjouterEntreeJournal("Rose-Vallée : jet " + statistique + " D20=" + de +
            (modificateur >= 0 ? "+" : "") + modificateur +
            " / cible " + cible + " : " + (reussi ? "réussite" : "échec") + ".", "fil_rouge");
        return reussi;
    }

    private int StatJoueurFilRouge(string nom) =>
        _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur)?
            .Statistiques?.GetValueOrDefault(nom, 10) ?? 10;

    private void CloreFinFilRouge(string fin, int prestige, int ecus = 0)
    {
        if (!string.IsNullOrEmpty(_filRouge.Fin)) return;
        _filRouge.Fin = fin;
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
        AjouterEntreeJournal("Rose-Vallée : fin du fil rouge « " +
            (fin == "A" ? "L'Indépendance" : fin == "B" ? "Le Compromis" :
            "Sous l'ombre de Facon") + " ». Taux de missions ×" +
            (fin == "A" ? "1,5" : fin == "B" ? "1" : "0,5") + ".", "fil_rouge");
        TerminerActeFilRouge(6);
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
        AjouterEntreeJournal("Rose-Vallée : les saboteurs ont été retrouvés dans la Rue4.", "fil_rouge");
        _filRouge.Noeud = "iv-rue4";
        ReprendreDialogueFilRouge();
    }

    private void AppliquerAbandonSerres()
    {
        ModifierPrestigeFilRouge(-30, "Abandon des serres de Perrine");
        ModifierBonheurTousFilRouge(-10, "Abandon des serres");
        AjouterEffetSpawnFilRouge("habitants_doutent", 0.5);
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

    private void TraiterFinMissionFilRouge(MissionGuilde mission, bool victoire,
        DateTime instant)
    {
        if (_filRouge == null || !FilRougeEstRoseVallee ||
            string.IsNullOrEmpty(mission.FilRougeId)) return;
        if (mission.FilRougeId == "enquete_facon")
        {
            if (victoire)
            {
                _filRouge.PreuveFalsification = true;
                AjouterEntreeJournal("Rose-Vallée : preuve de falsification découverte " +
                    "grâce à la mission de Facon.", "fil_rouge");
            }
            else AjouterEntreeJournal("Rose-Vallée : l'intermédiaire de Facon a disparu. " +
                "L'enquête a échoué.", "fil_rouge");
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
                bool identifie = de + ReglesCaracteristiques.Modificateur(
                    Math.Clamp(intelligence, 1, 20)) >= 12;
                _filRouge.PreuveSabotage |= identifie;
                AjouterEntreeJournal("Rose-Vallée : serres protégées ; " +
                    (identifie ? "un saboteur a été identifié, preuve obtenue." :
                    "les saboteurs ont fui sans être identifiés.") +
                    " (jet d'identification " + de + "/20, cible 12).", "fil_rouge");
            }
            else AjouterEntreeJournal("Rose-Vallée : nouvelle dégradation des serres " +
                "de Perrine, coût subi par les habitants : 600 écus.", "fil_rouge");
        }
    }
}
