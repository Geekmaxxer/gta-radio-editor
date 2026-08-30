using GTARadioEditor.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GTARadioEditor.Services;

public static class AudioConversionService
{
    public const int GtaSampleRate = 48_000;

    public static async Task<AudioTrack> InspectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var reader = new AudioFileReader(filePath);
            return new AudioTrack(filePath, reader.TotalTime);
        }, cancellationToken);
    }

    public static async Task<ConvertedRadioAudio> ConvertToGtaStereoPairAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => ConvertToGtaStereoPair(filePath, cancellationToken), cancellationToken);
    }

    private static ConvertedRadioAudio ConvertToGtaStereoPair(string filePath, CancellationToken cancellationToken)
    {
        using var reader = new AudioFileReader(filePath);
        ISampleProvider stereoInput = reader.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(reader),
            2 => reader,
            _ => throw new NotSupportedException($"{Path.GetFileName(filePath)} has {reader.WaveFormat.Channels} channels. Only mono or stereo sources are supported.")
        };

        var resampled = new WdlResamplingSampleProvider(stereoInput, GtaSampleRate);
        using var leftStream = new MemoryStream();
        using var rightStream = new MemoryStream();

        var samples = new float[16_384];
        var leftPcm = new byte[samples.Length];
        var rightPcm = new byte[samples.Length];
        var outputFrames = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sampleCount = resampled.Read(samples, 0, samples.Length);
            if (sampleCount == 0)
            {
                break;
            }
            if (sampleCount % 2 != 0)
            {
                throw new InvalidDataException("The audio decoder returned an incomplete stereo frame.");
            }

            var frames = sampleCount / 2;
            for (var frame = 0; frame < frames; frame++)
            {
                WritePcm16(leftPcm, frame * 2, samples[frame * 2]);
                WritePcm16(rightPcm, frame * 2, samples[(frame * 2) + 1]);
            }
            leftStream.Write(leftPcm, 0, frames * 2);
            rightStream.Write(rightPcm, 0, frames * 2);
            outputFrames += frames;
        }

        var duration = TimeSpan.FromSeconds(outputFrames / (double)GtaSampleRate);
        return new ConvertedRadioAudio(BuildMonoPcmWave(leftStream.ToArray()), BuildMonoPcmWave(rightStream.ToArray()), outputFrames, duration);
    }

    private static void WritePcm16(byte[] destination, int offset, float sample)
    {
        var clamped = Math.Clamp(sample, -1f, 1f);
        var value = (short)Math.Round(clamped * short.MaxValue, MidpointRounding.AwayFromZero);
        destination[offset] = (byte)(value & 0xFF);
        destination[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    private static byte[] BuildMonoPcmWave(byte[] pcm)
    {
        using var output = new MemoryStream(pcm.Length + 44);
        using var writer = new BinaryWriter(output);
        writer.Write("RIFF".ToCharArray());
        writer.Write(36 + pcm.Length);
        writer.Write("WAVE".ToCharArray());
        writer.Write("fmt ".ToCharArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(GtaSampleRate);
        writer.Write(GtaSampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data".ToCharArray());
        writer.Write(pcm.Length);
        writer.Write(pcm);
        writer.Flush();
        return output.ToArray();
    }
}
