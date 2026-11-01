using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>Gestion des propositions, tableau des missions et départs automatiques.</summary>
public partial class MainMenu
{
    private const int MoralMinPourPartir = 20;

    private EtatMissions _etatMissions;
    private Window _fenetreNouvelleMission;
    private RichTextLabel _infoNouvelleMission;
    private Window _fenetreInfoMission;
    private RichTextLabel _detailsMission;
    private GridContainer _tableauDesMissions;

    private void InitialiserMissions()
    {
        _etatMissions = EtatMissions.Charger();
        _fenetreNouvelleMission = GetNode<Window>("FenetreNouvelleMission");
        _infoNouvelleMission = GetNode<RichTextLabel>(
            "FenetreNouvelleMission/Marge/Layout/InfoMission");
        _tableauDesMissions = GetNode<GridContainer>(
            "LayoutsPanneauPrincipal/TableauDesMissions/ListeMission");

        _fenetreInfoMission = GetNode<Window>("FenetreInfoMission");
        _detailsMission = GetNode<RichTextLabel>(
            "FenetreInfoMission/Marge/Layout/InfoMission");
        GetNode<Button>("FenetreInfoMission/Marge/Layout/Boutons/ButtonFermer")
            .Pressed += () => _fenetreInfoMission.Hide();
        _fenetreInfoMission.CloseRequested += () => _fenetreInfoMission.Hide();
        // La prise de mission par le maître de guilde sera développée séparément.
        GetNode<Button>("FenetreInfoMission/Marge/Layout/Boutons/BoutonPrendre").Disabled = true;
        _fenetreInfoMission.Hide();

        GetNode<Button>("FenetreNouvelleMission/Marge/Layout/Boutons/BoutonAccepter")
            .Pressed += AccepterPropositionMission;
        GetNode<Button>("FenetreNouvelleMission/Marge/Layout/Boutons/BoutonRefuser")
            .Pressed += RefuserPropositionMission;
        _fenetreNouvelleMission.CloseRequested += RefuserPropositionMission;
        _fenetreNouvelleMission.Hide();

        RaffraichirTableauMissions();

        // Une nouvelle partie démarre à 08h00 : tirer le premier jalon dès
        // l'initialisation, sans appliquer deux fois le repos/moral au chargement.
        if (_horloge != null && _horloge.Instant.Hour == 8 &&
            _horloge.Instant.Minute == 0)
            TirerApparitionAuJalon(_horloge.Instant);

        // Réouverture d'une proposition non tranchée après chargement.
        if (_etatMissions.Proposition != null &&
            _etatMissions.Proposition.Etat == "Proposee")
            CallDeferred(nameof(AfficherPropositionMission));
    }

    /// <summary>Appelé exactement une fois après chaque minute avancée.</summary>
    private bool TraiterMinuteMissions()
    {
        if (_etatMissions == null || _horloge == null) return false;
        DateTime maintenant = _horloge.Instant;
        TirerApparitionAuJalon(maintenant);

        TerminerMissionsArrivees(maintenant);
        if (maintenant.Hour == 8 && maintenant.Minute == 0)
            ActualiserReposEtMoral();
        if (maintenant.Minute == 0 &&
            (maintenant.Hour == 8 || maintenant.Hour == 12 || maintenant.Hour == 16))
            ProposerMissionsAuxEquipes(maintenant);

        if (string.IsNullOrWhiteSpace(_etatMissions.DateAnnoncePrevue) ||
            maintenant < ReglesMissions.LireDate(_etatMissions.DateAnnoncePrevue))
            return false;

        _etatMissions.DateAnnoncePrevue = "";
        if (_etatMissions.Proposition != null) return false;
        _etatMissions.Proposition = ReglesMissions.Generer(_registreMembres.Membres);
        if (_etatMissions.Proposition == null) return false;
        InterrompreAvanceeTemps("Une nouvelle mission est proposée à la guilde.");
        AfficherPropositionMission();
        return true;
    }

    private void TirerApparitionAuJalon(DateTime maintenant)
    {
        // Quatre jets indépendants par jour : 08h, 12h, 16h et 20h.
        // Chaque réussite annonce une mission avant le jalon suivant.
        if (maintenant.Minute == 0 &&
            (maintenant.Hour == 8 || maintenant.Hour == 12 ||
             maintenant.Hour == 16 || maintenant.Hour == 20))
        {
            string jalon = ReglesMissions.DateIso(maintenant);
            if (_etatMissions.DernierJalonApparitionTraite != jalon)
            {
                _etatMissions.DernierJalonApparitionTraite = jalon;
                if (_etatMissions.Proposition == null &&
                    string.IsNullOrEmpty(_etatMissions.DateAnnoncePrevue) &&
                    _etat != null && _registreMembres != null)
                {
                    double probabilite = ReglesMissions.ProbabiliteApparition(_etat.Prestige);
                    // Multiplicateur de la ville de départ prévu dans les règles.
                    if (_villeDepart == "Gori Nirog")
                        probabilite = Math.Min(1.0, probabilite * 2.0);
                    if (Random.Shared.NextDouble() < probabilite)
                    {
                        // 1 à 239 minutes après le jalon : au plus tard XXh59.
                        // Pour 20h, l'annonce précède minuit.
                        _etatMissions.DateAnnoncePrevue = ReglesMissions.DateIso(
                            maintenant.AddMinutes(Random.Shared.Next(1, 240)));
                    }
                }
            }
        }

    }

    private void AfficherPropositionMission()
    {
        if (_etatMissions?.Proposition == null) return;
        MissionGuilde mission = _etatMissions.Proposition;
        _infoNouvelleMission.Text =
            "Catégorie de la mission : " + mission.Categorie +
            "\nNiveau requis : " + mission.Rang +
            "\nRécompense : " + mission.Recompense + " écus" +
            "\nDemandeur : " + mission.Demandeur;
        _fenetreNouvelleMission.PopupCentered();
    }

    private void AccepterPropositionMission()
    {
        if (_etatMissions?.Proposition == null) return;
        MissionGuilde mission = _etatMissions.Proposition;
        mission.Etat = "Disponible";
        _etatMissions.Missions.Add(mission);
        _etatMissions.Proposition = null;
        _fenetreNouvelleMission.Hide();
        AjouterEntreeJournal("Mission de rang " + mission.Rang +
            " acceptée : " + mission.Categorie + " (" + mission.Recompense + " écus).", "mission");
        RaffraichirTableauMissions();
    }

    private void RefuserPropositionMission()
    {
        if (_etatMissions == null) return;
        _etatMissions.Proposition = null;
        _fenetreNouvelleMission.Hide();
    }

    private void RaffraichirTableauMissions()
    {
        if (_tableauDesMissions == null || _etatMissions == null) return;
        foreach (Node ancien in _tableauDesMissions.GetChildren())
        {
            _tableauDesMissions.RemoveChild(ancien);
            ancien.QueueFree();
        }

        // Le tableau affiche désormais un bouton 30 × 30 par mission.
        // La grille de la scène conserve son nombre de colonnes (6 actuellement).
        foreach (MissionGuilde mission in _etatMissions.Missions
            .Where(m => m.Etat == "Disponible" || m.Etat == "EnCours"))
        {
            MissionGuilde missionSelectionnee = mission;
            Button bouton = new()
            {
                Name = "Mission_" + mission.Id,
                Text = mission.Rang,
                CustomMinimumSize = new Vector2(30, 30),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
                ClipText = true,
                TooltipText = mission.Categorie + " — Rang " + mission.Rang +
                    " — " + (mission.Etat == "EnCours" ? "En cours" : "Disponible")
            };
            // Les rangs longs (S*****, P*****) doivent tenir dans le format compact.
            bouton.AddThemeFontSizeOverride("font_size", mission.Rang.Length >= 4 ? 8 : 11);
            bouton.Pressed += () => OuvrirInfoMission(missionSelectionnee);
            _tableauDesMissions.AddChild(bouton);
        }
    }

    private void OuvrirInfoMission(MissionGuilde mission)
    {
        if (mission == null || _detailsMission == null || _fenetreInfoMission == null)
            return;

        bool enCours = mission.Etat == "EnCours";
        string texte = "Catégorie : " + mission.Categorie +
            "\nRang requis : " + mission.Rang +
            "\nRécompense : " + mission.Recompense + " écus" +
            "\nDemandeur : " + mission.Demandeur +
            "\nÉtat : " + (enCours ? "En cours" : "Disponible");

        if (enCours)
        {
            string[] participants = (_registreMembres?.Membres ?? new List<MembreGuilde>())
                .Where(m => mission.ParticipantIds != null && mission.ParticipantIds.Contains(m.Id))
                .Select(m => m.NomComplet).ToArray();
            texte += "\nParticipants : " +
                (participants.Length > 0 ? string.Join(", ", participants) : "Inconnus");
            if (!string.IsNullOrWhiteSpace(mission.DateDepart))
                texte += "\nDépart : " + HorlogeJeu.Formater(ReglesMissions.LireDate(mission.DateDepart));
            if (!string.IsNullOrWhiteSpace(mission.DateRetour))
                texte += "\nProchaine échéance : " + HorlogeJeu.Formater(ReglesMissions.LireDate(mission.DateRetour));
        }

        _detailsMission.Text = texte;
        _fenetreInfoMission.PopupCentered();
    }

    private static bool JetStat(MembreGuilde membre, string stat, int cible) =>
        Random.Shared.Next(1, 21) + ReglesCaracteristiques.Modificateur(
            Math.Clamp(membre.Statistiques.GetValueOrDefault(stat, 10), 1, 20)) >= cible;

    private static int MaxStat(List<MembreGuilde> equipe, string stat) =>
        equipe.Max(m => m.Statistiques.GetValueOrDefault(stat, 10));

    private static bool AdmissibleCategorie(List<MembreGuilde> equipe, MissionGuilde mission)
    {
        switch (mission.Categorie)
        {
            case "Enquête":
                return MaxStat(equipe, "Intelligence") >=
                    (ReglesMissions.IndexRang(mission.Rang) >= ReglesMissions.IndexRang("S") ? 15 : 12);
            case "Service":
                return MaxStat(equipe, "Gentillesse") >= 10 || MaxStat(equipe, "Empathie") >= 10;
            case "Recherche mort ou vif":
                return MaxStat(equipe, "Intelligence") >= 10;
            case "Capture vif":
                return MaxStat(equipe, "Agilite") >= 10;
            default: return true;
        }
    }

    private bool GroupeDisponible(List<MembreGuilde> equipe)
    {
        if (equipe.Any(m => m.Moral < MoralMinPourPartir)) return false;
        if (_etatMissions.Missions.Any(m => m.Etat == "EnCours" &&
            m.ParticipantIds.Any(id => equipe.Any(p => p.Id == id)))) return false;
        foreach (MembreGuilde membre in equipe)
        {
            int fatigue = membre.Fatigue ?? 0;
            if (fatigue > 75) return false;
            if (fatigue >= 50 && !JetStat(membre, "Constitution", 12)) return false;
        }
        return true;
    }

    private void ProposerMissionsAuxEquipes(DateTime instant)
    {
        if (_etatMissions == null || _registreMembres == null) return;
        var disponibles = _etatMissions.Missions.Where(m => m.Etat == "Disponible")
            .OrderBy(_ => Random.Shared.Next()).ToList();
        if (disponibles.Count == 0) return;
        var groupes = _registreMembres.Membres.Where(m => !m.EstMaitreDeGuilde)
            .GroupBy(m => string.IsNullOrWhiteSpace(m.EquipeId)
                ? "solo:" + m.Id : "equipe:" + m.EquipeId)
            .Select(g => g.ToList()).ToList();

        // L'ordre est à nouveau tiré pour CHAQUE mission.
        foreach (MissionGuilde mission in disponibles)
        {
            foreach (var equipe in groupes.OrderBy(_ => Random.Shared.Next()))
            {
                if (!equipe.All(m => ReglesMissions.RangSuffisant(m, mission.Rang)) ||
                    !AdmissibleCategorie(equipe, mission) ||
                    !GroupeDisponible(equipe)) continue;
                int meilleurBonusVolonte = equipe.Max(m => ReglesCaracteristiques.Modificateur(
                    Math.Clamp(m.Statistiques.GetValueOrDefault("Volonte", 10), 1, 20)));
                if (Random.Shared.Next(1, 21) + meilleurBonusVolonte < 5) continue;

                mission.Etat = "EnCours";
                mission.DateDepart = ReglesMissions.DateIso(instant);
                mission.DateRetour = ReglesMissions.DateIso(instant.AddDays(1));
                mission.ParticipantIds = equipe.Select(m => m.Id).ToList();
                mission.JoursEcoules = 0;
                mission.DureeFixee = ResolutionMissions.DureeFixee(mission);
                mission.SansIncident = mission.Categorie == "Protection d'un lieu" &&
                    Random.Shared.Next(2) == 0;
                AjouterEntreeJournal(string.Join(", ", equipe.Select(m => m.NomComplet)) +
                    " partent en mission (" + mission.Rang + " : " + mission.Categorie + ").",
                    "mission");
                break;
            }
        }
        RaffraichirTableauMissions();
    }

    private void ActualiserReposEtMoral()
    {
        if (_registreMembres == null) return;
        foreach (var membre in _registreMembres.Membres)
        {
            // La récupération concerne les membres présents ; le jet quotidien
            // de moral concerne tous les membres, y compris ceux en mission.
            bool absent = _etatMissions.Missions.Any(q => q.Etat == "EnCours" &&
                q.ParticipantIds.Contains(membre.Id));
            if (!absent)
                membre.Fatigue = Math.Max(0, (membre.Fatigue ?? 0) - Random.Shared.Next(1, 6));
            int frustration = membre.Statistiques.GetValueOrDefault("Frustration", 10);
            bool reussi = JetStat(membre, "Volonte", 8);
            int malus = frustration >= 10 ? 10 : frustration >= 5 ? 15 : 20;
            membre.Moral = Math.Clamp(membre.Moral + (reussi ? 10 : -malus), 0, 100);
        }
    }

    private static int PerteMoral(MembreGuilde membre)
    {
        int frustration = membre.Statistiques.GetValueOrDefault("Frustration", 10);
        return frustration >= 10 ? 1 : frustration >= 5 ? 2 : 4;
    }
    private static int PerteMort(MembreGuilde membre)
    {
        int frustration = membre.Statistiques.GetValueOrDefault("Frustration", 10);
        return frustration >= 10 ? 10 : frustration >= 5 ? 20 : 40;
    }

    private void TerminerMissionsArrivees(DateTime instant)
    {
        if (_etatMissions == null || _registreMembres == null || _etat == null) return;
        foreach (MissionGuilde mission in _etatMissions.Missions.Where(m =>
            m.Etat == "EnCours" && !string.IsNullOrEmpty(m.DateRetour) &&
            ReglesMissions.LireDate(m.DateRetour) <= instant).ToList())
        {
            var participants = _registreMembres.Membres.Where(m =>
                mission.ParticipantIds.Contains(m.Id)).ToList();
            if (participants.Count == 0) { mission.Etat = "Terminee"; mission.Reussie = false; continue; }

            mission.JoursEcoules++;
            ResultatJourMission resultat = ResolutionMissions.Resoudre(mission, participants);
            if (resultat.Issue == IssueMission.Continue)
            {
                mission.DateRetour = ReglesMissions.DateIso(instant.AddDays(1));
                continue;
            }

            mission.Etat = "Terminee";
            bool victoire = resultat.Issue == IssueMission.Reussite;
            mission.Reussie = victoire;
            mission.SansGainSecondaire = resultat.SansIncident;
            int nombreMorts = resultat.Issue == IssueMission.Mort
                ? Random.Shared.Next(1, participants.Count + 1) : 0;
            var morts = participants.OrderBy(_ => Random.Shared.Next()).Take(nombreMorts).ToList();
            var survivants = participants.Where(m => !morts.Contains(m)).ToList();

            _etat.Prestige = Math.Max(0,
                _etat.Prestige + ReglesMissions.VariationPrestige(mission.Rang, victoire));

            int xpPart = participants.Count == 0 ? 0 :
                ReglesMissions.ExperiencePourMission(mission.Rang) / participants.Count;
            int partGuilde = victoire ? mission.Recompense * 20 / 100 : 0;
            int ecusPart = victoire ? (mission.Recompense - partGuilde) / participants.Count : 0;
            _etat.Ecus += partGuilde;

            foreach (MembreGuilde m in survivants)
            {
                if (!resultat.SansIncident)
                {
                    ReglesMissions.AjouterExperience(m, xpPart);
                    if (victoire)
                    {
                        ReglesMissions.EnregistrerReussiteEtPromouvoir(m, mission.Rang);
                        bool magieElevee = mission.Categorie == "Élimination de danger" ||
                            mission.Categorie == "Recherche mort ou vif" ||
                            mission.Categorie == "Renfort pour Assaut";
                        bool magieStandard = magieElevee || mission.Categorie == "Escorte" ||
                            mission.Categorie == "Capture vif" || mission.Categorie == "Protection d'un lieu";
                        if (magieStandard) ResolutionMissions.ProgresserMagie(m, magieElevee);
                    }
                }
                if (victoire && !resultat.SansIncident)
                    ResolutionMissions.BonusStats(mission, new List<MembreGuilde> { m });

                int savoir = Random.Shared.Next(0, 3);
                if (!resultat.SansIncident) { m.ConnaissanceMagique += savoir; _etat.Savoir += savoir; }
                int constitution = m.Statistiques.GetValueOrDefault("Constitution", 10);
                m.Fatigue = Math.Clamp((m.Fatigue ?? 0) + 20 -
                    ReglesCaracteristiques.Modificateur(Math.Clamp(constitution, 1, 20)), 0, 100);
                m.Bonheur = Math.Clamp(m.Bonheur + (victoire ? 1 : -1) - 10 * nombreMorts, 0, 100);
                m.Moral = Math.Clamp(m.Moral + (victoire ? 1 : -PerteMoral(m)) -
                    PerteMort(m) * nombreMorts, 0, 100);
                if (victoire) m.EcusPersonnels += ecusPart;
            }

            foreach (MembreGuilde mort in morts) _registreMembres.Membres.Remove(mort);
            _etat.Membres = _registreMembres.Membres.Count;
            _etat.Pouvoir = _registreMembres.Membres.Sum(m => (long)m.NiveauMagique);
            foreach (var exclu in _registreMembres.Membres.Where(m => !m.EstMaitreDeGuilde && m.Bonheur <= 0).ToList())
                _registreMembres.Membres.Remove(exclu);
            _etat.Membres = _registreMembres.Membres.Count;
            _etat.Pouvoir = _registreMembres.Membres.Sum(m => (long)m.NiveauMagique);
            _etat.Savoir = _registreMembres.Membres.Sum(m => (long)m.ConnaissanceMagique);

            AfficherValeur(_ecus, _etat.Ecus);
            AfficherValeur(_savoir, _etat.Savoir);
            AfficherValeur(_prestige, _etat.Prestige);
            AfficherValeur(_membres, _etat.Membres);
            AfficherValeur(_pouvoir, _etat.Pouvoir);
            AjouterEntreeJournal((victoire ? "Mission réussie" : "Mission échouée") +
                " : " + mission.Categorie + " (rang " + mission.Rang + ")." +
                (nombreMorts > 0 ? " " + nombreMorts + " membre(s) décédé(s)." : ""), "mission");
        }
        RaffraichirTableauMissions();
    }
}
