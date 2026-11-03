using Godot;
using System;
using System.Linq;

/// <summary>Branches des six actes : noeuds déterministes et jets sauvegardables.</summary>
public partial class MainMenu
{
    private void MontrerFilRouge(string personnage, string nom, string texte,
        params (string Texte, string Action)[] options) =>
        AfficherFilRouge(personnage, nom, texte, options);

    private void TerminerRepliqueFilRouge(string texte, int prestige = 0, int bonheur = 0,
        string effet = "", double facteur = 1, string demandeur = "")
    {
        // Les conséquences ne sont appliquées qu'une fois, avant le bilan.
        // Le bilan reste dans la sauvegarde tant qu'il n'a pas été fermé.
        if (!string.IsNullOrEmpty(_filRouge.BilanTexte)) return;

        if (prestige != 0) ModifierPrestigeFilRouge(prestige, texte);
        if (bonheur != 0) ModifierBonheurTousFilRouge(bonheur, texte);
        if (effet != "") AjouterEffetSpawnFilRouge(effet, facteur, demandeur);
        AjouterEntreeJournal("Rose-Vallée : " + texte, "fil_rouge");

        string narration = texte switch
        {
            "Facon réussit son intimidation." =>
                "Charles quitte la guilde en ayant imposé ses menaces. " +
                "Un malaise s'est installé dans la guilde : les membres sont inquiets " +
                "de l'influence de Facon et de la capacité de leur chef à leur tenir tête.",
            "Facon ne réussit pas son intimidation." =>
                "Malgré les provocations de Charles, vous avez gardé votre position. " +
                "Il repart sans avoir réussi à vous intimider. " +
                "Votre attitude renforce la confiance accordée à votre guilde.",
            "Le joueur réussit à intimider Charles." =>
                "Charles finit par céder et quitte les lieux. Votre fermeté impressionne " +
                "les témoins, mais il n'oubliera pas cet affront.",
            "Charles détruit un mur ; Facon réussit son intimidation." =>
                "Furieux, Charles endommage un mur avant de repartir. " +
                "Son passage laisse un malaise dans la guilde et des réparations à financer.",
            "Léonie soutient la guilde : témoignage favorable obtenu." =>
                "Léonie promet de parler en votre faveur aux commerçants. " +
                "Sa confiance renforce la position de votre guilde à Rose-Vallée.",
            "Facon renforce son emprise commerciale." =>
                "Votre retrait laisse le champ libre à Facon. Les commerçants auront " +
                "désormais davantage de difficultés à confier leurs missions à votre guilde.",
            "Facon apprend que des représailles sont envisagées." =>
                "Votre réaction inquiète l'administration. Facon a été informée de " +
                "vos intentions et sa pression sur la guilde augmente.",
            "Opposition ouverte à Facon." =>
                "Vous refusez toute soumission à Facon. Le conflit est désormais ouvert " +
                "et les demandes de mission se raréfient.",
            "Accord de dépendance avec Facon signé." =>
                "Vous acceptez la supervision commerciale de Facon en échange de son aide. " +
                "La pression immédiate diminue, mais votre indépendance est remise en question.",
            "Aveux d'un saboteur de Facon obtenus." =>
                "Les aveux établissent un lien entre le sabotage et Facon. " +
                "Vous disposez d'une preuve importante pour l'audience à venir.",
            _ => texte
        };

        string bilan = narration;
        if (prestige != 0)
            bilan += "\nPrestige : " + (prestige > 0 ? "+" : "") + prestige + ".";
        if (bonheur != 0)
            bilan += "\nBonheur de tous les membres : " +
                (bonheur > 0 ? "+" : "") + bonheur + ".";
        if (texte == "Charles détruit un mur ; Facon réussit son intimidation.")
            bilan += "\nRéparations : -350 écus.";
        if (texte == "Accord de dépendance avec Facon signé.")
            bilan += "\nTrésorerie : +2 000 écus. Taux d'apparition des missions réinitialisé.";
        if (effet != "")
            bilan += "\n" + (demandeur == "" ? "Apparition des missions" :
                "Missions des commerçants") + " : multiplicateur ×" +
                facteur.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")) + ".";

        AfficherBilanFilRouge(bilan);
    }

    private void TraiterChoixFilRouge(string action)
    {
        if (!_filRougeDialogueActif || _filRouge == null) return;
        if (action == "filrouge-fermer-bilan")
        {
            if (!string.IsNullOrEmpty(_filRouge.BilanTexte))
                TerminerActeFilRouge(_filRouge.ActeEnCours);
            return;
        }
        if (!string.IsNullOrEmpty(_filRouge.BilanTexte)) return;
        switch (action)
        {
            // ACTE I — chaque branche conserve ses propres répliques.
            case "i-poli": case "i-aider": case "i-nous":
            case "i-partez-echec": case "i-sortez-echec":
                VersNoeudFilRouge(action); return;
            case "i-salut": VersNoeudFilRouge("i-salut-replique"); return;
            case "i-partez":
                VersNoeudFilRouge(JetFilRouge("Charisme", 12)
                    ? "i-partez-reussi" : "i-partez-echec"); return;
            case "i-sortez":
                VersNoeudFilRouge(JetFilRouge("Charisme", 13)
                    ? "i-sortez-reussi" : "i-sortez-echec"); return;
            case "i-calmez":
                VersNoeudFilRouge("i-calmez-reponse"); return;
            case "i-intimider":
                VersNoeudFilRouge(JetFilRouge("Charisme", 15)
                    ? "i-intimide-gagne" : "i-mur"); return;
            case "i-salut-test":
                VersNoeudFilRouge(JetFilRouge("Calme", 15)
                    ? "i-salut-reussi" : "i-salut-echec"); return;
            case "i-calmepoli":
                // Aucun dialogue supplémentaire : seul le résultat du jet
                // est montré avant de fermer la conversation.
                VersNoeudFilRouge(JetFilRouge("Calme", 15)
                    ? "i-calme-conclusion-ok" : "i-calme-conclusion-ko");
                return;
            case "i-conclusion-ok":
                TerminerRepliqueFilRouge("Facon ne réussit pas son intimidation.",
                    prestige: 10); return;
            case "i-conclusion-ko":
                TerminerRepliqueFilRouge("Facon réussit son intimidation.",
                    bonheur: -10); return;
            case "i-fin-facon":
                TerminerRepliqueFilRouge("Facon réussit son intimidation.",
                    bonheur: -10); return;
            case "i-fin-resiste":
                TerminerRepliqueFilRouge("Facon ne réussit pas son intimidation.",
                    prestige: 10); return;
            case "i-fin-intimide":
                _filRouge.RancuneCharles++;
                AjouterEntreeJournal("Rose-Vallée : rancune de Charles augmentée à " +
                    _filRouge.RancuneCharles + ".", "fil_rouge");
                TerminerRepliqueFilRouge("Le joueur réussit à intimider Charles.",
                    prestige: 20);
                return;
            case "i-fin-mur":
                ModifierEcusFilRouge(-350, "Réparation du mur endommagé par Charles");
                TerminerRepliqueFilRouge("Charles détruit un mur ; Facon réussit son intimidation.",
                    bonheur: -10);
                return;
            // ACTE II
            case "ii-accueil": case "ii-mefiant": case "ii-neutre":
                VersNoeudFilRouge(action); return;
            case "ii-preuve":
                VersNoeudFilRouge(JetFilRouge("Intelligence", 12) ? "ii-preuve-ok" : "ii-preuve-ko"); return;
            case "ii-soutien":
                VersNoeudFilRouge(JetFilRouge("Charisme", 13) ? "ii-soutien-ok" : "ii-soutien-ko"); return;
            case "ii-abandon": VersNoeudFilRouge("ii-abandon-fin"); return;
            case "ii-agressif":
                VersNoeudFilRouge(JetFilRouge("Charisme", 16) ? "ii-agressif-ok" : "ii-agressif-ko"); return;
            case "ii-sympathie": VersNoeudFilRouge("ii-sympathie-fin"); return;
            case "ii-insulte": VersNoeudFilRouge("ii-insulte-fin"); return;
            case "ii-fin-preuve":
                _filRouge.PreuveCommerciale = true;
                TerminerRepliqueFilRouge("Lettre commerciale suspecte obtenue.",prestige:10);return;
            case "ii-fin-preuve-ko":
                TerminerRepliqueFilRouge("Facon conserve son influence sur les commerçants.",
                    prestige:-10);return;
            case "ii-fin-soutien":
                _filRouge.TemoignageFavorable = true;
                TerminerRepliqueFilRouge("Léonie soutient la guilde : témoignage favorable obtenu.",
                    prestige:20,effet:"temoignage_commercants",facteur:1.5,
                    demandeur:"Commerçant de la ville");return;
            case "ii-fin-neutre":TerminerRepliqueFilRouge("Léonie repart sans changement.");return;
            case "ii-fin-insulte":TerminerRepliqueFilRouge("Léonie quitte la guilde vexée.",prestige:-10);return;
            case "ii-fin-abandon":
                TerminerRepliqueFilRouge("Facon renforce son emprise commerciale.",prestige:-20,
                    effet:"emprise_commerciale",facteur:0.5,demandeur:"Commerçant de la ville");return;
            case "ii-fin-agressif":
                TerminerRepliqueFilRouge("Le représentant de Facon quitte le marché.",prestige:10);return;
            case "ii-fin-mefiance":TerminerRepliqueFilRouge("Les commerçants deviennent méfiants.");return;
            // ACTE III
            case "iii-accueil":case "iii-soupcon":case "iii-taxe":
                VersNoeudFilRouge(action);return;
            case "iii-documents":
                VersNoeudFilRouge(JetFilRouge("Intelligence",12) ?
                    "iii-documents-ok":"iii-documents-ko");return;
            case "iii-enquete":VersNoeudFilRouge("iii-enquete");return;
            case "iii-liberta":
                VersNoeudFilRouge(JetFilRouge("Calme",14) ?
                    "iii-liberta-ok":"iii-liberta-ko");return;
            case "iii-mission":
                ProposerMissionSpecialeFilRouge("enquete_facon","Enquête","*");
                TerminerRepliqueFilRouge("Mission spéciale « Les signatures de Facon » disponible.");return;
            case "iii-autorites":TerminerRepliqueFilRouge("L'affaire est confiée aux autorités.");return;
            case "iii-fin-preuve":
                _filRouge.PreuveFalsification = true;
                TerminerRepliqueFilRouge("Preuve de falsification des contrats découverte.",prestige:20);return;
            case "iii-fin-recherche":
                _filRouge.EnquetePossibleDiscussion = true;
                TerminerRepliqueFilRouge("Une enquête pourra être demandée aux membres de la guilde.");return;
            case "iii-fin-peur":
                TerminerRepliqueFilRouge("Facon apprend que des représailles sont envisagées.",
                    prestige:-10,effet:"facon_represailles",facteur:0.8);return;
            // ACTE IV
            case "iv-intro1":case "iv-intro2":case "iv-intro3":
                VersNoeudFilRouge(action);return;
            case "iv-protection":
                ProposerMissionSpecialeFilRouge("serres_facon","Protection d'un lieu","***");
                TerminerRepliqueFilRouge("Mission de protection des serres confiée à la guilde.");return;
            case "iv-poursuivre":
                _filRouge.AttendRue4 = true;
                _filRouge.DateLimiteRue4 = ReglesMissions.DateIso(_horloge.Instant.AddHours(4));
                _filRouge.Noeud = "iv-rue4";
                AjouterEntreeJournal("Rose-Vallée : poursuivre les saboteurs en Rue4 avant " +
                    HorlogeJeu.Formater(_horloge.Instant.AddHours(4)) + ".", "fil_rouge");
                FermerInterfaceFilRouge();return;
            case "iv-refus":
                VersNoeudFilRouge("iv-refus");return;
            case "iv-saboteur":
                VersNoeudFilRouge(JetFilRouge("Charisme",16) ?
                    "iv-saboteur-ok":"iv-saboteur-ko");return;
            case "iv-fin-saboteur":
                _filRouge.PreuveSabotage = true;
                TerminerRepliqueFilRouge("Aveux d'un saboteur de Facon obtenus.",prestige:30);return;
            case "iv-fin-saboteur-ko":
                TerminerRepliqueFilRouge("Les saboteurs ont pris la fuite.");return;
            case "iv-fin-abandon":
                AppliquerAbandonSerres();
                AfficherBilanFilRouge("Vous refusez d'aider Perrine. Les habitants se sentent " +
                    "abandonnés et commencent à douter de leur nouvelle guilde." +
                    "\nPrestige : -30.\nBonheur de tous les membres : -10." +
                    "\nApparition des missions : multiplicateur ×0,5.");
                return;
            case "iv-fin-notification":
                // L'événement de notification était programmé après l'acte IV.
                _filRouge.ActeEnCours = 0;
                FermerInterfaceFilRouge();
                PreparerProchainEvenementFilRouge();return;
            // ACTE V
            case "v-enerve":case "v-poli":case "v-menace":
                VersNoeudFilRouge(action);return;
            case "v-adversaire":
                VersNoeudFilRouge(JetFilRouge("Charisme",15) ?
                    "v-adversaire-ok":"v-adversaire-ko");return;
            case "v-diplomatie":
                VersNoeudFilRouge(JetFilRouge("Charisme",13) ?
                    "v-diplomatie-ok":"v-diplomatie-ko");return;
            case "v-preuves":
                VersNoeudFilRouge(JetFilRouge("Calme",2) ?
                    "v-preuves-ok":"v-preuves-ko");return;
            case "v-accord":VersNoeudFilRouge("v-accord");return;
            case "v-accepter":
                _filRouge.AccordDependance = true;
                _filRouge.ReinitialiserMultiplicateurs(1,"dependance_signee");
                ModifierEcusFilRouge(2000,"Accord avec Facon");
                TerminerRepliqueFilRouge("Accord de dépendance avec Facon signé.",prestige:-20);return;
            case "v-refuser":
                TerminerRepliqueFilRouge("Opposition ouverte à Facon.",
                    effet:"opposition_facon",facteur:0.5);return;
            case "v-fin-adversaire":
                TerminerRepliqueFilRouge("Facon considère la guilde comme un adversaire sérieux.",
                    prestige:20,effet:"adversaire_serieux",facteur:1.2);return;
            case "v-fin-aucun":TerminerRepliqueFilRouge("L'entretien avec Mireille se termine.");return;
            case "v-fin-diplomatie":
                _filRouge.VoieDiplomatique = true;
                _filRouge.ReputationFacon++;
                AjouterEntreeJournal("Rose-Vallée : réputation auprès de Facon +1.", "fil_rouge");
                TerminerRepliqueFilRouge("Une voie diplomatique s'ouvre avec Facon.");return;
            case "v-fin-preuves":
                _filRouge.RancuneCharles++;
                AjouterEntreeJournal("Rose-Vallée : rancune de Charles " +
                    _filRouge.RancuneCharles + ".", "fil_rouge");
                TerminerRepliqueFilRouge("Mireille examine la conduite de Charles.",prestige:20);return;
            case "v-fin-preuves-ko":
                TerminerRepliqueFilRouge("Les accusations contre Facon ne convainquent pas Mireille.",
                    prestige:-5);return;
            // ACTE VI
            case "vi-independance":
            {
                int preuves = (_filRouge.PreuveCommerciale?1:0) +
                    (_filRouge.TemoignageFavorable?1:0) +
                    (_filRouge.PreuveFalsification?1:0) +
                    (_filRouge.PreuveSabotage?1:0);
                int minimum = _filRouge.AccordDependance ? 10 : 1;
                int cible = Math.Max(minimum,15 - 5*preuves);
                bool reussi = JetFilRouge("Charisme",cible);
                VersNoeudFilRouge(reussi?"vi-a":
                    preuves>0?"vi-b-defaut":"vi-c-defaut");return;
            }
            case "vi-cooperation":
                VersNoeudFilRouge(JetFilRouge("Charisme",11) ?
                    "vi-b-cooperation":"vi-c-cooperation");return;
            case "vi-accusation":
                VersNoeudFilRouge(JetFilRouge("Intelligence",8) ?
                    "vi-a-accusation":"vi-c-accusation");return;
            case "vi-soumission":VersNoeudFilRouge("vi-c-soumission");return;
            case "vi-fin-a":CloreFinFilRouge("A",60,1500);return;
            case "vi-fin-b":CloreFinFilRouge("B",30,500);return;
            case "vi-fin-c":CloreFinFilRouge("C",0);return;
            case "vi-fin-c-volontaire":
                ModifierEcusFilRouge(2000,"Accord confirmé devant le Seigneur");
                ModifierPrestigeFilRouge(-20,"Accord confirmé devant le Seigneur");
                CloreFinFilRouge("C",0);return;
        }
        GD.PushWarning("Action inconnue dans le fil rouge : " + action);
    }

    private void AfficherNoeudFilRouge()
    {
        if (_filRouge == null) return;
        string n = _filRouge.Noeud;
        int acte = _filRouge.ActeEnCours;
        switch (acte)
        {
            case 1: AfficherActeI(n);break;
            case 2: AfficherActeII(n);break;
            case 3: AfficherActeIII(n);break;
            case 4: AfficherActeIV(n);break;
            case 5: AfficherActeV(n);break;
            case 6: AfficherActeVI(n);break;
        }
    }

    /// <summary>
    /// Affiche une transcription de dialogue déjà attribuée aux interlocuteurs.
    /// Contrairement à MontrerFilRouge, n'ajoute pas « Charles : » à une
    /// première ligne qui peut être prononcée par le joueur.
    /// </summary>
    private void MontrerTranscriptionFilRouge(string portrait, string interlocuteur,
        string transcription, params (string Texte, string Action)[] options)
    {
        AfficherTranscriptionFilRouge(portrait, interlocuteur, transcription, options);
    }

    private void AfficherActeI(string n)
    {
        const string charles = "Charles Suriname, membre de Facon";
        switch (n)
        {
            case "debut":
                MontrerFilRouge("Charles", charles,
                    "Mais qu'est ce que c'est que cet endroit miteux ?",
                    ("Bonjour ! Bienvenu dans notre guilde, {Nom de la guilde}. Je peux faire quelque chose pour vous ?", "i-poli"),
                    ("Bonjour, je peux vous aider ?", "i-aider"),
                    ("Alors déjà bonjour...", "i-salut"),
                    ("Si cet endroit ne vous plait pas, vous être libre de partir", "i-partez"));
                return;

            // CHOIX 1 : le jet de Calme conclut directement la conversation.
            // La phrase « Oui... Pardon... » ne fait PAS partie de cette branche.
            case "i-poli":
                MontrerTranscriptionFilRouge("Charles", charles,
                    "Charles Suriname : Une guilde ? Dans ce trou à rat ? Pff ! Ridicule !\n" +
                    "Joueur : Il faut bien partir de quelque part...\n" +
                    "Charles Suriname : Qu'on soit clair : ici c'est Facon qui commande. Vous avez pas intérêt à marcher sur nos plates bandes.",
                    ("Continuer (D20 Calme, cible 15)", "i-calmepoli"));
                return;

            case "i-calme-conclusion-ok":
            case "i-calme-conclusion-ko":
                MontrerTranscriptionFilRouge("Charles", charles,
                    "Résultat du jet de Calme.",
                    ("Terminer la conversation",
                        n == "i-calme-conclusion-ok" ?
                        "i-conclusion-ok" : "i-conclusion-ko"));
                return;

            // CHOIX 2 : « Bonjour, je peux vous aider ? »
            case "i-aider":
                MontrerFilRouge("Charles", charles,
                    "M'aider ? Moi ? Ha ha ! Il est mignon lui ! Qu'est ce que vous faites dans ce taudis ?",
                    ("Sortez d'ici svp... (D20 Charisme, cible 13)", "i-sortez"),
                    ("Nous avons créer une guilde à Rose-Vallée, pour venir en aide aux habitants", "i-nous"));
                return;
            case "i-sortez-reussi":
                MontrerFilRouge("Charles", charles,
                    "Pff, j'avais pas l'intention de rester ici de toute façon...",
                    ("Terminer la conversation", "i-fin-resiste"));
                return;
            case "i-sortez-echec":
                if (StatJoueurFilRouge("Calme") > 9)
                    MontrerFilRouge("Charles", charles,
                        "C'est à moi que tu parles ?",
                        ("Calmons-nous, je ne veux pas de problèmes ici.", "i-calmez"));
                else
                    MontrerFilRouge("Charles", charles,
                        "C'est à moi que tu parles ?",
                        ("Ouais c'est à toi ! Et tu commences à me les briser alors il va falloir que tu redescendes (D20 Charisme, cible 15)",
                        "i-intimider"));
                return;
            case "i-calmez-reponse":
                MontrerFilRouge("Charles", charles,
                    "En effet t'en veux pas ouais... en effet",
                    ("Terminer la conversation", "i-fin-facon"));
                return;
            case "i-intimide-gagne":
                MontrerFilRouge("Charles", charles,
                    "ha ha, c'est qu'il se débat en plus... De toute façon, j'avais pas envie de rester plus longtemps ici",
                    ("Terminer la conversation", "i-fin-intimide"));
                return;
            case "i-mur":
                MontrerTranscriptionFilRouge("Charles", charles,
                    "Charles fait un trou dans le mur d'énervement et repart en ricanant.",
                    ("Terminer la conversation", "i-fin-mur"));
                return;
            case "i-nous":
                MontrerFilRouge("Charles", charles,
                    "Trop mignon ! Vous avez pas intérêt a marcher sur les plates bandes de Facon. Sinon vous allez entendre parler de nous.",
                    ("Continuer (D20 Calme, cible 15)", "i-calmepoli"));
                return;

            // CHOIX 3 : seul ce chemin peut afficher « Oui... Pardon... ».
            case "i-salut-replique":
                MontrerFilRouge("Charles", charles,
                    "Tu fais le malin ? Tu sais à qui tu as affaire là ?",
                    ("Continuer (D20 Calme, cible 15)", "i-salut-test"));
                return;
            case "i-salut-reussi":
                MontrerTranscriptionFilRouge("Charles", charles,
                    "Joueur : Oui je le sais. Mais vous êtes chez nous ici. Alors si vous n'avez pas l'intention d'être poli, je vous invite à sortir.",
                    ("Terminer la conversation", "i-fin-resiste"));
                return;
            case "i-salut-echec":
                MontrerTranscriptionFilRouge("Charles", charles,
                    "Joueur : Oui... Pardon...",
                    ("Terminer la conversation", "i-fin-facon"));
                return;

            // CHOIX 4 : succès de Charisme et échec suivi de Calme distincts.
            case "i-partez-reussi":
                MontrerFilRouge("Charles", charles,
                    "Ouh ! Sacré ambiance chez les pleutres... J'avais pas l'intention de rester de toute façon...",
                    ("Terminer la conversation", "i-fin-resiste"));
                return;
            case "i-partez-echec":
                MontrerFilRouge("Charles", charles,
                    "C'est moi qui déciderait quand je partirais. Tout comme c'est Facon qui décide ici. Vous avez pas intérêt a marcher sur nos plates bandes.",
                    ("Continuer (D20 Calme, cible 15)", "i-calmepoli"));
                return;
        }
        GD.PushWarning("Acte I Rose-Vallée : noeud non reconnu : " + n);
    }

    private void AfficherActeII(string n)
    {
        string ch = "Léonie Marchal, marchande de fleurs";
        switch (n)
        {
            case "debut":
                MontrerFilRouge("Léonie",ch,"Bonjour... Excusez-moi de vous déranger. Vous êtes bien le maître de cette nouvelle guilde ?",
                    ("Bonjour ! Oui, c'est bien moi. Que puis-je faire pour vous ?","ii-accueil"),
                    ("Ça dépend. Vous venez demander de l'aide ou vous moquer de nos locaux ?","ii-mefiant"),
                    ("Oui. Il y a un problème ?","ii-neutre"));return;
            case "ii-accueil":case "ii-mefiant":case "ii-neutre":
                MontrerFilRouge("Léonie",ch,
                    (n=="ii-accueil"?"Ah, tant mieux ! Vous avez l'air plus aimable que les autres...":
                    n=="ii-mefiant"?"Oh non ! Pas du tout ! Pardonnez-moi... Je vois que je tombe mal.":
                    "Eh bien, oui... Enfin, je crois.") +
                    "\nUn homme est passé au marché ce matin. Il nous a dit que nous n'avions pas le droit de faire appel à votre guilde. Que tous les contrats de protection devaient passer par Facon, à Liberta.",
                    OptionsActeII());return;
            case "ii-preuve-ok":
                MontrerFilRouge("Léonie",ch,"Vous a-t-il présenté un document officiel ?\nOui ! Une lettre avec un sceau. Mais maintenant que vous le dites... Le sceau ne ressemblait pas à celui du Comté.\nVous : Pourriez-vous m'en apporter une copie ?\nLéonie : Bien sûr ! Je vous apporterai ça demain.",
                    ("Terminer la conversation","ii-fin-preuve"));return;
            case "ii-preuve-ko":
                MontrerFilRouge("Léonie",ch,"Vous : Ils ont probablement le droit de faire ça...\nOh... Je comprends. C'est dommage, nous espérions tellement avoir une guilde ici.",
                    ("Terminer la conversation","ii-fin-preuve-ko"));return;
            case "ii-soutien-ok":
                MontrerFilRouge("Léonie",ch,"Vous avez raison ! Et puis, depuis quand doit-on demander la permission à Liberta pour vivre à Rose-Vallée ?\nJe vais en parler aux autres commerçants. Vous avez mon soutien !",
                    ("Terminer la conversation","ii-fin-soutien"));return;
            case "ii-soutien-ko":
                MontrerFilRouge("Léonie",ch,"Vous dites ça, mais eux ont des dizaines de membres. Et vous... enfin...",
                    ("Je comprends votre inquiétude.","ii-sympathie"),
                    ("Si vous ne nous faites pas confiance, pourquoi venir ici ?","ii-insulte"));return;
            case "ii-sympathie-fin":
                MontrerFilRouge("Léonie",ch,"Merci... Peut-être qu'un jour les choses changeront.",
                    ("Terminer la conversation","ii-fin-neutre"));return;
            case "ii-insulte-fin":
                MontrerFilRouge("Léonie",ch,"Je... Pardonnez-moi de vous avoir dérangé.",
                    ("Terminer la conversation","ii-fin-insulte"));return;
            case "ii-abandon-fin":
                MontrerFilRouge("Léonie",ch,"Oh... Je pensais que vous étiez justement venus pour changer les choses. Très bien. Je ne vous dérange pas plus longtemps.",
                    ("Terminer la conversation","ii-fin-abandon"));return;
            case "ii-agressif-ok":
                MontrerFilRouge("Léonie",ch,"Bon... Je vais vous indiquer où il se trouve. Mais soyez prudent.",
                    ("Terminer la conversation","ii-fin-agressif"));return;
            case "ii-agressif-ko":
                MontrerFilRouge("Léonie",ch,"Non ! Je ne veux pas être responsable d'une bagarre ! Oubliez ce que j'ai dit.",
                    ("Terminer la conversation","ii-fin-mefiance"));return;
        }
    }

    private (string Texte,string Action)[] OptionsActeII()
    {
        var choix = new System.Collections.Generic.List<(string,string)>
        {
            ("Et sur quoi s'appuie-t-il pour affirmer ça ? (D20 Intelligence, 12)","ii-preuve"),
            ("Facon peut bien raconter ce qu'elle veut. Vous êtes libres de choisir votre guilde. (D20 Charisme, 13)","ii-soutien"),
            ("Qu'ils gardent leurs contrats. Je n'ai pas envie d'entrer en conflit avec Facon.","ii-abandon")
        };
        if (StatJoueurFilRouge("Calme") < 10)
            choix.Add(("Je vais m'occuper personnellement de cet individu. (D20 Charisme, 16)","ii-agressif"));
        return choix.ToArray();
    }

    private void AfficherActeIII(string n)
    {
        string nom = "Étienne Morel, employé du Comté";
        switch (n)
        {
            case "debut":
                MontrerFilRouge("Étienne",nom,"Bonjour, maître de guilde. Je viens du château de Rose-Vallée. Nous avons... un petit problème.",
                    ("Je vous écoute.","iii-accueil"),("Encore Facon, je suppose ?","iii-soupcon"),
                    ("S'il s'agit d'une taxe supplémentaire, vous pouvez repartir.","iii-taxe"));return;
            case "iii-accueil":case "iii-soupcon":case "iii-taxe":
                MontrerFilRouge("Étienne",nom,
                    (n=="iii-accueil"?"Il semblerait que certaines demandes ne soient jamais arrivées jusqu'à vous.":
                    n=="iii-soupcon"?"Comment... Comment êtes-vous au courant ?":
                    "Non, non ! Rien de tout ça ! C'est plutôt embarrassant pour nous.")+
                    "\nPlusieurs habitants ont déposé des demandes à votre attention. Seulement, ces missions ont finalement été confiées à Facon. Et les documents portent une autorisation de votre guilde.\nVous : Une autorisation ? Nous n'avons rien signé !\nÉtienne : C'est bien ce qui m'inquiète.",
                    ("Montrez-moi ces documents. (D20 Intelligence, 12)","iii-documents"),
                    ("Je veux savoir qui a fait ça.","iii-enquete"),
                    ("Je vais aller régler ça à Liberta ! (D20 Calme, 14)","iii-liberta"));return;
            case "iii-documents-ok":
                MontrerFilRouge("Étienne",nom,"Vous : Cette signature est fausse. Et regardez cette date... Notre guilde n'avait même pas encore ouvert !\nPar les Esprits... Vous avez raison ! Il va falloir que j'en informe le Seigneur.",
                    ("Terminer la conversation","iii-fin-preuve"));return;
            case "iii-documents-ko":
                MontrerFilRouge("Étienne",nom,"Vous : Je ne comprends pas comment ils ont fait...\nJe vais conserver les documents. Nous finirons peut-être par comprendre.",
                    ("Terminer la conversation","iii-fin-recherche"));return;
            case "iii-enquete":
                MontrerFilRouge("Étienne",nom,"Je peux vous confier une copie des dossiers. Mais notre administration manque de personnel pour mener une enquête.",
                    ("Nous allons mener l'enquête nous-mêmes.","iii-mission"),
                    ("Vous devriez plutôt contacter les autorités.","iii-autorites"));return;
            case "iii-liberta-ok":
                MontrerFilRouge("Étienne",nom,"Vous : Non... Ce serait leur donner ce qu'ils veulent. Nous allons d'abord réunir des preuves.\nÉtienne : Je crois que c'est la meilleure décision.",
                    ("Débloquer l'enquête spéciale","iii-mission"));return;
            case "iii-liberta-ko":
                MontrerFilRouge("Étienne",nom,"Vous : J'en ai assez de ces gens !\nMaître de guilde, je vous en prie ! Ne provoquez pas un conflit entre nos deux villes !",
                    ("Terminer la conversation","iii-fin-peur"));return;
        }
    }

    private void AfficherActeIV(string n)
    {
        string perrine="Perrine Arnaud, productrice de fleurs";
        switch (n)
        {
            case "debut":
                MontrerFilRouge("Perrine",perrine,"À l'aide ! Il faut que vous veniez ! Ils ont tout détruit !",
                    ("Calmez-vous. Que s'est-il passé ?","iv-intro1"),
                    ("Qui a fait ça ?!","iv-intro2"),
                    ("Encore une histoire de fleurs ?","iv-intro3"));return;
            case "iv-intro1":case "iv-intro2":case "iv-intro3":
                MontrerFilRouge("Perrine",perrine,
                    (n=="iv-intro1"?"Mes serres ! Quelqu'un est venu cette nuit ! Des vitres brisées, des plantations arrachées...":
                    n=="iv-intro2"?"Je ne sais pas ! Mais on a retrouvé des traces près de la rivière Tangue.":
                    "Vous ne comprenez pas ! C'est toute ma récolte ! Je risque de tout perdre !")+
                    "\nLe pire, c'est que deux hommes sont revenus ce matin. Ils m'ont proposé la protection de Facon. Vous trouvez ça normal, vous ?",
                    ("Nous allons protéger vos serres. Cela ne se reproduira pas.","iv-protection"),
                    ("Ces hommes sont peut-être encore dans les environs. Allons les chercher.","iv-poursuivre"),
                    ("Je ne peux pas mobiliser ma guilde pour une affaire sans preuve.","iv-refus"));return;
            case "iv-refus":
                MontrerFilRouge("Perrine",perrine,"Mais... Qui va nous aider alors ?\nVous : Je suis désolé.\nPerrine : Oui... Moi aussi.",
                    ("Terminer la conversation","iv-fin-abandon"));return;
            case "iv-rue4":
                MontrerFilRouge("Saboteur","Saboteur de Facon","Vous retrouvez les deux hommes près d'un chemin longeant la rivière.\nQuoi ? Qu'est-ce que vous nous voulez ?",
                    ("Je sais ce que vous avez fait. Dites-moi qui vous a envoyés ! (D20 Charisme, 16)","iv-saboteur"));return;
            case "iv-saboteur-ok":
                MontrerFilRouge("Saboteur","Saboteur de Facon","On nous a payés, d'accord ?! Un type de Facon ! Mais on ne devait blesser personne !",
                    ("Terminer la conversation","iv-fin-saboteur"));return;
            case "iv-saboteur-ko":
                MontrerFilRouge("Saboteur","Saboteur de Facon","Vous feriez mieux de retourner dans votre petite guilde.\nLes saboteurs prennent la fuite.",
                    ("Terminer la conversation","iv-fin-saboteur-ko"));return;
            case "iv-abandon-notification":
                MontrerFilRouge("Perrine",perrine,"Vous n'êtes pas venu... Les saboteurs ont disparu. Les habitants ne peuvent plus compter sur vous.",
                    ("Terminer","iv-fin-notification"));return;
        }
    }

    private void AfficherActeV(string n)
    {
        string nom="Mireille Valence, membre S★★ de Facon";
        switch (n)
        {
            case "debut":
                MontrerFilRouge("Mireille",nom,"Bonjour. Je cherche le maître de cette guilde.\nVous : C'est moi. Et vous êtes ?\nMireille : Je représente Facon, à Liberta.",
                    ("Encore vous ? Votre guilde commence sérieusement à me fatiguer.","v-enerve"),
                    ("Bienvenue. J'imagine que vous venez pour une raison particulière.","v-poli"),
                    ("Si c'est pour nous menacer, vous pouvez repartir.","v-menace"));return;
            case "v-enerve":case "v-poli":case "v-menace":
                MontrerFilRouge("Mireille",nom,
                    (n=="v-enerve"?"Je vous rassure, le sentiment est réciproque.":
                    n=="v-poli"?"Enfin quelqu'un qui sait recevoir un invité.":
                    "Je vous conseille de ne pas confondre menace et proposition commerciale.")+
                    "\nFacon protège cette région depuis des années. Nous avons investi des sommes considérables. Nous avons des familles à nourrir et des engagements à tenir.\nEt vous arrivez, vous installez votre guilde, puis vous commencez à récupérer nos contrats.",
                    OptionsActeV());return;
            case "v-adversaire-ok":
                MontrerFilRouge("Mireille",nom,"Hum... Vous avez du répondant. Je comprends mieux pourquoi Charles se plaint autant de vous.\nVous : Charles devrait apprendre à respecter les autres.\nMireille : C'est possible. Mais vous devriez également apprendre à respecter vos concurrents.",
                    ("Terminer la conversation","v-fin-adversaire"));return;
            case "v-adversaire-ko":
                MontrerFilRouge("Mireille",nom,"Des paroles faciles lorsqu'on n'a presque personne à payer. Nous reparlerons de votre indépendance lorsque vous aurez une véritable guilde.",
                    ("Terminer la conversation","v-fin-aucun"));return;
            case "v-diplomatie-ok":
                MontrerFilRouge("Mireille",nom,"Une coopération ? Voilà une idée nettement plus intéressante. Je ne peux rien promettre, mais je pourrais en parler à notre maître de guilde.\nVous : Je pense que tout le monde aurait à y gagner.\nMireille : Peut-être. Nous verrons.",
                    ("Terminer la conversation","v-fin-diplomatie"));return;
            case "v-diplomatie-ko":
                MontrerFilRouge("Mireille",nom,"Vous voulez notre aide alors que vous n'avez rien à nous apporter ? Vous êtes ambitieux. Je vous accorde au moins cela.",
                    ("Terminer la conversation","v-fin-aucun"));return;
            case "v-preuves-ok":
                MontrerFilRouge("Mireille",nom,"Des preuves ? ... Je ne sais pas ce que Charles a bien pu faire sans notre autorisation.\nVous : Alors vous devriez peut-être lui poser quelques questions.\nMireille : Je vais m'en charger. Personnellement.",
                    ("Terminer la conversation","v-fin-preuves"));return;
            case "v-preuves-ko":
                MontrerFilRouge("Mireille",nom,"Vous avez surtout beaucoup d'accusations. Vous devriez éviter de salir publiquement le nom de Facon sans pouvoir les défendre devant le Comté.",
                    ("Terminer la conversation","v-fin-preuves-ko"));return;
            case "v-accord":
                MontrerFilRouge("Mireille",nom,"Un arrangement simple. Vous reconnaissez Facon comme guilde principale de la région. En échange, nous vous versons 2 000 écus.\nVous conservez votre établissement, mais Facon supervise les contrats commerciaux importants.",
                    ("J'accepte votre proposition.","v-accepter"),
                    ("Jamais. Notre guilde restera indépendante.","v-refuser"));return;
        }
    }

    private (string Texte,string Action)[] OptionsActeV()
    {
        var choix = new System.Collections.Generic.List<(string,string)>
        {
            ("Les habitants ne vous appartiennent pas. (D20 Charisme, 15)","v-adversaire"),
            ("Nous pourrions travailler ensemble, plutôt que de nous battre. (D20 Charisme, 13)","v-diplomatie")
        };
        if (_filRouge.PreuveCommerciale || _filRouge.TemoignageFavorable ||
            _filRouge.PreuveFalsification || _filRouge.PreuveSabotage)
            choix.Add(("J'ai des preuves de vos agissements. (D20 Calme, 2)","v-preuves"));
        choix.Add(("Que voulez-vous exactement ?","v-accord"));
        return choix.ToArray();
    }

    private void AfficherActeVI(string n)
    {
        string nom="Seigneur de Rose-Vallée";
        switch (n)
        {
            case "debut":
                var choix = new System.Collections.Generic.List<(string,string)>
                {
                    ("Je demande que notre indépendance soit officiellement reconnue. (D20 Charisme)","vi-independance"),
                    ("Nous proposons une coopération entre nos deux guildes. (D20 Charisme, 11)","vi-cooperation")
                };
                if (_filRouge.PreuveFalsification)
                    choix.Add(("Facon a falsifié des contrats ! (D20 Intelligence, 8)","vi-accusation"));
                if (!_filRouge.AccordDependance)
                    choix.Add(("Je souhaite confirmer l'accord proposé par Facon.","vi-soumission"));
                MontrerFilRouge("Seigneur",nom,
                    "Nous sommes réunis aujourd'hui afin de mettre un terme aux différends opposant la guilde de Rose-Vallée à Facon, établie à Liberta.\nMireille : Mon Seigneur, nous ne faisons que défendre nos intérêts légitimes.\nSeigneur : Et vous, maître de guilde ? Que souhaitez-vous déclarer ?",
                    choix.ToArray());return;
            case "vi-a":
                MontrerFilRouge("Seigneur",nom,"Rose-Vallée a besoin de sa propre guilde.\nJe partage votre avis. Aucune guilde ne peut prétendre posséder l'exclusivité de notre Comté sans décision officielle. Votre guilde est reconnue pleinement indépendante.",
                    ("Terminer l'audience","vi-fin-a"));return;
            case "vi-a-accusation":
                MontrerFilRouge("Seigneur",nom,"Vous : Voici les documents. Notre autorisation a été falsifiée.\nQu'est-ce que cela signifie, madame Valence ?\nMireille : Je vais devoir m'entretenir avec Charles Suriname.\nSeigneur : Vous ferez cela plus tard. Ces accords sont suspendus.",
                    ("Terminer l'audience","vi-fin-a"));return;
            case "vi-b-defaut":
                MontrerFilRouge("Seigneur",nom,"Vos arguments sont recevables, mais je ne peux ignorer les engagements déjà pris avec Facon.\nMireille : Nous sommes prêts à faire un effort.\nSeigneur : Un accord de non-ingérence me semble préférable.",
                    ("Accepter le compromis","vi-fin-b"));return;
            case "vi-b-cooperation":
                MontrerFilRouge("Seigneur",nom,"Mireille : Je crois que nous pouvons trouver un terrain d'entente.\nVoilà une proposition constructive. Les deux guildes s'engagent à une coexistence pacifique.",
                    ("Terminer l'audience","vi-fin-b"));return;
            case "vi-c-defaut":case "vi-c-accusation":
                MontrerFilRouge("Seigneur",nom,"Je ne dispose pas d'éléments suffisants pour remettre en cause les accords existants.\nMireille : Une décision raisonnable.",
                    ("Terminer l'audience","vi-fin-c"));return;
            case "vi-c-cooperation":
                MontrerFilRouge("Seigneur",nom,"Mireille : Une coopération exige de la confiance. Or, je ne pense pas que nous en soyons là.\nSeigneur : Les dispositions existantes resteront provisoirement en vigueur.",
                    ("Terminer l'audience","vi-fin-c"));return;
            case "vi-c-soumission":
                MontrerFilRouge("Seigneur",nom,"Mireille : Voilà qui simplifie grandement les choses.\nSeigneur : Si tel est votre choix, je n'ai aucune objection.",
                    ("Confirmer la dépendance à Facon","vi-fin-c-volontaire"));return;
        }
    }
}
