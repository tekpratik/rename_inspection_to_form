using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string solutionPath = @"/Users/pratikbanawalkar/am-mobile";
        string constantFilePath = Path.Combine(solutionPath, "FormConstants.cs");

        string constantName = "M_ROUTE_FORM";
        string constantValue = "MInspection";
        string searchText = "(\"/api/mobile/MInspection";
        string replaceText = "($\"/api/mobile/{FormConstants." + constantName + "}";

        var files = Directory.GetFiles(solutionPath, "*.cs", SearchOption.AllDirectories)
                             .Where(f => !f.EndsWith("FormConstants.cs"))
                             .ToList();

        int replaceCount = 0;

        foreach (var file in files)
        {
            string code = File.ReadAllText(file);
            if (!code.Contains(searchText)) continue;

            var newCode = code.Replace(searchText, replaceText);

            if (newCode != code)
            {
                File.WriteAllText(file, newCode);
                Console.WriteLine($"✅ Updated: {file}");
                replaceCount++;
            }
        }

        // --- Ensure FormConstants.cs exists ---
        if (!File.Exists(constantFilePath))
        {
            File.WriteAllText(constantFilePath, "public static class FormConstants\n{\n}\n");
        }

        // --- Add constant if missing ---
        string formConstantsCode = File.ReadAllText(constantFilePath);
        if (!formConstantsCode.Contains($"const string {constantName}"))
        {
            int insertIndex = formConstantsCode.LastIndexOf('}');
            if (insertIndex > 0)
            {
                string newConstLine = $"    public const string {constantName} = \"{constantValue}\";\n";
                formConstantsCode = formConstantsCode.Insert(insertIndex, newConstLine);
                File.WriteAllText(constantFilePath, formConstantsCode);
                Console.WriteLine($"🆕 Added constant to FormConstants.cs: {constantName} = \"{constantValue}\"");
            }
        }

        Console.WriteLine($"\n🎯 Replacement complete. Updated {replaceCount} files.");
    }
}
