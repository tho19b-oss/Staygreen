using System;
using StayGreen.Core;
using Xunit;

namespace StayGreen.Tests
{
    /// <summary>Eingabelogik der Zahlenfelder (Intervall, Mausweg, Vorwarnung): tippen, schrittweise aendern, Grenzen.</summary>
    public class NumberEntryTests
    {
        /// <summary>Wie das Intervall: 5 bis 600 Sekunden.</summary>
        static NumberEntry Interval(int value)
        {
            var entry = new NumberEntry(Settings.MinInterval, Settings.MaxInterval);
            entry.SetValue(value);
            return entry;
        }

        static void TypeAll(NumberEntry entry, string keys)
        {
            foreach (char c in keys) entry.Type(c);
        }

        [Fact]
        public void New_StartsAtMinimum()
        {
            var entry = new NumberEntry(5, 600);
            Assert.Equal(5, entry.Value);
            Assert.Equal("5", entry.Text);
            Assert.False(entry.IsTyping);
            Assert.Equal(3, entry.MaxDigits);
        }

        [Fact]
        public void Constructor_RejectsInvalidRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NumberEntry(-1, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NumberEntry(10, 9));
        }

        [Fact]
        public void SetValue_IsClampedAndEndsTyping()
        {
            NumberEntry entry = Interval(30);
            entry.Type('4');
            entry.SetValue(9999);
            Assert.False(entry.IsTyping);
            Assert.Equal(600, entry.Value);
            entry.SetValue(-3);
            Assert.Equal(5, entry.Value);
            Assert.Equal("5", entry.Text);
        }

        [Fact]
        public void Typing_FirstDigitReplaces_FurtherDigitsAppend()
        {
            NumberEntry entry = Interval(30);
            entry.Type('4');
            Assert.True(entry.IsTyping);
            Assert.Equal("4", entry.Text);
            entry.Type('5');
            Assert.Equal("45", entry.Text);
            Assert.Equal(45, entry.Value);
        }

        [Fact]
        public void Typing_BelowMinimum_StaysWhileTyping_ValueIsClamped()
        {
            NumberEntry entry = Interval(30);
            entry.Type('3');
            Assert.Equal("3", entry.Text);     // auf dem Weg zu "30"
            Assert.Equal(5, entry.Value);      // gilt schon in den Grenzen
            entry.Commit();
            Assert.False(entry.IsTyping);
            Assert.Equal("5", entry.Text);
        }

        [Fact]
        public void Typing_AboveMaximum_SnapsToMaximumAtOnce()
        {
            var pixels = new NumberEntry(Settings.MinMousePixels, Settings.MaxMousePixels);
            pixels.SetValue(2);
            TypeAll(pixels, "25");
            Assert.Equal("20", pixels.Text);
            Assert.Equal(20, pixels.Value);

            NumberEntry interval = Interval(30);
            TypeAll(interval, "700");
            Assert.Equal("600", interval.Text);
            Assert.Equal(600, interval.Value);
        }

        [Fact]
        public void Typing_TakesNoMoreDigitsThanTheMaximumHas()
        {
            NumberEntry entry = Interval(30);
            TypeAll(entry, "1234");
            Assert.Equal("123", entry.Text);
        }

        [Fact]
        public void Typing_DropsLeadingZeros_AndIgnoresOtherCharacters()
        {
            NumberEntry entry = Interval(30);
            TypeAll(entry, "007");
            Assert.Equal("7", entry.Text);
            entry.Type('a');
            entry.Type('-');
            Assert.Equal("7", entry.Text);

            NumberEntry untouched = Interval(30);
            untouched.Type('x');
            Assert.False(untouched.IsTyping);
            Assert.Equal("30", untouched.Text);
        }

        [Fact]
        public void Backspace_WithoutTyping_ClearsAll_EmptyKeepsPreviousValue()
        {
            NumberEntry entry = Interval(30);
            entry.Backspace();
            Assert.True(entry.IsTyping);
            Assert.Equal("", entry.Text);
            Assert.Equal(30, entry.Value);
            entry.Commit();
            Assert.Equal("30", entry.Text);
            Assert.Equal(30, entry.Value);
        }

        [Fact]
        public void Backspace_WhileTyping_RemovesLastDigit()
        {
            NumberEntry entry = Interval(30);
            TypeAll(entry, "45");
            entry.Backspace();
            Assert.Equal("4", entry.Text);
            entry.Backspace();
            entry.Backspace();
            Assert.Equal("", entry.Text);
            TypeAll(entry, "90");
            Assert.Equal(90, entry.Value);
        }

        [Fact]
        public void Clear_EmptiesTheField_ForNewDigits()
        {
            NumberEntry entry = Interval(30);
            TypeAll(entry, "45");
            entry.Clear();
            Assert.Equal("", entry.Text);
            Assert.Equal(30, entry.Value);
            TypeAll(entry, "60");
            Assert.Equal(60, entry.Value);
        }

        [Fact]
        public void Step_CommitsDraftFirst()
        {
            NumberEntry entry = Interval(30);
            entry.Type('7');
            entry.Step(1);
            Assert.False(entry.IsTyping);
            Assert.Equal(8, entry.Value);
            Assert.Equal("8", entry.Text);

            entry.Type('3');       // unter dem Minimum: erst auf 5, dann einen Schritt weiter
            entry.Step(1);
            Assert.Equal(6, entry.Value);
        }

        [Fact]
        public void Step_StopsAtTheLimits()
        {
            NumberEntry entry = Interval(598);
            entry.Step(10);
            Assert.Equal(600, entry.Value);
            entry.Step(1);
            Assert.Equal(600, entry.Value);
            Assert.False(entry.CanStep(1));
            Assert.True(entry.CanStep(-1));

            entry.SetValue(7);
            entry.Step(-10);
            Assert.Equal(5, entry.Value);
            Assert.False(entry.CanStep(-1));
            Assert.True(entry.CanStep(1));
            Assert.False(entry.CanStep(0));
        }

        [Fact]
        public void CanStep_FollowsTheDraft()
        {
            NumberEntry entry = Interval(30);
            TypeAll(entry, "600");
            Assert.False(entry.CanStep(1));
            entry.Backspace();
            Assert.True(entry.CanStep(1));
        }

        [Fact]
        public void LargeMaximum_DoesNotOverflow()
        {
            var entry = new NumberEntry(0, int.MaxValue);
            TypeAll(entry, "9999999999");
            Assert.Equal(int.MaxValue, entry.Value);
            entry.Step(1);
            Assert.Equal(int.MaxValue, entry.Value);
        }
    }
}
