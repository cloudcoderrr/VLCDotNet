using System;
using System.Collections.Generic;

namespace VLCDotNet.Tests.Shared
{
    /// <summary>Result of a single test case.</summary>
    public sealed class TestOutcome
    {
        public TestOutcome(string category, string name)
        {
            Category = category;
            Name = name;
        }

        public string Category { get; }

        public string Name { get; }

        public bool Passed { get; set; }

        public bool Skipped { get; set; }

        public string? Message { get; set; }

        public TimeSpan Duration { get; set; }

        /// <summary>Human-readable measurements (codec names, RMS, colour %, etc.).</summary>
        public List<string> Details { get; } = new List<string>();

        /// <summary>Paths of files produced by the test (snapshots, audio, logs).</summary>
        public List<string> Artifacts { get; } = new List<string>();

        public string Status => Skipped ? "SKIP" : Passed ? "PASS" : "FAIL";

        public override string ToString() => $"[{Status}] {Category} / {Name}: {Message}";
    }
}
