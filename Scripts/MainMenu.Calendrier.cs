using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

/// <summary>
/// Calendrier mensuel de la guilde. Les cases sont créées en C# dans le
/// GridContainer déjà présent dans MainMenu.tscn.
/// Les événements viennent des états de la partie, pas d'un fichier InfoJeu.
/// </summary>
public partial class MainMenu
{
    private const int NombreCasesCalendrier = 42; // 7 colonnes x 6 semaines
    private static readonly CultureInfo CultureCalendrier =
        CultureInfo.GetCultureInfo("fr-FR");

    private Label _labelMoisCalendrier;
    private GridContainer _grilleCalendrier;
    private RichTextLabel _infoJourCalendrier;
    private Button _boutonMoisPrecedentCalendrier;
    private Button _boutonMoisSuivantCalendrier;
    private DateTime _moisAfficheCalendrier;
    private DateTime? _jourSelectionneCalendrier;

    private enum TypeEvenementCalendrier
    {
        Recrutement,
        Mission,
        Concours,
        Echeance
    }

    private sealed class EvenementCalendrier
    {
        public DateTime Instant { get; }
        public string Titre { get; }
        public string Detail { get; }
        public TypeEvenementCalendrier Type { get; }

        public EvenementCalendrier(DateTime instant, string titre, string detail,
            TypeEvenementCalendrier type)
        {
            Instant = instant;
            Titre = titre;
            Detail = detail;
            Type = type;
        }
    }

    private static Color CouleurEvenementCalendrier(TypeEvenementCalendrier type) =>
        type switch
        {
            TypeEvenementCalendrier.Recrutement => new Color("#DBB36E"),
            TypeEvenementCalendrier.Mission => new Color("#77A5D6"),
            TypeEvenementCalendrier.Concours => new Color("#BCA1E0"),
            TypeEvenementCalendrier.Echeance => new Color("#DB7E70"),
            _ => Colors.White
        };

    private void InitialiserCalendrier()
    {
        const string racine = "FenetreCalendrier/Marge/Layout/";
        _labelMoisCalendrier = GetNode<Label>(racine + "PartieSuperieur/LabelMois");
        _boutonMoisPrecedentCalendrier = GetNode<Button>(
            racine + "PartieSuperieur/BoutonMoisPrecedent");
        _boutonMoisSuivantCalendrier = GetNode<Button>(
            racine + "PartieSuperieur/BoutonMoisSuivant");
        _grilleCalendrier = GetNode<GridContainer>(racine + "GrilleCalendrier");
        _infoJourCalendrier = GetNode<RichTextLabel>(racine + "InfoJourSelectionne");

        _grilleCalendrier.Columns = 7;
        _grilleCalendrier.AddThemeConstantOverride("h_separation", 5);
        _grilleCalendrier.AddThemeConstantOverride("v_separation", 5);
        _infoJourCalendrier.BbcodeEnabled = false;

        _boutonMoisPrecedentCalendrier.Pressed += () => ChangerMoisCalendrier(-1);
        _boutonMoisSuivantCalendrier.Pressed += () => ChangerMoisCalendrier(1);
        GetNode<Button>(racine + "ButtonFermer").Pressed +=
            () => _fenetreCalendrierBureau?.Hide();

        if (_horloge != null)
        {
            _moisAfficheCalendrier = new DateTime(
                _horloge.Instant.Year, _horloge.Instant.Month, 1);
            _jourSelectionneCalendrier = _horloge.Instant.Date;
            RafraichirCalendrier();
        }
    }

    private void AfficherCalendrierMoisCourant()
    {
        if (_fenetreCalendrierBureau == null || _horloge == null || _tempsEnCours)
            return;

        // Chaque ouverture revient au mois courant, sans avancer l'horloge.
        _moisAfficheCalendrier = new DateTime(
            _horloge.Instant.Year, _horloge.Instant.Month, 1);
        _jourSelectionneCalendrier = _horloge.Instant.Date;
        RafraichirCalendrier();
        _fenetreCalendrierBureau.PopupCentered();
    }

    private void ChangerMoisCalendrier(int nombreMois)
    {
        if (_moisAfficheCalendrier == default || _grilleCalendrier == null)
            return;

        if ((nombreMois < 0 && _moisAfficheCalendrier.Year == 1 &&
                _moisAfficheCalendrier.Month == 1) ||
            (nombreMois > 0 && _moisAfficheCalendrier.Year == 9999 &&
                _moisAfficheCalendrier.Month == 12))
            return;

        _moisAfficheCalendrier = _moisAfficheCalendrier.AddMonths(nombreMois);
        _jourSelectionneCalendrier = null;
        RafraichirCalendrier();
    }

    private static bool EssayerLireDateCalendrier(string texte, out DateTime date) =>
        DateTime.TryParseExact(texte, "yyyy-MM-dd'T'HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>
    /// Ne montre que les événements que le joueur connaît déjà.
    /// Les dates secrètes de déclenchement des actes du fil rouge ou
    /// des propositions aléatoires de missions ne sont pas affichées.
    /// </summary>
    private List<EvenementCalendrier> CollecterEvenementsCalendrier()
    {
        List<EvenementCalendrier> evenements = new();

        RendezVousAuberge rendezVous = _aubergeEtat?.RendezVous;
        if (rendezVous != null && !rendezVous.EnCours &&
            EssayerLireDateCalendrier(rendezVous.Date, out DateTime dateRendezVous))
        {
            evenements.Add(new EvenementCalendrier(dateRendezVous,
                "Recrutement à l'auberge",
                "Rendez-vous prévu avec les candidats au recrutement.",
                TypeEvenementCalendrier.Recrutement));
        }

        if (_etatMissions?.Missions != null)
        {
            foreach (MissionGuilde mission in _etatMissions.Missions)
            {
                if (mission.Etat != "EnCours" ||
                    !EssayerLireDateCalendrier(mission.DateRetour, out DateTime retour))
                    continue;

                string titre = string.IsNullOrWhiteSpace(mission.Titre)
                    ? mission.Categorie : mission.Titre;
                evenements.Add(new EvenementCalendrier(retour,
                    "Retour prévu : " + titre,
                    "Mission de rang " + ReglesMissions.FormaterRang(mission.Rang) +
                    ". Date indicative : une mission peut être prolongée selon son résultat.",
                    TypeEvenementCalendrier.Mission));
            }
        }

        if (_registreMembres?.Membres != null)
        {
            foreach (MembreGuilde membre in _registreMembres.Membres)
            {
                if (string.IsNullOrWhiteSpace(membre.ConcoursEnCours) ||
                    !EssayerLireDateCalendrier(membre.DateRetourConcours,
                        out DateTime dateConcours))
                    continue;

                evenements.Add(new EvenementCalendrier(dateConcours,
                    "Retour du concours " + membre.ConcoursEnCours,
                    "Participant : " + membre.NomComplet + ".",
                    TypeEvenementCalendrier.Concours));
            }
        }

        // Le joueur connaît le délai de quatre heures pour retrouver les
        // saboteurs. À l'inverse, les futurs actes secrets restent cachés.
        if (_filRouge?.AttendRue4 == true &&
            EssayerLireDateCalendrier(_filRouge.DateLimiteRue4,
                out DateTime dateLimite))
        {
            evenements.Add(new EvenementCalendrier(dateLimite,
                "Dernier délai pour retrouver les saboteurs",
                "Atteindre la rue indiquée avant l'expiration du délai.",
                TypeEvenementCalendrier.Echeance));
        }

        return evenements.OrderBy(e => e.Instant).ThenBy(e => e.Titre).ToList();
    }

    private static StyleBoxFlat StyleJourCalendrier(bool aujourdHui,
        bool selectionne, bool evenement, bool survol)
    {
        Color bordure = selectionne ? new Color("#F0CE8A") :
            aujourdHui ? new Color("#C59B58") :
            evenement ? new Color("#8D7857") : new Color("#5A5147");

        return new StyleBoxFlat
        {
            BgColor = selectionne ? new Color(0.39f, 0.31f, 0.20f, 0.85f) :
                survol ? new Color(0.34f, 0.29f, 0.23f, 0.8f) :
                aujourdHui ? new Color(0.34f, 0.29f, 0.20f, 0.65f) :
                evenement ? new Color(0.26f, 0.25f, 0.24f, 0.55f) :
                new Color(0.21f, 0.20f, 0.19f, 0.35f),
            BorderColor = bordure,
            BorderWidthLeft = selectionne || aujourdHui ? 2 : 1,
            BorderWidthTop = selectionne || aujourdHui ? 2 : 1,
            BorderWidthRight = selectionne || aujourdHui ? 2 : 1,
            BorderWidthBottom = selectionne || aujourdHui ? 2 : 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5
        };
    }

    private void RafraichirCalendrier()
    {
        if (_grilleCalendrier == null || _labelMoisCalendrier == null ||
            _moisAfficheCalendrier == default)
            return;

        string mois = _moisAfficheCalendrier.ToString("MMMM yyyy", CultureCalendrier);
        _labelMoisCalendrier.Text = char.ToUpper(mois[0], CultureCalendrier) + mois.Substring(1);

        _boutonMoisPrecedentCalendrier.Disabled =
            _moisAfficheCalendrier.Year == 1 && _moisAfficheCalendrier.Month == 1;
        _boutonMoisSuivantCalendrier.Disabled =
            _moisAfficheCalendrier.Year == 9999 && _moisAfficheCalendrier.Month == 12;

        foreach (Node ancien in _grilleCalendrier.GetChildren())
        {
            _grilleCalendrier.RemoveChild(ancien);
            ancien.QueueFree();
        }

        List<EvenementCalendrier> evenements = CollecterEvenementsCalendrier();
        var parJour = evenements.GroupBy(e => e.Instant.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        DateTime premier = _moisAfficheCalendrier;
        int decalageLundi = ((int)premier.DayOfWeek + 6) % 7;
        int nombreJours = DateTime.DaysInMonth(premier.Year, premier.Month);
        DateTime aujourdhui = _horloge?.Instant.Date ?? DateTime.MinValue;

        for (int index = 0; index < NombreCasesCalendrier; index++)
        {
            int numero = index - decalageLundi + 1;
            if (numero < 1 || numero > nombreJours)
            {
                _grilleCalendrier.AddChild(new Control
                {
                    CustomMinimumSize = new Vector2(40, 48),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill
                });
                continue;
            }

            DateTime dateJour = new(premier.Year, premier.Month, numero);
            bool aujourdHui = dateJour == aujourdhui;
            bool selectionne = _jourSelectionneCalendrier == dateJour;
            List<EvenementCalendrier> duJour = parJour.TryGetValue(dateJour,
                out List<EvenementCalendrier> trouves) ? trouves : new();

            Button boutonJour = new()
            {
                Name = "Jour" + numero,
                Text = numero.ToString(CultureInfo.InvariantCulture),
                CustomMinimumSize = new Vector2(40, 48),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                TooltipText = duJour.Count == 0 ?
                    dateJour.ToString("dddd d MMMM yyyy", CultureCalendrier) :
                    string.Join("\n", duJour.Select(e =>
                        e.Instant.ToString("HH:mm", CultureCalendrier) + " — " + e.Titre))
            };
            boutonJour.AddThemeFontSizeOverride("font_size", 22);
            boutonJour.AddThemeColorOverride("font_color", new Color("#E8D7B8"));
            boutonJour.AddThemeColorOverride("font_hover_color", new Color("#FFF0CF"));
            boutonJour.AddThemeStyleboxOverride("normal", StyleJourCalendrier(
                aujourdHui, selectionne, duJour.Count > 0, false));
            boutonJour.AddThemeStyleboxOverride("hover", StyleJourCalendrier(
                aujourdHui, selectionne, duJour.Count > 0, true));
            boutonJour.AddThemeStyleboxOverride("pressed", StyleJourCalendrier(
                aujourdHui, true, duJour.Count > 0, true));

            boutonJour.Pressed += () => SelectionnerJourCalendrier(dateJour);
            _grilleCalendrier.AddChild(boutonJour);

            if (duJour.Count == 0) continue;

            // Jusqu'à quatre points de couleurs différentes sous le numéro.
            // Plusieurs événements du même type sont détaillés au clic.
            HBoxContainer marqueurs = new()
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            boutonJour.AddChild(marqueurs);
            marqueurs.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
            marqueurs.OffsetTop = -20f;
            marqueurs.OffsetBottom = -1f;
            marqueurs.AddThemeConstantOverride("separation", 2);
            foreach (TypeEvenementCalendrier type in duJour.Select(e => e.Type)
                .Distinct().Take(4))
            {
                Label point = new()
                {
                    Text = "●",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                point.AddThemeFontSizeOverride("font_size", 17);
                point.AddThemeColorOverride("font_color",
                    CouleurEvenementCalendrier(type));
                marqueurs.AddChild(point);
            }
        }

        RafraichirInfoJourCalendrier(evenements);
    }

    private void SelectionnerJourCalendrier(DateTime jour)
    {
        _jourSelectionneCalendrier = jour.Date;
        RafraichirCalendrier();
    }

    private void RafraichirInfoJourCalendrier(List<EvenementCalendrier> evenements)
    {
        if (_infoJourCalendrier == null) return;
        if (_jourSelectionneCalendrier == null)
        {
            _infoJourCalendrier.Text =
                "Sélectionnez une journée pour afficher les événements prévus.";
            return;
        }

        DateTime jour = _jourSelectionneCalendrier.Value.Date;
        List<EvenementCalendrier> duJour = evenements
            .Where(e => e.Instant.Date == jour).OrderBy(e => e.Instant).ToList();

        string entete = jour.ToString("dddd d MMMM yyyy", CultureCalendrier);
        entete = char.ToUpper(entete[0], CultureCalendrier) + entete.Substring(1);

        _infoJourCalendrier.Text = duJour.Count == 0
            ? entete + "\nAucun événement prévu."
            : entete + "\n\n" + string.Join("\n\n", duJour.Select(e =>
                e.Instant.ToString("HH:mm", CultureCalendrier) + " — " + e.Titre +
                (string.IsNullOrWhiteSpace(e.Detail) ? "" : "\n" + e.Detail)));
    }
}
