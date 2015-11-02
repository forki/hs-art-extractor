﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HearthstoneDisunityCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                PrintUsageAndExit();
            }

            var command = args[0];
            var fileDir = args[1];
            var outDir = args[2];

            switch (command.ToLower())
            {
                case "extract":
                    Extract(fileDir, outDir);
                    break;
                case "cardart":
                    CardArt(fileDir, outDir);
                    break;
                default:
                    PrintUsageAndExit();
                    break;
            }
        }

        private static void CardArt(string hsDir, string outDir)
        {
            Console.WriteLine("Extracting Card Art from {0} to {1}", hsDir, outDir);
        }

        private static void Extract(string file, string outDir)
        {
            Console.WriteLine("Extracting Assets from {0} to {1}", file, outDir);
        }

        private static bool IsValidFile(string file)
        {
            return false;
        }

        private static bool IsValidDir(string dir)
        {
            return false;
        }

        private static void PrintErrorAndExit(string message)
        {
            Console.WriteLine("Error: " + message);
            Environment.Exit(1);
        }

        private static void PrintUsageAndExit()
        {
            Console.WriteLine("Usage: ");
            Environment.Exit(1);
        }
    }
}
