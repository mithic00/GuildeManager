using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Récits courts fondés sur les étapes réellement résolues, sans dévoiler
/// les jets de dés ni leurs valeurs. Tout est mémorisé dans MissionGuilde et
/// reste lisible même après le départ ou le décès des participants.
/// </summary>
public static class RapportsMissions
{
    /// <summary>Garde quelques étapes marquantes d'une longue mission :
    /// les trois premières, puis la plus récente.</summary>
    public static void Consigner(MissionGuilde mission, string scene)
    {
        if (mission == null || string.IsNullOrWhiteSpace(scene)) return;
        mission.EtapesRapport ??= new List<string>();
        if (mission.EtapesRapport.Count < 4)
            mission.EtapesRapport.Add(scene.Trim());
        else
            mission.EtapesRapport[3] = scene.Trim();
    }

    public static string Rediger(MissionGuilde mission,
        IReadOnlyList<MembreGuilde> participants, IssueMission issue,
        IReadOnlyList<MembreGuilde> morts)
    {
        if (mission == null) return "Rapport indisponible.";

        string[] noms = mission.ParticipantsRapport?.Where(x =>
            !string.IsNullOrWhiteSpace(x)).ToArray() ?? Array.Empty<string>();
        if (noms.Length == 0)
            noms = participants?.Where(m => m != null)
                .Select(m => m.NomComplet).ToArray() ?? Array.Empty<string>();

        string commanditaire = mission.Demandeur switch
        {
            "Cité" => "les autorités de la cité",
            "Autre Cité" => "les autorités d'une cité étrangère",
            "Particulier" => "un particulier",
            "Marchand" => "un marchand",
            "Commerçant de la ville" => "un commerçant de la ville",
            "Guilde de Mercenaire" => "une guilde de mercenaires",
            "" => "un commanditaire non identifié",
            _ => mission.Demandeur
        };
        int jours = Math.Max(1, mission.JoursEcoules);
        string duree = jours == 1 ? "une journée" : jours + " jours";
        string sujet = noms.Length switch
        {
            0 => "Les membres envoyés",
            1 => noms[0],
            _ => "L'équipe de " + noms[0]
        };
        string introduction = sujet + " a répondu à la demande de " +
            commanditaire + " pour une mission de " +
            mission.Categorie.ToLowerInvariant() + ".";

        string rythme = jours switch
        {
            1 => "L'intervention s'est déroulée en une journée.",
            2 => "L'expédition a occupé l'équipe pendant deux journées.",
            <= 5 => "Plusieurs jours d'efforts ont été nécessaires.",
            _ => "Le travail s'est prolongé de nombreux jours avant son dénouement."
        };

        string traces = string.Join(" ", (mission.EtapesRapport ??
            new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(traces))
            traces = mission.Categorie switch
            {
                "Enquête" => "Les recherches ont mené l'équipe de piste en piste.",
                "Escorte" => "Le groupe a suivi son itinéraire en veillant sur le convoi.",
                "Protection d'un lieu" => "Les membres ont surveillé les accès et maintenu leurs tours de garde.",
                "Élimination de danger" => "L'équipe est allée au-devant de la menace signalée.",
                "Renfort pour Assaut" => "Les membres ont prêté main-forte à l'offensive.",
                "Capture vif" => "Une traque minutieuse a été engagée.",
                "Recherche mort ou vif" => "L'équipe a suivi la trace de sa cible.",
                "Recherche d'un objet" => "Les indices et les témoignages ont orienté les recherches.",
                "Recherche d'un ingrédient" => "L'expédition a parcouru plusieurs terrains à la recherche de ressources.",
                "Recherche d'un animal" => "Le groupe a étudié les traces et les passages de l'animal.",
                "Service" => "La mission a demandé patience et attention aux personnes rencontrées.",
                _ => "L'équipe a progressé en suivant les instructions du commanditaire."
            };

        string conclusion = issue switch
        {
            IssueMission.Reussite =>
                "Finalement, la tâche confiée a été accomplie et le groupe a regagné la guilde.",
            IssueMission.Mort =>
                "L'expédition a tourné au drame et le contrat n'a pas été mené à bien.",
            _ =>
                "Malgré les efforts déployés, le contrat n'a pas pu être rempli."
        };
        if (issue == IssueMission.Reussite && mission.SansGainSecondaire)
            conclusion += " Aucun incident notable n'a troublé son déroulement.";
        if (mission.AvantageTraitsConsomme)
            conclusion += " Les talents particuliers des membres ont été mis à contribution.";

        string[] victimes = morts?.Where(m => m != null)
            .Select(m => m.NomComplet).ToArray() ?? Array.Empty<string>();
        if (victimes.Length > 0)
            conclusion += " " + (victimes.Length == 1
                ? victimes[0] + " n'est pas revenu de cette expédition."
                : string.Join(", ", victimes) + " n'ont pas survécu à cette expédition.");

        string titre = string.IsNullOrWhiteSpace(mission.Titre)
            ? mission.Categorie : mission.Titre;
        string entete = "RAPPORT DE MISSION\n\n" +
            titre + " — rang " + ReglesMissions.FormaterRang(mission.Rang) +
            "\nCommanditaire : " + mission.Demandeur +
            "\nParticipants : " + (noms.Length > 0
                ? string.Join(", ", noms) : "Non précisés") +
            "\nDurée : " + duree + ".";

        string rapport = entete + "\n\n" +
            introduction + " " + rythme + "\n\n" +
            traces + "\n\n" + conclusion;
        // Le rapport est figé à la fin de la mission et sauvegardé tel quel :
        // l'évolution ultérieure des personnages n'en change pas le contenu.
        return rapport;
    }
}
