using System;
using System.Collections.Generic;

namespace VLCDotNet.ViewTests
{
    /// <summary>The result of a single test case.</summary>
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

        public string Message { get; set; } = string.Empty;

        public List<string> Details { get; } = new List<string>();

        public List<string> Artifacts { get; } = new List<string>();

        public TimeSpan Duration { get; set; }

        /// <summary>PASS / FAIL / SKIP display token.</summary>
        public string Status => Skipped ? "SKIP" : Passed ? "PASS" : "FAIL";

        public override string ToString()
        {
            string line = $"[{Status}] {Category}/{Name} ({Duration.TotalSeconds:F1}s) - {Message}";
            return line;
        }
    }

    /// <summary>Inputs for a test run: where the media lives and where artifacts go.</summary>
    public sealed class TestEnvironment
    {
        public string MediaDirectory { get; set; } = string.Empty;

        public string OutputDirectory { get; set; } = string.Empty;

        /// <summary>Optional progress sink (per completed test).</summary>
        public Action<TestOutcome>? Progress { get; set; }
    }
}
