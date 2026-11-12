using System.Collections.Generic;
using NUnit.Framework;

namespace Kanshilike.Tests.EditMode
{
	public class HourClockTests
	{
		const float Eps = 1e-4f;

		readonly List<HourTick> ticks = new List<HourTick>();

		List<HourTick> Advance(HourClock clock, float time)
		{
			ticks.Clear();
			clock.Advance(time, ticks);
			return ticks;
		}

		[Test]
		public void FirstCall_OnlySetsStart_NoTick()
		{
			var clock = new HourClock();
			Assert.IsEmpty(Advance(clock, 8f));
		}

		[Test]
		public void WithinTheSameHour_NoTick()
		{
			var clock = new HourClock();
			Advance(clock, 8f);
			Assert.IsEmpty(Advance(clock, 8.5f));
			Assert.IsEmpty(Advance(clock, 8.75f));
		}

		[Test]
		public void CrossingAnHourMark_OneTickForTheNewHour()
		{
			var clock = new HourClock();
			Advance(clock, 8.5f);
			var result = Advance(clock, 9.25f);
			Assert.AreEqual(1, result.Count);
			Assert.AreEqual(9, result[0].Hour);
			Assert.AreEqual(0f, result[0].AsleepFraction, Eps);
		}

		[Test]
		public void LandingExactlyOnAMark_TicksOnceOnly()
		{
			var clock = new HourClock();
			Advance(clock, 8.5f);
			Assert.AreEqual(1, Advance(clock, 9f).Count);
			Assert.IsEmpty(Advance(clock, 9.25f));
		}

		[Test]
		public void JumpOfSeveralHours_OneTickPerHour()
		{
			var clock = new HourClock();
			Advance(clock, 8.5f);
			var result = Advance(clock, 12.25f);
			CollectionAssert.AreEqual(new[] { 9, 10, 11, 12 }, Hours(result));
		}

		[Test]
		public void PastMidnight_TicksWrapToZero()
		{
			var clock = new HourClock();
			Advance(clock, 22.5f);
			var result = Advance(clock, 1.25f);
			CollectionAssert.AreEqual(new[] { 23, 0, 1 }, Hours(result));
		}

		[Test]
		public void NapAcrossTheMark_CountsOnlyTimeAsleep()
		{
			var clock = new HourClock();
			Advance(clock, 8.75f);
			clock.StartSleep(8f);
			Advance(clock, 9f);       // 15 minutes asleep, then the tick
			clock.StopSleep();
			Assert.AreEqual(1, ticks.Count);
			Assert.AreEqual(0.25f, ticks[0].AsleepFraction, Eps);

			var next = Advance(clock, 10f);
			Assert.AreEqual(0f, next[0].AsleepFraction, Eps, "awake for the whole next hour");
		}

		[Test]
		public void WakingMidHour_KeepsTheTimeAlreadySlept()
		{
			var clock = new HourClock();
			Advance(clock, 8f);
			clock.StartSleep(8f);
			Advance(clock, 8.5f);
			clock.StopSleep();
			var result = Advance(clock, 9f);
			Assert.AreEqual(0.5f, result[0].AsleepFraction, Eps);
		}

		[Test]
		public void Sleep_EndsAfterItsHours_AndReportsIt()
		{
			var clock = new HourClock();
			Advance(clock, 8f);
			clock.StartSleep(1.5f);

			ticks.Clear();
			Assert.IsFalse(clock.Advance(9f, ticks));
			Assert.IsTrue(clock.IsSleeping);
			Assert.AreEqual(1f, ticks[0].AsleepFraction, Eps);

			ticks.Clear();
			Assert.IsTrue(clock.Advance(10f, ticks), "sleep ran out at 9:30");
			Assert.IsFalse(clock.IsSleeping);
			Assert.AreEqual(0.5f, ticks[0].AsleepFraction, Eps);
		}

		[Test]
		public void Sleep_EndingInsideABigJump_SplitsAcrossHours()
		{
			var clock = new HourClock();
			Advance(clock, 8f);
			clock.StartSleep(2.5f);
			var result = Advance(clock, 12f);
			Assert.AreEqual(4, result.Count);
			Assert.AreEqual(1f, result[0].AsleepFraction, Eps);
			Assert.AreEqual(1f, result[1].AsleepFraction, Eps);
			Assert.AreEqual(0.5f, result[2].AsleepFraction, Eps);
			Assert.AreEqual(0f, result[3].AsleepFraction, Eps);
		}

		[Test]
		public void StartSleep_Negative_DoesNotSleep()
		{
			var clock = new HourClock();
			clock.StartSleep(-3f);
			Assert.IsFalse(clock.IsSleeping);
		}

		static int[] Hours(List<HourTick> list)
		{
			var hours = new int[list.Count];
			for (int i = 0; i < list.Count; i++)
				hours[i] = list[i].Hour;
			return hours;
		}
	}
}
