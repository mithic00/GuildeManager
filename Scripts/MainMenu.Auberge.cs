using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Globalization;

/// <summary>
/// Prise de rendez-vous, entretiens à l'auberge et recrutement.
/// L'ensemble de l'état est en mémoire jusqu'au clic "Sauvegarder et Quitter".
/// </summary>
public partial class MainMenu
{
    private RecrutementAubergeEtat _aubergeEtat;
    private Window _choixRecrutement;
    private Control _contenuAuberge;
    private TextureRect _fondAuberge;
    private TextureRect _portraitAuberge;
    private RichTextLabel _infoAuberge, _statAuberge;
    private Label _rencontresRestantes;
    private Button _discuterAuberge, _suivantAuberge, _recruterAuberge, _partirAuberge;
    private string _villeDepart;

    private void InitialiserRecrutementAuberge()
    {
        _aubergeEtat = RecrutementAubergeEtat.Charger();
        _choixRecrutement = GetNode<Window>("FenetreChoixRecrutement");
        _contenuAuberge = GetNode<Control>("LayoutsPanneauPrincipal/RecrutementAuberge");
        _fondAuberge = GetNode<TextureRect>("AffichagePanneauPrincipal/Auberge");
        const string chemin = "LayoutsPanneauPrincipal/RecrutementAuberge/";
        _portraitAuberge = GetNode<TextureRect>(chemin + "Panel/HBoxContainer/VBoxContainer/Portrait");
        _infoAuberge = GetNode<RichTextLabel>(chemin + "Panel/HBoxContainer/VBoxContainer/RichTextLabelInfo");
        _statAuberge = GetNode<RichTextLabel>(chemin + "Panel/HBoxContainer/RichTextLabelStat");
        _rencontresRestantes = GetNode<Label>(chemin + "Panel/LabelRencontreRestante");
        _discuterAuberge = GetNode<Button>(chemin + "Boutons/BoutonDiscuter");
        _suivantAuberge = GetNode<Button>(chemin + "Boutons/BoutonSuivant");
        _recruterAuberge = GetNode<Button>(chemin + "Boutons/BoutonRecruter");
        _partirAuberge = GetNode<Button>(chemin + "Boutons/BoutonPartir");

        var creation = JsonSerializer.Deserialize<CreationPerso.DonneesCreationInitiale>(
            SaveManager.GetValue("creation_initiale", ""));
        _villeDepart = creation?.VilleDepart ?? "";

        GetNode<Button>("FenetreChoixRecrutement/Marge/Boutons/BoutonAuberge").Pressed += ReserverAuberge;
        GetNode<Button>("FenetreChoixRecrutement/Marge/Boutons/BoutonCampagneRecrutement").Pressed +=
            () => AfficherInformationAuberge("La campagne de recrutement sera disponible ultérieurement.");
        GetNode<Button>("FenetreChoixRecrutement/Marge/Boutons/BoutonFermer").Pressed += () => _choixRecrutement.Hide();
        _choixRecrutement.CloseRequested += () => _choixRecrutement.Hide();

        _discuterAuberge.Pressed += DiscuterAvecCandidat;
        _suivantAuberge.Pressed += CandidatSuivant;
        _recruterAuberge.Pressed += RecruterCandidat;
        _partirAuberge.Pressed += QuitterAuberge;

        _contenuAuberge.Hide();
        _fondAuberge.Hide();

        if (_aubergeEtat.RendezVous?.EnCours == true)
            OuvrirAuberge();
        else if (_aubergeEtat.RendezVous != null && _horloge != null
                 && HeureRendezVousAtteinte())
            OuvrirAuberge();
    }

    private void OuvrirChoixRecrutement()
    {
        if (_aubergeEtat == null)
            return;
        if (_aubergeEtat.RendezVous?.EnCours == true)
        {
            OuvrirAuberge();
            return;
        }

        _choixRecrutement.PopupCentered();
    }

    private static DateTime LireInstantRendezVous(string valeur) =>
        DateTime.ParseExact(valeur, "yyyy-MM-dd'T'HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None);

    private bool HeureRendezVousAtteinte() =>
        _aubergeEtat?.RendezVous != null && _horloge != null &&
        _horloge.Instant >= LireInstantRendezVous(_aubergeEtat.RendezVous.Date);

    private void ReserverAuberge()
    {
        _choixRecrutement.Hide();
        if (_horloge == null || _etat == null || _aubergeEtat == null)
            return;
        if (_aubergeEtat.RendezVous != null)
        {
            AfficherInformationAuberge("Un rendez-vous à l'auberge est déjà programmé.");
            return;
        }
        if (_etat.Ecus < 300)
        {
            AfficherInformationAuberge("Le recrutement à l'auberge coûte 300 écus.");
            return;
        }

        // Premier jour à 20h qui se trouve au moins 48 heures dans le futur.
        DateTime limite = _horloge.Instant.AddHours(48);
        DateTime date = limite.Date.AddHours(20);
        if (date < limite) date = date.AddDays(1);

        List<CandidatAuberge> candidats;
        try { candidats = GenerateurCandidats.Generer(_villeDepart); }
        catch (Exception e)
        {
            GD.PushError("Recrutement : " + e.Message);
            AfficherInformationAuberge("Impossible de préparer les candidats : " + e.Message);
            return;
        }

        _etat.Ecus -= 300;
        AfficherValeur(_ecus, _etat.Ecus);
        _aubergeEtat.RendezVous = new RendezVousAuberge
        {
            Date = date.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            Candidats = candidats,
            Index = 0
        };
        AjouterEntreeJournal("300 écus versés pour un recrutement à l'auberge. Rendez-vous : " +
            HorlogeJeu.Formater(date) + ".", "recrutement");
    }

    /// <summary>Retourne vrai si l'événement interrompt la minute qui vient d'être avancée.</summary>
    private bool VerifierRendezVousAuberge()
    {
        if (_aubergeEtat?.RendezVous == null || _aubergeEtat.RendezVous.EnCours)
            return false;
        if (!HeureRendezVousAtteinte()) return false;

        InterrompreAvanceeTemps("Rendez-vous de recrutement à l'auberge.");
        OuvrirAuberge();
        return true;
    }

    private void OuvrirAuberge()
    {
        if (_aubergeEtat?.RendezVous == null) return;
        _choixRecrutement?.Hide();
        _aubergeEtat.RendezVous.EnCours = true;
        AfficherPanneau("auberge");
        _contenuAuberge.Show();
        _fondAuberge.Show();
        _boutonAvancerTemps.Disabled = true;
        AfficherCandidat();
    }

    private CandidatAuberge CandidatCourant
    {
        get
        {
            var rdv = _aubergeEtat?.RendezVous;
            return rdv == null || rdv.Index < 0 || rdv.Index >= rdv.Candidats.Count
                ? null : rdv.Candidats[rdv.Index];
        }
    }

    private void AfficherCandidat()
    {
        var rdv = _aubergeEtat?.RendezVous;
        var candidat = CandidatCourant;
        _rencontresRestantes.Text = rdv == null ? "Rencontres restantes : 0" :
            "Rencontres restantes : " + Math.Max(0, rdv.Candidats.Count - rdv.Index - 1);
        if (candidat == null)
        {
            _infoAuberge.Text = "Les entretiens sont terminés.";
            _statAuberge.Text = "";
            _discuterAuberge.Disabled = _suivantAuberge.Disabled = _recruterAuberge.Disabled = true;
            return;
        }

        var m = candidat.Personnage;
        string genre = m.Sexe == "F" ? "Femme" : "Homme";
        string[] nomsPortraits = { "Humain", "ElfeGris", "Grumol", "HumainDesMonts",
            "HautElfe", "Nain", "Tanneur", "Khajiit" };
        string racePortrait = m.ApparenceId is >= 1 and <= 8 ? nomsPortraits[m.ApparenceId - 1] : "";
        string portrait = "res://Images/Portraits/" + racePortrait + "_" + genre + ".png";
        _portraitAuberge.Texture = ResourceLoader.Load<Texture2D>(
            ResourceLoader.Exists(portrait) ? portrait : "res://Images/Portraits/Portait_Defaut.png");

        _infoAuberge.Text = "Prénom : " + m.Prenom +
            "\nNom : " + m.Nom + "\nÂge : " + m.Age +
            "\nRace : " + m.Race + "\nPouvoirs : " + string.Join(", ", m.Pouvoirs) +
            "\nNiveau magique : " + m.NiveauMagique;

        var connues = candidat.StatsConnues
            .Select(n => n + " : " + m.Statistiques.GetValueOrDefault(n).ToString()).ToList();
        if (candidat.StatsConnues.Contains("Motivation")) connues.Add("Motivation : " + m.Motivation);
        _statAuberge.Text = "Statistiques connues :\n" + string.Join("\n", connues);
        _discuterAuberge.Disabled = candidat.DiscussionBloquee || candidat.Recrute ||
            (candidat.StatsConnues.Count >= GenerateurCandidats.Stats.Length &&
             candidat.StatsConnues.Contains("Motivation"));
        _recruterAuberge.Disabled = candidat.Recrute;
        _suivantAuberge.Disabled = rdv.Index >= rdv.Candidats.Count - 1;
    }

    private void DiscuterAvecCandidat()
    {
        var candidat = CandidatCourant;
        if (candidat == null || candidat.DiscussionBloquee || candidat.Recrute) return;
        int charisme = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur)?
            .Statistiques.GetValueOrDefault("Charisme", 10) ?? 10;
        int bonus = candidat.Personnage.Statistiques.GetValueOrDefault("Communication") >= 15 ? 3 : 0;
        int jet = Random.Shared.Next(1, 21);
        if (jet > charisme + bonus)
        {
            candidat.DiscussionBloquee = true;
            AfficherInformationAuberge("Discussion échouée (d20 : " + jet + ", seuil : " +
                (charisme + bonus) + ").");
        }
        else
        {
            foreach (string cle in new[] { "Memoire", "Motivation" })
                if (!candidat.StatsConnues.Contains(cle)) candidat.StatsConnues.Add(cle);
            string[] cachees = GenerateurCandidats.Stats
                .Where(c => !candidat.StatsConnues.Contains(c)).ToArray();
            if (cachees.Length > 0)
                candidat.StatsConnues.Add(cachees[Random.Shared.Next(cachees.Length)]);
            AfficherInformationAuberge("Discussion réussie (d20 : " + jet + ").");
        }
        AfficherCandidat();
    }

    private void CandidatSuivant()
    {
        var rdv = _aubergeEtat?.RendezVous;
        if (rdv == null || rdv.Index >= rdv.Candidats.Count - 1) return;
        rdv.Index++;
        AfficherCandidat();
    }

    private void RecruterCandidat()
    {
        var candidat = CandidatCourant;
        if (candidat == null || candidat.Recrute || _registreMembres == null) return;
        candidat.Recrute = true;
        MembreGuilde membre = candidat.Personnage;
        int classement = _registreMembres.Membres.Where(m => m.Classement > 0 && m.Classement < int.MaxValue)
            .Select(m => m.Classement).DefaultIfEmpty(0).Max() + 1;
        membre.Classement = membre.Rang == "U" ? 0 : classement;
        _registreMembres.Membres.Add(membre);
        _etat.Membres = _registreMembres.Membres.Count;
        _etat.Pouvoir = _registreMembres.Membres.Sum(m => (long)m.NiveauMagique);
        AfficherValeur(_membres, _etat.Membres);
        AfficherValeur(_pouvoir, _etat.Pouvoir);
        AjouterEntreeJournal(membre.NomComplet + " rejoint la guilde.", "recrutement");
        AfficherCandidat();
    }

    private void QuitterAuberge()
    {
        _aubergeEtat.RendezVous = null;
        _contenuAuberge.Hide();
        _fondAuberge.Hide();
        AfficherPanneau("bureau");
        ActualiserBoutonTemps();
        AjouterEntreeJournal("Fin des entretiens à l'auberge.", "recrutement");
    }

    private void AfficherInformationAuberge(string message)
    {
        // La fenêtre de choix est fermée avant l'affichage pour éviter
        // deux fenêtres exclusives simultanées.
        AcceptDialog dialogue = new() { Title = "Recrutement", DialogText = message };
        AddChild(dialogue);
        dialogue.Confirmed += () => dialogue.QueueFree();
        dialogue.Canceled += () => dialogue.QueueFree();
        dialogue.PopupCentered();
    }
}
