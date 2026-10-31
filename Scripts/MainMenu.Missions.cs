using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>Gestion des propositions, tableau des missions et départs automatiques.</summary>
public partial class MainMenu
{
    // Paramètres provisoires en attente des règles complètes de fatigue.
    private const int FatigueMaxPourPartir = 10;
    private const int MoralMinPourPartir = 25;
    private const int ChanceMissionQuotidienne = 10;
    private const int CibleMotivation = 10;
    private const double TauxSuccesMission = 0.50;

    private EtatMissions _etatMissions;
    private Window _fenetreNouvelleMission;
    private RichTextLabel _infoNouvelleMission;
    private GridContainer _tableauDesMissions;

    private void InitialiserMissions()
    {
        _etatMissions = EtatMissions.Charger();
        _fenetreNouvelleMission = GetNode<Window>("FenetreNouvelleMission");
        _infoNouvelleMission = GetNode<RichTextLabel>(
            "FenetreNouvelleMission/Marge/Layout/InfoMission");
        _tableauDesMissions = GetNode<GridContainer>(
            "LayoutsPanneauPrincipal/TableauDesMissions/ListeMission");

        GetNode<Button>("FenetreNouvelleMission/Marge/Layout/Boutons/BoutonAccepter")
            .Pressed += AccepterPropositionMission;
        GetNode<Button>("FenetreNouvelleMission/Marge/Layout/Boutons/BoutonRefuser")
            .Pressed += RefuserPropositionMission;
        _fenetreNouvelleMission.CloseRequested += RefuserPropositionMission;
        _fenetreNouvelleMission.Hide();

        RaffraichirTableauMissions();

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
        if (maintenant.Hour == 0 && maintenant.Minute == 0)
        {
            string jour = maintenant.ToString("yyyy-MM-dd");
            if (_etatMissions.DernierMinuitTraite != jour)
            {
                _etatMissions.DernierMinuitTraite = jour;
                if (_etatMissions.Proposition == null &&
                    string.IsNullOrEmpty(_etatMissions.DateAnnoncePrevue) &&
                    Random.Shared.Next(100) < ChanceMissionQuotidienne)
                {
                    // Horaire compris entre 08:00 et 16:00 inclus.
                    int minutes = Random.Shared.Next(8 * 60, 16 * 60 + 1);
                    _etatMissions.DateAnnoncePrevue =
                        ReglesMissions.DateIso(maintenant.Date.AddMinutes(minutes));
                }
            }
        }

        TerminerMissionsArrivees(maintenant);
        if (maintenant.Minute == 0 &&
            (maintenant.Hour == 0 || maintenant.Hour == 8 ||
             maintenant.Hour == 12 || maintenant.Hour == 16 ||
             maintenant.Hour == 20))
            ProposerMissionsAuxEquipes(maintenant);

        if (string.IsNullOrWhiteSpace(_etatMissions.DateAnnoncePrevue) ||
            maintenant < ReglesMissions.LireDate(_etatMissions.DateAnnoncePrevue))
            return false;

        _etatMissions.DateAnnoncePrevue = "";
        if (_etatMissions.Proposition != null) return false;
        _etatMissions.Proposition = ReglesMissions.Generer(_registreMembres.Membres);
        InterrompreAvanceeTemps("Une nouvelle mission est proposée à la guilde.");
        AfficherPropositionMission();
        return true;
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
        foreach (Node n in _tableauDesMissions.GetChildren())
        {
            _tableauDesMissions.RemoveChild(n);
            n.QueueFree();
        }

        string[] entetes = { "Rang", "Catégorie", "Demandeur", "Écus", "État", "Équipe" };
        foreach (string entete in entetes)
            AjouterCelluleMission(entete, true);

        foreach (MissionGuilde mission in _etatMissions.Missions
            .Where(m => m.Etat != "Terminee"))
        {
            AjouterCelluleMission(mission.Rang);
            AjouterCelluleMission(mission.Categorie);
            AjouterCelluleMission(mission.Demandeur);
            AjouterCelluleMission(mission.Recompense.ToString());
            AjouterCelluleMission(mission.Etat == "Disponible" ? "Disponible" : "En cours");
            var participants = _registreMembres?.Membres
                .Where(m => mission.ParticipantIds.Contains(m.Id))
                .Select(m => m.Prenom).ToArray() ?? Array.Empty<string>();
            AjouterCelluleMission(participants.Length == 0
                ? "—" : string.Join(", ", participants));
        }
    }

    private void AjouterCelluleMission(string contenu, bool entete = false)
    {
        Label cellule = new()
        {
            Text = contenu,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(115, 36)
        };
        if (entete) cellule.AddThemeFontSizeOverride("font_size", 17);
        _tableauDesMissions.AddChild(cellule);
    }

    private void ProposerMissionsAuxEquipes(DateTime instant)
    {
        if (_etatMissions == null || _registreMembres == null) return;
        if (!_etatMissions.Missions.Any(m => m.Etat == "Disponible")) return;

        // Les vrais groupes et les indépendants sont chacun une unité de départ.
        // Le maître de guilde n'effectue pas encore de départ automatique.
        var candidats = _registreMembres.Membres.Where(m => !m.EstMaitreDeGuilde)
            .GroupBy(m => string.IsNullOrWhiteSpace(m.EquipeId)
                ? "solo:" + m.Id : "equipe:" + m.EquipeId)
            .Select(g => g.ToList())
            .OrderBy(_ => Random.Shared.Next()).ToList();

        foreach (List<MembreGuilde> equipe in candidats)
        {
            MissionGuilde mission = _etatMissions.Missions
                .Where(m => m.Etat == "Disponible" &&
                    equipe.All(p => ReglesMissions.RangSuffisant(p, m.Rang)))
                .OrderBy(_ => Random.Shared.Next()).FirstOrDefault();
            if (mission == null) continue;
            if (equipe.Any(m => (m.Fatigue ?? 0) > FatigueMaxPourPartir ||
                                m.Moral < MoralMinPourPartir)) continue;
            if (_etatMissions.Missions.Any(m => m.Etat == "EnCours" &&
                m.ParticipantIds.Any(id => equipe.Any(p => p.Id == id)))) continue;

            // Une équipe part seulement si chacun de ses membres est partant.
            bool motivés = equipe.All(m =>
                Random.Shared.Next(1, 21) +
                ReglesCaracteristiques.Modificateur(Math.Clamp(m.Motivation ?? 10, 1, 20))
                >= CibleMotivation);
            if (!motivés) continue;

            mission.Etat = "EnCours";
            mission.DateDepart = ReglesMissions.DateIso(instant);
            mission.DateRetour = ReglesMissions.DateIso(instant.AddDays(1));
            mission.ParticipantIds = equipe.Select(m => m.Id).ToList();
            AjouterEntreeJournal(string.Join(", ", equipe.Select(m => m.NomComplet)) +
                " partent en mission (" + mission.Rang + " : " + mission.Categorie + ").",
                "mission");
        }
        RaffraichirTableauMissions();
    }

    private void TerminerMissionsArrivees(DateTime instant)
    {
        if (_etatMissions == null || _registreMembres == null || _etat == null) return;
        foreach (MissionGuilde mission in _etatMissions.Missions.Where(m =>
            m.Etat == "EnCours" && !string.IsNullOrEmpty(m.DateRetour) &&
            ReglesMissions.LireDate(m.DateRetour) <= instant).ToList())
        {
            List<MembreGuilde> participants = _registreMembres.Membres
                .Where(m => mission.ParticipantIds.Contains(m.Id)).ToList();
            mission.Etat = "Terminee";
            bool victoire = Random.Shared.NextDouble() < TauxSuccesMission;
            mission.Reussie = victoire;
            _etat.Prestige = Math.Max(0,
                _etat.Prestige + ReglesMissions.VariationPrestige(mission.Rang, victoire));

            int xpPart = participants.Count == 0 ? 0 :
                ReglesMissions.ExperiencePourMission(mission.Rang) / participants.Count;
            int ecusPart = victoire && participants.Count > 0
                ? mission.Recompense / participants.Count : 0;

            foreach (MembreGuilde m in participants)
            {
                ReglesMissions.AjouterExperience(m, xpPart);
                if (victoire)
                    ReglesMissions.EnregistrerReussiteEtPromouvoir(m, mission.Rang);
                int connaissance = Random.Shared.Next(0, 3);
                m.ConnaissanceMagique += connaissance;
                _etat.Savoir += connaissance;

                // Application littérale de « fait perdre de la fatigue ».
                int constitution = m.Statistiques.GetValueOrDefault("Constitution", 10);
                int variation = 20 + ReglesCaracteristiques.Modificateur(
                    Math.Clamp(constitution, 1, 20));
                m.Fatigue = Math.Max(0, (m.Fatigue ?? 0) - variation);
                if (victoire)
                    _etat.Ecus += m.EstMaitreDeGuilde ? ecusPart
                        : ecusPart * 20 / 100; // 20 % pour la guilde
            }

            AfficherValeur(_ecus, _etat.Ecus);
            AfficherValeur(_savoir, _etat.Savoir);
            AfficherValeur(_prestige, _etat.Prestige);
            AjouterEntreeJournal((victoire ? "Mission réussie" : "Mission échouée") +
                " : " + mission.Categorie + " (rang " + mission.Rang + ").",
                "mission");
        }
        RaffraichirTableauMissions();
    }
}
