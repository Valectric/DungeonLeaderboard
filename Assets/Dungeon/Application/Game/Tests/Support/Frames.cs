using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MooseRunner;
using NUnit.Framework;
using UnityEngine;

namespace Dungeon.Game.Tests
{
    /// <summary>
    /// Photographs the composited frame, interface included, for a person to read.
    /// </summary>
    /// <remarks>
    /// One copy, because five test classes had grown their own. The project's doctrine is that
    /// composition faults — two correct things drawn in the same place — are invisible to assertions
    /// and obvious in a picture, so several fixtures capture frames; there is no reason each needed
    /// its own directory constant and its own encode-and-write.
    /// </remarks>
    public static class Frames
    {
        /// <summary>Where frames are written to be looked at.</summary>
        /// <remarks>
        /// <b>Overwritten by every run.</b> CLAUDE.md carries a page about an investigation lost to
        /// exactly that: two "pale bands" were measured out of a screenshot across several turns
        /// while the suite kept rewriting it underneath, and the finding was not reproducible. Copy a
        /// PNG out under a unique name before analysing it across more than one step.
        /// </remarks>
        public static string Directory =>
            Path.Combine(UnityEngine.Application.dataPath, "..", "Screenshots");

        /// <summary>
        /// Captures the screen to a PNG and asserts it landed.
        /// </summary>
        /// <param name="name">File name stem, without extension.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The awaitable capture.</returns>
        public static async UniTask Capture(string name, CancellationToken ct)
        {
            await UniTask.WaitForEndOfFrame(ct);

            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.Directory.CreateDirectory(Directory);
            string path = Path.Combine(Directory, $"{name}.png");
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            MooseRunnerFacade.Log($"captured {path}");
            Assert.IsTrue(File.Exists(path), $"{name} was not written to disk");
        }
    }
}
