using System.Text;
using System.Text.RegularExpressions;

namespace Mis6
{
    public class Squasher
    {
        public void SquashFile(string annotatedFileName, string squashedFileName)
        {
            var annotated = File.ReadAllText(annotatedFileName);
            var squashed = Squash(annotated);
            File.WriteAllText(squashedFileName, squashed);
        }

        private string Squash(string annotated)
        {
            annotated = Regex.Replace(annotated, @"\/\*.*?\*\/", "", RegexOptions.Singleline); // Replace all multi line comments
            annotated = Regex.Replace(annotated, @"\/\/.*$", "", RegexOptions.Multiline); // Replace all single line comments
            annotated = Regex.Replace(annotated, @"\t", ""); // Remove tabs and spaces
            var lines = annotated.Split('\n');
            var output = new StringBuilder();
            Dictionary<string, int>? labelDefinitions = new Dictionary<string, int>();
            var labelOccurences = new Dictionary<string, int>();
            foreach (var lineUntrimmed in lines)
            {
                string? line = lineUntrimmed.Trim();
                if (line.Length == 0)
                {
                    continue; 
                }

                // Capture label definitions
                if (line[0] == '~')
                {
                    var label = line.Substring(1);
                    if (labelDefinitions.ContainsKey(label))
                    {
                        throw new Exception($"Label {label} defined more than once.");
                    }
                    labelDefinitions.Add(label, output.Length);
                    continue;
                }

                // Copy all characters, ignoring spaces, and processing labels
                for (int i = 0; i < line.Length; i++)
                {
                    var c = line[i];
                    if (c != ' ')
                    {
                        // Capture label occurences
                        if (c == '~')
                        {
                            var nextSpace = line.IndexOf(' ', i);
                            if (nextSpace == -1)
                            {
                                nextSpace = line.Length;
                            }
                            var label = line.Substring(i + 1, nextSpace - i - 1);
                            labelOccurences.Add(label, output.Length);
                            output.Append("\"xx");
                            i = nextSpace;
                        }
                        else
                        {
                            if (!System.Charmap.Contains(c))
                            {
                                throw new Exception("Invalid character found: " + c);
                            }

                            output.Append(c);
                        }
                    }
                }
            }

            // Replace all label occurences with absolute addresses of labels
            foreach (var labelOccurence in labelOccurences)
            {
                if (!labelDefinitions.ContainsKey(labelOccurence.Key))
                {
                    throw new Exception($"Label definition for label {labelOccurence.Key} not found.");
                }

                var labelAddress = labelDefinitions[labelOccurence.Key];
                var address = "\"" + FormatAddress(labelAddress);
                for (int i = 0; i < address.Length; i++)
                {
                    output[labelOccurence.Value + i] = address[i];
                }
            }

            return output.ToString();
        }

        private string FormatAddress(int labelAddress)
        {
            var hi = System.ToChar(System.GetHigh((uint)labelAddress));
            var lo = System.ToChar(System.GetLow((uint)labelAddress));
            return hi.ToString() + lo.ToString();
        }
    }
}