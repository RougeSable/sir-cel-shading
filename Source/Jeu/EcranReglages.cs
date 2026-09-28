using System;
using System.Text;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace SirCelShading
{
    // Réglages du greffon, ouverts depuis Plugin Loader. Chaque changement
    // s'applique à l'image suivante et s'enregistre aussitôt : le joueur voit
    // l'effet derrière l'écran, sans relancer le jeu.
    internal sealed class EcranReglages : MyGuiScreenBase
    {
        private const float Largeur = 0.62f;
        private const float Hauteur = 0.66f;
        private const float ColonneLibelle = -0.27f;
        private const float ColonneControle = 0.10f;
        private const float ColonneValeur = 0.27f;
        private const float Interligne = 0.07f;

        // Un curseur qu'on fait glisser change de valeur à chaque cran, et chaque
        // valeur nouvelle demande au jeu de compiler l'effet. On attend que le
        // joueur s'arrête un instant avant d'appliquer.
        private static readonly TimeSpan DelaiDesCurseurs = TimeSpan.FromMilliseconds(400);

        private readonly SirCelShadingPlugin m_greffon;
        private Reglages m_reglages;
        private DateTime? m_aAppliquer;

        public EcranReglages(SirCelShadingPlugin greffon)
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(Largeur, Hauteur), false, null, 0.9f, 0.9f)
        {
            m_greffon = greffon;
            m_reglages = greffon.Reglages;
            EnabledBackgroundFade = true;
            CloseButtonEnabled = true;
            RecreateControls(true);
        }

        public override string GetFriendlyName()
        {
            return "SirCelShadingEcranReglages";
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            AddCaption(Textes.TitreEcran);

            var y = -Hauteur / 2 + 0.13f;

            // Premier réglage, toujours : la case qui active ou coupe tout.
            AjouterLibelle(Textes.CaseActiver, Textes.CaseActiverAide, y);
            var caseActiver = new MyGuiControlCheckbox(new Vector2(ColonneControle, y), null, Textes.CaseActiverAide, m_reglages.Active);
            caseActiver.IsCheckedChanged = c =>
            {
                m_reglages.Active = c.IsChecked;
                Appliquer();
            };
            Controls.Add(caseActiver);
            y += Interligne;

            AjouterCurseur(Textes.Teintes, Textes.TeintesAide, y, Reglages.TeintesMin, Reglages.TeintesMax,
                m_reglages.Teintes, v => m_reglages.Teintes = v, "");
            y += Interligne;
            AjouterCurseur(Textes.Epaisseur, Textes.EpaisseurAide, y, Reglages.EpaisseurMin, Reglages.EpaisseurMax,
                m_reglages.Epaisseur, v => m_reglages.Epaisseur = v, " px");
            y += Interligne;
            AjouterCurseur(Textes.Force, Textes.ForceAide, y, Reglages.ForceMin, Reglages.ForceMax,
                m_reglages.Force, v => m_reglages.Force = v, " %");
            y += Interligne;
            AjouterCurseur(Textes.Sensibilite, Textes.SensibiliteAide, y, Reglages.SensibiliteMin, Reglages.SensibiliteMax,
                m_reglages.Sensibilite, v => m_reglages.Sensibilite = v, "");
            y += Interligne;
            AjouterCurseur(Textes.Vivacite, Textes.VivaciteAide, y, Reglages.VivaciteMin, Reglages.VivaciteMax,
                m_reglages.Vivacite, v => m_reglages.Vivacite = v, " %");
            y += Interligne;

            if (m_greffon.Arret != null && m_greffon.Arret.EstArrete)
            {
                Controls.Add(new MyGuiControlLabel(new Vector2(ColonneLibelle, y), null, Textes.EcranArrete, null, 0.8f, "Red"));
            }

            var yBoutons = Hauteur / 2 - 0.07f;
            Controls.Add(new MyGuiControlButton(new Vector2(-0.12f, yBoutons), text: new StringBuilder(Textes.BoutonParDefaut),
                onButtonClick: b =>
                {
                    var active = m_reglages.Active;
                    m_reglages = new Reglages { Active = active };
                    Appliquer();
                    RecreateControls(false);
                }));
            Controls.Add(new MyGuiControlButton(new Vector2(0.12f, yBoutons), text: new StringBuilder(Textes.BoutonFermer),
                onButtonClick: b => CloseScreen()));
        }

        private void AjouterLibelle(string texte, string aide, float y)
        {
            var libelle = new MyGuiControlLabel(new Vector2(ColonneLibelle, y), null, texte);
            libelle.SetToolTip(aide);
            Controls.Add(libelle);
        }

        private void AjouterCurseur(string texte, string aide, float y, int min, int max, int valeur,
            Action<int> affecter, string unite)
        {
            AjouterLibelle(texte, aide, y);

            var affichage = new MyGuiControlLabel(new Vector2(ColonneValeur, y), null, valeur + unite,
                null, 0.8f, "White", MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER);

            var curseur = new MyGuiControlSlider(new Vector2(ColonneControle, y), min, max, 0.2f, valeur,
                toolTip: aide, intValue: true);
            curseur.Value = valeur;
            curseur.ValueChanged = c =>
            {
                var v = (int)Math.Round(c.Value);
                affichage.Text = v + unite;
                affecter(v);
                m_aAppliquer = DateTime.UtcNow + DelaiDesCurseurs;
            };

            Controls.Add(curseur);
            Controls.Add(affichage);
        }

        public override bool Update(bool hasFocus)
        {
            if (m_aAppliquer.HasValue && DateTime.UtcNow >= m_aAppliquer.Value)
                Appliquer();
            return base.Update(hasFocus);
        }

        public override bool CloseScreen(bool isUnloading = false)
        {
            if (m_aAppliquer.HasValue)
                Appliquer();
            return base.CloseScreen(isUnloading);
        }

        private void Appliquer()
        {
            m_aAppliquer = null;
            m_greffon.Appliquer(m_reglages.Copie());
        }
    }
}
