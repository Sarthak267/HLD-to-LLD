using System.IO.Compression;
using System.Text;

namespace HldToLldTool.Services;

/// <summary>
/// Implements PlantUML's URL text-encoding scheme: UTF-8 bytes -> raw DEFLATE ->
/// a custom 6-bit-per-character base64-like alphabet (not standard Base64).
/// This lets us build https://www.plantuml.com/plantuml/png/{encoded} URLs
/// without shelling out to Java/Graphviz locally.
/// Reference: https://plantuml.com/en/text-encoding
/// </summary>
public static class PlantUmlEncoder
{
    private const string PlantUmlAlphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    public static string Encode(string plantUmlSource)
    {
        var utf8Bytes = Encoding.UTF8.GetBytes(plantUmlSource);
        var compressed = RawDeflate(utf8Bytes);
        return ToPlantUmlBase64(compressed);
    }

    private static byte[] RawDeflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private static string ToPlantUmlBase64(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 4 + 2) / 3);
        var i = 0;
        while (i < data.Length)
        {
            int b1 = data[i++];
            int b2 = i < data.Length ? data[i++] : 0;
            int b3 = i < data.Length ? data[i++] : 0;
            AppendThree(sb, b1, b2, b3);
        }
        return sb.ToString();
    }

    private static void AppendThree(StringBuilder sb, int b1, int b2, int b3)
    {
        var c1 = b1 >> 2;
        var c2 = ((b1 & 0x3) << 4) | (b2 >> 4);
        var c3 = ((b2 & 0xF) << 2) | (b3 >> 6);
        var c4 = b3 & 0x3F;
        sb.Append(PlantUmlAlphabet[c1 & 0x3F]);
        sb.Append(PlantUmlAlphabet[c2 & 0x3F]);
        sb.Append(PlantUmlAlphabet[c3 & 0x3F]);
        sb.Append(PlantUmlAlphabet[c4 & 0x3F]);
    }
}
