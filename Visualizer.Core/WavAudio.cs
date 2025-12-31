using System.Text;

namespace Visualizer.Core;

public sealed record WavAudio(float[] Samples, int SampleRate, int Channels)
{
    public int TotalSamplesPerChannel => Samples.Length / Channels;

    public double DurationSeconds => TotalSamplesPerChannel / (double)SampleRate;

    public static WavAudio Load(string path)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static WavAudio Load(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        var riff = ReadChunkId(reader, "RIFF header");
        if (!string.Equals(riff, "RIFF", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Invalid RIFF header.");
        }

        _ = reader.ReadInt32(); // file size (unused)

        var wave = ReadChunkId(reader, "WAVE header");
        if (!string.Equals(wave, "WAVE", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Invalid WAVE header.");
        }

        bool fmtFound = false;
        bool dataFound = false;

        ushort channels = 0;
        uint sampleRate = 0;
        ushort bitsPerSample = 0;
        ushort blockAlign = 0;

        byte[]? dataBytes = null;

        while (TryReadChunkHeader(reader, out var chunkId, out var chunkSize))
        {
            switch (chunkId)
            {
                case "fmt ":
                    ParseFmtChunk(reader, chunkSize, ref fmtFound, out channels, out sampleRate, out bitsPerSample, out blockAlign);
                    break;
                case "data":
                    dataBytes = ReadBytesExact(reader, chunkSize, "data chunk");
                    dataFound = true;
                    break;
                default:
                    SkipBytes(reader, chunkSize);
                    break;
            }

            if ((chunkSize & 1) == 1)
            {
                SkipBytes(reader, 1); // padding byte
            }
        }

        if (!fmtFound)
        {
            throw new InvalidDataException("Missing fmt chunk.");
        }

        if (!dataFound || dataBytes is null)
        {
            throw new InvalidDataException("Missing data chunk.");
        }

        if (sampleRate == 0 || sampleRate > int.MaxValue)
        {
            throw new InvalidDataException($"Unsupported sample rate: {sampleRate}.");
        }

        if (dataBytes.Length % blockAlign != 0)
        {
            throw new InvalidDataException("Data chunk size is not aligned to sample frames.");
        }

        var totalFrames = dataBytes.Length / blockAlign;
        var totalSamples = totalFrames * channels;
        var samples = new float[totalSamples];

        const float scale = 1f / 32768f;

        for (int frame = 0; frame < totalFrames; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                var offset = (frame * blockAlign) + (channel * 2);
                var sample = (short)(dataBytes[offset] | (dataBytes[offset + 1] << 8));
                samples[(frame * channels) + channel] = sample * scale;
            }
        }

        return new WavAudio(samples, (int)sampleRate, channels);
    }

    private static void ParseFmtChunk(BinaryReader reader, int chunkSize, ref bool fmtFound, out ushort channels, out uint sampleRate, out ushort bitsPerSample, out ushort blockAlign)
    {
        if (fmtFound)
        {
            throw new InvalidDataException("Multiple fmt chunks are not supported.");
        }

        if (chunkSize < 16)
        {
            throw new InvalidDataException("Invalid fmt chunk size.");
        }

        var audioFormat = reader.ReadUInt16();
        channels = reader.ReadUInt16();
        sampleRate = reader.ReadUInt32();
        var byteRate = reader.ReadUInt32();
        blockAlign = reader.ReadUInt16();
        bitsPerSample = reader.ReadUInt16();

        var remaining = chunkSize - 16;
        if (remaining > 0)
        {
            SkipBytes(reader, remaining);
        }

        if (audioFormat != 1)
        {
            throw new NotSupportedException("Only PCM WAV files are supported.");
        }

        if (channels is not 1 and not 2)
        {
            throw new NotSupportedException($"Unsupported channel count: {channels}.");
        }

        if (bitsPerSample != 16)
        {
            throw new NotSupportedException($"Unsupported bits per sample: {bitsPerSample}. Only 16-bit PCM is supported.");
        }

        var expectedBlockAlign = (ushort)(channels * (bitsPerSample / 8));
        if (blockAlign != expectedBlockAlign)
        {
            throw new InvalidDataException($"Unexpected block align: {blockAlign}. Expected {expectedBlockAlign}.");
        }

        var expectedByteRate = sampleRate * expectedBlockAlign;
        if (byteRate != expectedByteRate)
        {
            throw new InvalidDataException($"Unexpected byte rate: {byteRate}. Expected {expectedByteRate}.");
        }

        fmtFound = true;
    }

    private static bool TryReadChunkHeader(BinaryReader reader, out string chunkId, out int chunkSize)
    {
        var idBytes = reader.ReadBytes(4);
        if (idBytes.Length == 0)
        {
            chunkId = string.Empty;
            chunkSize = 0;
            return false;
        }

        if (idBytes.Length < 4)
        {
            throw new InvalidDataException("Unexpected end of stream while reading chunk id.");
        }

        chunkId = Encoding.ASCII.GetString(idBytes);
        if (reader.BaseStream.Position + 4 > reader.BaseStream.Length)
        {
            throw new InvalidDataException($"Unexpected end of stream while reading size for chunk '{chunkId}'.");
        }

        chunkSize = reader.ReadInt32();
        if (chunkSize < 0)
        {
            throw new InvalidDataException($"Invalid chunk size {chunkSize} for chunk '{chunkId}'.");
        }

        return true;
    }

    private static string ReadChunkId(BinaryReader reader, string context)
    {
        var idBytes = reader.ReadBytes(4);
        if (idBytes.Length < 4)
        {
            throw new InvalidDataException($"Unexpected end of stream while reading {context}.");
        }

        return Encoding.ASCII.GetString(idBytes);
    }

    private static void SkipBytes(BinaryReader reader, int count)
    {
        if (count == 0)
        {
            return;
        }

        var skipped = reader.ReadBytes(count);
        if (skipped.Length < count)
        {
            throw new EndOfStreamException("Unexpected end of stream while skipping bytes.");
        }
    }

    private static byte[] ReadBytesExact(BinaryReader reader, int count, string context)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length < count)
        {
            throw new EndOfStreamException($"Unexpected end of stream while reading {context}.");
        }

        return bytes;
    }
}
