using System;
using System.IO;
using System.Text;
using CabLib;

namespace PtpTool
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: PtpUnpack.exe <ptp-file> <output-folder>");
                return 1;
            }

            string ptpFile = args[0];
            string outputDir = args[1];

            if (!File.Exists(ptpFile))
            {
                Console.WriteLine("File not found: " + ptpFile);
                return 2;
            }

            if (!outputDir.EndsWith("\\"))
            {
                outputDir += "\\";
            }

            Directory.CreateDirectory(outputDir);

            Console.WriteLine("Unpacking: " + ptpFile);
            Console.WriteLine("Destination: " + outputDir);

            try
            {
                Extract extract = new Extract();
                // PMDG internal Blowfish encryption key for cabinet archives
                extract.SetDecryptionKey(Encoding.ASCII.GetBytes("PMDG_SecurityCode"));

                int count = 0;
                extract.evBeforeCopyFile += (info) =>
                {
                    count++;
                    Console.WriteLine("  Extracting: " + info.s_RelPath + " (" + info.s32_Size + " bytes)");
                    return true;
                };

                extract.ExtractFile(ptpFile, outputDir);
                Console.WriteLine("Done! Extracted " + count + " files.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("Inner: " + ex.InnerException.Message);
                }
                return 3;
            }
        }
    }
}
