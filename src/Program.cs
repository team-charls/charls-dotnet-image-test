// Copyright (c) Team CharLS.
// SPDX-License-Identifier: BSD-3-Clause

using System.Diagnostics;
using System.Globalization;
using CharLS.Managed;

const int success = 0;
const int failure = 1;

if (args.Length == 0)
{
    Console.WriteLine("Usage: charls-dotnet-image-test <file or directory-to-test>");
    return failure;
}

try
{
    foreach (string path in args)
    {
        if (File.Exists(path))
        {
            if (!ProcessFile(path))
                return failure;
        }
        else if (Directory.Exists(path))
        {
            if (!ProcessDirectory(path))
                return failure;
        }
        else
        {
            Console.WriteLine("{0} is not a valid file or directory.", path);
            return failure;
        }
    }

    return success;
}
catch (IOException e)
{
    Console.WriteLine("Error: " + e.Message);
    return failure;
}
catch (InvalidDataException e)
{
    Console.WriteLine("Error: " + e.Message);
    return failure;
}

static bool ProcessFile(string path)
{
    if (!IsAnymapFile(path, out bool monochromeImage))
        return true;

    Console.WriteLine("Checking file: {0}", path);
    bool result = monochromeImage ? CheckMonochromeImage(path) : CheckColorImage(path);
    Console.WriteLine(" Status: {0}", result ? "Passed" : "Failed");
    return result;
}

static bool ProcessDirectory(string targetDirectory)
{
    // Process the list of files found in the directory.
    string[] fileEntries = Directory.GetFiles(targetDirectory);
    if (fileEntries.Any(fileName => !ProcessFile(fileName)))
    {
        return false;
    }

    // Recurse into subdirectories of this directory.
    string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
    return subdirectoryEntries.All(ProcessDirectory);
}

static bool IsAnymapFile(string filePath, out bool monochromeAnymap)
{
    var extension = Path.GetExtension(filePath);
    monochromeAnymap = string.Equals(extension, ".pgm", StringComparison.OrdinalIgnoreCase);
    return monochromeAnymap || string.Equals(extension, ".ppm", StringComparison.OrdinalIgnoreCase);
}

static bool CheckMonochromeImage(string path)
{
    return CheckImage(path, InterleaveMode.None);
}

static bool CheckColorImage(string path)
{
    bool result = CheckImage(path, InterleaveMode.None);
    if (!result)
        return result;

    result = CheckImage(path, InterleaveMode.Line);
    return !result ? result : CheckImage(path, InterleaveMode.Sample);
}

static bool CheckImage(string path, InterleaveMode interleaveMode)
{
    PortableAnymapFile referenceFile = ReadAnymapReferenceFile(path, interleaveMode);

    JpegLSEncoder encoder = new(
        referenceFile.Width, referenceFile.Height, referenceFile.BitsPerSample, referenceFile.ComponentCount, interleaveMode);

    Stopwatch stopwatch = new();
    stopwatch.Start();
    encoder.Encode(referenceFile.ImageData);
    stopwatch.Stop();

    File.WriteAllBytes(GenerateOutputFilename(path, interleaveMode), encoder.EncodedData.Span.ToArray());

    var compressionRatio = (referenceFile.ImageData.Length / (double)encoder.EncodedData.Length).ToString("F2", CultureInfo.InvariantCulture);
    (bool result, long elapsedMilliseconds) = TestByDecoding(encoder.EncodedData, referenceFile.ImageData);

    Console.WriteLine(" Info: original size = {0}, encoded size = {1}, interleave mode = {2}, compression ratio = {3}, encode time = {4} ms, decode time = {5} ms",
        referenceFile.ImageData.Length, encoder.EncodedData.Length, interleaveMode, compressionRatio, stopwatch.ElapsedMilliseconds, elapsedMilliseconds);

    return result;
}

static PortableAnymapFile ReadAnymapReferenceFile(string path, InterleaveMode interleaveMode)
{
    PortableAnymapFile referenceFile = new(path);

    if (interleaveMode == InterleaveMode.None && referenceFile.ComponentCount == 3)
    {
        referenceFile.ImageData = TripletToPlanar(referenceFile.ImageData, referenceFile.Width, referenceFile.Height);
    }

    return referenceFile;
}

static byte[] TripletToPlanar(byte[] tripletBuffer, int width, int height)
{
    byte[] planarBuffer = new byte[tripletBuffer.Length];

    int byteCount = width * height;
    for (int index = 0; index != byteCount; ++index)
    {
        planarBuffer[index] = tripletBuffer[(index * 3) + 0];
        planarBuffer[index + (1 * byteCount)] = tripletBuffer[(index * 3) + 1];
        planarBuffer[index + (2 * byteCount)] = tripletBuffer[(index * 3) + 2];
    }

    return planarBuffer;
}

static (bool result, long elapsedMilliseconds) TestByDecoding(ReadOnlyMemory<byte> encodedSource, ReadOnlySpan<byte> originalSource)
{
    JpegLSDecoder decoder = new(encodedSource);

    var decoded = new byte[decoder.GetDestinationSize()];

    Stopwatch stopwatch = new();
    stopwatch.Start();
    decoder.Decode(decoded);
    stopwatch.Stop();

    if (decoded.Length != originalSource.Length)
    {
        Console.WriteLine("Pixel data size doesn't match");
        return (false, stopwatch.ElapsedMilliseconds);
    }

    if (decoder.GetNearLossless() != 0)
        return (true, stopwatch.ElapsedMilliseconds);

    for (int i = 0; i < originalSource.Length; ++i)
    {
        if (decoded[i] == originalSource[i])
            continue;

        Console.WriteLine("Pixel data value doesn't match");
        return (false, stopwatch.ElapsedMilliseconds);
    }

    return (true, stopwatch.ElapsedMilliseconds);
}

static string GenerateOutputFilename(string sourceFilename, InterleaveMode interleaveMode)
{
    return Path.GetFileNameWithoutExtension(sourceFilename) + interleaveMode.ToString().ToLowerInvariant() + ".jls";
}
