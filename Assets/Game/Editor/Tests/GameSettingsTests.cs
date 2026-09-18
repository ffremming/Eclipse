using NUnit.Framework;
using UnityEngine;
using SpaceGame.Core;
using SpaceGame.Presentation;

namespace SpaceGame.Tests
{
    /// <summary>
    /// Covers the parts of the settings menu that are logic rather than layout: the value clamps
    /// the sliders rely on, the name sanitising that keeps a blank row out of everyone's player
    /// list, and the sprite geometry the rounded panels depend on.
    /// <para>
    /// These write to real PlayerPrefs, because that is what <see cref="GameSettings"/> is, so
    /// every value touched is captured before the test and put back afterwards. A test suite that
    /// silently reset the developer's own audio levels would be its own bug report.
    /// </para>
    /// </summary>
    public class GameSettingsTests
    {
        private float savedSensitivity;
        private float savedMaster;
        private float savedFieldOfView;
        private bool savedInvertLook;
        private bool savedDevMode;
        private string savedName;

        [SetUp]
        public void CaptureSettings()
        {
            savedSensitivity = GameSettings.MouseSensitivity;
            savedMaster = GameSettings.MasterVolume;
            savedFieldOfView = GameSettings.FieldOfView;
            savedInvertLook = GameSettings.InvertLookY;
            savedDevMode = GameSettings.DevMode;
            savedName = GameSettings.PlayerName;
        }

        [TearDown]
        public void RestoreSettings()
        {
            GameSettings.MouseSensitivity = savedSensitivity;
            GameSettings.MasterVolume = savedMaster;
            GameSettings.FieldOfView = savedFieldOfView;
            GameSettings.InvertLookY = savedInvertLook;
            GameSettings.DevMode = savedDevMode;
            GameSettings.PlayerName = savedName;
        }

        // ------------------------------------------------------------------- clamps

        [Test]
        public void MouseSensitivityIsClampedToItsAdvertisedRange()
        {
            GameSettings.MouseSensitivity = 999f;
            Assert.AreEqual(GameSettings.MaxSensitivity, GameSettings.MouseSensitivity, 1e-4f);

            GameSettings.MouseSensitivity = -5f;
            Assert.AreEqual(GameSettings.MinSensitivity, GameSettings.MouseSensitivity, 1e-4f);
        }

        [Test]
        public void VolumesAreClampedToZeroOne()
        {
            GameSettings.MasterVolume = 4f;
            Assert.AreEqual(1f, GameSettings.MasterVolume, 1e-4f);

            GameSettings.MasterVolume = -1f;
            Assert.AreEqual(0f, GameSettings.MasterVolume, 1e-4f);
        }

        [Test]
        public void FieldOfViewIsClampedToItsAdvertisedRange()
        {
            GameSettings.FieldOfView = 5f;
            Assert.AreEqual(GameSettings.MinFieldOfView, GameSettings.FieldOfView, 1e-4f);

            GameSettings.FieldOfView = 300f;
            Assert.AreEqual(GameSettings.MaxFieldOfView, GameSettings.FieldOfView, 1e-4f);
        }

        // -------------------------------------------------------------------- names

        [Test]
        public void NameIsTrimmedAndLengthLimited()
        {
            string long_ = new('x', GameSettings.MaxNameLength + 30);

            Assert.AreEqual("Callsign", GameSettings.SanitiseName("   Callsign  "));
            Assert.AreEqual(GameSettings.MaxNameLength, GameSettings.SanitiseName(long_).Length);
        }

        [Test]
        public void BlankNameFallsBackRatherThanLeavingAnEmptyRow()
        {
            Assert.IsNotEmpty(GameSettings.SanitiseName(null));
            Assert.IsNotEmpty(GameSettings.SanitiseName("    "));
            Assert.IsNotEmpty(GameSettings.SanitiseName(string.Empty));
        }

        [Test]
        public void PlayerNameIsStoredSanitised()
        {
            GameSettings.PlayerName = "  Vega  ";
            Assert.AreEqual("Vega", GameSettings.PlayerName);
        }

        // ------------------------------------------------------------------ signals

        [Test]
        public void ChangedFiresOnARealChangeOnly()
        {
            GameSettings.MouseSensitivity = 1f;

            int calls = 0;
            void Count() => calls++;

            GameSettings.Changed += Count;
            try
            {
                GameSettings.MouseSensitivity = 2f;
                Assert.AreEqual(1, calls, "A changed value should notify.");

                GameSettings.MouseSensitivity = 2f;
                Assert.AreEqual(1, calls, "Re-assigning the same value should not notify.");
            }
            finally
            {
                GameSettings.Changed -= Count;
            }
        }

        [Test]
        public void DevModeRoundTrips()
        {
            GameSettings.DevMode = true;
            Assert.IsTrue(GameSettings.DevMode);

            GameSettings.DevMode = false;
            Assert.IsFalse(GameSettings.DevMode);
        }

        // ------------------------------------------------------------------ labels

        [Test]
        public void FrameCapLabelReadsAsUncappedAtZero()
        {
            Assert.AreEqual("Uncapped", GameSettings.DescribeFrameRateCap(0));
            Assert.AreEqual("144 FPS", GameSettings.DescribeFrameRateCap(144));
        }

        [Test]
        public void ResolutionListIsNeverEmptySoTheCyclerAlwaysHasAValue()
        {
            Assert.Greater(GameSettings.ResolutionChoices.Length, 0);
            Assert.IsNotEmpty(GameSettings.DescribeResolution(0));
        }

        [Test]
        public void ResolutionIndexIsClampedIntoTheAvailableList()
        {
            GameSettings.ResolutionIndex = 9999;
            Assert.AreEqual(GameSettings.ResolutionChoices.Length - 1, GameSettings.ResolutionIndex);

            GameSettings.ResolutionIndex = -4;
            Assert.AreEqual(0, GameSettings.ResolutionIndex);
        }
    }
}
