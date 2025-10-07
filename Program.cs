using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OfficeOpenXml;

class Program
{
    static void Main()
    {
        string solutionPath = @"/Users/pratikbanawalkar/am-mobile";
        string excelPath = @"/Users/pratikbanawalkar/am-mobile/InspectionCaptions.xlsx";

        var files = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories);

        // Use HashSet to ignore duplicates based on Key
        var seenKeys = new HashSet<string>();
        var results = new List<(string Category, string Key, string File, int Line)>();

        foreach (var file in files)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var invocations = root.DescendantNodes()
                                  .OfType<InvocationExpressionSyntax>();

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

                        if (firstArg != null && firstArg.Token.ValueText == "Inspection")
                        {
                            string key = secondArg?.Token.ValueText ?? "(non-literal)";

                            // Skip if key is already added
                            if (!seenKeys.Contains(key))
                            {
                                seenKeys.Add(key);
                                var lineSpan = invocation.GetLocation().GetLineSpan();
                                results.Add((firstArg.Token.ValueText, key, file, lineSpan.StartLinePosition.Line + 1));
                            }
                        }
                    }
                }
            }
        }

        // --- EPPlus 8 License setup ---
        ExcelPackage.License.SetNonCommercialPersonal("Pratik Banawalkar");

        var excelFile = new FileInfo(excelPath);
        using var package = new ExcelPackage(excelFile);
        var ws = package.Workbook.Worksheets.Add("InspectionCaptions");

        // Header
        ws.Cells[1, 1].Value = "Category";
        ws.Cells[1, 2].Value = "Key";
        ws.Cells[1, 3].Value = "File";
        ws.Cells[1, 4].Value = "Line";

        // Data
        for (int i = 0; i < results.Count; i++)
        {
            ws.Cells[i + 2, 1].Value = results[i].Category;
            ws.Cells[i + 2, 2].Value = results[i].Key;
            ws.Cells[i + 2, 3].Value = results[i].File;
            ws.Cells[i + 2, 4].Value = results[i].Line;
        }

        package.Save();
        Console.WriteLine($"Excel exported successfully to {excelPath}");
    }
}
