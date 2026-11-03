using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Navigation minimale dans les sept villes : les rues et boutons sont déjà
/// positionnés dans MainMenu.tscn. Ne modifie aucun nœud de la scène.
/// </summary>
public partial class MainMenu
{
    private Control _zoneMagasins;
    private readonly Dictionary<string, Control> _ruesMagasins = new();
    private string _prefixeVilleMagasins = "";
    private string _rueActiveMagasins = "";

    private static readonly Dictionary<string, (string Prefixe, string Dossier)> VillesMagasins =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Rose-Vallée"] = ("RoseVallee", "Rose-Vallee"),
            ["Agrar"] = ("RoseAgrar", "Agrar"),
            ["Alturia"] = ("RoseAlturia", "Alturia"),
            ["Gori Nirog"] = ("RoseGoriNorog", "GoriNirog"),
            ["Niclass"] = ("RoseNiclass", "Niclass"),
            ["Shaelis"] = ("RoseShaelis", "Shaelis"),
            ["Thorm"] = ("RoseThorm", "Thorm")
        };

    private static readonly Dictionary<string, string> NomsMagasins = new()
    {
        ["BoutonForgeron"] = "Forge",
        ["BoutonLibrairie"] = "Librairie",
        ["BoutonBijouterie"] = "Bijouterie",
        ["BoutonBijoutier"] = "Bijouterie",
        ["BoutonMobilier"] = "Vendeur de mobilier",
        ["BoutonMeuble"] = "Vendeur de mobilier",
        ["BoutonAlchimiste"] = "Alchimiste",
        ["BoutonHerboriste"] = "Herboriste",
        ["BoutonEnchanteur"] = "Enchanteur",
        ["BoutonMarche"] = "Marché",
        ["BoutonMarché"] = "Marché",
        ["BoutonTailleur"] = "Tailleur",
        ["BoutonEcurie"] = "Écurie",
        ["BoutonPaysan"] = "Paysan",
        ["BoutonPort"] = "Port"
    };

    private void InitialiserNavigationMagasins()
    {
        _zoneMagasins = GetNode<Control>("LayoutsPanneauPrincipal/Magasins");
        _zoneMagasins.Hide();
        _ruesMagasins.Clear();

        // _villeDepart est chargée par InitialiserRecrutementAuberge().
        if (!VillesMagasins.TryGetValue(_villeDepart ?? "", out var ville))
        {
            GD.PushWarning("Ville de départ non reconnue pour les magasins : " + _villeDepart);
            ville = VillesMagasins["Rose-Vallée"];
        }
        _prefixeVilleMagasins = ville.Prefixe;

        foreach (Node enfant in _zoneMagasins.GetChildren())
        {
            if (enfant is not Control rue) continue;
            _ruesMagasins[rue.Name.ToString()] = rue;
            rue.Hide();

            foreach (Node sousNoeud in rue.GetChildren())
            {
                if (sousNoeud is not Button bouton) continue;
                string nomBouton = bouton.Name.ToString();
                string nomRue = rue.Name.ToString();

                if (nomBouton == "BoutonGuilde")
                {
                    bouton.Pressed += () => AfficherPanneau("bureau");
                }
                else if (nomBouton.StartsWith("BoutonExitRue", StringComparison.Ordinal))
                {
                    string suffixe = nomBouton.Substring("BoutonExitRue".Length);
                    string villeRue = nomRue.Substring(0, nomRue.IndexOf("Rue", StringComparison.Ordinal));
                    string destination = villeRue + "Rue" + suffixe;
                    bouton.Pressed += () => AfficherRueMagasins(destination);
                }
                else if (NomsMagasins.TryGetValue(nomBouton, out string magasin))
                {
                    string nomMagasin = magasin;
                    bouton.Pressed += () => AfficherInformation(
                        nomMagasin + " — boutique en cours de développement (WIP).");
                }
                else
                {
                    GD.PushWarning("Bouton de magasin non relié : " + nomRue + "/" + nomBouton);
                }
            }
        }

        // Rue 1 sélectionnée par défaut, affichée uniquement à l'ouverture du panneau.
        _rueActiveMagasins = _prefixeVilleMagasins + "Rue1";
        AfficherRueMagasins(_rueActiveMagasins);
        _zoneMagasins.Hide();
    }

    private void OuvrirMagasins()
    {
        if (BasculerPanneau("magasin"))
            AfficherRueMagasins(_prefixeVilleMagasins + "Rue1");
    }

    private void AfficherRueMagasins(string nomRue)
    {
        if (!_ruesMagasins.TryGetValue(nomRue, out Control rue))
        {
            GD.PushWarning("Rue de magasin introuvable : " + nomRue);
            return;
        }

        // On n'affiche que la ville de départ, jamais les rues d'une autre ville.
        if (!nomRue.StartsWith(_prefixeVilleMagasins + "Rue", StringComparison.Ordinal))
            return;

        _rueActiveMagasins = nomRue;
        VerifierRueFilRouge(nomRue);
        foreach (Control autreRue in _ruesMagasins.Values)
            autreRue.Visible = autreRue == rue;

        if (!VillesMagasins.TryGetValue(_villeDepart ?? "", out var ville))
            ville = VillesMagasins["Rose-Vallée"];

        string suffixe = nomRue.Substring(_prefixeVilleMagasins.Length + "Rue".Length);
        string image = "res://Images/Fonds/Cités/" + ville.Dossier +
            "/Rue" + suffixe.Replace('_', '.') + ".png";
        if (ResourceLoader.Exists(image))
            _fondMagasin.Texture = GD.Load<Texture2D>(image);
        else
            GD.PushWarning("Image de rue introuvable : " + image);
    }
}
