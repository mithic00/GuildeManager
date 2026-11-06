using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Affichage consultatif de l'inventaire commun de la guilde.
/// Les quantités sont détenues par InventaireGuilde et les catégories/détails
/// viennent de CatalogueObjets. Aucune action ne modifie les possessions ici.
/// </summary>
public partial class MainMenu
{
    private readonly Dictionary<string, ItemList> _listesInventaireBureau =
        new(StringComparer.Ordinal);

    private void InitialiserFenetreInventaire()
    {
        const string racine = "FenetreInventaire/Marge/Layout/";
        TabContainer onglets = GetNode<TabContainer>(racine + "Inventaire");
        _listesInventaireBureau.Clear();

        // Chaque onglet est un HBoxContainer contenant ListeObjet.
        // Les intitulés proviennent des nœuds de la scène, sans les modifier.
        foreach (Node enfant in onglets.GetChildren())
        {
            if (enfant is not Control conteneur)
                continue;

            ItemList liste = conteneur.GetNodeOrNull<ItemList>("ListeObjet");
            if (liste == null)
            {
                GD.PushWarning("Inventaire : ListeObjet absent de l'onglet " + conteneur.Name);
                continue;
            }

            _listesInventaireBureau[conteneur.Name.ToString()] = liste;
        }

        GetNode<Button>(racine + "ButtonFermer").Pressed +=
            () => _fenetreInventaireBureau.Hide();
    }

    /// <summary>
    /// Associe les catégories du catalogue aux onglets présents dans MainMenu.tscn.
    /// Armes et armures magiques rejoignent leurs catégories d'équipement.
    /// Les objets inconnus (ou retirés du catalogue) restent consultables.
    /// </summary>
    private static string OngletPourCategorieInventaire(string categorie) =>
        categorie switch
        {
            "Nourriture" or "Boisson non alcoolisé" or "Boisson alcoolisé"
                => "Nourriture & Boisson",
            "Vehicule" or "Véhicule" => "Véhicule",
            "Arme" or "Arme Magique" => "Arme",
            "Armure" or "Armure Magique" => "Armure",
            "Objet Magique Divers" or "Objet Magique" => "Objets Magiques",
            "Vêtement" or "Vêtements" => "Vêtements",
            "Objet de Décoration" => "Objet de Décoration",
            "Monture" => "Monture",
            "Meuble" => "Meuble",
            "Potion" => "Potion",
            "Composant Potion" => "Composant Potion",
            "Matière Première" => "Matière Première",
            _ => "Objet Divers"
        };

    /// <summary>
    /// Appelé à chaque ouverture : reflète aussi les achats, ventes,
    /// récompenses de mission ou changements depuis la dernière consultation.
    /// Ne recharge pas SQLite : les achats restent en mémoire jusqu'à la sauvegarde.
    /// </summary>
    private void RafraichirInventaireBureau()
    {
        if (_listesInventaireBureau.Count == 0)
            return;

        Dictionary<string, int> nombresParOnglet =
            _listesInventaireBureau.Keys.ToDictionary(nom => nom, _ => 0, StringComparer.Ordinal);
        foreach (ItemList liste in _listesInventaireBureau.Values)
            liste.Clear();

        if (_inventaireGuilde?.Quantites == null)
        {
            foreach (ItemList liste in _listesInventaireBureau.Values)
                AjouterLigneInventaireVide(liste, "Inventaire indisponible.");
            return;
        }

        foreach (var possession in _inventaireGuilde.Quantites
            .Where(p => p.Value > 0)
            .OrderBy(p => p.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            bool reference = _catalogueMagasin.TryGetValue(possession.Key, out ObjetCommerce objet);
            string onglet = OngletPourCategorieInventaire(reference ? objet.Categorie : "");
            if (!_listesInventaireBureau.TryGetValue(onglet, out ItemList liste))
            {
                GD.PushWarning("Inventaire : onglet introuvable pour " + possession.Key +
                               " (catégorie " + (reference ? objet.Categorie : "inconnue") + ").");
                continue;
            }

            string libelle = possession.Key + "  × " + possession.Value.ToString("N0", Francais);
            int index = liste.AddItem(libelle);
            string info = reference
                ? "Catégorie : " + objet.Categorie +
                  "\nUnité : " + objet.Unite +
                  (string.IsNullOrWhiteSpace(objet.Description)
                      ? "" : "\n" + objet.Description)
                : "Cet objet n'est plus référencé dans le catalogue.";
            liste.SetItemTooltip(index, info);
            nombresParOnglet[onglet]++;
        }

        foreach (var entree in _listesInventaireBureau)
        {
            if (nombresParOnglet[entree.Key] == 0)
                AjouterLigneInventaireVide(entree.Value, "Aucun objet dans cette catégorie.");
        }
    }

    private static void AjouterLigneInventaireVide(ItemList liste, string message)
    {
        int index = liste.AddItem(message);
        liste.SetItemDisabled(index, true);
    }
}
