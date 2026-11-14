using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SirCelShading
{
    // Un rapport envoyé au serveur, dans la forme fixée par la règle des
    // greffons côté joueur du studio (« rapport d'erreurs ») : un objet JSON
    // en UTF-8
    //   { greffon, version, genre, message, pile, premiere, derniere,
    //     nombre, session, lignes }
    // Les noms des champs et les deux genres (« erreur », « plantage ») font
    // partie de cette forme commune : tous les greffons du studio utilisent les
    // mêmes, et le serveur les lit tels quels.
    //
    // Rien sur le joueur n'entre dans un rapport : tout est nettoyé
    // (ReportScrubber) avant d'arriver ici.
    public sealed class ErrorReport
    {
        public const string KindError = "erreur";
        public const string KindCrash = "plantage";

        // Au-delà de cette taille, le rapport est raccourci : les lignes les plus
        // anciennes d'abord, puis la pile. Un rapport reste toujours bien en
        // deçà de ce que portent les messages réseau du jeu.
        public const int MaxBytes = 32000;
        public const int MaxLineLength = 1000;

        public string Plugin = "";
        public string Version = "";
        public string Kind = KindError;
        public string Message = "";
        public string Stack = "";
        public DateTime First;
        public DateTime Last;
        public int Count = 1;
        public DateTime Session;
        public List<string> Lines = new List<string>();

        // ISO 8601, en UTC, à la milliseconde : 2026-10-27T20:31:12.734Z
        public static string IsoUtc(DateTime time)
        {
            var utc = time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : time;
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        public string ToJson()
        {
            var json = new StringBuilder(256);
            json.Append('{');
            Field(json, "greffon", Plugin); json.Append(',');
            Field(json, "version", Version); json.Append(',');
            Field(json, "genre", Kind); json.Append(',');
            Field(json, "message", Message); json.Append(',');
            Field(json, "pile", Stack); json.Append(',');
            Field(json, "premiere", IsoUtc(First)); json.Append(',');
            Field(json, "derniere", IsoUtc(Last)); json.Append(',');
            json.Append("\"nombre\":").Append(Count.ToString(CultureInfo.InvariantCulture)).Append(',');
            Field(json, "session", IsoUtc(Session)); json.Append(',');
            json.Append("\"lignes\":[");
            for (var i = 0; i < Lines.Count; i++)
            {
                if (i > 0)
                    json.Append(',');
                String(json, Lines[i]);
            }
            json.Append("]}");
            return json.ToString();
        }

        // Les octets envoyés au serveur : UTF-8 sans marque d'ordre des octets,
        // jamais au-dessus de MaxBytes.
        public byte[] ToUtf8()
        {
            var encoding = new UTF8Encoding(false);
            var fitted = Copy();
            for (var i = 0; i < fitted.Lines.Count; i++)
                fitted.Lines[i] = Shorten(fitted.Lines[i], MaxLineLength);
            fitted.Message = Shorten(fitted.Message, MaxLineLength);

            var bytes = encoding.GetBytes(fitted.ToJson());
            while (bytes.Length > MaxBytes && fitted.Lines.Count > 0)
            {
                fitted.Lines.RemoveAt(0);
                bytes = encoding.GetBytes(fitted.ToJson());
            }
            if (bytes.Length > MaxBytes)
            {
                fitted.Stack = Shorten(fitted.Stack, MaxBytes / 4);
                bytes = encoding.GetBytes(fitted.ToJson());
            }
            return bytes;
        }

        public ErrorReport Copy()
        {
            var copy = (ErrorReport)MemberwiseClone();
            copy.Lines = new List<string>(Lines);
            return copy;
        }

        private static string Shorten(string text, int length)
        {
            if (text == null || text.Length <= length)
                return text ?? "";
            return text.Substring(0, length) + "...";
        }

        private static void Field(StringBuilder json, string name, string value)
        {
            json.Append('"').Append(name).Append("\":");
            String(json, value);
        }

        private static void String(StringBuilder json, string value)
        {
            json.Append('"');
            foreach (var c in value ?? "")
            {
                switch (c)
                {
                    case '"': json.Append("\\\""); break;
                    case '\\': json.Append("\\\\"); break;
                    case '\n': json.Append("\\n"); break;
                    case '\r': json.Append("\\r"); break;
                    case '\t': json.Append("\\t"); break;
                    case '\b': json.Append("\\b"); break;
                    case '\f': json.Append("\\f"); break;
                    default:
                        if (c < ' ' || c == (char)0x2028 || c == (char)0x2029)
                            json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            json.Append(c);
                        break;
                }
            }
            json.Append('"');
        }
    }
}
