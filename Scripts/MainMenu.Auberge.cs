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
    private Button _discuterAuberge, _precedentAuberge, _suivantAuberge, _recruterAuberge, _partirAuberge;
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
        _precedentAuberge = GetNode<Button>(chemin + "Boutons/BoutonPrécedent");
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
        _precedentAuberge.Pressed += CandidatPrecedent;
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
            _discuterAuberge.Disabled = _precedentAuberge.Disabled =
                _suivantAuberge.Disabled = _recruterAuberge.Disabled = true;
            return;
        }

        var m = candidat.Personnage;
        // Les candidats n'ont jamais de portrait spécifique lors des entretiens.
        _portraitAuberge.Texture = ResourceLoader.Load<Texture2D>(
            "res://Images/Portraits/GrandFormat/Portait_Defaut.png");

        var equipe = MembresEquipe(candidat);
        int positionEquipe = equipe.FindIndex(c => c.Personnage.Id == m.Id) + 1;
        string descriptionEquipe = equipe.Count > 1
            ? "Équipe de " + equipe.Count + " candidats (" + positionEquipe + "/" + equipe.Count +
              "). Recrutement indissociable : tous les membres rejoindront la guilde ensemble.\n\n"
            : "Candidat indépendant.\n\n";

        _infoAuberge.Text = descriptionEquipe + "Prénom : " + m.Prenom +
            "\nNom : " + m.Nom + "\nÂge : " + m.Age +
            "\nRace : " + m.Race + "\nPouvoirs : " + string.Join(", ", m.Pouvoirs) +
            "\nNiveau magique : " + m.NiveauMagique;

        // Motivation n'appartient pas au dictionnaire Statistiques.
        // Elle est révélée et affichée à part, une seule fois.
        var connues = candidat.StatsConnues
            .Where(n => n != "Motivation")
            .Distinct()
            .Select(n => n + " : " + m.Statistiques.GetValueOrDefault(n).ToString()).ToList();
        if (candidat.StatsConnues.Contains("Motivation"))
            connues.Add("Motivation : " + m.Motivation);
        _statAuberge.Text = "Statistiques connues :\n" + string.Join("\n", connues);
        _discuterAuberge.Disabled = candidat.DiscussionBloquee || candidat.Recrute ||
            (candidat.StatsConnues.Count >= GenerateurCandidats.Stats.Length &&
             candidat.StatsConnues.Contains("Motivation"));
        _recruterAuberge.Disabled = equipe.All(c => c.Recrute);
        _precedentAuberge.Disabled = rdv.Index <= 0;
        _suivantAuberge.Disabled = rdv.Index >= rdv.Candidats.Count - 1;
    }

    private void DiscuterAvecCandidat()
    {
        var candidat = CandidatCourant;
        if (candidat == null || candidat.DiscussionBloquee || candidat.Recrute) return;
        int charisme = _registreMembres?.Membres.FirstOrDefault(m => m.EstJoueur)?
            .Statistiques.GetValueOrDefault("Charisme", 10) ?? 10;
        // Le charisme agit désormais comme modificateur du d20 face au seuil 10.
        // Bonus contextuel de communication du candidat conservé.
        int bonusCommunication = candidat.Personnage.Statistiques.GetValueOrDefault("Communication") >= 15 ? 3 : 0;
        int jet = Random.Shared.Next(1, 21);
        int resultat = jet + ReglesCaracteristiques.Modificateur(charisme) + bonusCommunication;
        if (resultat < 10)
        {
            candidat.DiscussionBloquee = true;
            AfficherInformationAuberge("Vous n'apprenez rien de plus après cette discussion");
        }
        else
        {
            bool premiereReussite = !candidat.StatsConnues.Contains("Motivation");
            foreach (string cle in new[] { "Memoire", "Motivation" })
                if (!candidat.StatsConnues.Contains(cle)) candidat.StatsConnues.Add(cle);
            // La première réussite donne Mémoire et Motivation.
            // Les suivantes révèlent chacune une caractéristique supplémentaire.
            if (!premiereReussite)
            {
                string[] cachees = GenerateurCandidats.Stats
                    .Where(c => !candidat.StatsConnues.Contains(c)).ToArray();
                if (cachees.Length > 0)
                    candidat.StatsConnues.Add(cachees[Random.Shared.Next(cachees.Length)]);
            }
            // En cas de réussite, rafraîchir directement les statistiques sans popup.
        }
        AfficherCandidat();
    }

    private List<CandidatAuberge> MembresEquipe(CandidatAuberge candidat)
    {
        if (candidat == null) return new List<CandidatAuberge>();
        // Les anciens rendez-vous ne contiennent pas encore d’identifiant d’équipe.
        if (candidat.GroupeId < 0) return new List<CandidatAuberge> { candidat };
        return _aubergeEtat.RendezVous.Candidats
            .Where(c => c.GroupeId == candidat.GroupeId).ToList();
    }

    private void CandidatPrecedent()
    {
        var rdv = _aubergeEtat?.RendezVous;
        if (rdv == null || rdv.Index <= 0) return;
        rdv.Index--;
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
        if (candidat == null || _registreMembres == null || _etat == null) return;

        List<CandidatAuberge> equipe = MembresEquipe(candidat);
        List<CandidatAuberge> nouvellesRecrues = equipe.Where(c => !c.Recrute).ToList();
        if (nouvellesRecrues.Count == 0) return;

        // Un identifiant commun, unique entre rendez-vous et persistant en sauvegarde.
        string identifiantEquipe = equipe.Count > 1
            ? (string.IsNullOrWhiteSpace(equipe[0].Personnage.EquipeId)
                ? Guid.NewGuid().ToString("N") : equipe[0].Personnage.EquipeId)
            : "";
        foreach (CandidatAuberge nouvelle in nouvellesRecrues)
        {
            nouvelle.Personnage.EquipeId = identifiantEquipe;
            nouvelle.Recrute = true;
            _registreMembres.Membres.Add(nouvelle.Personnage);
        }

        int classement = 1;
        foreach (MembreGuilde membre in _registreMembres.Membres
            .Where(m => !m.EstMaitreDeGuilde && m.Rang != "U")
            .OrderByDescending(m => m.NiveauMagique).ThenBy(m => m.NomComplet))
            membre.Classement = classement++;
        foreach (MembreGuilde membre in _registreMembres.Membres
            .Where(m => !m.EstMaitreDeGuilde && m.Rang == "U"))
            membre.Classement = 0;

        _etat.Membres = _registreMembres.Membres.Count;
        _etat.Pouvoir = _registreMembres.Membres.Sum(m => (long)m.NiveauMagique);
        AfficherValeur(_membres, _etat.Membres);
        AfficherValeur(_pouvoir, _etat.Pouvoir);
        AjouterEntreeJournal(nouvellesRecrues.Count == 1
            ? nouvellesRecrues[0].Personnage.NomComplet + " rejoint la guilde."
            : "Une équipe de " + nouvellesRecrues.Count + " membres rejoint la guilde : " +
              string.Join(", ", nouvellesRecrues.Select(c => c.Personnage.NomComplet)) + ".",
            "recrutement");
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
        AfficherInformation(message);
    }
}
