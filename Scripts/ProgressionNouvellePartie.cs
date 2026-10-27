using Godot;

/// <summary>
/// Progression persistante du profil joueur, indépendante des sauvegardes de partie.
/// Les futures récompenses de progression pourront appeler DebloquerExperience.
/// </summary>
public static class ProgressionNouvellePartie
{
    private const string Chemin = "user://progression_profil.cfg";
    private const string Section = "nouvelle_partie";
    private const string Cle = "experience_maximum_debloquee";
    private const int DernierChoixExperience = 3;

    public static int ExperienceMaximumDebloquee
    {
        get
        {
            var configuration = new ConfigFile();
            if (configuration.Load(Chemin) != Error.Ok)
                return 0;

            return Mathf.Clamp((int)configuration.GetValue(Section, Cle, 0), 0, DernierChoixExperience);
        }
    }

    /// <summary>
    /// Débloque l'option indiquée et toutes les options précédentes.
    /// Indices : 0 = Sans expérience, 1 = Ancien membre de petite guilde,
    /// 2 = Ancien rang S, 3 = Ancien rang P.
    /// Ne décide pas à quel moment une récompense de jeu est obtenue.
    /// </summary>
    public static bool DebloquerExperience(int indice)
    {
        if (indice < 0 || indice > DernierChoixExperience)
            return false;

        int niveau = ExperienceMaximumDebloquee;
        if (indice <= niveau)
            return true;

        var configuration = new ConfigFile();
        configuration.Load(Chemin);
        configuration.SetValue(Section, Cle, indice);
        Error erreur = configuration.Save(Chemin);
        if (erreur != Error.Ok)
        {
            GD.PushWarning("ProgressionNouvellePartie : sauvegarde du déblocage impossible (" + erreur + ").");
            return false;
        }

        return true;
    }
}
