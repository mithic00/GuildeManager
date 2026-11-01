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
    private Control _tableauDesMissions;
    private const float TailleParchemin = 60f;
    private const float MargeParchemin = 8f;

    private void InitialiserMissions()
    {
        _etatMissions = EtatMissions.Charger();
        _fenetreNouvelleMission = GetNode<Window>("FenetreNouvelleMission");
        _infoNouvelleMission = GetNode<RichTextLabel>(
            "FenetreNouvelleMission/Marge/Layout/InfoMission");
        _tableauDesMissions = GetNode<Control>(
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

        _tableauDesMissions.Resized += PositionnerParchemins;
        RaffraichirTableauMissions();
        CallDeferred(nameof(PositionnerParchemins));

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

        Texture2D textureMission = GD.Load<Texture2D>("res://Images/UI/Bouton_Mission.png");
        foreach (MissionGuilde mission in _etatMissions.Missions
            .Where(m => m.Etat == "Disponible" || m.Etat == "EnCours"))
        {
            MissionGuilde selection = mission;
            TextureButton bouton = new()
            {
                Name = "Mission_" + mission.Id,
                TextureNormal = textureMission,
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.Scale,
                CustomMinimumSize = new Vector2(TailleParchemin, TailleParchemin),
                Size = new Vector2(TailleParchemin, TailleParchemin),
                TooltipText = mission.Categorie + " — Rang " + mission.Rang +
                    " — " + (mission.Etat == "EnCours" ? "En cours" : "Disponible")
            };
            bouton.Pressed += () => OuvrirInfoMission(selection);
            _tableauDesMissions.AddChild(bouton);
        }
        PositionnerParchemins();
    }

    /// <summary>
    /// Utilise des coordonnées relatives persistées. À l'apparition d'une mission,
    /// cherche une place libre, sans déplacer les parchemins déjà présents.
    /// </summary>
    private void PositionnerParchemins()
    {
        if (_tableauDesMissions == null || _etatMissions == null) return;
        Vector2 surface = _tableauDesMissions.Size;
        if (surface.X < TailleParchemin || surface.Y < TailleParchemin)
            return; // Le conteneur n'est pas encore disposé par Godot.

        float largeur = Math.Max(0, surface.X - TailleParchemin);
        float hauteur = Math.Max(0, surface.Y - TailleParchemin);
        var occupees = new List<Rect2>();
        var actives = _etatMissions.Missions
            .Where(m => m.Etat == "Disponible" || m.Etat == "EnCours").ToList();

        // Traiter les anciennes positions en premier évite de les modifier
        // lorsque de nouvelles missions arrivent.
        foreach (MissionGuilde mission in actives
            .OrderByDescending(m => m.PositionTableauX.HasValue && m.PositionTableauY.HasValue))
        {
            TextureButton bouton = _tableauDesMissions.GetNodeOrNull<TextureButton>(
                "Mission_" + mission.Id);
            if (bouton == null) continue;

            bool possedePosition = mission.PositionTableauX.HasValue &&
                mission.PositionTableauY.HasValue;
            Vector2 position = possedePosition
                ? new Vector2(
                    Math.Clamp(mission.PositionTableauX.Value, 0f, 1f) * largeur,
                    Math.Clamp(mission.PositionTableauY.Value, 0f, 1f) * hauteur)
                : TrouverPositionLibre(largeur, hauteur, occupees);

            if (!possedePosition)
            {
                mission.PositionTableauX = largeur > 0 ? position.X / largeur : 0;
                mission.PositionTableauY = hauteur > 0 ? position.Y / hauteur : 0;
            }

            bouton.Position = position;
            bouton.Size = new Vector2(TailleParchemin, TailleParchemin);
            occupees.Add(new Rect2(position, new Vector2(TailleParchemin, TailleParchemin)));
        }
    }

    private static Vector2 TrouverPositionLibre(float largeur, float hauteur, List<Rect2> occupees)
    {
        Vector2 taille = new(TailleParchemin + MargeParchemin,
            TailleParchemin + MargeParchemin);
        bool Libre(Vector2 p) => occupees.All(r =>
            !new Rect2(p - new Vector2(MargeParchemin / 2, MargeParchemin / 2),
                taille).Intersects(r));

        for (int essai = 0; essai < 160; essai++)
        {
            Vector2 position = new(
                (float)Random.Shared.NextDouble() * largeur,
                (float)Random.Shared.NextDouble() * hauteur);
            if (Libre(position)) return position;
        }

        // Cherche ensuite un emplacement sur un quadrillage décalé aléatoirement.
        // Si le tableau est plein, limite les collisions autant que possible.
        Vector2 meilleure = Vector2.Zero;
        int collisionsMin = int.MaxValue;
        int colonnes = Math.Max(1, (int)MathF.Ceiling(largeur / 12f) + 1);
        int lignes = Math.Max(1, (int)MathF.Ceiling(hauteur / 12f) + 1);
        int decalageX = Random.Shared.Next(colonnes);
        int decalageY = Random.Shared.Next(lignes);
        for (int y = 0; y < lignes; y++)
            for (int x = 0; x < colonnes; x++)
            {
                Vector2 p = new(
                    Math.Min(largeur, ((x + decalageX) % colonnes) * 12f),
                    Math.Min(hauteur, ((y + decalageY) % lignes) * 12f));
                if (Libre(p)) return p;
                int collisions = occupees.Count(r =>
                    new Rect2(p, new Vector2(TailleParchemin, TailleParchemin)).Intersects(r));
                if (collisions < collisionsMin)
                {
                    collisionsMin = collisions;
                    meilleure = p;
                }
            }
        return meilleure;
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
                string sujetDepart = equipe.Count == 1
                    ? equipe[0].NomComplet
                    : "Equipe de " + equipe[0].NomComplet;
                AjouterEntreeJournal(sujetDepart +
                    " part en mission (" + mission.Rang + " : " + mission.Categorie + ").",
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

            // Le prestige dépend du rang de la mission, jamais de celui de
            // l'équipe qui l'a effectuée. Conserver la variation réelle permet
            // de diagnostiquer les gains compensés par une autre mission.
            long prestigeAvantMission = _etat.Prestige;
            int variationPrestige = ReglesMissions.VariationPrestige(mission.Rang, victoire);
            _etat.Prestige = Math.Max(0, prestigeAvantMission + variationPrestige);
            long prestigeGagneOuPerdu = _etat.Prestige - prestigeAvantMission;

            int xpPart = participants.Count == 0 ? 0 :
                ReglesMissions.ExperiencePourMission(mission.Rang) / participants.Count;
            int partGuilde = victoire ? mission.Recompense * 20 / 100 : 0;
            int ecusPart = victoire ? (mission.Recompense - partGuilde) / participants.Count : 0;
            _etat.Ecus += partGuilde;

            foreach (MembreGuilde m in survivants)
            {
                if (victoire)
                {
                    string ancienRang = m.Rang;
                    if (ReglesMissions.EnregistrerReussiteEtPromouvoir(m, mission.Rang))
                        AjouterEntreeJournal(m.NomComplet + " est promu au rang " +
                            m.Rang + " (ancien rang : " + ancienRang + ").", "mission");
                }
                if (!resultat.SansIncident)
                {
                    ReglesMissions.AjouterExperience(m, xpPart);
                    if (victoire)
                    {
                        bool magieElevee = mission.Categorie == "Élimination de danger" ||
                            mission.Categorie == "Recherche mort ou vif" ||
                            mission.Categorie == "Renfort pour Assaut";
                        bool magieStandard = magieElevee || mission.Categorie == "Escorte" ||
                            mission.Categorie == "Capture vif" || mission.Categorie == "Protection d'un lieu";
                        if (magieStandard)
                        {
                            ResolutionMissions.ProgresserMagie(m, magieElevee);
                            string rangAvantProgression = m.Rang;
                            if (ReglesMissions.AppliquerPromotionsAcquises(m))
                                AjouterEntreeJournal(m.NomComplet + " est promu au rang " +
                                    m.Rang + " (ancien rang : " + rangAvantProgression + ").", "mission");
                        }
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

            // Le retour est notifié même lorsque la mission a échoué.
            // En cas de décès, seuls les survivants rentrent effectivement.
            if (survivants.Count > 0)
            {
                // Ne jamais annoncer le retour d'un membre qui est décédé.
                string sujetRetour = survivants.Count > 1
                    ? "Equipe de " + survivants[0].NomComplet
                    : survivants[0].NomComplet;
                AjouterEntreeJournal(sujetRetour + " rentre de mission (" +
                    mission.Rang + " : " + mission.Categorie + ").", "mission");
            }

            foreach (MembreGuilde mort in morts) _registreMembres.Membres.Remove(mort);
            _etat.Membres = _registreMembres.Membres.Count;
            _etat.Pouvoir = _registreMembres.Membres.Sum(m => (long)m.NiveauMagique);
            foreach (var exclu in _registreMembres.Membres.Where(m => !m.EstMaitreDeGuilde && m.Bonheur <= 0).ToList())
                _registreMembres.Membres.Remove(exclu);

            // Après les décès et les départs de la guilde, une équipe réduite
            // à un seul membre est dissoute : son survivant devient indépendant.
            // Le système de départ en mission et la fiche de membre utilisent
            // déjà EquipeId vide pour identifier les membres solos.
            var membresDevenusSolos = _registreMembres.Membres
                .Where(m => !string.IsNullOrWhiteSpace(m.EquipeId))
                .GroupBy(m => m.EquipeId)
                .Where(g => g.Count() == 1)
                .Select(g => g.First())
                .ToList();
            foreach (MembreGuilde membre in membresDevenusSolos)
                membre.EquipeId = "";

            _etat.Membres = _registreMembres.Membres.Count;
            _etat.Pouvoir = _registreMembres.Membres.Sum(m => (long)m.NiveauMagique);
            _etat.Savoir = _registreMembres.Membres.Sum(m => (long)m.ConnaissanceMagique);

            AfficherValeur(_ecus, _etat.Ecus);
            AfficherValeur(_savoir, _etat.Savoir);
            AfficherValeur(_prestige, _etat.Prestige);
            AfficherValeur(_membres, _etat.Membres);
            AfficherValeur(_pouvoir, _etat.Pouvoir);
            string signePrestige = prestigeGagneOuPerdu > 0 ? "+" : "";
            string descriptionResultat = (victoire ? "Mission réussie" : "Mission échouée") +
                " : " + mission.Categorie + " (rang " + mission.Rang + ")." +
                " Prestige : " + signePrestige + prestigeGagneOuPerdu +
                " (total : " + _etat.Prestige + ").";
            if (morts.Count == 1)
                descriptionResultat += " " + morts[0].NomComplet + " est décédé en mission.";
            else if (morts.Count > 1)
                descriptionResultat += " Membres décédés en mission : " +
                    string.Join(", ", morts.Select(m => m.NomComplet)) + ".";

            // Le type de résultat est conservé dans le journal pour que sa
            // couleur reste la même après sauvegarde et rechargement.
            string typeResultat = victoire ? "mission_reussite" :
                morts.Count > 0 ? "mission_deces" : "mission_echec";
            AjouterEntreeJournal(descriptionResultat, typeResultat);
        }
        RaffraichirTableauMissions();
    }
}
