using NCalc;

namespace Datemulte_2.Services.Security;

public class SecureFormulaEvaluator
{
    private static readonly HashSet<string> AllowedFunctions = new()
    {
        "Abs", "Acos", "Asin", "Atan", "Ceiling", "Cos", "Exp",
        "Floor", "Log", "Log10", "Max", "Min", "Pow", "Round",
        "Sign", "Sin", "Sqrt", "Tan", "Truncate", "Sum", "Avg"
    };

    public static (bool success, double result, string error) Evaluate(string expression)
    {
        try
        {
            var expr = new Expression(expression);

            // Disable dangerous functions
            expr.EvaluateFunction += (name, args) =>
            {
                if (!AllowedFunctions.Contains(name))
                    throw new InvalidOperationException($"Function '{name}' is not allowed");
            };

            var result = expr.Evaluate();
            return (true, Convert.ToDouble(result), "");
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }
}
