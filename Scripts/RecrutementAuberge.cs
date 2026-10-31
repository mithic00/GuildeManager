using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Globalization;

public sealed class RendezVousAuberge
{
    public string Date { get; set; } = "";
    public List<CandidatAuberge> Candidats { get; set; } = new();
    public int Index { get; set; }
    public bool EnCours { get; set; }
}
public sealed class CandidatAuberge
{
    public MembreGuilde Personnage { get; set; } = new();
    public List<string> StatsConnues { get; set; } = new();
    public bool DiscussionBloquee { get; set; }
    public bool Recrute { get; set; }
    // Identifiant stable du groupe au sein d'un rendez-vous.
    // -1 désigne les candidats des anciennes sauvegardes (recrutables seuls).
    public int GroupeId { get; set; } = -1;
}
public sealed class RecrutementAubergeEtat
{
    public const string CleSauvegarde = "recrutement_auberge";
    public RendezVousAuberge RendezVous { get; set; }
    public static RecrutementAubergeEtat Charger()
    {
        string contenu = SaveManager.GetValue(CleSauvegarde, "");
        if (!string.IsNullOrEmpty(SaveManager.LastError))
            throw new InvalidOperationException(SaveManager.LastError);
        return string.IsNullOrWhiteSpace(contenu) ? new() :
            JsonSerializer.Deserialize<RecrutementAubergeEtat>(contenu)
            ?? throw new InvalidOperationException("Recrutement sauvegardé illisible");
    }
    public string Serialiser() => JsonSerializer.Serialize(this);
}
public static class GenerateurCandidats
{
    public const double CoefficientPotentielAuberge = 120.0;
    private static readonly (string Nom, int PoidsPremier, int PoidsAutres)[] PoidsPouvoirs =
    {
        ("Mage", 100, 100),
        ("Sorcier", 60, 60),
        ("Clerc", 80, 80),
        ("Démoniste", 45, 45),
        ("Invocateur", 20, 20),
        ("Archimage", 20, 40),
        ("Ensorceleur", 8, 8),
        ("Enchanteur", 15, 15),
        ("Métamorphe", 50, 50),
        ("Purificateur", 60, 60),
        ("Scribtum", 5, 5),
        ("Schistite", 5, 5),
        ("Aquiste", 7, 7),
        ("Végenae", 5, 5),
        ("Follet", 10, 10),
        ("Storma", 5, 5),
        ("Druide", 30, 30),
        ("Chamane", 18, 18),
        ("Guerrier", 95, 95),
        ("Assassin de l'ombre", 25, 25),
        ("Archer Magique", 60, 60),
        ("Proctor", 50, 50),
        ("Poistodon", 12, 12),
        ("Berserker", 25, 25),
        ("Moine", 20, 20),
        ("Invocateur d'Arme", 5, 5),
        ("Élémentaire d'Eau", 75, 75),
        ("Élémentaire de Plante", 65, 65),
        ("Élémentaire de Feu", 65, 65),
        ("Élémentaire de Roche", 65, 65),
        ("Élémentaire de Vent", 65, 65),
        ("Élémentaire d'Alcool", 2, 2),
        ("Élémentaire de Glace", 30, 30),
        ("Élémentaire de Vapeur", 3, 3),
        ("Élémentaire de Sang", 1, 1),
        ("Élémentaire de Froid", 4, 4),
        ("Élémentaire de Lave", 10, 10),
        ("Élémentaire de Chaleur", 13, 13),
        ("Élémentaire de Bois", 9, 9),
        ("Élémentaire Fongique", 3, 3),
        ("Élémentaire de Spore", 3, 3),
        ("Élémentaire de Diamant", 2, 2),
        ("Élémentaire de Métal", 20, 20),
        ("Élémentaire de Pierre Précieuse", 1, 1),
        ("Élémentaire de Foudre", 30, 30),
        ("Élémentaire de Lumière", 25, 25),
        ("Élémentaire d'Ombre", 13, 13),
        ("Élémentaire de Fumée", 6, 6),
        ("Élémentaire Supérieur", 1, 1),
        ("Télékinésiste", 80, 80),
        ("Animiste", 50, 50),
        ("Devin", 10, 40),
        ("Crieur", 40, 40),
        ("Illusionniste", 16, 16),
        ("Télépathe", 15, 35)
    };
    private static readonly string[] Races =
    { "Humain", "Nain", "Elfe gris", "Haut-Elfe", "Grumol", "Humain des monts", "Tanneur", "Khajiit" };
    public static readonly string[] Stats =
    { "Communication", "Leadership", "Charisme", "Empathie", "Gentillesse",
      "Intelligence", "Memoire", "Volonte", "Calme", "Frustration",
      "Force", "Agilite", "Constitution", "Vitesse", "Endurance" };
    private static readonly string[] Competences =
    { "Athlétisme", "Acrobaties", "Discrétion", "Escamotage", "Arcanes", "Histoire", "Investigation", "Nature", "Religion", "Dressage", "Intuition", "Médecine", "Survie", "Intimidation", "Persuasion", "Représentation", "Tromperie", "Bonne vue", "Mauvaise vue", "Fêtard", "Calme", "Stratégie", "Connaissance Animalière", "Connaissance en Flore", "Navigation", "Enquête", "Vigilant", "Cuisine", "Esquive", "Fouille", "Diplomatie" };
    private static readonly Dictionary<string, string[]> CompetencesMetier = new()
    {
        ["Forgeron"]=new[]{"Athlétisme","Investigation"}, ["Chasseur"]=new[]{"Survie","Discrétion","Nature"},
        ["Paysan"]=new[]{"Nature","Survie"}, ["Eleveur"]=new[]{"Dressage","Connaissance Animalière"},
        ["Tailleur"]=new[]{"Escamotage","Investigation"}, ["Marchand"]=new[]{"Persuasion","Diplomatie"},
        ["Cuisinier"]=new[]{"Cuisine","Nature"}, ["Tavernier"]=new[]{"Persuasion","Intuition"},
        ["Serveur"]=new[]{"Intuition","Vigilant"}, ["Coiffeur"]=new[]{"Persuasion","Escamotage"},
        ["Garde"]=new[]{"Vigilant","Intimidation","Athlétisme"}, ["Noble"]=new[]{"Diplomatie","Histoire"},
        ["Enchanteur"]=new[]{"Arcanes","Investigation"}, ["Professeur"]=new[]{"Histoire","Investigation"},
        ["Etudiant"]=new[]{"Investigation","Histoire"}, ["Etudiant en Magie"]=new[]{"Arcanes","Investigation"},
        ["Marin"]=new[]{"Navigation","Survie"}, ["Boucher"]=new[]{"Cuisine","Nature"},
        ["Boulanger"]=new[]{"Cuisine"}, ["Maraicher"]=new[]{"Nature","Connaissance en Flore"},
        ["Libraire"]=new[]{"Histoire","Investigation"}, ["Acteur de Théâtre"]=new[]{"Représentation","Tromperie"},
        ["Herboriste"]=new[]{"Connaissance en Flore","Nature"}, ["Bucheron"]=new[]{"Nature","Athlétisme"},
        ["Travailleur du cuir"]=new[]{"Escamotage","Survie"}, ["Alchimiste"]=new[]{"Arcanes","Investigation"},
        ["Mineur"]=new[]{"Athlétisme","Survie"}, ["Constructeur"]=new[]{"Athlétisme","Investigation"},
        ["Employé de Ville"]=new[]{"Diplomatie","Investigation"}
    };
    private static readonly Dictionary<string, (string[] Races, int[] Poids)> Repartition = new()
    {
        ["Rose-Vallée"]=(new[]{"Humain","Nain"},new[]{90,5}),
        ["Agrar"]=(new[]{"Nain","Humain"},new[]{80,15}),
        ["Shaelis"]=(new[]{"Elfe gris","Humain"},new[]{90,5}),
        ["Alturia"]=(new[]{"Haut-Elfe"},new[]{98}),
        ["Gori Nirog"]=(new[]{"Grumol","Elfe gris","Nain"},new[]{80,10,5}),
        ["Niclass"]=(new[]{"Humain des monts","Tanneur"},new[]{90,8}),
        ["Thorm"]=(new[]{"Tanneur","Humain des monts"},new[]{90,5})
    };

    public static int TirerPotentiel(double coefficient)
    {
        if (coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(coefficient));
        return Math.Clamp((int)Math.Floor(-coefficient * Math.Log(1.0 - Random.Shared.NextDouble())), 1, 1000);
    }

    // Poids de formation d'un groupe : 25 % solo, 40 % duo, 20 % trio,
    // 10 % quatuor et 5 % groupe de cinq.
    // Les groupes plus grands que le nombre de places restantes sont exclus.
    private static readonly (int Taille, int Poids)[] TaillesGroupes =
    {
        (1, 25), (2, 40), (3, 20), (4, 10), (5, 5)
    };

    public static List<CandidatAuberge> Generer(string ville)
    {
        // La quantité totale reste uniforme de 1 à 10, comme auparavant.
        int total = Random.Shared.Next(1, 11);
        List<CandidatAuberge> resultat = new();
        int groupeId = 0;
        while (resultat.Count < total)
        {
            int taille = TirerTailleGroupe(total - resultat.Count);
            for (int i = 0; i < taille; i++)
            {
                CandidatAuberge candidat = GenererCandidat(ville);
                candidat.GroupeId = groupeId;
                resultat.Add(candidat);
            }
            groupeId++;
        }
        return resultat;
    }

    private static int TirerTailleGroupe(int placesRestantes)
    {
        var possibles = TaillesGroupes.Where(g => g.Taille <= placesRestantes).ToArray();
        int tirage = Random.Shared.Next(possibles.Sum(g => g.Poids));
        foreach (var groupe in possibles)
        {
            tirage -= groupe.Poids;
            if (tirage < 0) return groupe.Taille;
        }
        return possibles[^1].Taille;
    }

    private static CandidatAuberge GenererCandidat(string ville)
    {
        string race = TirerRace(ville);
        string sexe = Random.Shared.Next(2)==0 ? "H" : "F";
        var nom = BanqueNomsPersonnages.Generer(race, sexe);
        int potentiel = TirerPotentiel(CoefficientPotentielAuberge);
        int magie = Math.Min(potentiel, Random.Shared.Next(1,81));
        int niveau = TirerNiveau();
        var stats = Stats.ToDictionary(k=>k,k=>Random.Shared.Next(1,21));
        stats["Memoire"] = TirerMemoire();
        int motivation=TirerMotivation();
        List<string> pouvoirs=TirerPouvoirs(potentiel,motivation,stats["Memoire"]);
        var pvpm=CalculCaracteristiquesMagiques.TirerPourNiveaux(race,pouvoirs,niveau);
        string metier=Random.Shared.Next(100)<80 ? "" :
            CompetencesMetier.Keys.ElementAt(Random.Shared.Next(CompetencesMetier.Count));
        List<string> competences=new();
        // Un personnage sans métier possède de 0 à 3 compétences.
        // Avec un métier, sa compétence est unique et cohérente avec ce métier.
        int nombre=metier==""?Random.Shared.Next(0,4):1;
        string[] selections=metier==""?Competences:CompetencesMetier[metier];
        for(int i=0;i<nombre && competences.Count<selections.Length;i++)
        {
            string candidat=selections[Random.Shared.Next(selections.Length)];
            if(!competences.Contains(candidat))competences.Add(candidat);
            else i--;
        }
        MembreGuilde m=new()
        {
            Id=Guid.NewGuid().ToString("N"), Prenom=nom.Prenom,Nom=nom.Nom,
            Race=race,Sexe=sexe,ApparenceId=Array.IndexOf(new[]{"Humain","Elfe gris","Grumol","Humain des monts","Haut-Elfe","Nain","Tanneur","Khajiit"},race)+1,
            Rang=magie>=1000?"U":"*", Classement=int.MaxValue,
            NiveauMagique=magie,PotentielMagique=potentiel,Pouvoirs=pouvoirs,
            Statistiques=stats,Age=TirerAge(race),Motivation=motivation,Fatigue=0,
            Experience=0,Niveau=niveau,PointsDeVie=pvpm.PointsDeVie,PointsDeMagie=pvpm.PointsDeMagie,
            Moral=100,Bonheur=100,Metier=metier,Competences=competences
        };
        int visibilite=Random.Shared.Next(1,8);
        var connues=Stats.OrderBy(_=>Random.Shared.Next()).Take(visibilite).ToList();
        return new CandidatAuberge{Personnage=m,StatsConnues=connues};
    }

    private static int TirerAge(string race)
    {
        int max=race switch
        {
            "Nain"=>120,"Elfe gris"=>170,"Haut-Elfe"=>200,
            "Grumol"=>60,"Humain des monts"=>45,"Tanneur"=>70,"Khajiit"=>60,_=>80
        };
        return Random.Shared.Next(14,max+1);
    }
    private static int TirerNiveau() {int d=Random.Shared.Next(100);return d<60?1:d<90?2:3;}
    private static int TirerMemoire()
    {
        int d=Random.Shared.Next(1000);
        return d<600?Random.Shared.Next(1,14):d<950?Random.Shared.Next(14,18):
               d<990?Random.Shared.Next(18,20):20;
    }
    private static int TirerMotivation()
    {
        // 75 % pour les valeurs 1..9 ; les autres poids sont recalibrés
        // proportionnellement depuis les 50 % restants.
        double d=Random.Shared.NextDouble();
        if(d<0.75)return Random.Shared.Next(1,10);
        d=(d-0.75)/0.25;
        if(d<0.60)return Random.Shared.Next(10,15);
        if(d<0.80)return Random.Shared.Next(15,18);
        if(d<0.90)return 18;
        if(d<0.96)return 19;
        return 20;
    }
    private static string TirerRace(string ville)
    {
        if (!Repartition.TryGetValue(ville,out var config))
            throw new ArgumentException("Ville de départ inconnue : "+ville);
        int d=Random.Shared.Next(100),c=0;
        for(int i=0;i<config.Races.Length;i++)
        {
            c+=config.Poids[i];
            if(d<c)return config.Races[i];
        }
        string[] autres=Races.Except(config.Races).ToArray();
        return autres[Random.Shared.Next(autres.Length)];
    }
    private static List<string> TirerPouvoirs(int potentiel,int motivation,int memoire)
    {
        List<string> resultat=new(){TiragePondere(false,new List<string>())};
        if(Random.Shared.NextDouble()<0.30)
        {
            resultat.Add(TiragePondere(true,resultat));
            double chance=potentiel>399&&motivation>16&&memoire>16?0.10:0.03;
            while(resultat.Count<PoidsPouvoirs.Length-1 && Random.Shared.NextDouble()<chance)
                resultat.Add(TiragePondere(true,resultat));
        }
        return resultat;
    }
    private static string TiragePondere(bool supplementaire,List<string> exclus)
    {
        var valides=PoidsPouvoirs.Where(p=>!exclus.Contains(p.Nom))
            .Select(p=>(p.Nom,Poids:supplementaire?p.PoidsAutres:p.PoidsPremier))
            .Where(p=>p.Poids>0).ToList();
        int total=valides.Sum(p=>p.Poids),tirage=Random.Shared.Next(total);
        foreach(var p in valides){tirage-=p.Poids;if(tirage<0)return p.Nom;}
        return valides[^1].Nom;
    }
}
