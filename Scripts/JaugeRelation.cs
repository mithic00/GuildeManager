using Godot;
using System;

/// <summary>
/// Jauge de réputation bidirectionnelle : le neutre se trouve au centre.
/// Une valeur négative remplit la moitié gauche, une valeur positive la droite.
/// À placer en enfant d'un Control de la scène, sans modifier le .tscn.
/// </summary>
public partial class JaugeRelation : Control
{
    private int _relation;

    public int Relation
    {
        get => _relation;
        set
        {
            int valeur = Math.Clamp(value, -100, 100);
            if (_relation == valeur) return;
            _relation = valeur;
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float largeur = Size.X;
        float hauteur = Size.Y;
        if (largeur <= 0 || hauteur <= 0) return;

        float centre = largeur / 2f;
        Color fond = new("#33373C");
        Color gauche = new("#583A3B");
        Color droite = new("#365142");
        Color mauvaise = new("#CB5A56");
        Color bonne = new("#55AE79");
        Color repere = new("#EBD09A");

        DrawRect(new Rect2(0, 0, largeur, hauteur), fond);
        DrawRect(new Rect2(0, 0, centre, hauteur), gauche);
        DrawRect(new Rect2(centre, 0, centre, hauteur), droite);

        float taille = Math.Abs(_relation) / 100f * centre;
        if (_relation < 0)
            DrawRect(new Rect2(centre - taille, 0, taille, hauteur), mauvaise);
        else if (_relation > 0)
            DrawRect(new Rect2(centre, 0, taille, hauteur), bonne);

        // Ligne centrale permanente et bordure, même lorsque la valeur vaut 0.
        DrawLine(new Vector2(centre, 0), new Vector2(centre, hauteur), repere, 3f);
        DrawRect(new Rect2(0, 0, largeur, hauteur),
            new Color("#A99E87"), false, 1f);
    }
}
