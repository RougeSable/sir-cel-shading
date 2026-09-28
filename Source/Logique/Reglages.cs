using System;

namespace SirCelShading
{
    // Réglages du joueur, enregistrés dans son dossier Storage\sir-cel-shading.
    // Aucune dépendance au jeu : la classe est testée telle quelle.
    public class Reglages
    {
        public const int TeintesMin = 3;
        public const int TeintesMax = 8;
        public const int TeintesParDefaut = 4;

        public const int EpaisseurMin = 1;
        public const int EpaisseurMax = 3;
        public const int EpaisseurParDefaut = 1;

        public const int ForceMin = 0;
        public const int ForceMax = 100;
        public const int ForceParDefaut = 100;

        public const int SensibiliteMin = 1;
        public const int SensibiliteMax = 5;
        public const int SensibiliteParDefaut = 3;

        public const int VivaciteMin = 100;
        public const int VivaciteMax = 150;
        public const int VivaciteParDefaut = 115;

        // Le premier réglage : le rendu bande dessinée, actif dès l'installation.
        public bool Active { get; set; } = true;

        // Nombre de teintes par couleur : moins il y en a, plus les aplats sont larges.
        public int Teintes { get; set; } = TeintesParDefaut;

        // Épaisseur des contours, en pixels de chaque côté de l'arête.
        public int Epaisseur { get; set; } = EpaisseurParDefaut;

        // Noirceur des contours, en pour cent.
        public int Force { get; set; } = ForceParDefaut;

        // Sensibilité aux arêtes : 1 ne trace que les silhouettes franches,
        // 5 trace aussi les plis doux.
        public int Sensibilite { get; set; } = SensibiliteParDefaut;

        // Saturation des aplats, en pour cent (100 : celle du jeu).
        public int Vivacite { get; set; } = VivaciteParDefaut;

        public Reglages Copie()
        {
            return new Reglages
            {
                Active = Active,
                Teintes = Teintes,
                Epaisseur = Epaisseur,
                Force = Force,
                Sensibilite = Sensibilite,
                Vivacite = Vivacite,
            };
        }

        // Ramène chaque valeur dans ses bornes : un fichier retouché à la main
        // ne peut pas produire une variante de shader absurde.
        public Reglages Normalises()
        {
            var r = Copie();
            r.Teintes = Borner(Teintes, TeintesMin, TeintesMax);
            r.Epaisseur = Borner(Epaisseur, EpaisseurMin, EpaisseurMax);
            r.Force = Borner(Force, ForceMin, ForceMax);
            r.Sensibilite = Borner(Sensibilite, SensibiliteMin, SensibiliteMax);
            r.Vivacite = Borner(Vivacite, VivaciteMin, VivaciteMax);
            return r;
        }

        public static int Borner(int valeur, int min, int max)
        {
            return Math.Max(min, Math.Min(max, valeur));
        }
    }
}
