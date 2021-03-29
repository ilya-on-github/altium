using System;
using System.Text.RegularExpressions;

namespace Altium
{
    public class Line
    {
        private static readonly Regex LineRegex = new Regex(@"^(?<num>\d+)\.\s(?<text>.*)$");

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