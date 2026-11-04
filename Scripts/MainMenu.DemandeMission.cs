using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Demande directe du maître de guilde : dialogue court puis jet de Leadership
/// dont la difficulté dépend de la relation avec le membre.
/// </summary>
public partial class MainMenu
{
    private Button _boutonDemanderMission;
    private bool _choixMissionEnAttente;
    private bool _confirmationMissionEnAttente;
    private readonly List<MissionGuilde> _missionsDialogueDisponibles = new();
    private MissionGuilde _missionDialogueSelectionnee;

    private void InitialiserDialogueDemandeMission(string interfaceDialogue)
    {
        const string nomBouton = "BoutonDemanderMission";
        _boutonDemanderMission = GetNodeOrNull<Button>(
            interfaceDialogue + "ListeSujetDiscussion/" + nomBouton);
        if (_boutonDemanderMission == null)
        {
            // La scène n'est jamais modifiée : le bouton est ajouté à l'exécution
            // tant que le créateur ne l'a pas placé dans l'éditeur Godot.
            _boutonDemanderMission = new Button
            {
                Name = nomBouton,
                Text = "Demander une mission",
                CustomMinimumSize = new Vector2(260, 65),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            _sujetsDiscussion.AddChild(_boutonDemanderMission);
        }
        _boutonDemanderMission.Pressed += ProposerDialogueDemandeMission;
    }

    private void ReinitialiserDialogueDemandeMission()
    {
        _choixMissionEnAttente = false;
        _confirmationMissionEnAttente = false;
        _missionDialogueSelectionnee = null;
        _missionsDialogueDisponibles.Clear();
    }

    private static int CibleLeadershipMission(string relation) => relation switch
    {
        "Proche" => 5,
        "Ami" => 8,
        "Connaissance" => 10,
        "Neutre" => 12,
        "Inamical" => 14,
        _ => int.MaxValue
    };

    private static string PhraseDemandeMission(string relation) => relation switch
    {
        "Proche" => "J'aurais besoin d'un coup de main sur cette mission. Ça te dit de t'en occuper ?",
        "Ami" => "Dis, tu pourrais prendre cette mission pour la guilde ? Ça me rendrait service.",
        "Connaissance" => "J'aimerais te confier cette mission. Tu peux t'en charger pour la guilde ?",
        "Inamical" => "Je vous demande de prendre en charge cette mission au nom de la guilde.",
        _ => "Pouvez-vous prendre en charge cette mission pour la guilde ?"
    };

    private static string ReponseDemandeMission(string relation, bool accord) =>
        accord ? relation switch
        {
            "Proche" => "Pour toi, bien sûr ! On s'en occupe.",
            "Ami" => "Pas de souci, compte sur moi !",
            "Connaissance" => "D'accord, je vais m'en charger.",
            "Inamical" => "Très bien. Je remplirai cette mission.",
            _ => "Entendu, je vais m'en charger."
        } : relation switch
        {
            "Proche" => "Désolé, je ne le sens pas cette fois.",
            "Ami" => "Je préfère passer mon tour, désolé.",
            "Connaissance" => "Je préfère ne pas prendre cette mission.",
            "Inamical" => "Je ne compte pas accepter cette demande.",
            _ => "Je préfère décliner cette mission."
        };

    private List<MissionGuilde> MissionsDemandablesParCible(MembreGuilde cible)
    {
        List<MembreGuilde> equipe = EquipeDuMembrePourMission(cible);
        return _etatMissions?.Missions
            .Where(m => MissionDemandableParEquipe(equipe, m))
            .ToList() ?? new List<MissionGuilde>();
    }

    private void ActualiserBoutonDemanderMission()
    {
        if (_boutonDemanderMission == null) return;
        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        bool relationCompatible = joueur != null && _cibleDiscussion != null &&
            CibleLeadershipMission(RelationsMembres.Qualifier(
                RelationsMembres.Lire(joueur, _cibleDiscussion))) != int.MaxValue;
        _boutonDemanderMission.Visible = relationCompatible &&
            MissionsDemandablesParCible(_cibleDiscussion).Count > 0;
    }

    private void ProposerDialogueDemandeMission()
    {
        if (_discussionEnCours || _cibleDiscussion == null || _horloge == null ||
            !DialogueAccessible(_cibleDiscussion, true, verifierHoraire: false))
            return;

        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        if (joueur == null) return;
        string relation = RelationsMembres.Qualifier(
            RelationsMembres.Lire(joueur, _cibleDiscussion));
        if (CibleLeadershipMission(relation) == int.MaxValue)
            return;

        ReinitialiserDialogueDemandeMission();
        _missionsDialogueDisponibles.AddRange(MissionsDemandablesParCible(_cibleDiscussion));
        if (_missionsDialogueDisponibles.Count == 0)
        {
            ActualiserBoutonDemanderMission();
            return;
        }

        _connaissanceAContinuer = false;
        _soutienMoralEnAttente = false;
        _choixMissionEnAttente = true;
        _resultatDiscussion.Text = "Vous : " +
            (relation == "Ami" || relation == "Proche"
                ? "J'ai une mission en tête et je pensais à toi."
                : relation == "Connaissance"
                    ? "J'aimerais te proposer une mission de la guilde."
                    : "J'aimerais vous confier une mission de la guilde.") +
            "\nChoisissez une mission disponible :";

        _sujetsDiscussion.Hide();
        _reponsesDiscussion.Show();
        _listeReponsesDiscussion.Clear();
        foreach (MissionGuilde mission in _missionsDialogueDisponibles)
            _listeReponsesDiscussion.AddItem(
                (string.IsNullOrWhiteSpace(mission.Titre) ? mission.Categorie : mission.Titre) +
                " — " + ReglesMissions.FormaterRang(mission.Rang));
        _listeReponsesDiscussion.AddItem("Mettre fin à la conversation");
    }

    private void TraiterChoixDialogueDemandeMission(int choix)
    {
        if (_choixMissionEnAttente)
        {
            _choixMissionEnAttente = false;
            if (choix < 0 || choix >= _missionsDialogueDisponibles.Count)
            {
                FermerDiscussion();
                return;
            }
            _missionDialogueSelectionnee = _missionsDialogueDisponibles[choix];
            MembreGuilde joueur = _registreMembres.Membres.First(m => m.EstJoueur);
            string relation = RelationsMembres.Qualifier(
                RelationsMembres.Lire(joueur, _cibleDiscussion));
            _resultatDiscussion.Text = "Vous : " + PhraseDemandeMission(relation) +
                "\nMission : " +
                (string.IsNullOrWhiteSpace(_missionDialogueSelectionnee.Titre)
                    ? _missionDialogueSelectionnee.Categorie : _missionDialogueSelectionnee.Titre) +
                " (" + ReglesMissions.FormaterRang(_missionDialogueSelectionnee.Rang) + ")";
            _confirmationMissionEnAttente = true;
            _listeReponsesDiscussion.Clear();
            _listeReponsesDiscussion.AddItem("Faire la demande (D20 Leadership)");
            _listeReponsesDiscussion.AddItem("Mettre fin à la conversation");
            return;
        }

        if (!_confirmationMissionEnAttente) return;
        _confirmationMissionEnAttente = false;
        if (choix != 0)
        {
            FermerDiscussion();
            return;
        }
        ResoudreDialogueDemandeMission();
    }

    private void ResoudreDialogueDemandeMission()
    {
        if (_discussionEnCours || _cibleDiscussion == null || _horloge == null ||
            _missionDialogueSelectionnee == null)
            return;

        MembreGuilde joueur = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur);
        if (joueur == null || !DialogueAccessible(_cibleDiscussion, true,
                verifierHoraire: false))
        {
            FermerDiscussion();
            return;
        }
        MembreGuilde cible = _cibleDiscussion;
        List<MembreGuilde> equipe = EquipeDuMembrePourMission(cible);
        MissionGuilde mission = _missionDialogueSelectionnee;
        if (!MissionDemandableParEquipe(equipe, mission))
        {
            _resultatDiscussion.Text =
                "Cette mission n'est plus disponible pour l'équipe.";
            AfficherReponsesDiscussion(false);
            ReinitialiserDialogueDemandeMission();
            return;
        }

        string relation = RelationsMembres.Qualifier(RelationsMembres.Lire(joueur, cible));
        int seuil = CibleLeadershipMission(relation);
        if (seuil == int.MaxValue)
        {
            FermerDiscussion();
            return;
        }

        _discussionEnCours = true;
        int leadership = joueur.Statistiques?.GetValueOrDefault("Leadership", 10) ?? 10;
        int modificateur = ReglesCaracteristiques.Modificateur(Math.Clamp(leadership, 1, 20));
        int de = Random.Shared.Next(1, 21);
        int total = de + modificateur;
        bool accepte = total >= seuil;
        bool depart = false;
        if (accepte && GroupeDisponible(equipe))
        {
            BonusTraitsMissions.Bilan bilan = BonusTraitsMissions.Evaluer(equipe, mission.Categorie);
            DemarrerMissionEquipe(equipe, mission, _horloge.Instant, bilan);
            RaffraichirTableauMissions();
            depart = true;
        }

        string signe = modificateur >= 0 ? "+" : "-";
        string jet = "D20 " + de + " " + signe + " " +
            Math.Abs(modificateur) + " = " + total +
            " (" + (accepte ? "réussite" : "échec") + ")";
        _resultatDiscussion.Text = "Vous : " + PhraseDemandeMission(relation) +
            "\n" + cible.Prenom + " : " + ReponseDemandeMission(relation, accepte) +
            "\n\nJet de Leadership : " + jet +
            (accepte && !depart
                ? "\nL'équipe ne peut finalement pas partir (fatigue ou indisponibilité)."
                : depart ? "\nL'équipe part en mission." : "\nLa mission reste disponible.") +
            "\nTemps écoulé : 10 minutes.";

        AjouterEntreeJournal("Demande de mission à " + cible.NomComplet +
            " : " + (depart ? "départ accepté" : accepte ?
                "accord obtenu, départ impossible" : "demande refusée") +
            " (" + ReglesMissions.FormaterRang(mission.Rang) +
            " : " + mission.Categorie + ").", "discussion");
        _historiqueMembres?.Ajouter(cible, _horloge.Instant, "DemandeMission",
            "Demande du maître de guilde : " + (depart ? "départ en mission" :
                accepte ? "accord sans départ" : "refus") + " (" + jet + ").");

        bool tempsComplet = AvancerTempsPourDiscussion(10);
        AfficherReponsesDiscussion(false);
        ReinitialiserDialogueDemandeMission();
        _discussionEnCours = false;
        if (!tempsComplet)
            FermerDiscussion();
    }
}
