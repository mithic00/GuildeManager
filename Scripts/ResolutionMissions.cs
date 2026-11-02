using System;
using System.Linq;
using System.Collections.Generic;

public enum IssueMission { Continue, Reussite, Echec, Mort }
public sealed class ResultatJourMission
{
    public IssueMission Issue { get; set; }
    public bool SansIncident { get; set; }
    public bool SansGainSecondaire { get; set; }
}
/// <summary>Jets par catégorie et progression magique, selon InfoJeu/Missions&Rang.txt.</summary>
public static class ResolutionMissions
{
    private static bool PeutRelancer(MissionGuilde mission) =>
        mission.AvantageTraitsDisponible && !mission.AvantageTraitsConsomme;

    // Comme auparavant : un seul D20 avec le meilleur bonus parmi les membres.
    private static int JetGroupeResultat(MissionGuilde mission,
        List<MembreGuilde> equipe, string stat, int cible)
    {
        int bonus = equipe.Max(m => ReglesCaracteristiques.Modificateur(
            Math.Clamp(m.Statistiques.GetValueOrDefault(stat, 10), 1, 20)));
        int resultat = Random.Shared.Next(1, 21) + bonus;
        if (resultat < cible && PeutRelancer(mission))
        {
            mission.AvantageTraitsConsomme = true;
            resultat = Random.Shared.Next(1, 21) + bonus;
        }
        return resultat;
    }

    private static bool JetGroupe(List<MembreGuilde> equipe, string stat, int cible) =>
        Random.Shared.Next(1, 21) + equipe.Max(m => ReglesCaracteristiques.Modificateur(
            Math.Clamp(m.Statistiques.GetValueOrDefault(stat, 10), 1, 20))) >= cible;

    private static bool JetGroupeAvecAvantage(MissionGuilde mission,
        List<MembreGuilde> equipe, string stat, int cible) =>
        JetGroupeResultat(mission, equipe, stat, cible) >= cible;

    // Exception Protection d'un lieu : TOUS les membres sont testés.
    private static bool JetIndividuel(MissionGuilde mission,
        MembreGuilde membre, string stat, int cible)
    {
        int bonus = ReglesCaracteristiques.Modificateur(
            Math.Clamp(membre.Statistiques.GetValueOrDefault(stat, 10), 1, 20));
        int resultat = Random.Shared.Next(1, 21) + bonus;
        if (resultat < cible && PeutRelancer(mission))
        {
            mission.AvantageTraitsConsomme = true;
            resultat = Random.Shared.Next(1, 21) + bonus;
        }
        return resultat >= cible;
    }
    private static IssueMission TirerIssue(params (IssueMission issue, double poids)[] issues)
    {
        double total = issues.Sum(x => x.poids);
        double val = Random.Shared.NextDouble() * total;
        foreach (var i in issues) { val -= i.poids; if (val < 0) return i.issue; }
        return issues[^1].issue;
    }
    /// <summary>
    /// Une fois par mission sans D20 : transfère AU PLUS 5 points de
    /// continuation/échec vers la réussite, sans toucher au risque de décès.
    /// </summary>
    private static IssueMission TirerIssueAvecTraits(MissionGuilde mission,
        params (IssueMission issue, double poids)[] issues)
    {
        if (!PeutRelancer(mission))
            return TirerIssue(issues);

        var modifiees = issues.ToArray();
        int indexReussite = Array.FindIndex(modifiees, x => x.issue == IssueMission.Reussite);
        if (indexReussite < 0) return TirerIssue(issues);

        double transfert = 0;
        foreach (IssueMission source in new[] { IssueMission.Continue, IssueMission.Echec })
        {
            for (int i = 0; i < modifiees.Length && transfert < 5; i++)
            {
                if (modifiees[i].issue != source) continue;
                double retire = Math.Min(5 - transfert, modifiees[i].poids);
                modifiees[i] = (modifiees[i].issue, modifiees[i].poids - retire);
                transfert += retire;
            }
        }
        if (transfert > 0)
        {
            modifiees[indexReussite] = (IssueMission.Reussite,
                modifiees[indexReussite].poids + transfert);
            mission.AvantageTraitsConsomme = true;
        }
        return TirerIssue(modifiees);
    }

    // Écart entre le niveau magique moyen du groupe et le palier attendu de la mission.
    private static int Avantage(MissionGuilde mission, List<MembreGuilde> groupe)
    {
        double moyen = groupe.Average(m => (double)m.NiveauMagique);
        int rang = ReglesMissions.IndexRang(mission.Rang);
        int palier = ReglesMissions.Rangs.Length - 1;
        for (int i = 0; i < ReglesMissions.Rangs.Length - 1; i++)
        {
            if (moyen < SeuilsMagie[i + 1]) { palier = i; break; }
        }
        return Math.Max(0, palier - rang);
    }
    private static readonly int[] SeuilsMagie =
    { 0,21,41,61,81,151,201,251,301,351,401,601,701,781,851,911,961,1000 };

    private static int CibleInvestigation(string rang)
    {
        int i = ReglesMissions.IndexRang(rang);
        return i < 5 ? 10 : i < 11 ? 13 : i < 17 ? 15 : 20;
    }
    private static int CibleRecherche(string rang)
    {
        int i=ReglesMissions.IndexRang(rang);
        return i < 5 ? 10 : i < 11 ? 12 : 15;
    }
    private static int CibleTraque(string rang)
    {
        int i=ReglesMissions.IndexRang(rang);
        return i < 5 ? 7 : i < 11 ? 10 : 13;
    }
    private static IssueMission Affrontement(int avantage, bool captureVivant = true)
    {
        if(avantage >= 2) return TirerIssue((IssueMission.Reussite,90),(IssueMission.Continue,captureVivant?9:5),
            (IssueMission.Echec,captureVivant?1:5));
        if(avantage == 1) return TirerIssue((IssueMission.Reussite,captureVivant?80:70),
            (IssueMission.Continue,captureVivant?15:25),(IssueMission.Echec,4),(IssueMission.Mort,1));
        return TirerIssue((IssueMission.Reussite,captureVivant?60:50),
            (IssueMission.Continue,30),(IssueMission.Echec,captureVivant?5:15),(IssueMission.Mort,5));
    }
    private static IssueMission MagieDanger(MissionGuilde mission, int avantage)
    {
        if(avantage >= 2) return TirerIssueAvecTraits(mission,(IssueMission.Reussite,90),(IssueMission.Continue,9),(IssueMission.Echec,1));
        if(avantage == 1) return TirerIssueAvecTraits(mission,(IssueMission.Reussite,80),(IssueMission.Continue,15),(IssueMission.Echec,4),(IssueMission.Mort,1));
        return TirerIssueAvecTraits(mission,(IssueMission.Reussite,60),(IssueMission.Continue,30),(IssueMission.Echec,5),(IssueMission.Mort,5));
    }
    public static int DureeFixee(MissionGuilde mission)
    {
        if(mission.Categorie=="Escorte")
            return ReglesMissions.IndexRang(mission.Rang) < 11 ? Random.Shared.Next(1,5):Random.Shared.Next(5,61);
        if(mission.Categorie=="Protection d'un lieu") return Random.Shared.Next(1,4);
        return 0;
    }
    public static ResultatJourMission Resoudre(MissionGuilde mission, List<MembreGuilde> equipe)
    {
        int avantage=Avantage(mission,equipe);
        int jour=mission.JoursEcoules;
        IssueMission issue=IssueMission.Continue;
        bool sansIncident=false;
        string cat=mission.Categorie;
        switch(cat)
        {
            case "Enquête":
            {
                int cible=CibleInvestigation(mission.Rang);
                int intelligence=JetGroupeResultat(mission,equipe,"Intelligence",cible);
                int charisme=JetGroupeResultat(mission,equipe,"Charisme",cible);
                if(intelligence>=cible && charisme>=cible) issue=IssueMission.Reussite;
                else
                {
                    mission.EchecsEnqueteConsecutifs = intelligence<10 && charisme<10
                        ? mission.EchecsEnqueteConsecutifs+1 : 0;
                    if(mission.EchecsEnqueteConsecutifs>=2) issue=IssueMission.Echec;
                }
                break;
            }
            case "Escorte":
                if(jour>=mission.DureeFixee)
                {
                    if(Random.Shared.NextDouble()<0.35) {issue=IssueMission.Reussite;sansIncident=true;}
                    else if(avantage>=2)issue=TirerIssueAvecTraits(mission,(IssueMission.Reussite,99),(IssueMission.Echec,1));
                    else if(avantage==1)issue=TirerIssueAvecTraits(mission,(IssueMission.Reussite,90),(IssueMission.Echec,9),(IssueMission.Mort,1));
                    else issue=TirerIssueAvecTraits(mission,(IssueMission.Reussite,80),(IssueMission.Echec,15),(IssueMission.Mort,5));
                }
                break;
            case "Élimination de danger":
            case "Renfort pour Assaut":
                issue=MagieDanger(mission,avantage);
                break;
            case "Recherche mort ou vif":
            case "Capture vif":
                if(jour==1)
                {
                    if(!JetGroupeAvecAvantage(mission,equipe,"Intelligence",CibleTraque(mission.Rang)))issue=IssueMission.Echec;
                }
                else
                {
                    bool capture=JetGroupeAvecAvantage(mission,equipe,"Agilite",10);
                    if(cat=="Capture vif"&&!capture)issue=IssueMission.Echec;
                    else issue=Affrontement(avantage,capture);
                }
                break;
            case "Protection d'un lieu":
                if(mission.DureeFixee==0)mission.DureeFixee=DureeFixee(mission);
                if(mission.SansIncident)
                {
                    if(jour>=mission.DureeFixee) {issue=IssueMission.Reussite;sansIncident=true;}
                }
                else
                {
                    bool tests=equipe.All(m=>JetIndividuel(mission,m,"Calme",10)&&JetIndividuel(mission,m,"Endurance",10));
                    if(tests)
                    {
                        issue=avantage>=2?TirerIssue((IssueMission.Reussite,95),(IssueMission.Echec,1),(IssueMission.Continue,4)):
                            avantage==1?TirerIssue((IssueMission.Reussite,90),(IssueMission.Continue,7),(IssueMission.Echec,2),(IssueMission.Mort,1)):
                            TirerIssue((IssueMission.Reussite,90),(IssueMission.Echec,3),(IssueMission.Continue,4),(IssueMission.Mort,3));
                    }
                    else issue=avantage>=2?TirerIssue((IssueMission.Reussite,90),(IssueMission.Echec,9),(IssueMission.Continue,1)):
                        avantage==1?TirerIssue((IssueMission.Reussite,70),(IssueMission.Continue,4),(IssueMission.Echec,25),(IssueMission.Mort,1)):
                        TirerIssue((IssueMission.Reussite,40),(IssueMission.Echec,40),(IssueMission.Continue,15),(IssueMission.Mort,5));
                    if(issue==IssueMission.Reussite && jour<mission.DureeFixee)issue=IssueMission.Continue;
                }
                break;
            default:
            {
                string[] stats=cat switch
                {
                    "Recherche d'un objet"=>new[]{"Intelligence","Frustration","Charisme"},
                    "Recherche d'un ingrédient"=>new[]{"Force","Frustration","Agilite"},
                    "Recherche d'un animal"=>new[]{"Vitesse","Endurance","Agilite"},
                    "Service"=>new[]{"Empathie","Gentillesse","Communication"},
                    _=>new[]{"Intelligence"}
                };
                // Recherche et service : la table défavorable ne s'applique
                // que si au moins deux des trois D20 échouent. Chaque jet est
                // effectué, même si un précédent a échoué.
                int cible = CibleRecherche(mission.Rang);
                var jetsRates = stats.Where(stat => !JetGroupe(equipe, stat, cible)).ToList();
                int echecs = jetsRates.Count;
                // En recherche/service, un seul échec ne rend pas le résultat
                // défavorable. La relance est réservée au deuxième échec.
                if (echecs >= 2 && PeutRelancer(mission))
                {
                    mission.AvantageTraitsConsomme = true;
                    if (JetGroupe(equipe, jetsRates[0], cible))
                        echecs--;
                }
                bool jetsFavorables = echecs < 2;
                if(cat=="Recherche d'un animal")
                    issue=jetsFavorables?TirerIssue((IssueMission.Reussite,79),(IssueMission.Continue,15),(IssueMission.Echec,5),(IssueMission.Mort,1)):
                        TirerIssue((IssueMission.Continue,79),(IssueMission.Echec,15),(IssueMission.Reussite,5),(IssueMission.Mort,1));
                else issue=jetsFavorables?TirerIssue((IssueMission.Reussite,80),(IssueMission.Continue,15),(IssueMission.Echec,5)):
                    TirerIssue((IssueMission.Continue,80),(IssueMission.Echec,15),(IssueMission.Reussite,5));
                break;
            }
        }
        return new ResultatJourMission { Issue=issue, SansIncident=sansIncident };
    }

    public static void ProgresserMagie(MembreGuilde membre, bool eleve)
    {
        if(membre.EstMaitreDeGuilde || !membre.PotentielMagique.HasValue)return;
        double actuel = Math.Max(membre.NiveauMagique, membre.NiveauMagiquePrecis);
        double potentiel=membre.PotentielMagique.Value;
        if(actuel>=potentiel)return;
        double vitesse=Random.Shared.Next(1,6)*(eleve?1.20:1.0);
        // Une même formule génère 20 courbes : linéaire à motivation 20,
        // ralentissement de plus en plus marqué lorsque la motivation diminue.
        int motivation = Math.Clamp(membre.Motivation ?? 10, 1, 20);
        double exposant = Math.Pow(20 - motivation, 2) / 38.0;
        double reste = (potentiel - actuel) / potentiel;
        membre.NiveauMagiquePrecis = Math.Min(potentiel,
            actuel + vitesse * Math.Pow(reste, exposant));
        membre.NiveauMagique=(int)Math.Floor(membre.NiveauMagiquePrecis);
    }

    public static void BonusStats(MissionGuilde mission, List<MembreGuilde> equipe)
    {
        string[] options=mission.Categorie switch
        {
            "Enquête"=>new[]{"Intelligence","Charisme"},
            "Capture vif"=>new[]{"Agilite","Intelligence"},
            "Protection d'un lieu"=>new[]{"Calme","Endurance"},
            "Recherche d'un objet"=>new[]{"Intelligence","Frustration","Charisme"},
            "Recherche d'un ingrédient"=>new[]{"Force","Frustration","Agilite"},
            "Recherche d'un animal"=>new[]{"Vitesse","Endurance","Agilite"},
            "Service"=>new[]{"Empathie","Communication","Gentillesse"},
            _=>Array.Empty<string>()
        };
        if(options.Length==0)return;
        foreach(var m in equipe)
        {
            if(Random.Shared.NextDouble()>=0.05)continue;
            string stat=options[Random.Shared.Next(options.Length)];
            m.Statistiques[stat]=Math.Min(20,m.Statistiques.GetValueOrDefault(stat,10)+1);
        }
    }
}
