using System;
using System.Collections.Generic;

/// <summary>
/// Un objet du monde de GuildeManager, avec son prix d'achat unitaire.
/// Les noms sont conservés pour rester compatibles avec les inventaires sauvegardés.
/// </summary>
public sealed class ObjetCommerce
{
    public string Nom { get; }
    public string Categorie { get; }
    public string Description { get; }
    public long Prix { get; }
    public string Unite { get; }

    public ObjetCommerce(string nom, string categorie, string description, long prix, string unite)
    {
        if (string.IsNullOrWhiteSpace(nom) || prix <= 0)
            throw new ArgumentException("Un objet doit avoir un nom et un prix positif.");
        Nom = nom;
        Categorie = categorie;
        Description = description;
        Prix = prix;
        Unite = unite;
    }
}

/// <summary>
/// Catalogue central du jeu. Aucune lecture de InfoJeu/Objets.txt à l'exécution.
/// Modifier ici les prix après validation des propositions documentées dans InfoJeu.
/// Conversion de référence : 1 euro = 1 écu. Les prix magiques sont fictifs.
/// </summary>
public static class CatalogueObjets
{
    public static IReadOnlyList<ObjetCommerce> Tous { get; } =
        new List<ObjetCommerce>
        {
            new("Falaquant", "Nourriture", "Grosse pomme de terre (qui n'existe pas dans cet univers) orange. Présent partout sur le globe.", 2L, "kg"),
            new("Choux", "Nourriture", "Présent partout sur le globe.", 3L, "chou"),
            new("Tomate", "Nourriture", "Présent partout sur le globe.", 5L, "kg"),
            new("Dlo", "Nourriture", "Céréale semblable à du blé (qui n'existe pas dans cet univers). Présent partout sur le globe.", 3L, "kg"),
            new("Carotte", "Nourriture", "Présent partout sur le globe.", 2L, "kg"),
            new("Pain", "Nourriture", "Présent partout sur le globe.", 2L, "pain de 500 g"),
            new("Jus de pomme", "Boisson non alcoolisé", "", 3L, "bouteille de 1 L"),
            new("Eau", "Boisson non alcoolisé", "", 1L, "bouteille de 1 L"),
            new("Artrik", "Boisson alcoolisé", "Sorte de cervoise. Présent partout sur le globe", 5L, "chope de 50 cl"),
            new("Rhum", "Boisson alcoolisé", "Originaire du Royaume Grumol et Haut Elfe.", 25L, "bouteille de 70 cl"),
            new("Ufaur", "Monture", "Présent partout sur le globe.", 5000L, "monture"),
            new("Obriche", "Monture", "Présent partout sur le globe pour la consommation. Utilisé comme monture au royaume des sables.", 3000L, "animal"),
            new("Petite Charette individuelle", "Vehicule", "Présent partout sur le globe.", 1200L, "charrette"),
            new("Grande Charette de transport", "Vehicule", "Présent partout sur le globe.", 4000L, "charrette"),
            new("Dague en fer", "Arme", "Présent partout sur le globe.", 110L, "pièce"),
            new("Epée en fer", "Arme", "Présent partout sur le globe.", 220L, "pièce"),
            new("Massue en fer", "Arme", "Présent partout sur le globe.", 60L, "pièce"),
            new("Armure en Fer", "Armure", "Présent partout sur le globe.", 3200L, "pièce"),
            new("Armure en Acier blanc", "Armure", "Présent partout sur le globe.", 6000L, "pièce"),
            new("Armure en Cuir renforcé", "Armure", "Présent partout sur le globe.", 1600L, "pièce"),
            new("Parapluie de lecture magique", "Objet Magique Divers", "permet de déterminer le potentiel magique d'un EM.", 12000L, "pièce"),
            new("Balais indépendant", "Objet Magique Divers", "nettoie une pièce tout seul", 1000L, "pièce"),
            new("Cartes du fou flamboyant", "Objet Magique Divers", "Carte de jeu censé se mélanger tout seul. Peut faire des blagues aux joueurs, voir carrément gâcher une partie.", 200L, "jeu de cartes"),
            new("Epée enflammée", "Arme Magique", "épée simple qui produit des flammes sur demande de son propriétaire", 6500L, "pièce"),
            new("Casque de vision nocture", "Armure Magique", "permet de voir dans le noir une fois porté.", 2900L, "pièce"),
            new("Lit Simple", "Meuble", "", 150L, "pièce"),
            new("Lit Double", "Meuble", "", 300L, "pièce"),
            new("Petite table ronde", "Meuble", "", 80L, "pièce"),
            new("Moyenne table ronde", "Meuble", "", 170L, "pièce"),
            new("Grande table ronde", "Meuble", "", 340L, "pièce"),
            new("Bar", "Meuble", "", 1000L, "pièce"),
            new("Coffre", "Meuble", "", 160L, "pièce"),
            new("Canapé deux places", "Meuble", "", 350L, "pièce"),
            new("Canapé trois places", "Meuble", "", 520L, "pièce"),
            new("Fauteuil", "Meuble", "", 220L, "pièce"),
            new("Etagère", "Meuble", "", 90L, "pièce"),
            new("Potion de guérison", "Potion", "+25 PV / Cicatrise", 75L, "flacon"),
            new("Remède basique", "Potion", "Guérie les maladies classiques", 35L, "flacon"),
            new("Remède d’Algolite (pierre assez rare)", "Potion", "Guérie presque tout.", 5000L, "flacon"),
            new("Remède solaire (retire les coups de soleils)", "Potion", "", 12L, "flacon"),
            new("Potion revigorante", "Potion", "+10 PV +10 PM", 100L, "flacon"),
            new("Potion de restauration", "Potion", "+50 PM", 160L, "flacon"),
            new("Potion de guérison amélioré", "Potion", "+100 PV / Cicatrise", 250L, "flacon"),
            new("Potion de restauration amélioré", "Potion", "+200 PM / +10 PM/min pendant 1h", 900L, "flacon"),
            new("Potion de l’être magique", "Potion", "+50 PV +50 PM", 350L, "flacon"),
            new("Potion suprême", "Potion", "+25% PV +35% PM / 1 par heure maximum.", 1600L, "flacon"),
            new("Potion d’amélioration", "Potion", "+5% d’une stat pendant 1h", 240L, "flacon"),
            new("Potion d’amélioration supérieur", "Potion", "+10 % d’une stat pendant 1h", 600L, "flacon"),
            new("Potion d’amélioration légendaire", "Potion", "+25 % d’une stat pendant 30 minutes", 2500L, "flacon"),
            new("Potion d'explosion magique", "Potion", "Potion sous pression. Une fois au contact de l'air, explose dans une déflagration magique (28 à 39).", 350L, "flacon"),
            new("Potion de brouillard toxique", "Potion", "Au contact de l’air, provoque un brouillard toxique opaque, qui agresse les yeux.", 190L, "flacon"),
            new("Potion électrisante", "Potion", "rend le buveur électrique pendant 5 minutes", 160L, "flacon"),
            new("Potion de changement de voix", "Potion", "", 60L, "flacon"),
            new("Potion de paralysie", "Potion", "", 550L, "flacon"),
            new("Potion d’intelligence", "Potion", "", 450L, "flacon"),
            new("Potion de fausse mort", "Potion", "", 650L, "flacon"),
            new("Potion aquatique", "Potion", "", 250L, "flacon"),
            new("Potion de nyctalopie", "Potion", "", 260L, "flacon"),
            new("Potion de perception", "Potion", "", 300L, "flacon"),
            new("Potion de sommeil", "Potion", "", 90L, "flacon"),
            new("Potion trompe-voix (empêche de dire les voyelles)", "Potion", "", 85L, "flacon"),
            new("Potion de colorisation", "Potion", "", 40L, "flacon"),
            new("Potion de gout", "Potion", "", 30L, "flacon"),
            new("Remède de pilosité", "Potion", "", 75L, "flacon"),
            new("Potion de vérité", "Potion", "", 850L, "flacon"),
            new("Camomille Dorée", "Composant Potion", "", 12L, "lot"),
            new("Menthe des Hautes Falaises", "Composant Potion", "", 10L, "lot"),
            new("Feuille de Solan", "Composant Potion", "", 7L, "lot"),
            new("Racine Rouge", "Composant Potion", "", 8L, "lot"),
            new("Lavande Lunaire", "Composant Potion", "", 15L, "lot"),
            new("Fleur de Verre", "Composant Potion", "", 45L, "lot"),
            new("Mousse d'Ékiaba", "Composant Potion", "", 18L, "lot"),
            new("Sauge Argentée", "Composant Potion", "", 12L, "lot"),
            new("Champignon Bleuvent", "Composant Potion", "Connu pour régénéré la magie", 65L, "lot"),
            new("Graine d'Orme", "Composant Potion", "", 9L, "lot"),
            new("Chemise Femme", "Vêtement", "", 30L, "pièce"),
            new("Chemise Homme", "Vêtement", "", 30L, "pièce"),
            new("Pantalon Femme", "Vêtement", "", 45L, "pièce"),
            new("Pantalon Homme", "Vêtement", "", 45L, "pièce"),
            new("Chapeau", "Vêtement", "", 25L, "pièce"),
            new("Bonnet", "Vêtement", "", 15L, "pièce"),
            new("Chaussure en cuir", "Vêtement", "", 85L, "paire"),
            new("Pierre simple", "Matière Première", "", 8L, "kg"),
            new("Bois de chêne", "Matière Première", "", 35L, "planche"),
            new("Sable", "Matière Première", "", 5L, "sac de 25 kg"),
            new("Tableau", "Objet de Décoration", "", 95L, "pièce"),
            new("Plante décorative", "Objet de Décoration", "", 25L, "pièce"),
            new("Tapis", "Objet de Décoration", "", 70L, "pièce"),
        }.AsReadOnly();
}
