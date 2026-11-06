using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Interface d'achat et de vente. Le catalogue est lu depuis InfoJeu/Objets.txt
/// sans modifier ce document. Les prix sont PROVISOIRES : le fichier ne contient
/// aucun tarif. Une unité est échangée par clic (pas de sélecteur de quantité).
/// </summary>
public partial class MainMenu
{
    private sealed class ObjetCommerce
    {
        public string Nom { get; set; } = "";
        public string Categorie { get; set; } = "";
        public string Description { get; set; } = "";
        public long Prix { get; set; }
    }

    // Barème temporaire centralisé, à remplacer par les tarifs du jeu.
    private static readonly Dictionary<string, long> PrixCategoriesMagasin =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Nourriture"] = 10,
            ["Boisson non alcoolisé"] = 6,
            ["Boisson alcoolisé"] = 18,
            ["Monture"] = 3000,
            ["Vehicule"] = 1500,
            ["Arme"] = 300,
            ["Armure"] = 800,
            ["Objet Magique Divers"] = 3500,
            ["Arme Magique"] = 5000,
            ["Armure Magique"] = 4200,
            ["Meuble"] = 240,
            ["Potion"] = 120,
            ["Composant Potion"] = 25,
            ["Vêtement"] = 80,
            ["Matière Première"] = 20,
            ["Objet de Décoration"] = 180
        };

    private readonly Dictionary<string, ObjetCommerce> _catalogueMagasin = new(StringComparer.Ordinal);
    private readonly List<ObjetCommerce> _objetsBoutiqueAffiches = new();
    private readonly List<string> _objetsGuildeAffiches = new();
    private InventaireGuilde _inventaireGuilde;
    private Window _fenetreAchatMagasin;
    private ItemList _listeItemsBoutique;
    private ItemList _listeItemsGuilde;
    private Button _boutonAcheterBoutique;
    private Button _boutonVendreBoutique;
    private Label _labelBoutique;
    private Control _clicInterieurMagasin;
    private string _magasinActuel = "";

    private void InitialiserAchatsMagasins()
    {
        _inventaireGuilde = InventaireGuilde.ChargerOuCreer();
        if (_inventaireGuilde == null)
            GD.PushError("MainMenu : inventaire de guilde indisponible.");
        ChargerCatalogueMagasin();

        const string racine = "FenetreAchatMagasin/Marge/Layout/";
        _fenetreAchatMagasin = GetNode<Window>("FenetreAchatMagasin");
        _labelBoutique = GetNode<Label>(racine + "Labels/LabelMagasin");
        _listeItemsBoutique = GetNode<ItemList>(racine + "Items/ListeItemMagasin");
        _listeItemsGuilde = GetNode<ItemList>(racine + "Items/ListeItemJoueur");
        _boutonAcheterBoutique = GetNode<Button>(racine + "Boutons/ButtonAcheter");
        _boutonVendreBoutique = GetNode<Button>(racine + "Boutons/ButtonVendre");

        _boutonAcheterBoutique.Pressed += AcheterObjetBoutique;
        _boutonVendreBoutique.Pressed += VendreObjetBoutique;
        _listeItemsBoutique.ItemSelected += _ => ActualiserBoutonsCommerce();
        _listeItemsGuilde.ItemSelected += _ => ActualiserBoutonsCommerce();
        GetNode<Button>(racine + "Boutons/ButtonFermer").Pressed += FermerAchatMagasin;
        _fenetreAchatMagasin.CloseRequested += FermerAchatMagasin;
        _fenetreAchatMagasin.Hide();

        // Capteur invisible couvrant toute l'image d'intérieur. La rue est
        // masquée pendant la visite : les boutons des façades n'interceptent
        // donc pas le second clic.
        _clicInterieurMagasin = new Control
        {
            Name = "ZoneClicInterieurMagasin",
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        _zoneMagasins.AddChild(_clicInterieurMagasin);
        _clicInterieurMagasin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _clicInterieurMagasin.GuiInput += evenement =>
        {
            if (evenement is InputEventMouseButton clic && clic.Pressed &&
                clic.ButtonIndex == MouseButton.Left && !_tempsEnCours &&
                !string.IsNullOrEmpty(_magasinActuel))
                OuvrirAchatMagasin();
        };
        _clicInterieurMagasin.Hide();
    }

    private void ChargerCatalogueMagasin()
    {
        const string chemin = "res://InfoJeu/Objets.txt";
        _catalogueMagasin.Clear();
        if (!Godot.FileAccess.FileExists(chemin))
        {
            GD.PushError("Magasins : catalogue absent : " + chemin);
            return;
        }

        string categorie = "";
        foreach (string ligne in Godot.FileAccess.GetFileAsString(chemin).Split('\n'))
        {
            string texte = ligne.Trim();
            if (texte.StartsWith("-- ", StringComparison.Ordinal))
            {
                string donnees = texte.Substring(3).Trim();
                int separateur = donnees.IndexOf(':');
                string nom = (separateur >= 0 ? donnees.Substring(0, separateur) : donnees).Trim();
                string description = separateur >= 0 ? donnees.Substring(separateur + 1).Trim() : "";
                if (nom.Length == 0 || !PrixCategoriesMagasin.TryGetValue(categorie, out long prix))
                    continue;
                _catalogueMagasin.TryAdd(nom, new ObjetCommerce
                {
                    Nom = nom, Categorie = categorie, Description = description, Prix = prix
                });
            }
            else if (texte.StartsWith("- ", StringComparison.Ordinal))
                categorie = texte.Substring(2).Trim();
        }

        if (_catalogueMagasin.Count == 0)
            GD.PushError("Magasins : aucun objet chargé depuis " + chemin);
    }

    /// <summary>Rattache les catégories documentées aux commerces existants.</summary>
    private static bool EstVenduDansMagasin(string magasin, ObjetCommerce objet)
    {
        if (objet == null) return false;
        string categorie = objet.Categorie;
        // Le rhum n'est pas commercialisé par défaut dans les villes jouables.
        if (objet.Nom == "Rhum") return false;
        return magasin switch
        {
            "Forge" => categorie == "Arme" || categorie == "Armure",
            "Vendeur de mobilier" => categorie == "Meuble" || categorie == "Objet de Décoration",
            "Alchimiste" => categorie == "Potion",
            "Herboriste" => categorie == "Composant Potion",
            "Enchanteur" => categorie == "Objet Magique Divers" ||
                categorie == "Arme Magique" || categorie == "Armure Magique",
            "Marché" => categorie == "Nourriture" || categorie == "Boisson non alcoolisé" ||
                categorie == "Boisson alcoolisé" || categorie == "Matière Première",
            "Tailleur" => categorie == "Vêtement",
            // L'obriche n'est pas proposée comme monture dans ces villes.
            "Écurie" => categorie == "Monture" && objet.Nom == "Ufaur",
            "Paysan" => categorie == "Nourriture",
            // Aucun livre, bijou ou navire n'est encore référencé dans Objets.txt.
            "Librairie" or "Bijouterie" or "Port" => false,
            _ => false
        };
    }

    private void EntrerDansMagasin(string magasin)
    {
        if (_tempsEnCours || _panneauActif != "magasin" || _fenetreAchatMagasin == null)
            return;

        string image = TrouverImageInterieurMagasin(magasin);
        if (string.IsNullOrEmpty(image))
        {
            AfficherInformation("Le décor de la boutique « " + magasin +
                " » est introuvable pour cette ville. Vérifier les images du dossier Cités.");
            return;
        }

        // L'image de rue et la rue active sont conservées pour le retour.
        _magasinActuel = magasin;
        foreach (Control rue in _ruesMagasins.Values) rue.Hide();
        _fondMagasin.Texture = ResourceLoader.Load<Texture2D>(image);
        _clicInterieurMagasin.Show();
    }

    private string TrouverImageInterieurMagasin(string magasin)
    {
        if (!VillesMagasins.TryGetValue(_villeDepart ?? "", out var ville))
            ville = VillesMagasins["Rose-Vallée"];

        string[] fichiers = magasin switch
        {
            "Forge" => new[] { "Forgeron.png", "Forge.png" },
            "Librairie" => new[] { "Librairie.png" },
            "Bijouterie" => new[] { "Bijouterie.png", "Bijoutier.png" },
            "Vendeur de mobilier" => new[] { "Meuble.png", "Mobilier.png", "VendeurDeMeuble.png" },
            "Alchimiste" => new[] { "Alchimiste.png" },
            "Herboriste" => new[] { "Herboriste.png", "Herboristerie.png" },
            "Enchanteur" => new[] { "Enchanteur.png" },
            "Marché" => new[] { "Marché.png", "Marche.png" },
            "Tailleur" => new[] { "Tailleur.png" },
            "Écurie" => new[] { "Ecurie.png", "Écurie.png" },
            "Paysan" => new[] { "Paysan.png" },
            "Port" => new[] { "Port.png" },
            _ => Array.Empty<string>()
        };

        foreach (string fichier in fichiers)
        {
            string chemin = "res://Images/Fonds/Cités/" + ville.Dossier + "/" + fichier;
            if (ResourceLoader.Exists(chemin)) return chemin;
        }
        GD.PushWarning("Image d'intérieur absente : " + ville.Dossier + "/" + magasin);
        return "";
    }

    private void OuvrirAchatMagasin()
    {
        if (_tempsEnCours || string.IsNullOrEmpty(_magasinActuel) ||
            _fenetreAchatMagasin.Visible)
            return;
        RafraichirListesCommerce();
        _fenetreAchatMagasin.PopupCentered();
    }

    private void FermerAchatMagasin()
    {
        _fenetreAchatMagasin.Hide();
        _magasinActuel = "";
        _clicInterieurMagasin.Hide();
        // Restaurer la rue exacte d'origine (y compris les rues bifurquées).
        AfficherRueMagasins(_rueActiveMagasins);
    }

    private void RafraichirListesCommerce()
    {
        _listeItemsBoutique.Clear();
        _listeItemsGuilde.Clear();
        _objetsBoutiqueAffiches.Clear();
        _objetsGuildeAffiches.Clear();
        _labelBoutique.Text = _magasinActuel + " :";
        _objetsBoutiqueAffiches.AddRange(_catalogueMagasin.Values
            .Where(objet => EstVenduDansMagasin(_magasinActuel, objet))
            .OrderBy(objet => objet.Categorie)
            .ThenBy(objet => objet.Nom, StringComparer.CurrentCultureIgnoreCase));

        foreach (ObjetCommerce objet in _objetsBoutiqueAffiches)
        {
            int index = _listeItemsBoutique.AddItem(objet.Nom + " — " +
                FormaterEcusBudget(objet.Prix));
            _listeItemsBoutique.SetItemTooltip(index, objet.Categorie +
                (string.IsNullOrEmpty(objet.Description) ? "" : "\n" + objet.Description) +
                "\nPrix d'achat : " + FormaterEcusBudget(objet.Prix));
        }
        if (_objetsBoutiqueAffiches.Count == 0)
        {
            int index = _listeItemsBoutique.AddItem("Aucun objet référencé dans cette boutique.");
            _listeItemsBoutique.SetItemDisabled(index, true);
        }

        if (_inventaireGuilde != null)
        {
            foreach (var possession in _inventaireGuilde.Quantites
                .Where(p => p.Value > 0)
                .OrderBy(p => p.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                _objetsGuildeAffiches.Add(possession.Key);
                int index = _listeItemsGuilde.AddItem(possession.Key + " × " + possession.Value);
                if (_catalogueMagasin.TryGetValue(possession.Key, out ObjetCommerce objet))
                    _listeItemsGuilde.SetItemTooltip(index, objet.Categorie +
                        "\nValeur de revente : " + FormaterEcusBudget(PrixRevente(objet)) +
                        (EstVenduDansMagasin(_magasinActuel, objet)
                            ? "" : "\nCette boutique ne reprend pas cet objet."));
            }
        }
        ActualiserBoutonsCommerce();
    }

    private static long PrixRevente(ObjetCommerce objet) =>
        Math.Max(1, objet.Prix / 2);

    private void ActualiserBoutonsCommerce()
    {
        bool possible = _etat != null && _horloge != null && _budget != null &&
            _inventaireGuilde != null && !_tempsEnCours;
        int[] achat = _listeItemsBoutique.GetSelectedItems();
        int[] vente = _listeItemsGuilde.GetSelectedItems();
        _boutonAcheterBoutique.Disabled = !possible || achat.Length == 0 ||
            achat[0] < 0 || achat[0] >= _objetsBoutiqueAffiches.Count;
        _boutonVendreBoutique.Disabled = !possible || vente.Length == 0 ||
            vente[0] < 0 || vente[0] >= _objetsGuildeAffiches.Count ||
            !_catalogueMagasin.TryGetValue(
                vente.Length > 0 && vente[0] >= 0 && vente[0] < _objetsGuildeAffiches.Count
                    ? _objetsGuildeAffiches[vente[0]] : "", out ObjetCommerce objet) ||
            !EstVenduDansMagasin(_magasinActuel, objet);
    }

    private void AcheterObjetBoutique()
    {
        int[] selection = _listeItemsBoutique.GetSelectedItems();
        if (_etat == null || _budget == null || _inventaireGuilde == null ||
            _horloge == null || _tempsEnCours || selection.Length == 0 ||
            selection[0] < 0 || selection[0] >= _objetsBoutiqueAffiches.Count)
            return;

        ObjetCommerce objet = _objetsBoutiqueAffiches[selection[0]];
        if (_etat.Ecus < objet.Prix)
        {
            AfficherInformation("Fonds insuffisants pour acheter " + objet.Nom +
                ". Prix : " + FormaterEcusBudget(objet.Prix) + ".");
            return;
        }
        if (!_inventaireGuilde.Ajouter(objet.Nom))
        {
            AfficherInformation("Impossible d'ajouter cet objet à l'inventaire.");
            return;
        }

        _etat.Ecus -= objet.Prix;
        EnregistrerMouvementBudget(RegistreBudget.AchatsMagasin, -objet.Prix,
            "Achat de " + objet.Nom + " à " + _magasinActuel + " (" + _villeDepart + ").");
        AjouterEntreeJournal("Achat de " + objet.Nom + " pour " +
            FormaterEcusBudget(objet.Prix) + " (" + _magasinActuel + ").", "commerce");
        AfficherValeur(_ecus, _etat.Ecus);
        RafraichirListesCommerce();
    }

    private void VendreObjetBoutique()
    {
        int[] selection = _listeItemsGuilde.GetSelectedItems();
        if (_etat == null || _budget == null || _inventaireGuilde == null ||
            _horloge == null || _tempsEnCours || selection.Length == 0 ||
            selection[0] < 0 || selection[0] >= _objetsGuildeAffiches.Count)
            return;

        string nom = _objetsGuildeAffiches[selection[0]];
        if (!_catalogueMagasin.TryGetValue(nom, out ObjetCommerce objet) ||
            !EstVenduDansMagasin(_magasinActuel, objet))
            return;

        long prix = PrixRevente(objet);
        if (_etat.Ecus > long.MaxValue - prix || !_inventaireGuilde.Retirer(nom))
            return;

        _etat.Ecus += prix;
        EnregistrerMouvementBudget(RegistreBudget.VentesMagasin, prix,
            "Vente de " + nom + " à " + _magasinActuel + " (" + _villeDepart + ").");
        AjouterEntreeJournal("Vente de " + nom + " pour " +
            FormaterEcusBudget(prix) + " (" + _magasinActuel + ").", "commerce");
        AfficherValeur(_ecus, _etat.Ecus);
        RafraichirListesCommerce();
    }
}
