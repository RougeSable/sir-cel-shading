namespace SirCelShading
{
    // Tout ce que le joueur lit, au même endroit.
    public static class Texts
    {
        public const string DisplayName = "Sir Cel Shading";

        public const string On = "Sir Cel Shading : effet activé.";
        public const string Off = "Sir Cel Shading : effet coupé, retour au rendu propre du jeu.";
        public const string CommandHelp = "Sir Cel Shading : /cel active ou coupe l'effet ; /cel activer, /cel couper, /cel etat, /cel aide.";
        public const string CommandUnknown = "Sir Cel Shading : option inconnue pour /cel. Options : /cel (bascule l'effet), /cel activer, /cel couper, /cel etat, /cel aide.";

        public const string StopPrefix = "Sir Cel Shading est arrêté pour cette session : ";
        public const string StopUnexpectedEngine = StopPrefix + "le moteur de rendu du jeu a changé. Le rendu du jeu est conservé.";
        public const string StopMissingHeader = StopPrefix + "un shader du jeu est introuvable. Le rendu du jeu est conservé.";
        public const string StopShaderFile = StopPrefix + "son shader n'a pas pu être écrit dans le dossier du joueur.";
        public const string StopVariantRefused = StopPrefix + "le jeu a refusé de compiler son shader. Le rendu du jeu est conservé.";
        public const string StopFault = StopPrefix + "une erreur est survenue pendant le rendu. Le rendu du jeu est conservé.";
        public const string StopPatch = StopPrefix + "il n'a pas pu s'accrocher aux couleurs finales du jeu.";
        public const string StopCoexistence = StopPrefix + "un autre greffon utilise déjà les couleurs finales du jeu ({0}). Sir Cel Shading s'efface.";

        // La réponse à /cel etat, et le message après chaque changement : il
        // dit toujours si l'effet est activé.
        public static string Status(bool enabled, string stopReason)
        {
            if (stopReason != null)
                return stopReason;
            return enabled ? On : Off;
        }

        // L'écran de réglages, ouvert depuis Pulsar.
        public const string ScreenTitle = "Sir Cel Shading";
        public const string EnableCheckbox = "Activer le greffon";
        public const string EnableCheckboxHelp = "Rendu cel shading façon film d'animation de la vue du jeu. Décoché, le jeu retrouve exactement son propre rendu.";

        public const string ShadeTones = "Tons d'ombre";
        public const string ShadeTonesHelp = "Nombre de tons de lumière doux sur une surface, ombre comprise.";
        public const string OutlineStrength = "Force des contours";
        public const string OutlineStrengthHelp = "Les contours prennent une teinte plus sombre de l'objet. 0 % : aucun contour.";
        public const string RimLight = "Lumière de contour";
        public const string RimLightHelp = "Liseré de lumière sur les silhouettes vues à contre-jour du soleil.";
        public const string Haze = "Brume de distance";
        public const string HazeHelp = "À quel point les choses lointaines se fondent dans la couleur du ciel. Sans effet sous un ciel noir.";

        public const string DefaultsButton = "Valeurs par défaut";
        public const string DefaultsButtonHelp = "Remet les réglages du rendu à leurs valeurs par défaut.";
        public const string CloseButton = "Fermer";
        public const string ScreenStopped = "Arrêté pour cette session, voir le journal du jeu.";
    }
}
