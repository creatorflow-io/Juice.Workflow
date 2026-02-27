using System.Globalization;

namespace Juice.Workflows.Models
{
    /// <summary>
    /// Built-in implementation of <see cref="ILogicConditionEvaluator"/> that evaluates
    /// simple binary expressions against the workflow execution context with no external dependencies.
    ///
    /// Grammar (recursive descent):
    ///   expression  = term ( ("AND" | "&&" | "OR" | "||") term )*
    ///   term        = "(" expression ")" | comparison
    ///   comparison  = variable operator literal
    ///   variable    = dot-path string  (e.g. "total", "order.status")
    ///   operator    = "==" | "!=" | ">" | "&lt;" | ">=" | "&lt;="
    ///   literal     = unquoted-string | integer | decimal
    ///
    /// Precedence: AND / &amp;&amp; binds tighter than OR / ||. Parentheses override.
    ///
    /// Variable resolution: context.Input first, then context.Output.
    /// Missing variable → comparison evaluates to false (no fault raised).
    /// Malformed expression → returns false and logs a warning (no throw).
    /// </summary>
    public class SimpleExpressionEvaluator : ILogicConditionEvaluator
    {
        private readonly ILogger<SimpleExpressionEvaluator> _logger;

        public SimpleExpressionEvaluator(ILogger<SimpleExpressionEvaluator> logger)
        {
            _logger = logger;
        }

        public Task<bool> EvaluateAsync(string expression, WorkflowContext context, NodeContext source)
        {
            try
            {
                var tokens = Tokenize(expression);
                var pos = 0;
                var result = ParseExpression(tokens, ref pos, context);
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to evaluate expression '{Expression}'. Treating as false.", expression);
                return Task.FromResult(false);
            }
        }

        // ── Tokenizer ─────────────────────────────────────────────────────────

        private static List<string> Tokenize(string expression)
        {
            var tokens = new List<string>();
            var i = 0;
            while (i < expression.Length)
            {
                if (char.IsWhiteSpace(expression[i]))
                {
                    i++;
                    continue;
                }

                // Two-char operators
                if (i + 1 < expression.Length)
                {
                    var two = expression.Substring(i, 2);
                    if (two == "==" || two == "!=" || two == ">=" || two == "<=" || two == "&&" || two == "||")
                    {
                        tokens.Add(two);
                        i += 2;
                        continue;
                    }
                }

                // Single-char operators / grouping
                var ch = expression[i];
                if (ch == '>' || ch == '<' || ch == '(' || ch == ')')
                {
                    tokens.Add(ch.ToString());
                    i++;
                    continue;
                }

                // Word token (identifier, keyword, or unquoted literal)
                var start = i;
                while (i < expression.Length && !char.IsWhiteSpace(expression[i])
                       && expression[i] != '(' && expression[i] != ')'
                       && !(i + 1 < expression.Length && (expression.Substring(i, 2) == "==" || expression.Substring(i, 2) == "!=" || expression.Substring(i, 2) == ">=" || expression.Substring(i, 2) == "<=" || expression.Substring(i, 2) == "&&" || expression.Substring(i, 2) == "||"))
                       && expression[i] != '>' && expression[i] != '<')
                {
                    i++;
                }

                if (i > start)
                {
                    tokens.Add(expression.Substring(start, i - start));
                }
            }
            return tokens;
        }

        // ── Parser ────────────────────────────────────────────────────────────

        /// <summary>
        /// expression = andExpr ( ("OR" | "||") andExpr )*
        /// </summary>
        private bool ParseExpression(List<string> tokens, ref int pos, WorkflowContext context)
        {
            var result = ParseAndExpr(tokens, ref pos, context);
            while (pos < tokens.Count && IsOr(tokens[pos]))
            {
                pos++;
                var right = ParseAndExpr(tokens, ref pos, context);
                result = result || right;
            }
            return result;
        }

        /// <summary>
        /// andExpr = term ( ("AND" | "&&") term )*
        /// AND/&amp;&amp; binds tighter than OR/||
        /// </summary>
        private bool ParseAndExpr(List<string> tokens, ref int pos, WorkflowContext context)
        {
            var result = ParseTerm(tokens, ref pos, context);
            while (pos < tokens.Count && IsAnd(tokens[pos]))
            {
                pos++;
                var right = ParseTerm(tokens, ref pos, context);
                result = result && right;
            }
            return result;
        }

        /// <summary>
        /// term = "(" expression ")" | comparison
        /// </summary>
        private bool ParseTerm(List<string> tokens, ref int pos, WorkflowContext context)
        {
            if (pos < tokens.Count && tokens[pos] == "(")
            {
                pos++; // consume "("
                var result = ParseExpression(tokens, ref pos, context);
                if (pos < tokens.Count && tokens[pos] == ")")
                {
                    pos++; // consume ")"
                }
                else
                {
                    throw new FormatException("Expected closing parenthesis");
                }
                return result;
            }
            return ParseComparison(tokens, ref pos, context);
        }

        /// <summary>
        /// comparison = variable operator literal
        /// </summary>
        private bool ParseComparison(List<string> tokens, ref int pos, WorkflowContext context)
        {
            if (pos + 2 >= tokens.Count)
            {
                throw new FormatException($"Incomplete comparison expression near token position {pos}");
            }

            var variable = tokens[pos++];
            var op = tokens[pos++];
            var literal = tokens[pos++];

            var value = ResolveVariable(variable, context);

            return Compare(value, op, literal);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool IsOr(string token)
            => token == "OR" || token == "||";

        private static bool IsAnd(string token)
            => token == "AND" || token == "&&";

        private static object? ResolveVariable(string key, WorkflowContext context)
        {
            // Support dot-path keys (e.g. "order.status") via simple dictionary lookup.
            // For nested paths the caller must store them with the dot-path key.
            if (context.Input != null && context.Input.TryGetValue(key, out var inputValue))
            {
                return inputValue;
            }
            if (context.Output != null && context.Output.TryGetValue(key, out var outputValue))
            {
                return outputValue;
            }
            return null;
        }

        private static bool Compare(object? value, string op, string literal)
        {
            if (value == null)
            {
                return false; // missing variable → false
            }

            // Try numeric comparison
            if (TryParseDecimal(literal, out var literalNum) && TryToDecimal(value, out var valueNum))
            {
                return op switch
                {
                    "==" => valueNum == literalNum,
                    "!=" => valueNum != literalNum,
                    ">" => valueNum > literalNum,
                    "<" => valueNum < literalNum,
                    ">=" => valueNum >= literalNum,
                    "<=" => valueNum <= literalNum,
                    _ => false
                };
            }

            // String comparison
            var valueStr = value.ToString() ?? string.Empty;
            return op switch
            {
                "==" => string.Equals(valueStr, literal, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(valueStr, literal, StringComparison.OrdinalIgnoreCase),
                ">" => string.Compare(valueStr, literal, StringComparison.OrdinalIgnoreCase) > 0,
                "<" => string.Compare(valueStr, literal, StringComparison.OrdinalIgnoreCase) < 0,
                ">=" => string.Compare(valueStr, literal, StringComparison.OrdinalIgnoreCase) >= 0,
                "<=" => string.Compare(valueStr, literal, StringComparison.OrdinalIgnoreCase) <= 0,
                _ => false
            };
        }

        private static bool TryParseDecimal(string s, out decimal value)
            => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

        private static bool TryToDecimal(object? value, out decimal result)
        {
            try
            {
                result = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                result = 0;
                return false;
            }
        }
    }
}
