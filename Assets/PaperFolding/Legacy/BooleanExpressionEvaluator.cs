using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PaperFolding
{
    /// <summary>
    /// Evaluates boolean expressions with tag names
    /// Supports: AND, OR, NOT, parentheses
    /// Example: "(tag1 OR tag2) AND NOT tag3"
    /// </summary>
    public static class BooleanExpressionEvaluator
    {
        public static bool Evaluate(string expression, HashSet<string> availableTags)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true; // Empty expression = match all

            expression = expression.Trim();
            return EvaluateExpression(expression, availableTags);
        }

        private static bool EvaluateExpression(string expr, HashSet<string> tags)
        {
            expr = expr.Trim();

            // Handle parentheses first
            while (expr.Contains("("))
            {
                int depth = 0;
                int start = -1;
                int end = -1;

                for (int i = 0; i < expr.Length; i++)
                {
                    if (expr[i] == '(')
                    {
                        if (depth == 0) start = i;
                        depth++;
                    }
                    else if (expr[i] == ')')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            end = i;
                            break;
                        }
                    }
                }

                if (start >= 0 && end > start)
                {
                    string inner = expr.Substring(start + 1, end - start - 1);
                    bool result = EvaluateExpression(inner, tags);
                    expr = expr.Substring(0, start) + (result ? "TRUE" : "FALSE") + expr.Substring(end + 1);
                }
                else
                {
                    break; // Malformed parentheses
                }
            }

            // Handle OR (lowest precedence)
            var orParts = SplitByOperator(expr, "OR");
            if (orParts.Count > 1)
            {
                return orParts.Any(part => EvaluateExpression(part, tags));
            }

            // Handle AND
            var andParts = SplitByOperator(expr, "AND");
            if (andParts.Count > 1)
            {
                return andParts.All(part => EvaluateExpression(part, tags));
            }

            // Handle NOT
            if (expr.StartsWith("NOT "))
            {
                string rest = expr.Substring(4).Trim();
                return !EvaluateExpression(rest, tags);
            }

            // Handle boolean literals
            if (expr == "TRUE") return true;
            if (expr == "FALSE") return false;

            // Treat as tag name
            string tagName = expr.Trim();
            return tags != null && tags.Contains(tagName);
        }

        private static List<string> SplitByOperator(string expr, string op)
        {
            List<string> parts = new List<string>();
            int depth = 0;
            int lastSplit = 0;

            for (int i = 0; i <= expr.Length - op.Length; i++)
            {
                if (expr[i] == '(') depth++;
                else if (expr[i] == ')') depth--;

                if (depth == 0)
                {
                    // Check if we're at the operator
                    if (i + op.Length <= expr.Length)
                    {
                        string segment = expr.Substring(i, op.Length);
                        if (segment == op)
                        {
                            // Check boundaries (word boundaries)
                            bool validBefore = (i == 0 || !char.IsLetterOrDigit(expr[i - 1]));
                            bool validAfter = (i + op.Length >= expr.Length || !char.IsLetterOrDigit(expr[i + op.Length]));

                            if (validBefore && validAfter)
                            {
                                parts.Add(expr.Substring(lastSplit, i - lastSplit));
                                lastSplit = i + op.Length;
                                i += op.Length - 1; // Skip ahead
                            }
                        }
                    }
                }
            }

            if (lastSplit == 0)
            {
                return new List<string> { expr };
            }

            parts.Add(expr.Substring(lastSplit));
            return parts;
        }

        /// <summary>
        /// Validate expression syntax and return error message if invalid
        /// </summary>
        public static (bool isValid, string errorMessage) ValidateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return (true, "");

            expression = expression.Trim();

            // Check for balanced parentheses
            int depth = 0;
            foreach (char c in expression)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                if (depth < 0) return (false, "Unbalanced parentheses");
            }
            if (depth != 0) return (false, "Unbalanced parentheses");

            // Check for invalid characters
            if (Regex.IsMatch(expression, @"[^a-zA-Z0-9_\s\(\)&|!]"))
            {
                // Allow AND, OR, NOT as words
                string cleaned = expression;
                cleaned = Regex.Replace(cleaned, @"\bAND\b", "");
                cleaned = Regex.Replace(cleaned, @"\bOR\b", "");
                cleaned = Regex.Replace(cleaned, @"\bNOT\b", "");
                if (Regex.IsMatch(cleaned, @"[^a-zA-Z0-9_\s\(\)]"))
                {
                    return (false, "Invalid characters in expression");
                }
            }

            return (true, "");
        }

        /// <summary>
        /// Extract all tag names referenced in the expression
        /// </summary>
        public static HashSet<string> ExtractTagNames(string expression)
        {
            HashSet<string> tags = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(expression))
                return tags;

            // Remove operators and parentheses
            string cleaned = expression;
            cleaned = Regex.Replace(cleaned, @"\bAND\b", " ");
            cleaned = Regex.Replace(cleaned, @"\bOR\b", " ");
            cleaned = Regex.Replace(cleaned, @"\bNOT\b", " ");
            cleaned = Regex.Replace(cleaned, @"[()]", " ");

            // Extract remaining words as tag names
            var matches = Regex.Matches(cleaned, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");
            foreach (Match match in matches)
            {
                string tag = match.Value;
                if (tag != "TRUE" && tag != "FALSE")
                {
                    tags.Add(tag);
                }
            }

            return tags;
        }
    }
}
