// Copyright (c) Team CharLS.
// SPDX-License-Identifier: BSD-3-Clause

using System.IO;

const int success = 0;
const int failure = 1;

if (!TryParseArguments(args, out string path))
{
    Console.WriteLine("Usage: charls-dotnet-image-test <directory-to-test>");
    return failure;
}

try
{
    foreach (string file in Directory.GetFiles(path))
    {
        if (IsAnymapFile(file))
        {

        }


        Console.WriteLine(file);
    }

    return success;
}
catch (IOException e)
{
    Console.WriteLine("Error: " + e.Message);
    return failure;
}

static bool TryParseArguments(IReadOnlyList<string> args, out string inputDirectoryArg)
{
    if (args.Count != 1)
    {
        inputDirectoryArg = string.Empty;
        return false;
    }

    inputDirectoryArg = args[0];
    return true;
}

static bool IsAnymapFile(string filePath)
{
    var extension = Path.GetExtension(filePath);
    return string.Equals(extension, ".pgm", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(extension, ".ppm", StringComparison.OrdinalIgnoreCase);
}

