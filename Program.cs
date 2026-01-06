using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OfficeOpenXml;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

class Program
{static readonly SourceRepository NuGetRepo =
        Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");

    static readonly Dictionary<PackageIdentity, List<string>> TfmCache = new();

    static async Task Main()
    {
        ExcelPackage.License.SetNonCommercialPersonal("Pratik Banawalkar");

        string solutionPath = "/Users/pratikbanawalkar/am-mobile";
        string outputDir = Path.Combine(solutionPath, "NetCompatibilityReports");
        Directory.CreateDirectory(outputDir);

        string excelPath = Path.Combine(outputDir, "NetCompatibilityReport.xlsx");

        var csprojFiles = Directory.GetFiles(solutionPath, "*.csproj", SearchOption.AllDirectories);

        using var excel = new ExcelPackage(new FileInfo(excelPath));

        foreach (var csproj in csprojFiles)
        {
            Console.WriteLine($"Processing {csproj}");
            await ProcessProject(csproj, excel);
        }

        excel.Save();
        Console.WriteLine("✅ Excel generated successfully");
    }

    static async Task ProcessProject(string csprojPath, ExcelPackage excel)
    {
        var doc = XDocument.Load(csprojPath);
        XNamespace ns = doc.Root!.Name.Namespace;

        var targetFrameworks =
            doc.Descendants(ns + "TargetFramework").Select(x => x.Value)
            .Concat(doc.Descendants(ns + "TargetFrameworks").SelectMany(x => x.Value.Split(';')))
            .Distinct()
            .ToList();

        bool isAndroid = targetFrameworks.Any(t => t.Contains("android"));
        bool isIos = targetFrameworks.Any(t => t.Contains("ios"));

        var packages = doc.Descendants(ns + "PackageReference")
            .Select(p => p.Attribute("Include")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        string sheetName = SanitizeSheetName(Path.GetFileNameWithoutExtension(csprojPath));
        var wb = excel.Workbook;

        if (wb.Worksheets[sheetName] != null)
            wb.Worksheets.Delete(sheetName);

        var ws = wb.Worksheets.Add(sheetName);

        string[] headers =
        {
            "Package",
            ".NET 10 Compatible",
            "Latest Compatible Version",
            "Published Date",
            "Compatibility Type",
            "NuGet Link"
        };

        for (int i = 0; i < headers.Length; i++)
            ws.Cells[1, i + 1].Value = headers[i];

        int row = 2;

        foreach (var pkg in packages)
        {
            var info = await GetNuGetInfo(pkg, isAndroid, isIos);

            ws.Cells[row, 1].Value = pkg;
            ws.Cells[row, 2].Value = info.SupportsNet10 ? "Yes" : "No";
            ws.Cells[row, 3].Value = info.LatestVersion;
            ws.Cells[row, 4].Value = info.ReleaseDate;
            ws.Cells[row, 5].Value = info.CompatibilityNote;
            ws.Cells[row, 6].Value = info.PackageUrl;

            row++;
        }

        ws.Cells.AutoFitColumns();
    }
    static async Task<List<string>> GetSupportedFrameworksAsync(
        PackageIdentity identity,
        SourceRepository repo,
        CancellationToken token)
    {
        var resource = await repo.GetResourceAsync<FindPackageByIdResource>(token);

        using var packageStream = new MemoryStream();
        await resource.CopyNupkgToStreamAsync(
            identity.Id,
            identity.Version,
            packageStream,
            new SourceCacheContext(),
            NullLogger.Instance,
            token);

        packageStream.Position = 0;

        using var reader = new PackageArchiveReader(packageStream);

        return reader.GetSupportedFrameworks()
            .Select(f => f.GetShortFolderName())
            .Distinct()
            .ToList();
    }

    // ✅ OFFICIAL NuGet SDK – this replaces ALL JSON logic
    // static async Task<NuGetInfo> GetNuGetInfo(string packageId, bool isAndroid, bool isIos)
    // {
    //     try
    //     {
    //         var providers = Repository.Provider.GetCoreV3();
    //         var source = new SourceRepository(
    //             new PackageSource("https://api.nuget.org/v3/index.json"),
    //             providers);
    //
    //         var metadata = await source.GetResourceAsync<PackageMetadataResource>();
    //         var logger = NullLogger.Instance;
    //
    //         var packages = await metadata.GetMetadataAsync(
    //             packageId,
    //             includePrerelease: false,
    //             includeUnlisted: false,
    //             sourceCacheContext: new SourceCacheContext(),
    //             log: NullLogger.Instance,
    //             token: CancellationToken.None
    //         );
    //
    //
    //         string latestVersion = "N/A";
    //         string compatibility = "N/A";
    //         bool supportsNet10 = false;
    //
    //         DateTimeOffset latest = DateTimeOffset.MinValue;
    //
    //         foreach (var pkg in packages)
    //         {
    //             var tfms = pkg.DependencySets
    //                 .Select(d => d.TargetFramework.GetShortFolderName())
    //                 .ToList();
    //
    //             if (!IsCompatible(tfms, isAndroid, isIos, out var note))
    //                 continue;
    //
    //             if (!pkg.Published.HasValue)
    //                 continue;
    //
    //             DateTimeOffset published;
    //
    //             try
    //             {
    //                 // Normalize – this is where AndroidX blows up
    //                 published = pkg.Published.Value.ToUniversalTime();
    //             }
    //             catch
    //             {
    //                 // Skip invalid NuGet metadata
    //                 continue;
    //             }
    //
    //             if (published <= DateTimeOffset.MinValue || published >= DateTimeOffset.MaxValue)
    //                 continue;
    //
    //             if (published > latest)
    //             {
    //                 latest = published;
    //                 latestVersion = pkg.Identity.Version.ToNormalizedString();
    //                 compatibility = note;
    //                 supportsNet10 = true;
    //             }
    //         }
    //
    //
    //
    //         return new NuGetInfo
    //         {
    //             SupportsNet10 = supportsNet10,
    //             LatestVersion = supportsNet10 ? latestVersion : "N/A",
    //             ReleaseDate = supportsNet10 ? latest.ToString("dd/MM/yyyy") : "N/A",
    //             CompatibilityNote = supportsNet10 ? compatibility : "Not compatible",
    //             PackageUrl = $"https://www.nuget.org/packages/{packageId}"
    //         };
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"⚠️ {packageId}: {ex.Message}");
    //
    //         return new NuGetInfo
    //         {
    //             SupportsNet10 = false,
    //             LatestVersion = "Unknown",
    //             ReleaseDate = "Unknown",
    //             CompatibilityNote = "Error",
    //             PackageUrl = $"https://www.nuget.org/packages/{packageId}"
    //         };
    //     }
    // }
static async Task<NuGetInfo> GetNuGetInfo(string packageId, bool isAndroid, bool isIos)
{
    try
    {
        var metadataResource = await NuGetRepo.GetResourceAsync<PackageMetadataResource>();
        var packages = await metadataResource.GetMetadataAsync(
            packageId,
            includePrerelease: false,
            includeUnlisted: false,
            new SourceCacheContext(),
            NullLogger.Instance,
            CancellationToken.None);

        bool supportsNet10 = false;
        string latestVersion = "N/A";
        string compatibility = "N/A";
        DateTimeOffset latest = DateTimeOffset.MinValue;

        // foreach (var pkg in packages)
        // {
        //     
        // }
        var pkg = packages.LastOrDefault();
        if (!TfmCache.TryGetValue(pkg.Identity, out var tfms))
        {
            try
            {
                tfms = await GetSupportedFrameworksAsync(
                    pkg.Identity,
                    NuGetRepo,
                    CancellationToken.None);

                TfmCache[pkg.Identity] = tfms;
            }
            catch
            {
               // continue; // broken nupkg
            }
        }

        if (!IsCompatible(tfms, isAndroid, isIos, out var note)){}
        //     //continue;
        //
        if (!pkg.Published.HasValue){}
        //     //continue;

        DateTimeOffset published = DateTimeOffset.MinValue;
        try
        {
            published = pkg.Published.Value.ToUniversalTime();
        }
        catch
        {
            //continue; // bad offset metadata
        }

        if (published > latest)
        {
            latest = published;
            latestVersion = pkg.Identity.Version.ToNormalizedString();
            compatibility = note;
            supportsNet10 = true;
        }
        return new NuGetInfo
        {
            SupportsNet10 = supportsNet10,
            LatestVersion = latestVersion,
            ReleaseDate = supportsNet10
                ? latest.ToString("dd/MM/yyyy")
                : "N/A",
            CompatibilityNote = supportsNet10
                ? compatibility
                : "Not compatible",
            PackageUrl = $"https://www.nuget.org/packages/{packageId}"
        };
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ {packageId}: {ex.Message}");

        return new NuGetInfo
        {
            SupportsNet10 = false,
            LatestVersion = "Unknown",
            ReleaseDate = "Unknown",
            CompatibilityNote = "Error",
            PackageUrl = $"https://www.nuget.org/packages/{packageId}"
        };
    }
}

    // ✅ Correct .NET 10 compatibility logic
    // static bool IsCompatible(
    //     IEnumerable<string> tfms,
    //     bool isAndroid,
    //     bool isIos,
    //     out string note)
    // {
    //     var list = tfms.Select(t => t.ToLowerInvariant()).ToList();
    //
    //     if (isAndroid)
    //     {
    //         note = "Explicit Android TFM";
    //         return list.Any(t => t.StartsWith("net10.0-android"));
    //     }
    //
    //     if (isIos)
    //     {
    //         note = "Explicit iOS TFM";
    //         return list.Any(t => t.StartsWith("net10.0-ios"));
    //     }
    //
    //     if (list.Any(t => t.StartsWith("net10.0")))
    //     {
    //         note = "Explicit .NET 10 support";
    //         return true;
    //     }
    //
    //     if (list.Any(t => t.StartsWith("net9.0") || t.StartsWith("net8.0")))
    //     {
    //         note = "Implicit .NET 10 compatible";
    //         return true;
    //     }
    //
    //     if (list.Any(t => t.StartsWith("netstandard2.")))
    //     {
    //         note = "Implicit via .NET Standard";
    //         return true;
    //     }
    //
    //     note = "No compatible TFM";
    //     return false;
    // }
    static bool IsCompatible(
        IEnumerable<string> tfms,
        bool isAndroid,
        bool isIos,
        out string note)
    {
        var list = tfms.Select(t => t.ToLowerInvariant()).ToList();

        if (isAndroid)
        {
            if (list.Any(t => t.StartsWith("net10.0-android")))
            {
                note = "Supports net10.0-android";
                return true;
            }
        }

        if (isIos)
        {
            if (list.Any(t => t.StartsWith("net10.0-ios")))
            {
                note = "Supports net10.0-ios";
                return true;
            }
        }

        if (list.Any(t => t.StartsWith("net10.0")))
        {
            note = "Supports net10.0 (platform-agnostic)";
            return true;
        }

        note = "No net10 support";
        return false;
    }

    static string SanitizeSheetName(string name)
    {
        foreach (var c in new[] { '\\', '/', '?', '*', '[', ']' })
            name = name.Replace(c.ToString(), "");

        return name.Length > 31 ? name[..31] : name;
    }
}

class NuGetInfo
{
    public bool SupportsNet10 { get; set; }
    public string LatestVersion { get; set; } = "";
    public string ReleaseDate { get; set; } = "";
    public string CompatibilityNote { get; set; } = "";
    public string PackageUrl { get; set; } = "";
}
