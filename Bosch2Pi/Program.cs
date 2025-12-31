using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Bosch2Pi
{
    internal class Program
    {
        // Helper to parse command line options and validate input.
        private class Options
        {
            public string NameMapPath { get; set; }
            public string OutingPath { get; set; }
            public string ConstantsPath { get; set; }
            public string Infile { get; set; }
            public string LapColumnName { get; set; } = "lapctr";

            public static Options Parse(string[] args)
            {
                var opt = new Options();
                var positional = new List<string>();

                for (int i = 0; i < args.Length; i++)
                {
                    var a = args[i];
                    if (string.Equals(a, "-namemap", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 >= args.Length)
                        {
                            Console.WriteLine("Error: -namemap requires a filename.");
                            PrintUsage();
                            return null;
                        }
                        opt.NameMapPath = args[++i];
                    }
                    else if (string.Equals(a, "-outinginfo", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 >= args.Length)
                        {
                            Console.WriteLine("Error: -outinginfo requires a filename.");
                            PrintUsage();
                            return null;
                        }
                        opt.OutingPath = args[++i];
                    }
                    else if (string.Equals(a, "-constants", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 >= args.Length)
                        {
                            Console.WriteLine("Error: -constants requires a filename.");
                            PrintUsage();
                            return null;
                        }
                        opt.ConstantsPath = args[++i];
                    }
                    else if (string.Equals(a, "-lapctr", StringComparison.OrdinalIgnoreCase))
                    {
                        if (i + 1 >= args.Length)
                        {
                            Console.WriteLine("Error: -lapctr requires a column name.");
                            PrintUsage();
                            return null;
                        }
                        opt.LapColumnName = args[++i];
                    }
                    else
                    {
                        positional.Add(a);
                    }
                }

                if (positional.Count < 1)
                {
                    PrintUsage();
                    return null;
                }

                opt.Infile = positional[0];
                return opt;
            }

            private static void PrintUsage()
            {
                Console.WriteLine("Usage: Bosch2Pi <data_file> [-namemap <name_map_file>] [-outinginfo <outing_file>] [-constants <constants_file>] [-lapctr <lap_column_name>]");
            }
        }

        // Key/value file parsing utilities
        private static class KeyValueFile
        {
            public static IEnumerable<KeyValuePair<string, string>> ParseKeyValueFile(string path)
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    int hashIdx = line.IndexOf('#');
                    if (hashIdx == 0) continue;
                    if (hashIdx > 0)
                    {
                        line = line.Substring(0, hashIdx).Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                    }

                    string[] parts = null;

                    if (line.Contains("->"))
                    {
                        parts = line.Split(new[] { "->" }, 2, StringSplitOptions.None);
                    }
                    else if (line.Contains('\t'))
                    {
                        parts = line.Split(new[] { '\t' }, 2);
                    }
                    else if (line.Contains('='))
                    {
                        parts = line.Split(new[] { '=' }, 2);
                    }
                    else if (line.Contains(','))
                    {
                        parts = line.Split(new[] { ',' }, 2);
                    }
                    else if (line.Contains(':'))
                    {
                        parts = line.Split(new[] { ':' }, 2);
                    }
                    else
                    {
                        // No separator found; skip line
                        continue;
                    }

                    if (parts == null || parts.Length != 2) continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) continue;

                    yield return new KeyValuePair<string, string>(key, value);
                }
            }

            public static List<KeyValuePair<string, string>> LoadOrderedPairs(string path)
            {
                return ParseKeyValueFile(path).ToList();
            }

            public static Dictionary<string, string> LoadDictionaryFirstWins(string path)
            {
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var kv in ParseKeyValueFile(path))
                {
                    if (!dict.ContainsKey(kv.Key))
                    {
                        dict.Add(kv.Key, kv.Value);
                    }
                }
                return dict;
            }
        }

        // CSV-like table loader (tab-separated)
        private class CsvTable
        {
            public List<List<string>> Columns { get; } = new List<List<string>>();

            public static CsvTable Load(string path)
            {
                var lines = File.ReadAllLines(path)
                    .Where(line => !line.TrimStart().StartsWith("#"))
                    .ToArray();

                var rows = lines.Select(line => line.Split('\t')).ToList();

                if (rows.Count == 0)
                {
                    return null;
                }

                int columnCount = rows[0].Length;
                var table = new CsvTable();

                for (int col = 0; col < columnCount; col++)
                {
                    var column = new List<string>();
                    foreach (var row in rows)
                    {
                        column.Add(col < row.Length ? row[col] : string.Empty);
                    }
                    table.Columns.Add(column);
                }

                return table;
            }
        }

        // Writer for Pi Toolbox ASCII format
        private class PiFileWriter : IDisposable
        {
            private StreamWriter _writer;

            public PiFileWriter(string outfile)
            {
                _writer = new StreamWriter(outfile, false, Encoding.GetEncoding(1252));
            }

            public void Write(
                List<List<string>> columns,
                Dictionary<string, string> nameMap,
                List<KeyValuePair<string, string>> outingInfo,
                List<KeyValuePair<string, string>> constants,
                string lapColumnName)
            {
                int lapColIndex = -1;

                _writer.WriteLine("PiToolboxVersionedASCIIDataSet");
                _writer.WriteLine("Version\t2");
                _writer.WriteLine();
                _writer.WriteLine("{OutingInformation}");

                if (outingInfo != null && outingInfo.Count > 0)
                {
                    foreach (var kv in outingInfo)
                    {
                        _writer.WriteLine($"{kv.Key}\t{kv.Value}");
                    }
                }

                _writer.WriteLine($"SystemDetails\tBosch");
                _writer.WriteLine("FirstLapNumber\t0");

                _writer.WriteLine();
                _writer.WriteLine("{ConstantBlock}");
                _writer.WriteLine("Name\tValue\tComment");

                if (constants != null && constants.Count > 0)
                {
                    foreach (var kv in constants)
                    {
                        if (double.TryParse(kv.Value, out double _))
                        {
                            _writer.WriteLine($"{kv.Key}\t{kv.Value}\t");
                        }
                    }
                }

                // Channel blocks
                for (int i = 1; i < columns.Count; i++)
                {
                    string fullChannelName = columns[i][0] ?? string.Empty;
                    int startIdx = fullChannelName.IndexOf('[');
                    int endIdx = fullChannelName.IndexOf(']', startIdx + 1);
                    string units = (startIdx != -1 && endIdx != -1 && endIdx > startIdx)
                        ? fullChannelName.Substring(startIdx, endIdx - startIdx + 1)
                        : "[]";

                    string channelName = fullChannelName.Split('[')[0].Trim();

                    if (channelName.Equals(lapColumnName))
                    {
                        lapColIndex = i;
                    }

                    units = CleanUpUnits(units, channelName);

                    if (nameMap != null && nameMap.TryGetValue(channelName, out string mapped))
                    {
                        channelName = mapped;
                    }

                    _writer.WriteLine();
                    _writer.WriteLine("{ChannelBlock}");
                    _writer.WriteLine($"Time\t{channelName}{units}");

                    for (int j = 1; j < columns[i].Count; j++)
                    {
                        if (double.TryParse(columns[0][j], out double value))
                        {
                            _writer.WriteLine($"{value}\t{columns[i][j]}");
                        }
                        else
                        {
                            _writer.WriteLine($"{columns[0][j]}\t{columns[i][j]}");
                        }
                    }
                }

                // Event block (lap markers)
                if (lapColIndex != -1)
                {
                    // Last cell of lap column is expected to be the number of laps (int)
                    int.TryParse(columns[lapColIndex][(columns[lapColIndex].Count - 1)], out int numLaps);

                    int lapCounter = 0;
                    double[] lapMarkers = new double[Math.Max(0, numLaps)];

                    for (int j = 1; j < columns[lapColIndex].Count; j++)
                    {
                        if (j == 1) continue;

                        if (double.TryParse(columns[lapColIndex][j], out double currentLapValue) &&
                            double.TryParse(columns[lapColIndex][j - 1], out double previousLapValue))
                        {
                            if ((currentLapValue == 1) && (lapCounter == 0))
                            {
                                continue; //keep incrementing until we get to the start of the first lap
                            }

                            if (double.TryParse(columns[0][j], out double timeValue))
                            {
                                //we've found the start of a lap, now we can record the lap marker
                                if (lapCounter == 0)
                                {
                                    if (lapMarkers.Length > 0)
                                    {
                                        lapMarkers[lapCounter] = timeValue;
                                    }
                                    lapCounter++;
                                    continue;
                                }

                                //lap counter increased
                                if (currentLapValue > previousLapValue)
                                {
                                    if (lapCounter < lapMarkers.Length)
                                    {
                                        lapMarkers[lapCounter] = timeValue;
                                    }
                                    lapCounter++;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Error parsing time {columns[0][j]}");
                            }
                        }
                    }

                    if (lapMarkers.Length > 0)
                    {
                        _writer.WriteLine();
                        _writer.WriteLine("{EventBlock}");
                        _writer.WriteLine("Time\tName\tCategory\tSource\tMessage");

                        for (int idx = 0; idx < lapMarkers.Length; ++idx)
                        {
                            _writer.WriteLine($"{lapMarkers[idx]}\tEnd of lap\tToolbox Added\tDRV\tEnd of lap");
                        }
                    }
                }
            }

            public void Dispose()
            {
                _writer?.Close();
                _writer = null;
            }
        }

        static string CleanUpUnits(string units, string name)
        {
            // Replace common unit abbreviations with their full forms or symbols
            units = units.Replace("deg", "\xB0"); // Degree symbol
            units = units.Replace("\xfffd", "\xB0"); // Degree symbol
            //units = units.Replace("[\xfffd\x43]", "[\xB0\x43]"); // Degree symbol C

            units = units.Replace("[km/h]", "[kph]");
            units = units.Replace("[l]", "[ltr]");
            units = units.Replace("[g]", "[G]");

            if (units == "[C]") units = "[\xB0\x43]"; // degrees Celsius symbol
            return units;
        }

        static void Main(string[] args)
        {
            var options = Options.Parse(args);
            if (options == null) return;

            if (!File.Exists(options.Infile))
            {
                Console.WriteLine("File not found: " + options.Infile);
                return;
            }

            Dictionary<string, string> nameMap = null;
            if (!string.IsNullOrEmpty(options.NameMapPath))
            {
                if (!File.Exists(options.NameMapPath))
                {
                    Console.WriteLine("Name map file not found: " + options.NameMapPath);
                    return;
                }

                try
                {
                    nameMap = KeyValueFile.LoadDictionaryFirstWins(options.NameMapPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read name map file: " + ex.Message);
                    return;
                }
            }

            List<KeyValuePair<string, string>> outingInfo = null;
            if (!string.IsNullOrEmpty(options.OutingPath))
            {
                if (!File.Exists(options.OutingPath))
                {
                    Console.WriteLine("Outing info file not found: " + options.OutingPath);
                    return;
                }

                try
                {
                    outingInfo = KeyValueFile.LoadOrderedPairs(options.OutingPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read outing info file: " + ex.Message);
                    return;
                }
            }

            List<KeyValuePair<string, string>> constants = null;
            if (!string.IsNullOrEmpty(options.ConstantsPath))
            {
                if (!File.Exists(options.ConstantsPath))
                {
                    Console.WriteLine("Constants file not found: " + options.ConstantsPath);
                    return;
                }

                try
                {
                    constants = KeyValueFile.LoadOrderedPairs(options.ConstantsPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to read constants file: " + ex.Message);
                    return;
                }
            }

            var table = CsvTable.Load(options.Infile);
            if (table == null || table.Columns.Count == 0)
            {
                Console.WriteLine("Input file contains no data.");
                return;
            }

            // Remove existing file extension (if any) before appending ".pi.txt"
            string outfile = System.IO.Path.ChangeExtension(options.Infile, null) + ".pi.txt";

            try
            {
                using (var writer = new PiFileWriter(outfile))
                {
                    writer.Write(table.Columns, nameMap, outingInfo, constants, options.LapColumnName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to write output file: " + ex.Message);
            }
        }
    }
}
