using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Relations réciproques entre personnages, de -100 à 100.</summary>
public static class RelationsMembres
{
    public static int Lire(MembreGuilde a, MembreGuilde b)
    {
        if (a == null || b == null || a.Id == b.Id) return 0;
        if (a.Relations != null && a.Relations.TryGetValue(b.Id, out int valeur)) return Math.Clamp(valeur, -100, 100);
        if (b.Relations != null && b.Relations.TryGetValue(a.Id, out valeur)) return Math.Clamp(valeur, -100, 100);
        return 0;
    }

    public static string Qualifier(int valeur) => Math.Clamp(valeur, -100, 100) switch
    {
        <= -90 => "Ennemi",
        <= -50 => "Hostile",
        <= -10 => "Inamical",
        <= 9 => "Neutre",
        <= 49 => "Connaissance",
        <= 89 => "Ami",
        _ => "Proche"
    };

    public static void Fixer(MembreGuilde a, MembreGuilde b, int valeur)
    {
        if (a == null || b == null || a.Id == b.Id) return;
        a.Relations ??= new Dictionary<string, int>();
        b.Relations ??= new Dictionary<string, int>();
        int relation = Math.Clamp(valeur, -100, 100);
        a.Relations[b.Id] = relation;
        b.Relations[a.Id] = relation;
    }

    public static int Ajuster(MembreGuilde a, MembreGuilde b, int delta)
    {
        int apres = Math.Clamp(Lire(a, b) + delta, -100, 100);
        Fixer(a, b, apres);
        return apres;
    }

    public static void InitialiserEquipe(IReadOnlyList<MembreGuilde> equipe)
    {
        if (equipe == null) return;
        for (int i = 0; i < equipe.Count; i++)
            for (int j = i + 1; j < equipe.Count; j++)
            {
                MembreGuilde a = equipe[i], b = equipe[j];
                bool memeFamille = !string.IsNullOrWhiteSpace(a.Nom) &&
                    string.Equals(a.Nom.Trim(), b.Nom.Trim(), StringComparison.OrdinalIgnoreCase);
                Fixer(a, b, memeFamille ? 100 : Random.Shared.Next(50, 90));
            }
    }

    public static void CompleterEquipesExistantes(IEnumerable<MembreGuilde> membres)
    {
        if (membres == null) return;
        foreach (var groupe in membres.Where(m => m != null &&
            !string.IsNullOrWhiteSpace(m.EquipeId)).GroupBy(m => m.EquipeId))
        {
            var equipe = groupe.ToArray();
            for (int i = 0; i < equipe.Length; i++)
                for (int j = i + 1; j < equipe.Length; j++)
                {
                    MembreGuilde a = equipe[i], b = equipe[j];
                    if (a.Relations?.ContainsKey(b.Id) == true ||
                        b.Relations?.ContainsKey(a.Id) == true)
                    {
                        Fixer(a, b, Lire(a, b));
                        continue;
                    }
                    bool famille = !string.IsNullOrWhiteSpace(a.Nom) &&
                        string.Equals(a.Nom.Trim(), b.Nom.Trim(), StringComparison.OrdinalIgnoreCase);
                    Fixer(a, b, famille ? 100 : Random.Shared.Next(50, 90));
                }
        }
    }
}
