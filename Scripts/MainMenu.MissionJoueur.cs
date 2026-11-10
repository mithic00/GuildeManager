using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Départ volontaire du maître de guilde : les mêmes contrôles de rang,
/// caractéristiques, moral et fatigue que pour un membre ordinaire.
/// Pendant son absence, seul MainBouton permet de faire avancer ou stopper
/// le temps. Les états antérieurs des boutons sont restaurés à son retour.
/// </summary>
public partial class MainMenu
{
    private Button _boutonPrendreMission;
    private MissionGuilde _missionSelectionneePourJoueur;

    // Les contrôles des autres fenêtres sont inclus : une interaction
    // différée ou une reconstruction de parchemin ne peut pas les activer.
    private readonly Dictionary<BaseButton, bool> _boutonsAvantMissionJoueur = new();

    private bool JoueurEnMission =>
        _registreMembres?.Membres != null && _etatMissions?.Missions != null &&
        _registreMembres.Membres.Any(joueur => joueur.EstJoueur &&
            _etatMissions.Missions.Any(mission =>
                mission.Etat == "EnCours" && mission.ParticipantIds != null &&
                mission.ParticipantIds.Contains(joueur.Id)));

    private MembreGuilde PersonnageJoueurPourMission() =>
        _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);

    private void InitialiserPriseMissionJoueur()
    {
        _boutonPrendreMission = GetNode<Button>(
            "FenetreInfoMission/Marge/Layout/Boutons/BoutonPrendre");
        _boutonPrendreMission.Pressed += PrendreMissionJoueur;
        _boutonPrendreMission.Disabled = true;
    }

    private void ActualiserPriseMissionJoueur()
    {
        if (_boutonPrendreMission == null) return;
        MembreGuilde joueur = PersonnageJoueurPourMission();
        MissionGuilde mission = _missionSelectionneePourJoueur;
        bool disponible = joueur != null && mission != null && !JoueurEnMission &&
            MissionDemandableParEquipe(new List<MembreGuilde> { joueur }, mission);
        _boutonPrendreMission.Disabled = !disponible;

        _boutonPrendreMission.TooltipText = joueur == null
            ? "Personnage du joueur indisponible."
            : mission == null || mission.Etat != "Disponible"
                ? "Cette mission n'est plus disponible."
                : JoueurEnMission
                    ? "Votre personnage est déjà en mission."
                    : joueur.Moral < MoralMinPourPartir
                        ? "Moral insuffisant : il faut au moins " +
                          MoralMinPourPartir + " points pour partir."
                        : disponible
                            ? "Envoyer votre personnage accomplir cette mission."
                            : "Mission inaccessible : vérifiez le rang, les aptitudes, " +
                              "la fatigue et les autres indisponibilités.";
    }

    private void PrendreMissionJoueur()
    {
        MembreGuilde joueur = PersonnageJoueurPourMission();
        MissionGuilde mission = _missionSelectionneePourJoueur;
        if (joueur == null || mission == null || _horloge == null ||
            _tempsEnCours || _traitementTempsDiscussion || JoueurEnMission)
            return;

        var equipe = new List<MembreGuilde> { joueur };
        if (!MissionDemandableParEquipe(equipe, mission))
        {
            ActualiserPriseMissionJoueur();
            if (joueur.Moral < MoralMinPourPartir)
            {
                _fenetreInfoMission.Hide();
                AfficherInformation("Votre personnage a besoin d'au moins " +
                    MoralMinPourPartir + " points de moral pour partir en mission.");
            }
            return;
        }

        // Le jet de Constitution éventuel (fatigue élevée) intervient
        // seulement lors du clic, jamais en survolant ou ouvrant la fenêtre.
        if (!GroupeDisponible(equipe))
        {
            // Deux fenêtres exclusives ne doivent pas être ouvertes ensemble.
            _fenetreInfoMission.Hide();
            AfficherInformation("Votre personnage n'est pas en état de partir " +
                "en mission pour le moment.");
            return;
        }

        BonusTraitsMissions.Bilan bilan =
            BonusTraitsMissions.Evaluer(equipe, mission.Categorie);
        _fenetreInfoMission.Hide();
        _missionSelectionneePourJoueur = null;
        DemarrerMissionEquipe(equipe, mission, _horloge.Instant, bilan);
        RaffraichirTableauMissions();

        // Seul le bureau et son journal restent visibles pendant l'absence.
        AfficherPanneau("accueil");
        ActualiserBoutonTemps();
    }

    private static IEnumerable<BaseButton> BoutonsDuSousArbre(Node noeud)
    {
        foreach (Node enfant in noeud.GetChildren())
        {
            if (enfant is BaseButton bouton)
                yield return bouton;
            foreach (BaseButton bouton in BoutonsDuSousArbre(enfant))
                yield return bouton;
        }
    }

    private void SynchroniserBoutonsMissionJoueur()
    {
        if (JoueurEnMission)
        {
            // Les parchemins de mission sont recréés régulièrement.
            // Ne pas garder en mémoire leurs anciens boutons libérés.
            foreach (BaseButton obsolete in _boutonsAvantMissionJoueur.Keys
                .Where(b => !GodotObject.IsInstanceValid(b)).ToList())
                _boutonsAvantMissionJoueur.Remove(obsolete);

            foreach (BaseButton bouton in BoutonsDuSousArbre(this))
            {
                if (bouton == _boutonAvancerTemps) continue;
                if (!_boutonsAvantMissionJoueur.ContainsKey(bouton))
                    _boutonsAvantMissionJoueur[bouton] = bouton.Disabled;
                bouton.Disabled = true;
            }
            return;
        }

        if (_boutonsAvantMissionJoueur.Count == 0) return;
        foreach (var entree in _boutonsAvantMissionJoueur)
            if (GodotObject.IsInstanceValid(entree.Key))
                entree.Key.Disabled = entree.Value;
        _boutonsAvantMissionJoueur.Clear();
    }
}
