using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MediaRenamer
{
    class Program
    {
        static void Main(string[] args)
        {
            // Check if the directory path is provided
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: MediaRenamer.exe <directoryPath> [commit] [--recursive | --non-recursive]");
                return;
            }

            string directoryPath = args[0];
            bool commitChanges = args.Contains("commit", StringComparer.OrdinalIgnoreCase);
            bool isRecursive = args.Contains("--recursive", StringComparer.OrdinalIgnoreCase);

            // Default behavior: recursive renaming
            if (!args.Contains("--recursive", StringComparer.OrdinalIgnoreCase) &&
                !args.Contains("--non-recursive", StringComparer.OrdinalIgnoreCase))
            {
                isRecursive = true;
            }

            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine($"Directory not found: {directoryPath}");
                return;
            }

            string mappingFilePath = Path.Combine(directoryPath, "RenameMapping.txt");
            string reportFilePath = Path.Combine(directoryPath, "RenameReport.txt");

            if (!commitChanges)
            {
                // Generate the mapping file
                var renameMappings = GenerateRenameMappings(directoryPath, isRecursive);
                WriteMappingFile(renameMappings, mappingFilePath);
                Console.WriteLine($"Mapping file generated at: {mappingFilePath}");
                Console.WriteLine("Please review and edit the 'ProposedNewFullPath' in the mapping file if necessary.");
            }
            else
            {
                // Perform the renaming
                if (!File.Exists(mappingFilePath))
                {
                    Console.WriteLine($"Mapping file not found: {mappingFilePath}");
                    return;
                }

                var renameMappings = ReadMappingFile(mappingFilePath);
                var reportEntries = PerformRenaming(renameMappings);
                WriteReportFile(reportEntries, reportFilePath);
                Console.WriteLine("Renaming completed successfully.");
                Console.WriteLine($"Report generated at: {reportFilePath}");
            }
        }

        static List<RenameMapping> GenerateRenameMappings(string directoryPath, bool isRecursive)
        {
            var renameMappings = new List<RenameMapping>();

            var searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var fileSystemEntries = Directory.GetFileSystemEntries(directoryPath, "*", searchOption);

            foreach (var path in fileSystemEntries)
            {
                string name = Path.GetFileName(path);
                bool isFile = File.Exists(path);

                string proposedNewName = CleanName(path, isFile);
                if (!name.Equals(proposedNewName, StringComparison.OrdinalIgnoreCase))
                {
                    string newFullPath = Path.Combine(Path.GetDirectoryName(path), proposedNewName);
                    renameMappings.Add(new RenameMapping
                    {
                        OriginalFullPath = path,
                        ProposedNewFullPath = newFullPath
                    });
                }
            }

            return renameMappings;
        }

        static string CleanName(string fullPath, bool isFile)
        {
            // Get the filename
            string fileName = Path.GetFileName(fullPath);

            // Initialize variables for extensions
            string extension = "";
            string languageExtension = "";
            string nameWithoutExtension = fileName;

            if (isFile)
            {
                // Get the primary extension
                extension = Path.GetExtension(fullPath); // e.g., ".srt"

                // Check if it's an .srt file
                if (extension.Equals(".srt", StringComparison.OrdinalIgnoreCase))
                {
                    // Get the name without the primary extension
                    string tempName = Path.GetFileNameWithoutExtension(fullPath);

                    // Get the secondary extension (language code)
                    languageExtension = Path.GetExtension(tempName); // e.g., ".es"

                    // List of common language codes
                    var languageCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ".en", ".es", ".fr", ".de", ".it", ".pt", ".ru", ".zh", ".ja", ".ko",
                        ".ar", ".nl", ".sv", ".no", ".da", ".fi", ".pl", ".tr", ".he", ".el",
                        ".cs", ".sk", ".hu", ".bg", ".ro", ".hr", ".sr", ".sl", ".uk", ".th",
                        ".vi", ".id", ".ms"
                    };

                    // If the secondary extension is a language code
                    if (languageCodes.Contains(languageExtension))
                    {
                        // Update the name without extension
                        nameWithoutExtension = Path.GetFileNameWithoutExtension(tempName);
                    }
                    else
                    {
                        // No language code; reset languageExtension
                        languageExtension = "";
                        nameWithoutExtension = tempName;
                    }
                }
                else
                {
                    // For other files, get the name without extension
                    nameWithoutExtension = Path.GetFileNameWithoutExtension(fullPath);
                }
            }
            else
            {
                // For directories, get the name
                nameWithoutExtension = fileName;
            }

            // Proceed with cleaning the name
            // Replace dots with spaces (for folders and files)
            nameWithoutExtension = nameWithoutExtension.Replace('.', ' ');

            // Remove content in square brackets and curly braces
            nameWithoutExtension = Regex.Replace(nameWithoutExtension, @"[\[\{].*?[\]\}]", "", RegexOptions.IgnoreCase);

            // Replace underscores and dashes with spaces
            string cleanedName = nameWithoutExtension.Replace('_', ' ').Replace('-', ' ');

            // Replace any remaining non-alphanumeric characters (excluding spaces) with spaces
            cleanedName = Regex.Replace(cleanedName, @"[^\w\s]", " ");

            // Convert multiple spaces into a single space
            cleanedName = Regex.Replace(cleanedName, @"\s+", " ").Trim();

            // Define a list of unwanted terms
            var unwantedTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // (Include the expanded unwanted terms list from previous versions)
                // Resolutions and video quality
                "1080p", "720p", "480p", "2160p", "4K", "8K", "HD", "HDTV", "SD", "HQ",
                "10bit", "8bit", "HEVC", "AVC", "H264", "H265", "x264", "x265",
                // Source and encoding
                "BluRay", "BRRip", "BDRip", "WEBRip", "WEB", "WEB-DL", "HDRip", "DVDRip", "REMUX", "CAM", "TS", "R5",
                // Audio codecs and configurations
                "AAC", "AC3", "EAC3", "DTS", "DTS-HD", "DTSHD", "MA", "TRUEHD", "MP3", "FLAC", "OGG", "DDP5", "DD5", "2.0", "5.1", "7.1", "ATMOS",
                // Release types and versions
                "EXTENDED", "UNRATED", "DIRECTORS CUT", "DC", "REMASTERED", "THEATRICAL CUT", "FINAL CUT", "SPECIAL EDITION", "SE",
                // Language and subtitles
                "SUBBED", "DUBBED", "MULTI", "DUAL AUDIO", "ENG", "ENG SUBS", "ITA", "GERMAN", "FRENCH", "SPANISH", "KOREAN", "JAPANESE", "CHINESE",
                // HDR formats
                "HDR", "SDR", "DV", "HDR10", "HDR10+", "HLG", "DOLBY VISION",
                // Release groups and uploader tags
                "YIFY", "YTS", "RARBG", "SHiTSoNy", "SiNNERS", "ANOXMOUS", "AN0NYM0US", "AN0NYM0US", "ANOXMOUS",
                "HON3Y", "HIGHCODE", "DELTA", "JYK", "Z3R0C00", "BRSHNKV", "MZABI", "ETRG", "GANJAMAN", "CMRG", "INSPiRAL", "Tigole", "FGT",
                // Other unwanted terms
                "RESTORED", "COMPLETE", "DUOLOGY", "TRILOGY", "QUADRILOGY", "COLLECTION", "SERIES", "SEASON", "EPISODE", "S0", "E0",
                "READNFO", "NFO", "CAMRip", "WORKPRINT", "TELESYNC", "TELECINE", "SCREENER", "DVDSCR", "DVDRIP", "BDrip",
                "NF", "AMZN", "AMAZON", "HULU", "NETFLIX", "IMAX", "Criterion", "Rip", "UHD", "ULTRAHD", "SD", "HQ", "HDCAM", "HDTS"
            };

            // Split the filename into words using spaces
            var words = cleanedName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var titleWords = new List<string>();
            string year = "";
            Regex yearRegex = new Regex(@"^(19|20)\d{2}$");
            Regex yearRangeRegex = new Regex(@"^(19|20)\d{2}-(19|20)\d{2}$");

            foreach (var word in words)
            {
                string trimmedWord = word.Trim();

                // Check if the word is a year or a year range
                if (yearRegex.IsMatch(trimmedWord))
                {
                    year = trimmedWord;
                    break; // Stop adding words after the year
                }
                else if (yearRangeRegex.IsMatch(trimmedWord))
                {
                    // Use the first year in the range
                    year = trimmedWord.Substring(0, 4);
                    break; // Stop adding words after the year
                }

                // If the word is an unwanted term, stop adding to the title
                if (unwantedTerms.Contains(trimmedWord.ToUpperInvariant()))
                {
                    break;
                }

                titleWords.Add(trimmedWord);
            }

            // Construct the title
            string title = string.Join(" ", titleWords);

            // Remove extra spaces and trim non-alphanumeric characters
            title = Regex.Replace(title, @"\s+", " ").Trim('-', '_', '(', ')', '[', ']', '{', '}', '\'');

            // Construct the new filename
            string newName;
            if (!string.IsNullOrEmpty(year))
            {
                newName = $"{title} ({year})";
            }
            else
            {
                newName = title;
            }

            // Re-add the language extension and the actual extension
            if (isFile)
            {
                newName += languageExtension + extension;
            }

            return newName;
        }

        static void WriteMappingFile(List<RenameMapping> mappings, string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("OriginalFullPath|ProposedNewFullPath");
                foreach (var mapping in mappings)
                {
                    writer.WriteLine($"{mapping.OriginalFullPath}|{mapping.ProposedNewFullPath}");
                }
            }
        }

        static List<RenameMapping> ReadMappingFile(string filePath)
        {
            var mappings = new List<RenameMapping>();
            var lines = File.ReadAllLines(filePath).Skip(1); // Skip header

            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length == 2)
                {
                    mappings.Add(new RenameMapping
                    {
                        OriginalFullPath = parts[0],
                        ProposedNewFullPath = parts[1]
                    });
                }
            }

            return mappings;
        }

        static List<ReportEntry> PerformRenaming(List<RenameMapping> mappings)
        {
            var reportEntries = new List<ReportEntry>();

            // Sort mappings to handle directories after files
            var filesFirst = mappings.OrderBy(m => File.GetAttributes(m.OriginalFullPath).HasFlag(FileAttributes.Directory));

            foreach (var mapping in filesFirst)
            {
                try
                {
                    if (File.Exists(mapping.OriginalFullPath))
                    {
                        File.Move(mapping.OriginalFullPath, mapping.ProposedNewFullPath);
                        reportEntries.Add(new ReportEntry
                        {
                            OriginalFullPath = mapping.OriginalFullPath,
                            NewFullPath = mapping.ProposedNewFullPath,
                            Status = "Success",
                            ErrorMessage = ""
                        });
                    }
                    else if (Directory.Exists(mapping.OriginalFullPath))
                    {
                        Directory.Move(mapping.OriginalFullPath, mapping.ProposedNewFullPath);
                        reportEntries.Add(new ReportEntry
                        {
                            OriginalFullPath = mapping.OriginalFullPath,
                            NewFullPath = mapping.ProposedNewFullPath,
                            Status = "Success",
                            ErrorMessage = ""
                        });
                    }
                    else
                    {
                        string message = $"Path not found: {mapping.OriginalFullPath}";
                        Console.WriteLine(message);
                        reportEntries.Add(new ReportEntry
                        {
                            OriginalFullPath = mapping.OriginalFullPath,
                            NewFullPath = mapping.ProposedNewFullPath,
                            Status = "Failed",
                            ErrorMessage = message
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error renaming '{mapping.OriginalFullPath}' to '{mapping.ProposedNewFullPath}': {ex.Message}");
                    reportEntries.Add(new ReportEntry
                    {
                        OriginalFullPath = mapping.OriginalFullPath,
                        NewFullPath = mapping.ProposedNewFullPath,
                        Status = "Failed",
                        ErrorMessage = ex.Message
                    });
                }
            }

            return reportEntries;
        }

        static void WriteReportFile(List<ReportEntry> reportEntries, string reportFilePath)
        {
            using (var writer = new StreamWriter(reportFilePath))
            {
                writer.WriteLine("Status|OriginalFullPath|NewFullPath|ErrorMessage");
                foreach (var entry in reportEntries)
                {
                    writer.WriteLine($"{entry.Status}|{entry.OriginalFullPath}|{entry.NewFullPath}|{entry.ErrorMessage}");
                }
            }
        }
    }

    class RenameMapping
    {
        public string OriginalFullPath { get; set; }
        public string ProposedNewFullPath { get; set; }
    }

    class ReportEntry
    {
        public string Status { get; set; }
        public string OriginalFullPath { get; set; }
        public string NewFullPath { get; set; }
        public string ErrorMessage { get; set; }
    }
}