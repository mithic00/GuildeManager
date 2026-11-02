using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Effets de traits par catégorie de mission.
/// Les membres favorables comptent chacun une fois ; pour les défauts, seul le
/// membre cumulant le plus grand nombre de traits négatifs est retenu.
/// </summary>
public static class BonusTraitsMissions
{
    public sealed class Bilan
    {
        public int MembresFavorables { get; init; }
        public int PlusGrandNombreNegatifs { get; init; }
        public MembreGuilde MembreLePlusDefavorable { get; init; }
        /// <summary>Valeur négative ou nulle, appliquée au meilleur bonus de Volonté.</summary>
        public int MalusVolonte { get; init; }
        public bool Avantage { get; init; }
    }

    public static Bilan Evaluer(IReadOnlyList<MembreGuilde> equipe, string categorie)
    {
        if (equipe == null || equipe.Count == 0)
            return new Bilan();

        int favorables = 0;
        int maximumNegatifs = 0;
        MembreGuilde plusDefavorable = null;

        foreach (MembreGuilde membre in equipe)
        {
            // false : les traits réels sont utilisés même si le joueur
            // ne connaît pas encore les caractéristiques de ce membre.
            var traits = ReglesTraitsPersonnage.Evaluer(membre, uniquementConnus: false);
            int positifs = ReglesTraitsPersonnage.AssociationsMissions.Count(a =>
                a.Positif && a.Categorie == categorie &&
                traits.Any(t => t.Nom == a.Trait));
            int negatifs = ReglesTraitsPersonnage.AssociationsMissions.Count(a =>
                !a.Positif && a.Categorie == categorie &&
                traits.Any(t => t.Nom == a.Trait));
            if (positifs > 0)
                favorables++;
            if (negatifs > maximumNegatifs)
            {
                maximumNegatifs = negatifs;
                plusDefavorable = membre;
            }
        }

        int solde = favorables - maximumNegatifs;
        return new Bilan
        {
            MembresFavorables = favorables,
            PlusGrandNombreNegatifs = maximumNegatifs,
            MembreLePlusDefavorable = plusDefavorable,
            MalusVolonte = solde < 0 ? 2 * solde : 0,
            Avantage = solde > 0
        };
    }
}
