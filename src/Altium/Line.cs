using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Altium
{
    public class Line
    {
        private static readonly Regex LineRegex = new Regex(@"^(?<num>\d+)\.\s(?<text>.*)$");
        private static readonly Random Rnd = new Random();

        public long Number { get; }
        public string Text { get; }

        public Line(long number, string text)
        {
            Number = number;
            Text = text;
        }

        public override string ToString()
        {
            return $"{Number}. {Text}";
        }

        public static Line Create(int numMaxValue = 1024, int textMaxLength = 1024)
        {
            var number = Rnd.Next(0, numMaxValue);
            var textDesiredLength = Rnd.Next(1, textMaxLength);

            var textSb = new StringBuilder();
            var i = 0;

            while (textSb.Length < textDesiredLength) // TODO: check infinite loop
            {
                var word = Guid.NewGuid().ToString("N"); // TODO: use vocabulary

                // 1 - это длина пробела, которым отделяется слово
                if (textSb.Length + word.Length > textMaxLength)
                {
                    // не добавляем слово, если длина строки превысит максимальную
                    break;
                }

                if (i != 0)
                {
                    textSb.Append(" ");
                }

                textSb.Append(word);

                i++;
            }

            return new Line(number, textSb.ToString());
        }

        public static Line Parse(string input)
        {
            var match = LineRegex.Match(input);

            if (!match.Success)
            {
                throw new ArgumentException("Invalid input format.", nameof(input));
            }

            var number = long.Parse(match.Groups["num"].Value);
            var text = match.Groups["text"].Value;

            return new Line(number, text);
        }
    }
}