using GTARadioEditor.Models;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GTARadioEditor.Services;

public static class AudioConversionService
{
    public const int GtaSampleRate = 48_000;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".wma", ".ogg"
    };

    public static bool IsSupportedFile(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    public static async Task<AudioTrack> InspectAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var audio = OpenAudio(filePath);
            return new AudioTrack(filePath, audio.TotalTime);
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
        using var audio = OpenAudio(filePath);
        ISampleProvider stereoInput = audio.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(audio.Samples),
            2 => audio.Samples,
            _ => throw new NotSupportedException($"{Path.GetFileName(filePath)} has {audio.WaveFormat.Channels} channels. Only mono or stereo sources are supported.")
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

    private static DecodedAudio OpenAudio(string filePath)
    {
        if (!IsSupportedFile(filePath))
        {
            throw new NotSupportedException($"{Path.GetExtension(filePath)} is not a supported audio format.");
        }

        if (Path.GetExtension(filePath).Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var reader = new VorbisWaveReader(filePath);
                return new DecodedAudio(reader, reader.ToSampleProvider(), reader.WaveFormat, reader.TotalTime);
            }
            catch (Exception vorbisException)
            {
                throw new InvalidDataException(
                    $"Could not decode {Path.GetFileName(filePath)} as Ogg Vorbis. Only Ogg Vorbis audio is supported for .ogg files.",
                    vorbisException);
            }
        }

        try
        {
            var reader = new AudioFileReader(filePath);
            return new DecodedAudio(reader, reader, reader.WaveFormat, reader.TotalTime);
        }
        catch (Exception exception) when (UsesWindowsMediaFoundationCodec(filePath))
        {
            throw new InvalidDataException(
                $"Could not decode {Path.GetFileName(filePath)}. This format uses Windows Media Foundation; install the Windows Media Feature Pack if this Windows installation does not include its media codecs.",
                exception);
        }
    }

    private static bool UsesWindowsMediaFoundationCodec(string filePath) =>
        Path.GetExtension(filePath).Equals(".flac", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(filePath).Equals(".aac", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(filePath).Equals(".m4a", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(filePath).Equals(".wma", StringComparison.OrdinalIgnoreCase);

    private sealed class DecodedAudio : IDisposable
    {
        private readonly IDisposable _reader;

        public DecodedAudio(IDisposable reader, ISampleProvider samples, WaveFormat waveFormat, TimeSpan totalTime)
        {
            _reader = reader;
            Samples = samples;
            WaveFormat = waveFormat;
            TotalTime = totalTime;
        }

        public ISampleProvider Samples { get; }
        public WaveFormat WaveFormat { get; }
        public TimeSpan TotalTime { get; }

        public void Dispose() => _reader.Dispose();
    }

    private static void WritePcm16(byte[] destination, int offset, float sample)
    {
        var clamped = PlatformCompatibility.Clamp(sample, -1f, 1f);
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
internal static class PlatformCompatibility
{
    public static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    public static float Clamp(float value, float minimum, float maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    public static string GetRelativePath(string relativeTo, string path)
    {
#if NET48
        var basePath = EnsureTrailingSeparator(Path.GetFullPath(relativeTo));
        var relativeUri = new Uri(basePath).MakeRelativeUri(new Uri(Path.GetFullPath(path)));
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
#else
        return Path.GetRelativePath(relativeTo, path);
#endif
    }

#if NET48
    private const int EmSetCueBanner = 0x1501;

    public static void SetCueBanner(TextBox textBox, string text)
    {
        SendMessage(textBox.Handle, EmSetCueBanner, IntPtr.Zero, text);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr wParam, string lParam);
#endif
    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? path
            : path + Path.DirectorySeparatorChar;
}