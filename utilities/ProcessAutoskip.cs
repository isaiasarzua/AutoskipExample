using System;
using System.Linq;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.ImageSharp.PixelFormats;

namespace AutoskipExample
{
    /// <summary>
    /// Using videoview does not allow you to use the same frame for other methods.
    /// The workaround is creating another mediaplayer to play the same video and analyze it's frames there.
    /// Obviously not an ideal or elegant approach, but it works for now as a proof of concept.
    /// </summary>
    class ProcessAutoskip
    {
        public Image<Rgba32> introScreenshot = null;

        public LibVLC libVLC = new LibVLC();
        public MediaPlayer mediaPlayer;
        Media newMedia = null;

        private const uint Width = 720;
        private const uint Height = 480;

        /// <summary>
        /// RGBA is used, so 4 byte per pixel, or 32 bits.
        /// </summary>
        private const uint BytePerPixel = 4;

        /// <summary>
        /// the number of bytes per "line"
        /// For performance reasons inside the core of VLC, it must be aligned to multiples of 32.
        /// </summary>
        private static readonly uint Pitch;

        /// <summary>
        /// The number of lines in the buffer.
        /// For performance reasons inside the core of VLC, it must be aligned to multiples of 32.
        /// </summary>
        private static readonly uint Lines;

        static ProcessAutoskip()
        {
            Pitch = Align(Width * BytePerPixel);
            Lines = Align(Height);

            uint Align(uint size)
            {
                if (size % 32 == 0)
                {
                    return size;
                }

                return ((size / 32) + 1) * 32;// Align on the next multiple of 32
            }
        }

        private static MemoryMappedFile CurrentMappedFile;
        private static MemoryMappedViewAccessor CurrentMappedViewAccessor;
        private static readonly ConcurrentQueue<(MemoryMappedFile file, MemoryMappedViewAccessor accessor)> FilesToProcess = new ConcurrentQueue<(MemoryMappedFile file, MemoryMappedViewAccessor accessor)>();

        public async Task StartGrab(string media)
        {
            if (mediaPlayer == null)
            {
                mediaPlayer = new MediaPlayer(libVLC);
                System.Diagnostics.Debug.WriteLine("no mediaplayer found, creating new one");
            }
            else
            {
                mediaPlayer.Stop();
            }

            newMedia = new Media(libVLC, media);
            newMedia.AddOption(":no-audio");

            // Listen to events
            var processingCancellationTokenSource = new CancellationTokenSource();
            mediaPlayer.Stopped += (s, e) => processingCancellationTokenSource.Cancel();

            // Set the size, format and mute here.
            mediaPlayer.SetVideoFormat("RV32", Width, Height, Pitch);
            mediaPlayer.SetVideoCallbacks(Lock, null, Display);

            // Start recording
            mediaPlayer.Play(newMedia);

            newMedia.Dispose();

            // Waits for the processing to stop
            try
            {
                await CompareFrames(processingCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            { }
        }

        #region Event
        public delegate void Notify();
        /// <summary>
        /// Called when comparer finds a match.
        /// </summary>
        public event Notify MatchFound;
        #endregion

        readonly ImageComparer exactComparer = ImageComparer.Exact;

        private async Task CompareFrames(CancellationToken token)
        {
            var frameNumber = 0;
            while (!token.IsCancellationRequested)
            {
                if (FilesToProcess.TryDequeue(out var file))
                {
                    using (var image = new Image<Rgba32>((int)(Pitch / BytePerPixel), (int)Lines))
                    using (var sourceStream = file.file.CreateViewStream())
                    {
                        sourceStream.Read(MemoryMarshal.AsBytes(image.GetPixelMemoryGroup().Single().Span));

                        ImageSimilarityReport exactReport = exactComparer.CompareImagesOrFrames(image, introScreenshot);

                        System.Diagnostics.Debug.WriteLine("-----------------------------------------");

                        System.Diagnostics.Debug.WriteLine($"Frame {frameNumber:0000} " + $"difference: {exactReport.TotalNormalizedDifference}");

                        if (exactReport.TotalNormalizedDifference < 0.05)
                        {
                            System.Diagnostics.Debug.WriteLine($"Found match: {frameNumber:0000} is a match! Shutting down autoskip.");
                            MatchFound();
                            file.accessor.Dispose();
                            file.file.Dispose();
                            mediaPlayer.Stop();
                        }

                    }
                    file.accessor.Dispose();
                    file.file.Dispose();
                    frameNumber++;
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1), token);
                }
            }
        }

        private static IntPtr Lock(IntPtr opaque, IntPtr planes)
        {
            CurrentMappedFile = MemoryMappedFile.CreateNew(null, Pitch * Lines);
            CurrentMappedViewAccessor = CurrentMappedFile.CreateViewAccessor();
            Marshal.WriteIntPtr(planes, CurrentMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle());
            return IntPtr.Zero;
        }

        private static long FrameCounter = 0;

        private static void Display(IntPtr opaque, IntPtr picture)
        {
            if (FrameCounter % 2 == 0)
            {
                FilesToProcess.Enqueue((CurrentMappedFile, CurrentMappedViewAccessor));
                CurrentMappedFile = null;
                CurrentMappedViewAccessor = null;
            }
            else
            {
                CurrentMappedViewAccessor.Dispose();
                CurrentMappedFile.Dispose();
                CurrentMappedFile = null;
                CurrentMappedViewAccessor = null;
            }
            FrameCounter++;
        }
    }
}