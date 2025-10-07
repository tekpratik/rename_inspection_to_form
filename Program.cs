using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using OfficeOpenXml;

class Program
{
    static void Main()
    {
        string solutionPath = @"/Users/pratikbanawalkar/am-mobile";
        string excelPath = @"/Users/pratikbanawalkar/am-mobile/APIRoutes.xlsx";

        // EPPlus license setup (required for version 8+)
        ExcelPackage.License.SetNonCommercialPersonal("Pratik Banawalkar");

        var files = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories);
        var results = new List<(string File, int Line, string Content)>();

        string searchText = "/api/mobile/MInspection";

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add((file, i + 1, lines[i].Trim()));
                }
            }
        }

        var excelFile = new FileInfo(excelPath);
        using var package = new ExcelPackage(excelFile);
        var ws = package.Workbook.Worksheets.Add("APIRoutes");

        // Header
        ws.Cells[1, 1].Value = "File";
        ws.Cells[1, 2].Value = "Line";
        ws.Cells[1, 3].Value = "Full Line";

        // Data
        for (int i = 0; i < results.Count; i++)
        {
            ws.Cells[i + 2, 1].Value = results[i].File;
            ws.Cells[i + 2, 2].Value = results[i].Line;
            ws.Cells[i + 2, 3].Value = results[i].Content;
        }

        package.Save();
        Console.WriteLine($"✅ Found {results.Count} matches.");
        Console.WriteLine($"📄 Excel exported successfully to {excelPath}");
    }
}
