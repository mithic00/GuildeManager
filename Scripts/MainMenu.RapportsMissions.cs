using Godot;
using System;
using System.Linq;

/// <summary>
/// Lecture des rapports depuis le journal de quête dans la fenêtre dédiée
/// FenetreRapportMission. Aucune modification de scène n'est nécessaire.
/// </summary>
public partial class MainMenu
{
    private Window _fenetreRapportMission;
    private RichTextLabel _texteRapportMission;

    private void InitialiserRapportsMissions()
    {
        _fenetreRapportMission = GetNode<Window>("FenetreRapportMission");
        _texteRapportMission = GetNode<RichTextLabel>(
            "FenetreRapportMission/Marge/Layout/InfoMission");

        GetNode<Button>("FenetreRapportMission/Marge/Layout/BoutonFermer")
            .Pressed += FermerRapportMission;
        _fenetreRapportMission.CloseRequested += FermerRapportMission;
        _fenetreRapportMission.Hide();
    }

    private void OuvrirRapportMissionDepuisJournal(Variant meta)
    {
        if (_tempsEnCours || _traitementTempsDiscussion || JoueurEnMission ||
            _fenetreRapportMission == null || _etatMissions?.Missions == null)
            return;

        string identifiant = meta.AsString();
        if (string.IsNullOrWhiteSpace(identifiant)) return;

        MissionGuilde mission = _etatMissions.Missions.FirstOrDefault(
            m => m.Id == identifiant && m.Etat == "Terminee");
        if (mission == null) return;

        // Modernise les rapports antérieurs à la correction des répétitions
        // et des accords français, sans toucher aux événements de mission.
        RapportsMissions.ModerniserAncienRapport(mission);

        _texteRapportMission.Text = string.IsNullOrWhiteSpace(mission.RapportNarratif)
            ? "Aucun récit détaillé n'a été conservé pour cette ancienne mission."
            : mission.RapportNarratif;

        _fenetreRapportMission.PopupCentered();
    }

    private void FermerRapportMission()
    {
        _fenetreRapportMission?.Hide();
    }
}
