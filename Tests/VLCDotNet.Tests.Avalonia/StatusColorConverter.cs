using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using VLCDotNet.Tests.Shared;

namespace VLCDotNet.Tests.Avalonia
{
    /// <summary>Maps a <see cref="TestOutcome"/> to a status colour for the results list.</summary>
    public sealed class StatusColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TestOutcome o)
            {
                if (o.Skipped)
                {
                    return Brushes.Gray;
                }

                return o.Passed ? Brushes.ForestGreen : Brushes.IndianRed;
            }

            return Brushes.Black;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
