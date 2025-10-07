using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string solutionPath = @"/Users/pratikbanawalkar/am-mobile";
        string constantsPath = Path.Combine(solutionPath, "FormConstants.cs");

        var files = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories);

        // Step 1: Collect unique NexgenAMCaption.Get calls where Category == "Inspection"
        var constants = new Dictionary<string, (string Category, string Key)>();

        foreach (var file in files)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax member &&
                    member.Name.ToString() == "Get" &&
                    member.Expression.ToString() == "NexgenAMCaption")
                {
                    var args = invocation.ArgumentList.Arguments;
                    if (args.Count >= 2)
                    {
                        var firstArg = args[0].Expression as LiteralExpressionSyntax;
                        var secondArg = args[1].Expression as LiteralExpressionSyntax;

                        if (firstArg != null && firstArg.Token.ValueText == "Inspection" && secondArg != null)
                        {
                            string key = secondArg.Token.ValueText;
                            if (!constants.ContainsKey(key))
                                constants[key] = ("Inspection", key);
                        }
                    }
                }
            }
        }

        // Step 2: Generate FormConstants.cs for Inspection keys
        using (var writer = new StreamWriter(constantsPath, false))
        {
            writer.WriteLine("public static class FormConstants");
            writer.WriteLine("{");

            foreach (var kvp in constants)
            {
                string key = kvp.Key;
                string category = kvp.Value.Category;
                string propName = MakePropertyName(key);
                writer.WriteLine($"\tpublic static string {propName} => NexgenAMCaption.Get(\"{category}\", \"{key}\");");
            }

            writer.WriteLine("}");
        }

        Console.WriteLine($"FormConstants.cs generated with {constants.Count} Inspection properties at {constantsPath}");

        // Step 3: Replace references in code files for Inspection keys
        foreach (var file in files)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var rewriter = new NexgenRewriter(constants);
            var newRoot = rewriter.Visit(root);

            if (newRoot != root)
            {
                File.WriteAllText(file, newRoot.ToFullString());
                Console.WriteLine($"Updated Inspection references in {file}");
            }
        }

        Console.WriteLine("All Inspection references replaced with FormConstants properties!");
    }

    // Convert key to valid property name
    // Convert key to valid property name in UPPERCASE with underscores
    static string MakePropertyName(string key)
    {
        // 1. Add underscore before capital letters that follow lowercase letters (camelCase → UPPER_WITH_UNDERSCORES)
        string name = Regex.Replace(key, "([a-z0-9])([A-Z])", "$1_$2");

        // 2. Replace any spaces with underscores
        name = name.Replace(" ", "_");

        // 3. Remove any remaining invalid characters (keep only A-Z, 0-9, _)
        name = Regex.Replace(name, @"[^A-Za-z0-9_]", "_");

        // 4. Convert to uppercase
        name = name.ToUpper();

        // 5. Prefix with underscore if starts with a digit
        if (char.IsDigit(name[0]))
            name = "_" + name;

        return name;
    }


    class NexgenRewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, (string Category, string Key)> _constants;

        public NexgenRewriter(Dictionary<string, (string Category, string Key)> constants)
        {
            _constants = constants;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax member &&
                member.Name.ToString() == "Get" &&
                member.Expression.ToString() == "NexgenAMCaption")
            {
                var args = node.ArgumentList.Arguments;
                if (args.Count >= 2)
                {
                    var firstArg = args[0].Expression as LiteralExpressionSyntax;
                    var secondArg = args[1].Expression as LiteralExpressionSyntax;

                    if (firstArg != null && firstArg.Token.ValueText == "Inspection" && secondArg != null)
                    {
                        string key = secondArg.Token.ValueText;
                        if (_constants.ContainsKey(key))
                        {
                            string propName = MakePropertyName(key);
                            var replacement = SyntaxFactory.ParseExpression($"FormConstants.{propName}");
                            return replacement.WithTriviaFrom(node);
                        }
                    }
                }
            }

            return base.VisitInvocationExpression(node);
        }
    }
}
