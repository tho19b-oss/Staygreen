using System;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    /// <summary>Rechenregeln des Datumsfelds (Auto-Stopp "Einmalig"): Grenzen, Blaettern, Monatsblatt des Kalenders.</summary>
    public class DateEntryTests
    {
        static DateEntry From(DateTime minimum, DateTime value)
        {
            var entry = new DateEntry { Minimum = minimum };
            entry.SetValue(value);
            return entry;
        }

        [Fact]
        public void SetValue_DropsTimeOfDay_AndRespectsMinimum()
        {
            DateEntry entry = From(new DateTime(2026, 10, 7), new DateTime(2026, 10, 9, 17, 30, 0));
            Assert.Equal(new DateTime(2026, 10, 9), entry.Value);

            entry.SetValue(new DateTime(2026, 10, 1));
            Assert.Equal(new DateTime(2026, 10, 7), entry.Value);
        }

        [Fact]
        public void RaisingMinimum_MovesAnEarlierValueUp()
        {
            DateEntry entry = From(DateEntry.MinSupported, new DateTime(2026, 10, 5));
            entry.Minimum = new DateTime(2026, 10, 7, 12, 0, 0);
            Assert.Equal(new DateTime(2026, 10, 7), entry.Minimum);
            Assert.Equal(new DateTime(2026, 10, 7), entry.Value);
        }

        [Fact]
        public void AddDays_StopsAtTheLimits_WithoutOverflow()
        {
            DateEntry entry = From(new DateTime(2026, 10, 7), new DateTime(2026, 10, 8));
            entry.AddDays(-1);
            Assert.Equal(new DateTime(2026, 10, 7), entry.Value);
            Assert.False(entry.CanAddDays(-1));
            Assert.True(entry.CanAddDays(1));

            entry.AddDays(int.MaxValue);
            Assert.Equal(DateEntry.MaxSupported, entry.Value);
            Assert.False(entry.CanAddDays(1));
            entry.AddDays(int.MinValue);
            Assert.Equal(new DateTime(2026, 10, 7), entry.Value);
        }

        [Fact]
        public void AddDays_CrossesMonthsAndYears()
        {
            DateEntry entry = From(DateEntry.MinSupported, new DateTime(2026, 12, 31));
            Assert.Equal(new DateTime(2027, 1, 1), entry.AddDays(entry.Value, 1));
            Assert.Equal(new DateTime(2026, 12, 24), entry.AddDays(entry.Value, -7));
        }

        [Fact]
        public void AddMonths_KeepsTheDay_OrTakesTheLastDayOfTheMonth()
        {
            var entry = new DateEntry();
            Assert.Equal(new DateTime(2027, 2, 28), entry.AddMonths(new DateTime(2027, 1, 31), 1));
            Assert.Equal(new DateTime(2028, 2, 29), entry.AddMonths(new DateTime(2028, 1, 31), 1));
            Assert.Equal(new DateTime(2026, 9, 30), entry.AddMonths(new DateTime(2026, 10, 31), -1));
            Assert.Equal(new DateTime(2027, 1, 9), entry.AddMonths(new DateTime(2026, 12, 9), 1));
        }

        [Fact]
        public void AddMonths_StopsAtTheLimits()
        {
            DateEntry entry = From(new DateTime(2026, 10, 7), new DateTime(2026, 10, 9));
            Assert.Equal(new DateTime(2026, 10, 7), entry.AddMonths(entry.Value, -1));
            Assert.Equal(new DateTime(9998, 12, 9), entry.AddMonths(entry.Value, int.MaxValue));   // der Tag bleibt
            Assert.Equal(new DateTime(2026, 10, 7), entry.AddMonths(entry.Value, int.MinValue));
        }

        [Fact]
        public void HasSelectableDays_IsFalseForMonthsBeforeTheMinimum()
        {
            DateEntry entry = From(new DateTime(2026, 10, 31), new DateTime(2026, 11, 2));
            Assert.False(entry.HasSelectableDays(new DateTime(2026, 9, 15)));
            Assert.True(entry.HasSelectableDays(new DateTime(2026, 10, 1)));   // nur der 31.
            Assert.True(entry.HasSelectableDays(new DateTime(2026, 11, 1)));
            Assert.False(entry.IsSelectable(new DateTime(2026, 10, 30)));
            Assert.True(entry.IsSelectable(new DateTime(2026, 10, 31, 23, 0, 0)));
        }

        [Theory]
        [InlineData(2026, 10, 2026, 9, 28)]    // beginnt am Donnerstag
        [InlineData(2026, 6, 2026, 6, 1)]      // beginnt am Montag
        [InlineData(2026, 11, 2026, 10, 26)]   // beginnt am Sonntag
        [InlineData(2027, 2, 2027, 2, 1)]
        public void FirstCell_IsTheMondayOnOrBeforeTheFirst(int year, int month, int y, int m, int d)
        {
            DateTime first = DateEntry.FirstCell(new DateTime(year, month, 17));
            Assert.Equal(new DateTime(y, m, d), first);
            Assert.Equal(DayOfWeek.Monday, first.DayOfWeek);
        }

        [Fact]
        public void SixWeeks_AlwaysCoverTheWholeMonth()
        {
            for (int month = 1; month <= 12 * 30; month++)
            {
                DateTime first = new DateTime(2020, 1, 1).AddMonths(month);
                DateTime last = first.AddMonths(1).AddDays(-1);
                DateTime cell = DateEntry.FirstCell(first);
                Assert.True(cell <= first);
                Assert.True(cell.AddDays(7 * DateEntry.Weeks - 1) >= last);
            }
        }
    }
}
