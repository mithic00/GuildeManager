using Godot;
using System;
using System.Globalization;
using System.Linq;

/// <summary>
/// Temps en jeu. Un tick de 0,1 s réelle fait avancer 1 minute du jeu.
/// Le timer est attaché à MainMenu et libéré automatiquement avec la scène.
/// Arrêts supplémentaires : appeler InterrompreAvanceeTemps() depuis un événement futur.
/// </summary>
public partial class MainMenu
{
    private const double SecondesReellesParMinuteJeu = 0.1;
    private const int MaxEntreesJournalVisibles = 100;

    private Timer _timerHorloge;
    private HorlogeJeu _horloge;
    private JournalPartie _journalPartie;
    private RichTextLabel _journalAffichage;
    private Button _boutonAvancerTemps;
    private bool _tempsEnCours;

    private void InitialiserTempsEtJournal()
    {
        _boutonAvancerTemps = GetNode<Button>("MainBouton");
        _journalAffichage = GetNode<RichTextLabel>("JournalJoueur/MarginContainer/InfoJournalJoueur");

        _horloge = HorlogeJeu.Charger();
        _journalPartie = JournalPartie.ChargerOuCreer();

        if (_horloge == null)
            GD.PushError("MainMenu - horloge indisponible : " + HorlogeJeu.DerniereErreur);
        if (_journalPartie == null)
            GD.PushError("MainMenu - journal indisponible : " + JournalPartie.DerniereErreur);

        _timerHorloge = new Timer
        {
            Name = "TimerHorlogeJeu",
            WaitTime = SecondesReellesParMinuteJeu,
            OneShot = false,
            Autostart = false,
            ProcessCallback = Timer.TimerProcessCallback.Idle
        };
        AddChild(_timerHorloge);
        _timerHorloge.Timeout += AvancerTempsUneMinute;
        _boutonAvancerTemps.Pressed += BasculerAvanceeTemps;

        if (_journalPartie != null && _journalPartie.Entrees.Count == 0 && _horloge != null)
        {
            if (!_journalPartie.Ajouter(_horloge.Instant, "Horloge de la guilde initialisée.", "systeme"))
                GD.PushWarning("MainMenu : impossible d'enregistrer la première entrée du journal.");
        }

        ActualiserHorlogeEtJournal();
        ActualiserBoutonTemps();
    }

    private void BasculerAvanceeTemps()
    {
        if (_tempsEnCours)
        {
            InterrompreAvanceeTemps("Avancée du temps interrompue par le joueur.");
            return;
        }

        if (_horloge == null || _journalPartie == null || !SaveManager.HasCurrentSave)
        {
            GD.PushWarning("MainMenu : impossible d'avancer le temps sans partie, horloge et journal valides.");
            return;
        }

        _tempsEnCours = true;
        _timerHorloge.Start();
        ActualiserBoutonTemps();
    }

    /// <summary>
    /// Point d'entrée destiné aux futurs événements. L'événement appelant
    /// conserve la maîtrise de son interface; cette méthode arrête seulement l'horloge.
    /// </summary>
    public void InterrompreAvanceeTemps(string raison = "Avancée du temps interrompue.")
    {
        if (!_tempsEnCours)
            return;

        _tempsEnCours = false;
        _timerHorloge.Stop();
        ActualiserBoutonTemps();
        AjouterEntreeJournal(raison, "temps");
    }

    private void AvancerTempsUneMinute()
    {
        if (!_tempsEnCours || _horloge == null)
            return;

        if (!_horloge.AvancerUneMinute())
        {
            InterrompreAvanceeTemps("Avancée du temps arrêtée : sauvegarde impossible.");
            GD.PushError("MainMenu : " + HorlogeJeu.DerniereErreur);
            return;
        }

        _date.Text = HorlogeJeu.Formater(_horloge.Instant);

        // L'arrêt est effectué à la minute exacte du jalon, même à minuit.
        if (_horloge.EstJalon())
        {
            InterrompreAvanceeTemps("Jalon horaire atteint : " + HorlogeJeu.Formater(_horloge.Instant) + ".");
        }
    }

    private void ActualiserBoutonTemps()
    {
        if (_boutonAvancerTemps != null)
        {
            _boutonAvancerTemps.Text = _tempsEnCours ? "Stop" : "Suivant";
            _boutonAvancerTemps.Disabled = _horloge == null || _journalPartie == null;
        }
    }

    private void AjouterEntreeJournal(string description, string type)
    {
        if (_journalPartie == null || _horloge == null)
            return;

        if (!_journalPartie.Ajouter(_horloge.Instant, description, type))
        {
            GD.PushWarning("MainMenu : erreur d'écriture du journal : " + JournalPartie.DerniereErreur);
            return;
        }

        ActualiserJournal();
    }

    private void ActualiserHorlogeEtJournal()
    {
        _date.Text = _horloge == null ? "Date indisponible" : HorlogeJeu.Formater(_horloge.Instant);
        ActualiserJournal();
    }

    private void ActualiserJournal()
    {
        _journalAffichage.Clear();
        if (_journalPartie == null)
        {
            _journalAffichage.AddText("Journal indisponible.");
            return;
        }

        // Les entrées sont du texte brut : les noms d'événements ne sont pas
        // interprétés comme du BBCode dans le RichTextLabel.
        foreach (EntreeJournal entree in _journalPartie.Entrees
                     .Skip(Math.Max(0, _journalPartie.Entrees.Count - MaxEntreesJournalVisibles)))
        {
            string date = entree.DateJeu;
            if (DateTime.TryParseExact(entree.DateJeu, "yyyy-MM-dd'T'HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime instant))
                date = HorlogeJeu.Formater(instant);

            _journalAffichage.AddText("[" + date + "] " + entree.Description + "\n");
        }

        _journalAffichage.ScrollToLine(Math.Max(0, _journalAffichage.GetLineCount() - 1));
    }

    private void ArreterHorlogeSansJournal()
    {
        if (!_tempsEnCours) return;
        _tempsEnCours = false;
        _timerHorloge.Stop();
        ActualiserBoutonTemps();
    }
}
