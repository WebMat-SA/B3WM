using B3WM.Client.Model;
using B3WM.Shared.Entity;
using System.Numerics;

namespace B3WM.Client.Services
{
    public class StructureVolumeHelper
    {
        public event EventHandler<StructureVolumeStorageItem>? OnStructureChange;
        public event EventHandler<int>? OnQueueCount;
        public event EventHandler<string>? OnQueueTime;

        private int _queueCount { get; set; }
        private string _queueTime { get; set; }

        private PeriodicTimer? _timer;

        // 🔥 controle do filtro FFT (0.05 = detalhado | 0.2 = suave)
        private double fftStrength { get; set; } = 0.1;

        List<VolumeLevel> queue { get; set; } = new();
        string Symbol { get; set; } = string.Empty;
        DateTime Date { get; set; } = DateTime.Today;

        public void Init(int throtlingms = 5000, double _fftStrength = 0.1)
        {
            fftStrength = _fftStrength;

            _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(throtlingms));
            _ = RunLoop();

            HelperPerformanceConfig.Log(nameof(StructureVolumeHelper), nameof(Init), 0,
                $"Init => fftStrength:{fftStrength}");
        }

        private async Task RunLoop()
        {
            while (await _timer!.WaitForNextTickAsync())
            {
                var result = FindPeaksFFT();

                OnStructureChange?.Invoke(this, result);
                OnQueueCount?.Invoke(this, _queueCount);
                OnQueueTime?.Invoke(this, _queueTime);

                HelperPerformanceConfig.Log(nameof(StructureVolumeHelper), nameof(RunLoop), 0,
                    $"{System.Text.Json.JsonSerializer.Serialize(result)}");
            }
        }

        public Task Enqueu(VolumeLevelStorageItem input)
        {
            Date = input.Date.Date;
            Symbol = input.Symbol;
            queue = input.Volumes.ToList();

            _queueCount = queue.Count;
            _queueTime = DateTime.Now.ToString("HH:mm:ss");

            return Task.CompletedTask;
        }

        // =============================
        // 🔵 FFT CORE
        // =============================

        private Complex[] FFT(Complex[] buffer)
        {
            int n = buffer.Length;

            if (n <= 1)
                return buffer;

            var even = FFT(buffer.Where((x, i) => i % 2 == 0).ToArray());
            var odd = FFT(buffer.Where((x, i) => i % 2 != 0).ToArray());

            var result = new Complex[n];

            for (int k = 0; k < n / 2; k++)
            {
                var t = Complex.Exp(-2 * Math.PI * Complex.ImaginaryOne * k / n) * odd[k];
                result[k] = even[k] + t;
                result[k + n / 2] = even[k] - t;
            }

            return result;
        }

        private Complex[] IFFT(Complex[] buffer)
        {
            int n = buffer.Length;

            var conjugated = buffer.Select(Complex.Conjugate).ToArray();
            var fft = FFT(conjugated);

            return fft.Select(x => Complex.Conjugate(x) / n).ToArray();
        }

        // =============================
        // 🧠 FFT + ZONE DETECTION
        // =============================

        private StructureVolumeStorageItem FindPeaksFFT()
        {
            var input = queue.OrderBy(x => x.Price).ToList();

            if (input.Count < 10)
                return new StructureVolumeStorageItem()
                {
                    Date = Date,
                    Symbol = Symbol,
                    Peaks = new(),
                    Valleys = new()
                };

            int n = input.Count;

            int size = 1;
            while (size < n) size <<= 1;

            var complex = new Complex[size];

            double max = input.Max(x => x.Total);

            for (int i = 0; i < n; i++)
            {
                double normalized = input[i].Total / max;
                complex[i] = new Complex(normalized, 0);
            }

            var freq = FFT(complex);

            int cutoff = (int)(size * fftStrength);

            for (int i = cutoff; i < size - cutoff; i++)
                freq[i] = Complex.Zero;

            var filtered = IFFT(freq);

            var smoothed = filtered
                .Take(n)
                .Select(x => Math.Max(0, x.Real))
                .ToList();

            // 🔥 normalização pós FFT
            double maxSmooth = smoothed.Max();
            smoothed = smoothed.Select(x => x / maxSmooth).ToList();

            var peaks = DetectPeaksLocal(input, smoothed);
            var valleys = DetectValleysFinal(input, smoothed);

            // 🔥 filtro final de sanidade
            if (valleys.Count > peaks.Count)
                valleys = valleys.Take(peaks.Count).ToList();

            return new StructureVolumeStorageItem()
            {
                Date = Date,
                Symbol = Symbol,
                Peaks = peaks,
                Valleys = valleys
            };
        }

        // =============================
        // 🔧 AGRUPAMENTO FINAL
        // =============================

        private List<VolumeLevel> MergeCloseLevels(List<VolumeLevel> levels, int priceStep = 5)
        {
            var result = new List<VolumeLevel>();

            foreach (var level in levels.OrderBy(x => x.Price))
            {
                if (!result.Any())
                {
                    result.Add(level);
                    continue;
                }

                var last = result.Last();

                if (Math.Abs(level.Price - last.Price) <= priceStep)
                {
                    last.Total += level.Total;
                    last.BuyVolume += level.BuyVolume;
                    last.SellVolume += level.SellVolume;
                    //last.Delta += level.Delta;
                }
                else
                {
                    result.Add(level);
                }
            }

            return result;
        }

        // =============================
        // ⚙️ CONFIG
        // =============================

        private List<VolumeLevel> DetectPeaksLocal(List<VolumeLevel> input, List<double> smoothed)
        {
            var peaks = new List<VolumeLevel>();

            for (int i = 2; i < smoothed.Count - 2; i++)
            {
                double current = smoothed[i];

                bool isPeak =
                    current > smoothed[i - 1] &&
                    current > smoothed[i - 2] &&
                    current > smoothed[i + 1] &&
                    current > smoothed[i + 2];

                if (isPeak)
                    peaks.Add(input[i]);
            }

            // 🔥 manter só os mais relevantes
            if (peaks.Any())
            {
                double maxPeak = peaks.Max(x => x.Total);
                peaks = peaks
                    .Where(p => p.Total >= maxPeak * 0.6)
                    .ToList();
            }

            return MergeCloseLevels(peaks, 5);
        }

        private List<VolumeLevel> DetectValleysFinal(List<VolumeLevel> input, List<double> smoothed)
        {
            var valleys = new List<VolumeLevel>();

            double globalMax = smoothed.Max();

            for (int i = 3; i < smoothed.Count - 3; i++)
            {
                double left = smoothed[i - 2] + smoothed[i - 1];
                double right = smoothed[i + 1] + smoothed[i + 2];
                double center = smoothed[i];

                double avgSides = (left + right) / 2.0;

                // ✅ contraste local
                bool contrast = center < avgSides * 0.7;

                // ✅ baixo global
                bool lowGlobal = center < globalMax * 0.5;

                // 🔥 reversão obrigatória (mata tendência)
                bool reversal =
                    smoothed[i + 1] > center &&
                    smoothed[i + 2] > smoothed[i + 1];

                // 🔥 veio de cima (estrutura real)
                bool cameFromHigh =
                    smoothed[i - 1] > center &&
                    smoothed[i - 2] > smoothed[i - 1];

                if (contrast && lowGlobal && reversal && cameFromHigh)
                {
                    valleys.Add(input[i]);
                }
            }

            return MergeCloseLevels(valleys, 5);
        }

        public void SetFftStrength(double strength)
        {
            fftStrength = Math.Clamp(strength, 0.01, 0.5);
        }
    }
}