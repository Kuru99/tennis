using System;
using System.Text;

namespace PrideCourt.Networking
{
    public static class EosRoomCode
    {
        public const int Length = 6;
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789OI";

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            StringBuilder builder = new StringBuilder(Length);
            foreach (char raw in value.ToUpperInvariant())
            {
                char character = raw switch
                {
                    '0' => 'O',
                    '1' => 'I',
                    _ => raw
                };
                if (Alphabet.IndexOf(character) < 0) continue;
                builder.Append(character);
                if (builder.Length == Length) break;
            }
            return builder.ToString();
        }

        public static bool IsValid(string value) => Normalize(value).Length == Length;

        public static string Create(Random random = null)
        {
            random ??= new Random();
            char[] code = new char[Length];
            for (int i = 0; i < code.Length; i++) code[i] = Alphabet[random.Next(Alphabet.Length)];
            return new string(code);
        }
    }
}
