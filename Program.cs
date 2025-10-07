using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using OfficeOpenXml;

class Program
{
    static void Main()
    {
        string solutionPath = @"/Users/pratikbanawalkar/am-mobile";
        string excelPath = Path.Combine(solutionPath, "InspectionUIDisplayReferences.xlsx");
        string searchWord = "inspection";

        // EPPlus 8 license setup
        ExcelPackage.License.SetNonCommercialPersonal("Pratik Banawalkar");

        // UI file types
        var uiExtensions = new[]
        {
            ".axml", ".xml",        // Android XML layouts
            ".storyboard", ".xib",  // iOS UI
            ".xaml", ".razor",      // Shared .NET UI
            ".cs"                   // For UI strings in code
        };

        // Regex patterns to match UI-visible text
        var uiRegexes = new[]
        {
            new Regex(@"android:text\s*=\s*""[^""]*inspection[^""]*""", RegexOptions.IgnoreCase),
            new Regex(@"text\s*=\s*""[^""]*inspection[^""]*""", RegexOptions.IgnoreCase),
            new Regex(@"title\s*=\s*""[^""]*inspection[^""]*""", RegexOptions.IgnoreCase),
            new Regex(@"\bSetTitle\s*\(\s*""[^""]*inspection[^""]*""", RegexOptions.IgnoreCase),
            new Regex(@"\bText\s*=\s*""[^""]*inspection[^""]*""", RegexOptions.IgnoreCase),
            new Regex(@"\bTitle\s*=\s*""[^""]*inspection[^""]*""", RegexOptions.IgnoreCase),
            new Regex(@"\bPlaceholder\s*=\s*""[^""]*inspection[^""]*""", RegexOptions.IgnoreCase)
        };

        var files = Directory.GetFiles(solutionPath, "*.*", SearchOption.AllDirectories)
            .Where(f => uiExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var results = new List<(string File, int Line, string Matched)>();

        foreach (var file in files)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (uiRegexes.Any(r => r.IsMatch(line)))
                    {
                        results.Add((file, i + 1, line.Trim()));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Skipped {file}: {ex.Message}");
            }
        }

        // --- Export results to Excel ---
        var excelFile = new FileInfo(excelPath);
        using var package = new ExcelPackage(excelFile);
        var ws = package.Workbook.Worksheets.Add("UIDisplay");

        // Header
        ws.Cells[1, 1].Value = "File";
        ws.Cells[1, 2].Value = "Line";
        ws.Cells[1, 3].Value = "Matched UI Line";

        // Data
        for (int i = 0; i < results.Count; i++)
        {
            ws.Cells[i + 2, 1].Value = results[i].File;
            ws.Cells[i + 2, 2].Value = results[i].Line;
            ws.Cells[i + 2, 3].Value = results[i].Matched;
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        package.Save();

        Console.WriteLine($"✅ Found {results.Count} UI display strings containing '{searchWord}'.");
        Console.WriteLine($"📄 Excel exported to: {excelPath}");
    }
}
