using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using VRageRender;

namespace SirCelShading
{
    // Le préfixe, le postfixe et le finaliseur posés sur MyToneMapping.Run,
    // le passage des couleurs finales du jeu (méthode éprouvée sur #208).
    //
    // Activé : le préfixe met notre variante dans le champ statique de la
    // variante en cours et lie la profondeur de la scène en t31 ; le postfixe
    // rend au champ le shader du jeu et délie t31. Coupé, ou arrêté : rien
    // n'est touché, le jeu dessine avec ses propres shaders.
    //
    // Tout ce qui suit s'exécute sur le fil de rendu, et lui seul.
    internal static class PassageCouleursFinales
    {
        // Posés une fois par le greffon, avant la pose du patch.
        public static MoteurDeRendu Moteur;
        public static ArretDeSession Arret;
        public static string CheminDuShader;

        // Instantané des réglages, remplacé en bloc par le fil principal.
        private static volatile Reglages s_reglages = new Reglages();

        public static Reglages ReglagesCourants
        {
            get { return s_reglages; }
            set { s_reglages = (value ?? new Reglages()).Normalises(); }
        }

        // Variantes déjà créées par le jeu, par signature de réglages.
        private static readonly Dictionary<string, object[]> s_variantes = new Dictionary<string, object[]>();

        // Ce que le préfixe a changé, pour que le postfixe le rende.
        private static FieldInfo s_champRemplace;
        private static object s_shaderDuJeu;
        private static object s_etageLie;

        private static readonly object[] s_delier = { SourceDuShader.EmplacementProfondeur, null };
        private static readonly object[] s_lier = { SourceDuShader.EmplacementProfondeur, null };

        public static void Prefix(object[] __args)
        {
            try
            {
                if (Arret == null || Arret.EstArrete || Moteur == null)
                    return;

                var reglages = s_reglages;
                if (!reglages.Active)
                    return;

                var ids = Variantes(reglages);
                if (ids == null)
                    return;

                var etage = Moteur.EtageDeCalcul();
                var profondeur = Moteur.Profondeur();
                if (etage == null || profondeur == null)
                    return; // GBuffer pas encore prêt : cette image reste celle du jeu

                var variante = VariantesDuShader.Choisir(
                    (bool)__args[Moteur.IndexActiverTonemapping],
                    (bool)__args[Moteur.IndexLuminanceAlpha]);
                var champ = Moteur.Champs[(int)variante];

                s_shaderDuJeu = champ.GetValue(null);
                champ.SetValue(null, ids[(int)variante]);
                s_champRemplace = champ;

                s_lier[1] = profondeur;
                Moteur.LierRessource.Invoke(etage, s_lier);
                s_lier[1] = null;
                s_etageLie = etage;
            }
            catch (Exception e)
            {
                Rendre();
                Arret.Arreter(
                    "anomalie sur le fil de rendu avant les couleurs finales : " + e,
                    Textes.ArretAnomalie);
            }
        }

        public static void Postfix()
        {
            try
            {
                Rendre();
            }
            catch (Exception e)
            {
                Arret.Arreter(
                    "anomalie sur le fil de rendu après les couleurs finales : " + e,
                    Textes.ArretAnomalie);
            }
        }

        // Le passage du jeu a levé une exception : le postfixe n'a pas tourné.
        // On rend quand même au jeu ses shaders, et l'exception suit son cours.
        public static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && s_champRemplace != null)
            {
                try { Rendre(); }
                catch (Exception) { }
                if (Arret != null)
                    Arret.Arreter(
                        "le passage des couleurs finales a échoué avec notre variante : " + __exception,
                        Textes.ArretAnomalie);
            }
            return __exception;
        }

        private static void Rendre()
        {
            if (s_champRemplace != null)
            {
                var champ = s_champRemplace;
                s_champRemplace = null;
                champ.SetValue(null, s_shaderDuJeu);
                s_shaderDuJeu = null;
            }

            if (s_etageLie != null)
            {
                var etage = s_etageLie;
                s_etageLie = null;
                Moteur.LierRessource.Invoke(etage, s_delier);
            }
        }

        // Les trois variantes pour ces réglages, compilées par le jeu à la
        // première demande. Null si l'une est refusée : l'effet est alors arrêté
        // pour la session, jamais servi à moitié.
        private static object[] Variantes(Reglages reglages)
        {
            var signature = VariantesDuShader.Signature(reglages);
            object[] ids;
            if (s_variantes.TryGetValue(signature, out ids))
                return ids;

            ids = new object[VariantesDuShader.NombreDeVariantes];
            foreach (Variante variante in Enum.GetValues(typeof(Variante)))
            {
                var macros = MoteurDeRendu.VersSharpDX(VariantesDuShader.Macros(variante, reglages));
                var description = variante + " (" + string.Join(" ", VariantesDuShader.Macros(variante, reglages)) + ")";

                // D'abord le compilateur du jeu, qui refuse sans planter : null,
                // ou une exception du compilateur HLSL.
                byte[] code;
                try
                {
                    code = (byte[])Moteur.Compiler.Invoke(null, new object[]
                    {
                        CheminDuShader, macros, MyShaderProfile.cs_5_0, CheminDuShader, false,
                    });
                }
                catch (TargetInvocationException e)
                {
                    Arret.Arreter(
                        "variante " + description + " refusée par le compilateur du jeu : " + e.InnerException,
                        Textes.ArretVarianteRefusee);
                    return null;
                }

                if (code == null || code.Length == 0)
                {
                    Arret.Arreter(
                        "variante " + description + " refusée par le compilateur du jeu (détail dans le journal du rendu)",
                        Textes.ArretVarianteRefusee);
                    return null;
                }

                // Puis la création par le jeu, qui la retrouve dans son cache.
                try
                {
                    ids[(int)variante] = Moteur.Creer.Invoke(null, new object[] { CheminDuShader, macros });
                }
                catch (TargetInvocationException e)
                {
                    Arret.Arreter(
                        "variante " + description + " refusée à la création : " + e.InnerException,
                        Textes.ArretVarianteRefusee);
                    return null;
                }
            }

            s_variantes[signature] = ids;
            return ids;
        }

        // Vérifié avant la pose du patch : un en-tête absent ferait échouer la
        // compilation dans le préprocesseur du jeu, qui s'arrête alors sur
        // un point d'arrêt de débogage.
        public static string EnTeteManquant(string dossierDesShaders)
        {
            foreach (var enTete in SourceDuShader.EnTetesDuJeu)
            {
                if (!File.Exists(Path.Combine(dossierDesShaders, enTete)))
                    return enTete;
            }
            return null;
        }
    }
}
