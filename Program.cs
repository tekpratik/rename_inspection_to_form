using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using OfficeOpenXml;

public class ViewCaptionVM
{
    public int ViewCaptionID { get; set; }
    public string Module { get; set; }
    public string Label { get; set; }
    public string Caption { get; set; }
    public string Language { get; set; }
}

public class MasterLookupVM
{
    public List<ViewCaptionVM> ViewCaptions { get; set; } = new();
}

class Program
{
    static async System.Threading.Tasks.Task Main()
    {
        string solutionPath = @"/Users/pratikbanawalkar/am-mobile";
        string excelPath = Path.Combine(solutionPath, "InspectionLabelCaptions.xlsx");
        string apiUrl = "https://test.api.nexgen.am/api/mobile/MLookups/Phone/Masterlookups/1";
        string bearerToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJVc2VySWQiOiIyMTk4IiwiVXNlck5hbWUiOiJwcmF0aWsiLCJEb21haW5JZCI6IjEiLCJMYW5ndWFnZSI6ImVuIiwiU2lkIjoiYzBhMmMwNWItOWVmMC00ZmI4LWJkNDQtODc3MGU1YjU5MmNiIiwiVG9rZW5HZW5lcmF0ZWRUaW1lIjoiMTAvOC8yMDI1IDU6NDI6NTQgQU0iLCJMb2NhbGl0eSI6IjAiLCJuYmYiOjE3NTk5MDIxNzQsImV4cCI6MTc2MDUwNjk3NCwiaWF0IjoxNzU5OTAyMTc0LCJpc3MiOiJodHRwczovL3Rlc3QuYXBpLm5leGdlbi5hbSIsImF1ZCI6Imh0dHBzOi8vdGVzdC5hcGkubmV4Z2VuLmFtIn0.nm-xmoi_ykr3MG8-cmAnmS6L6l2xWohEM3eQCPwe194"; // Replace with Preferences.Get("Token", string.Empty)

        // EPPlus 8 license setup
        ExcelPackage.License.SetNonCommercialPersonal("Pratik Banawalkar");

        Console.WriteLine("📡 Fetching data from API...");

        MasterLookupVM model;
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", bearerToken);
            var response = await client.GetAsync(apiUrl);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            model = JsonSerializer.Deserialize<MasterLookupVM>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new MasterLookupVM();
        }

        var captions = model.ViewCaptions ?? new List<ViewCaptionVM>();
        var inspectionCaptions = captions.FindAll(c =>
            string.Equals(c.Module, "Inspection", StringComparison.OrdinalIgnoreCase) && c.Caption.Contains("Inspection"));

        Console.WriteLine($"✅ Found {inspectionCaptions.Count} inspection-related captions.");

        // --- Export results to Excel ---
        var excelFile = new FileInfo(excelPath);
        using var package = new ExcelPackage(excelFile);
        var ws = package.Workbook.Worksheets.Add("InspectionCaptions");

        // Header
        ws.Cells[1, 1].Value = "ViewCaptionID";
        ws.Cells[1, 2].Value = "Module";
        ws.Cells[1, 3].Value = "Label";
        ws.Cells[1, 4].Value = "Caption";
        ws.Cells[1, 5].Value = "Language";
        ws.Cells[1, 6].Value = "NewCaption";

        // Data
        for (int i = 0; i < inspectionCaptions.Count; i++)
        {
            var c = inspectionCaptions[i];
            ws.Cells[i + 2, 1].Value = c.ViewCaptionID;
            ws.Cells[i + 2, 2].Value = c.Module;
            ws.Cells[i + 2, 3].Value = c.Label;
            ws.Cells[i + 2, 4].Value = c.Caption;
            ws.Cells[i + 2, 5].Value = c.Language;
            ws.Cells[i + 2, 6].Value = c.Caption.Replace("Inspection", "Form");
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        package.Save();

        Console.WriteLine($"📄 Excel exported to: {excelPath}");
    }
}
