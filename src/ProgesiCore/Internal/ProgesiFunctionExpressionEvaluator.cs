using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProgesiCore.Internal
{
  /// <summary>
  /// Pure-managed deterministic evaluator for axis function expressions.
  /// Variable: x (normalized position). Supports + - * / ^ and sin cos tan log ln abs sqrt min max.
  /// </summary>
  internal static class ProgesiFunctionExpressionEvaluator
  {
    public static double Evaluate(string expression, double x)
    {
      if (string.IsNullOrWhiteSpace(expression))
        throw new ArgumentException("Expression is required.", nameof(expression));

      var tokens = Tokenize(expression);
      var parser = new Parser(tokens, x);
      var value = parser.ParseExpression();
      if (parser.HasRemaining)
        throw new FormatException("Unexpected trailing tokens in expression.");
      return value;
    }

    private static List<Token> Tokenize(string expression)
    {
      var tokens = new List<Token>();
      int i = 0;
      while (i < expression.Length)
      {
        char c = expression[i];
        if (char.IsWhiteSpace(c))
        {
          i++;
          continue;
        }

        if (char.IsDigit(c) || (c == '.' && i + 1 < expression.Length && char.IsDigit(expression[i + 1])))
        {
          int start = i;
          i++;
          while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
            i++;
          tokens.Add(new Token(TokenKind.Number, expression.Substring(start, i - start)));
          continue;
        }

        if (char.IsLetter(c))
        {
          int start = i;
          i++;
          while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
            i++;
          tokens.Add(new Token(TokenKind.Identifier, expression.Substring(start, i - start)));
          continue;
        }

        if ("+-*/^(),".IndexOf(c) >= 0)
        {
          tokens.Add(new Token(TokenKind.Symbol, c.ToString()));
          i++;
          continue;
        }

        throw new FormatException($"Unsupported character '{c}' in expression.");
      }

      return tokens;
    }

    private enum TokenKind { Number, Identifier, Symbol }

    private readonly struct Token
    {
      public TokenKind Kind { get; }
      public string Text { get; }
      public Token(TokenKind kind, string text) { Kind = kind; Text = text; }
    }

    private sealed class Parser
    {
      private readonly List<Token> _tokens;
      private readonly double _x;
      private int _index;

      public Parser(List<Token> tokens, double x)
      {
        _tokens = tokens;
        _x = x;
      }

      public bool HasRemaining => _index < _tokens.Count;

      public double ParseExpression() => ParseAddSub();

      private double ParseAddSub()
      {
        double left = ParseMulDiv();
        while (Match("+", "-"))
        {
          string op = Previous().Text;
          double right = ParseMulDiv();
          left = op == "+" ? left + right : left - right;
        }
        return left;
      }

      private double ParseMulDiv()
      {
        double left = ParsePower();
        while (Match("*", "/"))
        {
          string op = Previous().Text;
          double right = ParsePower();
          left = op == "*" ? left * right : left / right;
        }
        return left;
      }

      private double ParsePower()
      {
        double left = ParseUnary();
        if (Match("^"))
          left = Math.Pow(left, ParseUnary());
        return left;
      }

      private double ParseUnary()
      {
        if (Match("-"))
          return -ParseUnary();
        return ParsePrimary();
      }

      private double ParsePrimary()
      {
        if (MatchNumber(out var number))
          return number;

        if (MatchIdentifier(out var ident))
        {
          if (string.Equals(ident, "x", StringComparison.OrdinalIgnoreCase))
            return _x;

          if (Match("("))
          {
            var args = new List<double>();
            if (!Check(")"))
            {
              do
              {
                args.Add(ParseExpression());
              } while (Match(","));
            }
            Consume(")", "Expected ')' after function arguments.");
            return EvaluateFunction(ident, args);
          }

          throw new FormatException($"Unknown identifier '{ident}'.");
        }

        if (Match("("))
        {
          double val = ParseExpression();
          Consume(")", "Expected ')' after expression.");
          return val;
        }

        throw new FormatException("Expected number, identifier, or '('.");
      }

      private static double EvaluateFunction(string name, List<double> args)
      {
        switch (name.ToLowerInvariant())
        {
          case "sin": return RequireArgs(name, args, 1, a => Math.Sin(a[0]));
          case "cos": return RequireArgs(name, args, 1, a => Math.Cos(a[0]));
          case "tan": return RequireArgs(name, args, 1, a => Math.Tan(a[0]));
          case "log": return RequireArgs(name, args, 1, a => Math.Log10(a[0]));
          case "ln": return RequireArgs(name, args, 1, a => Math.Log(a[0]));
          case "abs": return RequireArgs(name, args, 1, a => Math.Abs(a[0]));
          case "sqrt": return RequireArgs(name, args, 1, a => Math.Sqrt(a[0]));
          case "min": return RequireArgs(name, args, 2, a => Math.Min(a[0], a[1]));
          case "max": return RequireArgs(name, args, 2, a => Math.Max(a[0], a[1]));
          default:
            throw new FormatException($"Unsupported function '{name}'.");
        }
      }

      private static double RequireArgs(string name, List<double> args, int count, Func<List<double>, double> eval)
      {
        if (args.Count != count)
          throw new FormatException($"Function '{name}' expects {count} argument(s).");
        return eval(args);
      }

      private bool MatchNumber(out double value)
      {
        if (_index < _tokens.Count && _tokens[_index].Kind == TokenKind.Number)
        {
          value = double.Parse(_tokens[_index].Text, CultureInfo.InvariantCulture);
          _index++;
          return true;
        }
        value = 0;
        return false;
      }

      private bool MatchIdentifier(out string ident)
      {
        if (_index < _tokens.Count && _tokens[_index].Kind == TokenKind.Identifier)
        {
          ident = _tokens[_index].Text;
          _index++;
          return true;
        }
        ident = string.Empty;
        return false;
      }

      private bool Match(params string[] symbols)
      {
        if (_index >= _tokens.Count || _tokens[_index].Kind != TokenKind.Symbol)
          return false;
        foreach (var sym in symbols)
        {
          if (_tokens[_index].Text == sym)
          {
            _index++;
            return true;
          }
        }
        return false;
      }

      private bool Check(string symbol) =>
        _index < _tokens.Count && _tokens[_index].Kind == TokenKind.Symbol && _tokens[_index].Text == symbol;

      private void Consume(string symbol, string message)
      {
        if (!Match(symbol))
          throw new FormatException(message);
      }

      private Token Previous() => _tokens[_index - 1];
    }
  }
}
