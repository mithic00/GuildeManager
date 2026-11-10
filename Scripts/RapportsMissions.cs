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
    /// <summary>
    /// Conserve les trois premières étapes et la dernière, en variant le récit
    /// d'une même situation d'un jour à l'autre. Jamais de doublon textuel :
    /// lorsque toutes les formulations sont utilisées, on préfère omettre
    /// une étape redondante plutôt que répéter la même phrase.
    /// Les variantes sont purement littéraires et ne relancent aucun dé.
    /// </summary>
    public static void Consigner(MissionGuilde mission, string scene)
    {
        if (mission == null || string.IsNullOrWhiteSpace(scene)) return;
        mission.EtapesRapport ??= new List<string>();
        string[] variantes = VariantesEpisode(scene.Trim());
        int depart = (Math.Max(1, mission.JoursEcoules) - 1) % variantes.Length;

        // Ne pas réutiliser une formulation déjà présente, même lorsque
        // la quatrième étape sera remplacée par une plus récente.
        for (int i = 0; i < variantes.Length; i++)
        {
            string phrase = variantes[(depart + i) % variantes.Length];
            if (mission.EtapesRapport.Contains(phrase, StringComparer.Ordinal))
                continue;

            if (mission.EtapesRapport.Count < 4)
                mission.EtapesRapport.Add(phrase);
            else
                mission.EtapesRapport[3] = phrase;
            return;
        }
    }

    /// <summary>
    /// Variantes liées à des faits identiques : elles ne modifient jamais
    /// la réussite, l'échec ni les événements observés par la résolution.
    /// Plusieurs formulations sont prévues pour les situations récurrentes
    /// de service, d'enquête, de protection et de recherche.
    /// </summary>
    private static string[] VariantesEpisode(string scene) => scene switch
    {
        // Service : plusieurs journées peuvent alterner les mêmes épreuves.
        "L'équipe a prêté une oreille attentive aux besoins exprimés et organisé son intervention." =>
            new[]
            {
                scene,
                "Les demandes ont été écoutées avec attention, puis les tâches ont été réparties méthodiquement.",
                "En prenant le temps de comprendre les attentes de chacun, les membres ont trouvé une façon d'avancer.",
                "Une bonne coordination et une écoute patiente ont permis de mener le service dans de bonnes conditions.",
                "Les échanges avec les personnes concernées ont facilité l'organisation du travail.",
                "Les membres ont su adapter leur aide aux besoins rencontrés sur place."
            },
        "Un malentendu a compliqué les échanges avec les personnes à aider." =>
            new[]
            {
                scene,
                "Des paroles mal interprétées ont retardé la bonne entente avec les bénéficiaires.",
                "Les explications échangées n'ont pas suffi à dissiper une confusion persistante.",
                "Une incompréhension a obligé les membres à reprendre les discussions depuis le début.",
                "Les personnes présentes n'ont pas compris les intentions de la guilde, ce qui a ralenti l'intervention.",
                "La communication s'est révélée difficile et chacun a dû reformuler ses attentes."
            },
        "Les attentes du commanditaire se sont révélées délicates à concilier." =>
            new[]
            {
                scene,
                "Les exigences du commanditaire ont compliqué l'organisation du service.",
                "Des demandes contradictoires ont contraint les membres à revoir leurs priorités.",
                "L'équipe a eu du mal à satisfaire des attentes qui changeaient au fil des échanges.",
                "Le travail a progressé lentement, tant les consignes reçues se prêtaient à discussion.",
                "Malgré les efforts fournis, les besoins exprimés sont restés difficiles à accorder."
            },

        // Enquête : distinguer compréhension des indices et coopération des témoins.
        "Le recoupement des indices et les confidences recueillies ont permis de démêler l'affaire." =>
            new[] { scene, "Les faits ont fini par s'éclaircir grâce aux témoignages et aux indices concordants.",
                "Les déclarations recueillies ont confirmé les pistes étudiées et permis de conclure l'enquête." },
        "Les indices se précisaient, mais les témoins demeuraient réticents à parler." =>
            new[] { scene, "Les éléments matériels pointaient dans une direction, sans que personne n'accepte de la confirmer.",
                "Les recherches avançaient, mais le silence des témoins laissait des zones d'ombre." },
        "Des témoins ont accepté de parler, sans que les faits puissent encore être recoupés." =>
            new[] { scene, "Des confidences ont été obtenues, mais rien ne permettait encore de les vérifier.",
                "Les récits des témoins ont ouvert des pistes sans fournir de preuve décisive." },
        "Les indices se contredisaient et les témoins gardaient leurs distances." =>
            new[] { scene, "Aucune piste ne se détachait et les personnes interrogées refusaient de s'impliquer.",
                "Les témoignages faisaient défaut tandis que les éléments recueillis semaient le doute." },

        // Protection : plusieurs jours de garde peuvent se ressembler.
        "Les tours de garde se sont succédé sans qu'aucune alerte ne vienne troubler les lieux." =>
            new[] { scene, "La surveillance s'est poursuivie dans le calme, sans visite suspecte.",
                "Les postes ont été tenus avec régularité et la journée s'est écoulée sans incident.",
                "Aucun mouvement inquiétant n'a perturbé la vigilance des gardiens." },
        "Lors d'une alerte, les gardiens ont conservé leur sang-froid et tenu leurs postes." =>
            new[] { scene, "Une menace a été signalée, mais les gardiens ont gardé leur position sans céder à la panique.",
                "Grâce à une réaction maîtrisée, la surveillance a résisté à une nouvelle alerte.",
                "Malgré l'agitation, les défenseurs ont préservé leur discipline." },
        "Une alerte a mis les gardiens à rude épreuve, compliquant la défense des lieux." =>
            new[] { scene, "La tension a gagné les postes de garde et rendu la protection plus difficile.",
                "Une menace inattendue a désorganisé la surveillance pendant un temps.",
                "L'alerte a éprouvé les nerfs et l'endurance des gardiens." },

        // Recherches : conserver la nature des obstacles rencontrés.
        "Les indices, les contacts et les témoignages ont permis de remonter la piste de l'objet." =>
            new[] { scene, "Les pistes recueillies auprès des habitants ont orienté la recherche de l'objet.",
                "Des renseignements concordants ont rapproché les membres de l'objet recherché." },
        "Les personnes interrogées ont gardé le silence, laissant les indices sans confirmation." =>
            new[] { scene, "Personne n'a voulu confirmer les informations collectées sur l'objet.",
                "Les contacts approchés sont restés muets malgré les indices déjà réunis." },
        "Les recherches ont conduit à plusieurs fausses pistes difficiles à départager." =>
            new[] { scene, "Des indices trompeurs ont dispersé les efforts de recherche.",
                "Les pistes découvertes se contredisaient et ont compliqué la progression." },
        "Les membres ont traversé le terrain et identifié les ressources qu'ils cherchaient." =>
            new[] { scene, "L'exploration du terrain a permis de repérer des ressources prometteuses.",
                "En examinant soigneusement les environs, les membres ont localisé les ingrédients recherchés." },
        "Le terrain difficile a freiné l'expédition avant qu'elle puisse atteindre les bons sites." =>
            new[] { scene, "Les obstacles naturels ont ralenti la marche vers les zones de récolte.",
                "L'effort nécessaire pour traverser le terrain a retardé l'accès aux ressources." },
        "Les ressources convoitées se sont révélées difficiles à repérer et à récolter." =>
            new[] { scene, "Les ingrédients étaient rares et leur collecte a demandé plus de travail que prévu.",
                "Malgré leurs recherches, les membres peinaient à trouver des ressources exploitables." },
        "L'équipe a suivi les traces de l'animal et anticipé ses déplacements." =>
            new[] { scene, "Les empreintes relevées ont permis de prévoir la direction prise par l'animal.",
                "L'observation des pistes a aidé à suivre les mouvements de la bête." },
        "L'animal a pris de l'avance et ses traces se sont dissipées." =>
            new[] { scene, "La bête a distancé ses poursuivants, laissant derrière elle une piste incertaine.",
                "Les traces se sont espacées jusqu'à rendre la poursuite difficile." },
        "La poursuite a épuisé le groupe sur un terrain peu favorable." =>
            new[] { scene, "Le relief a éprouvé les membres et ralenti leur progression derrière l'animal.",
                "Sur ce terrain accidenté, suivre la bête a demandé des efforts considérables." },

        // Interventions dangereuses : plusieurs tentatives restent distinctes.
        "La menace s'est révélée redoutable et la confrontation a éprouvé le groupe." =>
            new[] { scene, "La résistance rencontrée a forcé les membres à redoubler de prudence.",
                "Face à un adversaire dangereux, l'intervention s'est prolongée sans issue décisive." },
        "Les renforts se sont heurtés à une résistance acharnée, ralentissant toute progression." =>
            new[] { scene, "La défense adverse a opposé une résistance tenace à l'avancée des assaillants.",
                "Les renforts ont dû se réorganiser devant la solidité des positions ennemies." },
        "La cible a été rattrapée, mais la confrontation a tourné à l'avantage de l'adversaire." =>
            new[] { scene, "Malgré l'interception, la cible est parvenue à reprendre l'initiative.",
                "La poursuite a abouti à un affrontement dont la cible est sortie avantagée." },

        // Repli pour des catégories supplémentaires ou futures.
        "L'équipe a su exploiter les indices recueillis." =>
            new[] { scene, "Les éléments disponibles ont été mis à profit avec méthode.",
                "Les observations ont aidé à orienter la suite de la mission." },
        "Les pistes explorées sont restées incertaines." =>
            new[] { scene, "Les informations réunies n'ont pas permis de choisir une direction sûre.",
                "Le manque d'indices fiables a freiné la progression de la mission." },
        _ => new[] { scene }
    };

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

        // Préposition incluse pour éviter « de un marchand », « de une
        // guilde », etc. Les demandeurs spéciaux inconnus restent désignés
        // par leur nom sans supposer une catégorie grammaticale.
        string origine = mission.Demandeur switch
        {
            "Cité" => "des autorités de la cité",
            "Autre Cité" => "des autorités d'une autre cité",
            "Particulier" => "d'un particulier",
            "Marchand" => "d'un marchand",
            "Commerçant de la ville" => "d'un commerçant de la ville",
            "Guilde de Mercenaire" => "d'une guilde de mercenaires",
            "Facon" => "de Facon",
            null or "" => "d'un commanditaire non identifié",
            _ => "d'un commanditaire nommé « " + mission.Demandeur + " »"
        };
        int jours = Math.Max(1, mission.JoursEcoules);
        string duree = jours == 1 ? "une journée" : jours + " jours";
        string sujet = noms.Length switch
        {
            0 => "Les membres envoyés",
            1 => noms[0],
            _ => "L'équipe de " + noms[0]
        };
        // La catégorie comporte également sa préposition : « d'enquête »,
        // « d'escorte », « de service », et non « de enquête ».
        string typeMission = mission.Categorie switch
        {
            "Enquête" => "d'enquête",
            "Escorte" => "d'escorte",
            "Élimination de danger" => "d'élimination d'un danger",
            "Recherche mort ou vif" => "de recherche d'une cible, morte ou vive",
            "Capture vif" => "de capture d'une cible vivante",
            "Protection d'un lieu" => "de protection d'un lieu",
            "Recherche d'un objet" => "de recherche d'un objet",
            "Recherche d'un ingrédient" => "de recherche d'un ingrédient",
            "Recherche d'un animal" => "de recherche d'un animal",
            "Service" => "de service",
            "Renfort pour Assaut" => "de renfort à un assaut",
            _ => "de type « " + (mission.Categorie ?? "inconnu") + " »"
        };
        string introduction = sujet + " a répondu à la demande " +
            origine + " pour une mission " + typeMission + ".";

        string rythme = jours switch
        {
            1 => "L'intervention s'est déroulée en une journée.",
            2 => "L'expédition s'est étendue sur deux journées.",
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
            IssueMission.Reussite when noms.Length == 1 =>
                "Finalement, la mission confiée a été accomplie. " + noms[0] +
                " a regagné la guilde.",
            IssueMission.Reussite when noms.Length > 1 =>
                "Finalement, la mission confiée a été accomplie et l'équipe a regagné la guilde.",
            IssueMission.Reussite =>
                "Finalement, la mission confiée a été accomplie et les membres ont regagné la guilde.",
            IssueMission.Mort =>
                "L'expédition a tourné au drame et la mission n'a pas été menée à bien.",
            _ =>
                "Malgré les efforts déployés, la mission n'a pas pu être remplie."
        };
        if (issue == IssueMission.Reussite && mission.SansGainSecondaire)
            conclusion += " Aucun incident notable n'a troublé son déroulement.";
        if (mission.AvantageTraitsConsomme)
            conclusion += noms.Length == 1
                ? " Ses talents particuliers ont été mis à contribution."
                : " Les talents particuliers des membres ont été mis à contribution.";

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
