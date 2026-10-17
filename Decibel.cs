using System.Globalization;
using System.Text.Json;
using NAudio.CoreAudioApi;
using NAudio.Wave;

internal static partial class Program
{
    private static async Task<int> RunDecibel(string[] args)
    {
        var calibrating = args.Length == 2 && args[0].Equals("calibrate", StringComparison.OrdinalIgnoreCase);
        var raw = args.Length == 1 && args[0].Equals("--raw", StringComparison.OrdinalIgnoreCase);
        double reference = 0;
        if (!(args.Length == 0 || calibrating || raw) ||
            (calibrating && (!double.TryParse(args[1], NumberStyles.Float,
                CultureInfo.InvariantCulture, out reference) || !double.IsFinite(reference) ||
                reference is < 0 or > 140)))
        {
            Console.Error.WriteLine("사용법: tool decibel [--raw | calibrate <기준 dB SPL>]");
            return 1;
        }

        using var devices = new MMDeviceEnumerator();
        MMDevice microphone;
        try
        {
            microphone = devices.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"기본 마이크를 찾을 수 없습니다: {ex.Message}");
            return 1;
        }

        using (microphone)
        using (var capture = new WasapiCapture(microphone))
        using (var cancellation = new CancellationTokenSource())
        {
            var calibrationPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Tool", "decibel-calibration.json");
            Dictionary<string, double> calibrations;
            try
            {
                calibrations = File.Exists(calibrationPath)
                    ? JsonSerializer.Deserialize<Dictionary<string, double>>(File.ReadAllText(calibrationPath)) ?? new()
                    : new();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"보정값을 읽을 수 없습니다: {ex.Message}");
                return 1;
            }
            if (!raw && !calibrating && !calibrations.ContainsKey(microphone.ID))
            {
                Console.WriteLine("이 마이크의 dB SPL 보정값이 없습니다.");
                Console.WriteLine("기준 소음계의 측정값으로 'tool decibel calibrate <dB SPL>'을 실행하세요.");
                Console.WriteLine("보정 전 디지털 입력 레벨은 'tool decibel --raw'로 볼 수 있습니다.");
                return 1;
            }

            var sync = new object();
            double sumOfSquares = 0;
            long sampleCount = 0;
            var format = capture.WaveFormat;
            var isFloat = format.Encoding == WaveFormatEncoding.IeeeFloat ||
                (format is WaveFormatExtensible extensible &&
                 extensible.SubFormat == new Guid("00000003-0000-0010-8000-00aa00389b71"));

            if (format.BitsPerSample is not (16 or 24 or 32) ||
                (isFloat && format.BitsPerSample != 32) ||
                format.Encoding is not (WaveFormatEncoding.IeeeFloat or WaveFormatEncoding.Pcm or WaveFormatEncoding.Extensible))
            {
                Console.Error.WriteLine($"지원하지 않는 마이크 형식: {format.Encoding}");
                return 1;
            }

            capture.DataAvailable += (_, e) =>
            {
                double blockSum = 0;
                long blockCount = 0;
                var bytesPerSample = format.BitsPerSample / 8;
                for (var offset = 0; offset + bytesPerSample <= e.BytesRecorded; offset += bytesPerSample)
                {
                    double sample = isFloat
                        ? BitConverter.ToSingle(e.Buffer, offset)
                        : format.BitsPerSample switch
                        {
                            16 => BitConverter.ToInt16(e.Buffer, offset) / 32768.0,
                            24 => ((e.Buffer[offset] | e.Buffer[offset + 1] << 8 |
                                e.Buffer[offset + 2] << 16) << 8 >> 8) / 8388608.0,
                            32 => BitConverter.ToInt32(e.Buffer, offset) / 2147483648.0,
                            _ => 0
                        };
                    blockSum += sample * sample;
                    blockCount++;
                }
                lock (sync)
                {
                    sumOfSquares += blockSum;
                    sampleCount += blockCount;
                }
            };

            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            var started = false;
            try
            {
                capture.StartRecording();
                started = true;
                Console.WriteLine($"마이크: {microphone.FriendlyName}");
                Console.WriteLine(calibrating ? "기준 소리를 3초간 일정하게 유지하세요..." :
                    raw ? "디지털 입력 레벨 (dBFS, 종료: Ctrl+C)" :
                    "추정 소음 수준 (dB SPL, 종료: Ctrl+C)");
                double calibrationSum = 0;
                long calibrationCount = 0;
                while (!cancellation.IsCancellationRequested)
                {
                    await Task.Delay(250, cancellation.Token);
                    double sum;
                    long count;
                    lock (sync)
                    {
                        sum = sumOfSquares;
                        count = sampleCount;
                        sumOfSquares = 0;
                        sampleCount = 0;
                    }
                    var level = count == 0 || sum == 0 ? double.NegativeInfinity :
                        20 * Math.Log10(Math.Sqrt(sum / count));
                    if (calibrating)
                    {
                        calibrationSum += sum;
                        calibrationCount += count;
                        if (calibrationCount >= format.SampleRate * format.Channels * 3L)
                        {
                            if (calibrationSum == 0)
                            {
                                Console.Error.WriteLine("마이크 입력이 감지되지 않아 보정할 수 없습니다.");
                                return 1;
                            }
                            var measured = 20 * Math.Log10(Math.Sqrt(calibrationSum / calibrationCount));
                            calibrations[microphone.ID] = reference - measured;
                            Directory.CreateDirectory(Path.GetDirectoryName(calibrationPath)!);
                            File.WriteAllText(calibrationPath, JsonSerializer.Serialize(calibrations));
                            Console.WriteLine($"보정 완료. 기준 {reference:F1} dB SPL, 마이크 {measured:F1} dBFS");
                            Console.WriteLine("이제 'tool decibel'로 측정할 수 있습니다.");
                            return 0;
                        }
                    }
                    else
                    {
                        var shown = raw ? level : level + calibrations[microphone.ID];
                        Console.Write($"\r{(double.IsNegativeInfinity(shown) ? "  -∞" : $"{shown,6:F1}")} {(raw ? "dBFS" : "dB SPL")}   ");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\n마이크 입력을 시작할 수 없습니다: {ex.Message}");
                return 1;
            }
            finally
            {
                if (started) capture.StopRecording();
                Console.WriteLine();
            }
        }
        return 0;
    }
}
