using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TrimuiSmartHub.Application.Helpers
{
    internal static class LevenshteinAlgorithm
    {
        public static string FindMostSimilar(string target, List<string> list, int maxDistance = 7)
        {
            if (target.IsNullOrEmpty()) return null;

            List<(string str, int distance)> probableMatches = new List<(string str, int distance)>();
            string processedTarget = PreprocessString(target);

            foreach (var str in list)
            {
                string processedStr = PreprocessString(str);

                if (processedStr.Contains(processedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    probableMatches.Add((str, 0));
                    continue;
                }

                int distance = LevenshteinDistance(processedTarget, processedStr);

                if (distance <= maxDistance)
                {
                    probableMatches.Add((str, distance));
                }
            }

            var mostSimilar = probableMatches
                .OrderBy(x => x.distance)
                .ThenBy(x => x.str.Length)
                .FirstOrDefault();

            return mostSimilar.str;
        }

        private static string PreprocessString(string input)
        {
            string result = Regex.Replace(input, @"[\W_]+", " ").Trim();
            return result.ToLower();
        }

        private static int LevenshteinDistance(string source, string target)
        {
            int[,] matrix = new int[source.Length + 1, target.Length + 1];

            for (int i = 0; i <= source.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= target.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= source.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = CalculateCost(source[i - 1], target[j - 1]);
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source.Length, target.Length];
        }

        private static int CalculateCost(char sourceChar, char targetChar)
        {
            if (sourceChar == targetChar)
                return 0;

            if (char.ToLower(sourceChar) == char.ToLower(targetChar))
                return 1;

            return 2;
        }
    }
}
