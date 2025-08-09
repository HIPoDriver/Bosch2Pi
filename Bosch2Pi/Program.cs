using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bosch2Pi
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Check argumants and open files
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: Bosch2Pi <data_file>");
                return;
            }
            if (!File.Exists(args[0]))
            {
                Console.WriteLine("File not found: " + args[0]);
                return;
            }

            string infile = args[0];
            string outfile = args[0] + ".pi.txt";


            // Read all lines from the CSV file
            var lines = File.ReadAllLines(infile)
                .Where(line => !line.TrimStart().StartsWith("#"))
                .ToArray();

            // Split each line into fields
            var rows = lines.Select(line => line.Split('\t')).ToList();

            // Transpose rows to columns
            int columnCount = rows[0].Length;
            var columns = new List<List<string>>();

            for (int col = 0; col < columnCount; col++)
            {
                var column = new List<string>();
                foreach (var row in rows)
                {
                    column.Add(row[col]);
                }
                columns.Add(column);
            }

            // Write the Pi file header
            var writer = new StreamWriter(outfile, false, Encoding.GetEncoding(1252));      //Important to use the 1252 encoding to match Pi Toolbox ASCII format      

            // File header
            writer.WriteLine("PiToolboxVersionedASCIIDataSet");
            writer.WriteLine("Version\t2");
            writer.WriteLine();
            writer.WriteLine("{OutingInformation}");
            writer.WriteLine($"CarName\tBosch");
            writer.WriteLine("FirstLapNumber\t0");

            // Cycle through and create channel blocks
            for (int i = 1; i < columns.Count; i++)
            {
                writer.WriteLine();
                writer.WriteLine("{ChannelBlock}");
                writer.WriteLine($"Time\t{columns[i][0]}");

                for (int j = 1; j < columns[i].Count; j++)
                {
                    if (double.TryParse(columns[0][j], out double value))
                    {
                        writer.WriteLine($"{value}\t{columns[i][j]}");
                    }
                    else
                    {
                        // Handle non-numeric data (optional)
                        writer.WriteLine($"{columns[0][j]}\t{columns[i][j]}");
                    }

                }
            }

            writer.Close();
        }
    }
}
