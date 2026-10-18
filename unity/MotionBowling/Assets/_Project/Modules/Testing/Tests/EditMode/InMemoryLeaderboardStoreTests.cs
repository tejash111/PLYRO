using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bowling.Contracts;
using NUnit.Framework;

namespace Bowling.Testing.Tests
{
    // Pinned semantics of InMemoryLeaderboardStore.
    public class InMemoryLeaderboardStoreTests
    {
        private static readonly DateTime Noon = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

        // A fixed-offset zone has no daylight saving, so the tests don't depend on the machine's tz database.
        private static TimeZoneInfo Zone(int hoursFromUtc)
        {
            return TimeZoneInfo.CreateCustomTimeZone("test+" + hoursFromUtc, TimeSpan.FromHours(hoursFromUtc), "test", "test");
        }

        private static LeaderboardEntry Entry(string name, int score, DateTime playedAtUtc, int frameCount = 10)
        {
            return new LeaderboardEntry { DisplayName = name, Score = score, FrameCount = frameCount, PlayedAtUtc = playedAtUtc };
        }

        private static string[] Names(IReadOnlyList<LeaderboardEntry> entries)
        {
            return entries.Select(e => e.DisplayName).ToArray();
        }

        private static InMemoryLeaderboardStore UtcStore(DateTime now)
        {
            return new InMemoryLeaderboardStore(() => now, TimeZoneInfo.Utc);
        }

        [Test]
        public void EmptyStoreReturnsNoEntries()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);

            Assert.AreEqual(0, store.Top(10, LeaderboardScope.AllTime, 5).Count);
            Assert.AreEqual(0, store.Top(10, LeaderboardScope.Today, 5).Count);
        }

        [Test]
        public void TopOrdersByScoreDescendingThenPlayedAtAscending()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            store.Submit(Entry("late-200", 200, Noon.AddHours(-1)));
            store.Submit(Entry("low-100", 100, Noon.AddHours(-9)));
            store.Submit(Entry("early-200", 200, Noon.AddHours(-3)));
            store.Submit(Entry("high-250", 250, Noon.AddHours(-2)));

            IReadOnlyList<LeaderboardEntry> top = store.Top(10, LeaderboardScope.AllTime, 10);

            Assert.That(Names(top), Is.EqualTo(new[] { "high-250", "early-200", "late-200", "low-100" }));
        }

        [Test]
        public void TopKeepsSubmissionOrderForEqualScoreAndTime()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            store.Submit(Entry("first", 150, Noon));
            store.Submit(Entry("second", 150, Noon));
            store.Submit(Entry("third", 150, Noon));

            Assert.That(Names(store.Top(10, LeaderboardScope.AllTime, 10)), Is.EqualTo(new[] { "first", "second", "third" }));
        }

        [Test]
        public void TopReturnsOnlyEntriesOfTheRequestedFrameCount()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            store.Submit(Entry("ten", 180, Noon, 10));
            store.Submit(Entry("three", 90, Noon, 3));
            store.Submit(Entry("five", 120, Noon, 5));

            Assert.That(Names(store.Top(3, LeaderboardScope.AllTime, 10)), Is.EqualTo(new[] { "three" }));
            Assert.That(Names(store.Top(5, LeaderboardScope.AllTime, 10)), Is.EqualTo(new[] { "five" }));
            Assert.That(Names(store.Top(10, LeaderboardScope.AllTime, 10)), Is.EqualTo(new[] { "ten" }));
        }

        [Test]
        public void TopReturnsAtMostMaxEntriesFromTheTopOfTheOrder()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            for (int i = 1; i <= 5; i++)
            {
                store.Submit(Entry("p" + i, i * 10, Noon));
            }

            Assert.That(Names(store.Top(10, LeaderboardScope.AllTime, 3)), Is.EqualTo(new[] { "p5", "p4", "p3" }));
            Assert.AreEqual(5, store.Top(10, LeaderboardScope.AllTime, 99).Count);
            Assert.AreEqual(0, store.Top(10, LeaderboardScope.AllTime, 0).Count);
        }

        [Test]
        public void TopRejectsANegativeMax()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);

            Assert.Throws<ArgumentOutOfRangeException>(() => store.Top(10, LeaderboardScope.AllTime, -1));
        }

        [Test]
        public void AllTimeIncludesEveryDay()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            store.Submit(Entry("today", 100, Noon));
            store.Submit(Entry("last-year", 200, Noon.AddDays(-365)));

            Assert.That(Names(store.Top(10, LeaderboardScope.AllTime, 10)), Is.EqualTo(new[] { "last-year", "today" }));
        }

        [Test]
        public void TodayKeepsOnlyEntriesFromTheCurrentLocalDate()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            store.Submit(Entry("this-morning", 100, Noon.AddHours(-11)));
            store.Submit(Entry("tonight", 120, Noon.AddHours(11)));
            store.Submit(Entry("yesterday", 300, Noon.AddDays(-1)));
            store.Submit(Entry("tomorrow", 300, Noon.AddDays(1)));

            Assert.That(Names(store.Top(10, LeaderboardScope.Today, 10)), Is.EqualTo(new[] { "tonight", "this-morning" }));
        }

        [Test]
        public void TodayComparesLocalDatesInAZoneEastOfUtc()
        {
            // Now is 2026-10-18 20:00 UTC, which is 2026-10-19 06:00 at UTC+10.
            DateTime now = new DateTime(2026, 10, 18, 20, 0, 0, DateTimeKind.Utc);
            var store = new InMemoryLeaderboardStore(() => now, Zone(10));
            store.Submit(Entry("local-oct-18", 100, new DateTime(2026, 10, 18, 13, 59, 59, DateTimeKind.Utc))); // 23:59:59 on the 18th
            store.Submit(Entry("local-oct-19", 90, new DateTime(2026, 10, 18, 14, 0, 0, DateTimeKind.Utc)));    // 00:00:00 on the 19th
            store.Submit(Entry("local-oct-20", 80, new DateTime(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc)));    // 00:00:00 on the 20th

            Assert.That(Names(store.Top(10, LeaderboardScope.Today, 10)), Is.EqualTo(new[] { "local-oct-19" }));
        }

        [Test]
        public void TodayComparesLocalDatesInAZoneWestOfUtc()
        {
            // Now is 2026-10-18 03:00 UTC, which is 2026-10-17 19:00 at UTC-8.
            DateTime now = new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc);
            var store = new InMemoryLeaderboardStore(() => now, Zone(-8));
            store.Submit(Entry("local-oct-16", 100, new DateTime(2026, 10, 17, 7, 59, 59, DateTimeKind.Utc))); // 23:59:59 on the 16th
            store.Submit(Entry("local-oct-17", 90, new DateTime(2026, 10, 17, 8, 0, 0, DateTimeKind.Utc)));    // 00:00:00 on the 17th
            store.Submit(Entry("local-oct-18", 80, new DateTime(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc)));    // 00:00:00 on the 18th

            Assert.That(Names(store.Top(10, LeaderboardScope.Today, 10)), Is.EqualTo(new[] { "local-oct-17" }));
        }

        [Test]
        public void TodayReadsTheClockOnEveryCall()
        {
            DateTime now = Noon;
            var store = new InMemoryLeaderboardStore(() => now, TimeZoneInfo.Utc);
            store.Submit(Entry("played-oct-18", 100, Noon));

            Assert.AreEqual(1, store.Top(10, LeaderboardScope.Today, 10).Count);

            now = Noon.AddDays(1);

            Assert.AreEqual(0, store.Top(10, LeaderboardScope.Today, 10).Count);
            Assert.AreEqual(1, store.Top(10, LeaderboardScope.AllTime, 10).Count);
        }

        [Test]
        public void SubmitStoresACopyOfTheEntry()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            LeaderboardEntry entry = Entry("reused", 100, Noon);
            store.Submit(entry);

            entry.Score = 250;
            entry.DisplayName = "changed";
            store.Submit(entry);

            IReadOnlyList<LeaderboardEntry> top = store.Top(10, LeaderboardScope.AllTime, 10);
            Assert.AreEqual(2, top.Count);
            Assert.AreEqual("changed", top[0].DisplayName);
            Assert.AreEqual("reused", top[1].DisplayName);
            Assert.AreEqual(100, top[1].Score);
        }

        [Test]
        public void TopReturnsCopiesCarryingEveryField()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            store.Submit(Entry("p", 123, Noon, 5));

            LeaderboardEntry first = store.Top(5, LeaderboardScope.AllTime, 1)[0];
            first.Score = 999;
            LeaderboardEntry second = store.Top(5, LeaderboardScope.AllTime, 1)[0];

            Assert.AreEqual("p", second.DisplayName);
            Assert.AreEqual(123, second.Score, "mutating a returned entry does not reach the store");
            Assert.AreEqual(5, second.FrameCount);
            Assert.AreEqual(Noon, second.PlayedAtUtc);
        }

        [Test]
        public void ResetClearsEveryEntry()
        {
            InMemoryLeaderboardStore store = UtcStore(Noon);
            store.Submit(Entry("a", 100, Noon));
            store.Submit(Entry("b", 200, Noon, 3));

            store.Reset();

            Assert.AreEqual(0, store.Top(10, LeaderboardScope.AllTime, 10).Count);
            Assert.AreEqual(0, store.Top(3, LeaderboardScope.AllTime, 10).Count);
        }

        [Test]
        public void ConstructorAndSubmitRejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => new InMemoryLeaderboardStore(null, TimeZoneInfo.Utc));
            Assert.Throws<ArgumentNullException>(() => new InMemoryLeaderboardStore(() => Noon, null));
            Assert.Throws<ArgumentNullException>(() => UtcStore(Noon).Submit(null));
        }

        [Test]
        public void LeaderboardEntryStillHasTheFourFieldsTheStoreCopies()
        {
            // InMemoryLeaderboardStore copies LeaderboardEntry field by field. If this fails, a field was
            // added: update the copy in the store and the assertions above.
            FieldInfo[] fields = typeof(LeaderboardEntry).GetFields(BindingFlags.Public | BindingFlags.Instance);

            Assert.AreEqual(4, fields.Length);
        }
    }
}
