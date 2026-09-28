namespace SirCelShading
{
    // Tout ce que le joueur lit, au même endroit.
    public static class Textes
    {
        public const string NomAffiche = "Sir Cel Shading";

        public const string Active = "Sir Cel Shading : rendu bande dessinée activé.";
        public const string Coupe = "Sir Cel Shading : rendu bande dessinée coupé, retour au rendu du jeu.";
        public const string AideCommande = "Sir Cel Shading : /cel bascule le rendu, /cel activer, /cel couper, /cel etat.";

        public const string ArretPrefixe = "Sir Cel Shading est arrêté pour cette session : ";
        public const string ArretMoteurInattendu = ArretPrefixe + "le moteur de rendu du jeu a changé. Le rendu du jeu est conservé.";
        public const string ArretEnTeteManquant = ArretPrefixe + "un effet d'image du jeu est introuvable. Le rendu du jeu est conservé.";
        public const string ArretFichierShader = ArretPrefixe + "impossible d'écrire son effet d'image dans le dossier du joueur.";
        public const string ArretVarianteRefusee = ArretPrefixe + "le jeu a refusé de compiler son effet d'image. Le rendu du jeu est conservé.";
        public const string ArretAnomalie = ArretPrefixe + "anomalie pendant le rendu. Le rendu du jeu est conservé.";
        public const string ArretPatch = ArretPrefixe + "impossible de se brancher sur les couleurs finales du jeu.";
        public const string ArretCohabitation = ArretPrefixe + "un autre greffon utilise déjà les couleurs finales du jeu ({0}). Sir Cel Shading lui cède la place.";

        public static string Etat(bool actif, string raisonDArret)
        {
            if (raisonDArret != null)
                return raisonDArret;
            return actif ? Active : Coupe;
        }

        // Écran de réglages, ouvert depuis Plugin Loader.
        public const string TitreEcran = "Sir Cel Shading";
        public const string CaseActiver = "Activer le greffon";
        public const string CaseActiverAide = "Rendu bande dessinée : contours noirs et couleurs en aplats. Décoché, le jeu retrouve exactement son rendu.";
        public const string Teintes = "Teintes par couleur";
        public const string TeintesAide = "Moins de teintes, des aplats plus larges.";
        public const string Epaisseur = "Épaisseur des contours";
        public const string EpaisseurAide = "En pixels, de chaque côté de l'arête.";
        public const string Force = "Noirceur des contours";
        public const string ForceAide = "0 % : aucun contour ; 100 % : contours noirs.";
        public const string Sensibilite = "Sensibilité aux arêtes";
        public const string SensibiliteAide = "1 : silhouettes seulement ; 5 : aussi les plis doux.";
        public const string Vivacite = "Vivacité des couleurs";
        public const string VivaciteAide = "100 % : la saturation du jeu.";
        public const string BoutonParDefaut = "Valeurs par défaut";
        public const string BoutonFermer = "Fermer";
        public const string EcranArrete = "Arrêté pour cette session, voir le journal du jeu.";
    }
}
