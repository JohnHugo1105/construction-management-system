using ConstructOPX.Application.Features.TimeClock;
using ConstructOPX.Domain.Common;
using ConstructOPX.Domain.Features.Organizations;
using ConstructOPX.Domain.Features.Projects;
using ConstructOPX.Domain.Features.TimeClock;
using ConstructOPX.Domain.Features.Workforce;

namespace ConstructOPX.Domain.UnitTests.Features;

/// <summary>The time clock's rules: badge codes, work dates, the clock bounds, pairing, break, rounding, overtime, night hours and exceptions.</summary>
public sealed class TimeClockTests
{
    private static readonly TimeClockSettings Manila = new("Asia/Manila", 240, 60, 15, 8m);

    /// <summary>A Manila wall-clock time on a date (Manila is UTC+8 all year).</summary>
    private static DateTimeOffset Local(int month, int day, int hour, int minute, int second = 0) =>
        new DateTimeOffset(2026, month, day, hour, minute, second, TimeSpan.FromHours(8));

    private static PunchMoment In(DateTimeOffset at) => new(at, PunchDirection.In);
    private static PunchMoment Out(DateTimeOffset at) => new(at, PunchDirection.Out);

    private static TimeClockDayResult Work(TimeClockSettings settings, params PunchMoment[] punches) => TimeClockDay.Work(punches, settings, notPostedHere: false);
    private static TimeClockDayResult Work(params PunchMoment[] punches) => Work(Manila, punches);

    // ---- Badge codes ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_code_is_twelve_characters_from_the_alphabet_without_look_alikes()
    {
        Assert.Equal(31, PunchCode.Alphabet.Length);
        foreach (var lookAlike in "01ILO") Assert.DoesNotContain(lookAlike, PunchCode.Alphabet);

        var codes = Enumerable.Range(0, 200).Select(_ => PunchCodes.New()).ToArray();
        Assert.All(codes, code =>
        {
            Assert.Equal(12, code.Length);
            Assert.All(code, character => Assert.Contains(character, PunchCode.Alphabet));
        });
        Assert.Equal(200, codes.Distinct().Count());
    }

    [Fact]
    public void A_code_is_generated_from_the_random_source_it_is_given()
    {
        var calls = 0;
        var code = PunchCode.Generate(max =>
        {
            Assert.Equal(31, max);
            return calls++ % max;
        });

        Assert.Equal("23456789ABCD", code);
    }

    [Theory]
    [InlineData("ABCD-EFGH-JKMN", "ABCDEFGHJKMN")]
    [InlineData("abcd efgh jkmn", "ABCDEFGHJKMN")]
    [InlineData("  23456789ABCD\r\n", "23456789ABCD")]
    public void A_scanned_or_typed_code_is_read_without_case_spaces_or_dashes(string typed, string expected) =>
        Assert.Equal(expected, PunchCode.Normalize(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABCDEFGHJKM")]   // 11 characters
    [InlineData("ABCDEFGHJKMNP")] // 13 characters
    [InlineData("ABCDEFGHJKM0")]  // zero is not in the alphabet
    [InlineData("ABCDEFGHJKMO")]  // nor the letter O
    [InlineData("ABCDEFGHJKM1")]
    [InlineData("ABCDEFGHJKMI")]
    [InlineData("ABCDEFGHJKML")]
    [InlineData("W-001")]         // an employee number is never a code
    public void Text_that_cannot_be_a_code_matches_nothing(string? typed) => Assert.Null(PunchCode.Normalize(typed));

    [Fact]
    public void Very_long_input_is_refused_unread() => Assert.Null(PunchCode.Normalize(new string('A', 65)));

    [Fact]
    public void A_code_is_printed_in_groups_of_four() => Assert.Equal("ABCD-EFGH-JKMN", PunchCode.Grouped("ABCDEFGHJKMN"));

    [Fact]
    public void Issuing_a_code_sets_it_and_reissuing_replaces_it()
    {
        var worker = TestData.NewWorker();

        Assert.False(worker.IssuePunchCode("ABCDEFGHJKMN", TestData.Now));
        Assert.Equal("ABCDEFGHJKMN", worker.PunchCode);
        Assert.True(worker.IssuePunchCode("23456789ABCD", TestData.Now));
        Assert.Equal("23456789ABCD", worker.PunchCode);
    }

    [Fact]
    public void Only_an_active_worker_gets_a_code_and_only_a_well_formed_one()
    {
        var worker = TestData.NewWorker();
        Assert.Throws<ArgumentException>(() => worker.IssuePunchCode("abcdefghjkmn", TestData.Now));
        worker.ChangeStatus(WorkerStatus.Resigned, TestData.Today, null, TestData.Today, TestData.Now);

        var ex = Assert.Throws<DomainRuleException>(() => worker.IssuePunchCode("ABCDEFGHJKMN", TestData.Now));
        Assert.Equal("Juan Dela Cruz is resigned. Only an active worker gets a badge code.", ex.Message);
        Assert.Null(worker.PunchCode);
    }

    // ---- Device keys -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_device_key_is_256_random_bits_and_only_its_hash_is_kept()
    {
        var key = DeviceKeys.NewKey();
        var hash = DeviceKeys.HashOf(key);

        Assert.Equal(43, key.Length);
        Assert.NotEqual(key, DeviceKeys.NewKey());
        Assert.NotNull(hash);
        Assert.Equal(32, hash!.Length);
        Assert.True(DeviceKeys.Matches(hash, DeviceKeys.HashOf(key)!));
        Assert.False(DeviceKeys.Matches(hash, DeviceKeys.HashOf(DeviceKeys.NewKey())!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a key")]
    [InlineData("not-a-key")]                                                         // the right alphabet, a length base64 can't have
    [InlineData("c2hvcnQ")]                                                           // decodes to 5 bytes
    [InlineData("ZGVtby1raW9zay1rZXktZm9yLWRldmVsb3BtZW50LTEx")]                      // 33 bytes
    [InlineData("ZGVtby1raW9zay1rZXktZm9yLWRldmVsb3BtZW50LTEZGVtby1raW9zay1rZXktZm9yLWRldmVsb3BtZW50LTE")] // too long to read
    public void Text_that_cannot_be_a_device_key_has_no_hash(string? key) => Assert.Null(DeviceKeys.HashOf(key));

    [Fact]
    public void A_device_is_registered_with_a_name_and_a_kind_on_an_open_project()
    {
        var project = TestData.NewProject();
        var hash = DeviceKeys.HashOf(DeviceKeys.NewKey())!;

        var ex = Assert.Throws<ValidationFailedException>(() =>
            TimeClockDevice.Register(Guid.NewGuid(), TestData.Tenant, " ", project, (TimeClockDeviceKind)7, hash, TestData.UserId, null, TestData.Now));
        Assert.Equal(["kind", "name"], ex.Errors.Keys.Order());

        var device = TimeClockDevice.Register(Guid.NewGuid(), TestData.Tenant, " Main gate ", project, TimeClockDeviceKind.Kiosk, hash, TestData.UserId, null,
            TestData.Now);
        Assert.Equal(("Main gate", TimeClockDeviceStatus.Active, project.Id), (device.Name, device.Status, device.ProjectId));

        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Throws<DomainRuleException>(() =>
            TimeClockDevice.Register(Guid.NewGuid(), TestData.Tenant, "Gate", project, TimeClockDeviceKind.Kiosk, hash, TestData.UserId, null, TestData.Now));
    }

    [Fact]
    public void Rotating_replaces_the_hash_and_revoking_is_final()
    {
        var device = TimeClockDevice.Register(Guid.NewGuid(), TestData.Tenant, "Gate", TestData.NewProject(), TimeClockDeviceKind.Terminal,
            DeviceKeys.HashOf(DeviceKeys.NewKey())!, TestData.UserId, null, TestData.Now);
        var next = DeviceKeys.HashOf(DeviceKeys.NewKey())!;

        device.RotateKey(next, TestData.Now);
        Assert.Equal(next, device.KeyHash);
        device.Revoke(TestData.Now);

        Assert.Equal("This time clock is already revoked.", Assert.Throws<DomainRuleException>(() => device.Revoke(TestData.Now)).Message);
        Assert.Throws<DomainRuleException>(() => device.RotateKey(DeviceKeys.HashOf(DeviceKeys.NewKey())!, TestData.Now));
    }

    // ---- Work date and clock -------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_work_date_is_the_company_date_not_the_utc_date()
    {
        // 23:30 UTC on 1 Nov is 07:30 on 2 Nov in Manila.
        var at = new DateTimeOffset(2026, 11, 1, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 11, 2), TimeClockRules.WorkDateOf(at, Manila));
        Assert.Equal(new DateOnly(2026, 11, 1), DateOnly.FromDateTime(at.UtcDateTime));
    }

    [Theory]
    [InlineData(3, 59, 59, 1)] // before the 04:00 day-start: the previous date (a night shift that ends at 2 am is one day)
    [InlineData(4, 0, 0, 2)]   // the day-start itself: the new date
    [InlineData(0, 0, 0, 1)]
    [InlineData(23, 59, 59, 2)]
    public void A_scan_before_the_day_start_belongs_to_the_previous_date(int hour, int minute, int second, int expectedDay) =>
        Assert.Equal(new DateOnly(2026, 11, expectedDay), TimeClockRules.WorkDateOf(Local(11, 2, hour, minute, second), Manila));

    [Fact]
    public void A_day_start_of_midnight_keeps_the_calendar_date() =>
        Assert.Equal(new DateOnly(2026, 11, 2), TimeClockRules.WorkDateOf(Local(11, 2, 2, 0), Manila with { DayStartMinutes = 0 }));

    [Fact]
    public void The_machine_clock_may_be_up_to_five_minutes_ahead_and_seven_days_behind()
    {
        var now = TestData.Now;
        Assert.Null(TimeClockRules.CheckClock(now.AddMinutes(5), now));
        Assert.Equal(PunchRejection.ClockWrong, TimeClockRules.CheckClock(now.AddMinutes(5).AddSeconds(1), now));
        Assert.Null(TimeClockRules.CheckClock(now.AddDays(-7), now));
        Assert.Equal(PunchRejection.ClockWrong, TimeClockRules.CheckClock(now.AddDays(-7).AddSeconds(-1), now));
    }

    [Fact]
    public void An_unknown_a_replaced_and_an_inactive_badge_are_answered_in_the_same_words()
    {
        Assert.Equal("This badge isn't active. See your supervisor.", TimeClockRules.Explain(PunchRejection.BadgeNotActive));
        Assert.Equal("The time on this machine is wrong, so the scan wasn't kept. Ask your supervisor to set the clock.",
            TimeClockRules.Explain(PunchRejection.ClockWrong));
    }

    [Fact]
    public void An_automatic_direction_is_the_opposite_of_the_last_scan_that_day()
    {
        Assert.Equal(PunchDirection.In, TimeClockRules.Resolve(null, null));
        Assert.Equal(PunchDirection.Out, TimeClockRules.Resolve(null, PunchDirection.In));
        Assert.Equal(PunchDirection.In, TimeClockRules.Resolve(null, PunchDirection.Out));
        Assert.Equal(PunchDirection.In, TimeClockRules.Resolve(PunchDirection.In, PunchDirection.In));
    }

    // ---- Where a scan belongs (a shift that ends after the day-start) -------------------------------------------------------------

    private static EarlierPunch Earlier(DateTimeOffset at, PunchDirection direction, int day) => new(at, direction, new DateOnly(2026, 11, day));

    [Fact]
    public void A_time_out_closes_the_shift_still_open_and_takes_its_work_date_even_after_the_day_start()
    {
        var openIn = Earlier(Local(11, 2, 22, 0), PunchDirection.In, 2);
        var at = Local(11, 3, 6, 0);
        var own = TimeClockRules.WorkDateOf(at, Manila);
        Assert.Equal(new DateOnly(2026, 11, 3), own);

        // 06:00 is after the 04:00 day-start, so by the clock alone it would start 3 Nov as a time in. It is the time out of the shift that began on 2 Nov.
        Assert.Equal((PunchDirection.Out, new DateOnly(2026, 11, 2)), TimeClockRules.Place(null, at, own, openIn));
        Assert.Equal((PunchDirection.Out, new DateOnly(2026, 11, 2)), TimeClockRules.Place(PunchDirection.Out, at, own, openIn));
        Assert.Equal((PunchDirection.In, own), TimeClockRules.Place(PunchDirection.In, at, own, openIn));
    }

    [Fact]
    public void A_time_in_that_is_over_sixteen_hours_old_was_forgotten_and_does_not_pair_with_the_next_scan()
    {
        var openIn = Earlier(Local(11, 2, 7, 0), PunchDirection.In, 2);

        var sixteenHours = Local(11, 2, 23, 0);
        Assert.Equal((PunchDirection.Out, new DateOnly(2026, 11, 2)), TimeClockRules.Place(null, sixteenHours, new DateOnly(2026, 11, 2), openIn));

        var justOver = sixteenHours.AddSeconds(1);
        Assert.Equal((PunchDirection.In, new DateOnly(2026, 11, 2)), TimeClockRules.Place(null, justOver, new DateOnly(2026, 11, 2), openIn));

        var nextMorning = Local(11, 3, 7, 0);
        Assert.Equal((PunchDirection.In, new DateOnly(2026, 11, 3)), TimeClockRules.Place(null, nextMorning, new DateOnly(2026, 11, 3), openIn));
    }

    [Fact]
    public void A_scan_with_no_open_shift_is_a_time_in_on_its_own_date_and_an_explicit_time_out_keeps_its_own_date()
    {
        var at = Local(11, 3, 17, 0);
        var own = new DateOnly(2026, 11, 3);
        var closed = Earlier(Local(11, 3, 12, 0), PunchDirection.Out, 3);

        Assert.Equal((PunchDirection.In, own), TimeClockRules.Place(null, at, own, null));
        Assert.Equal((PunchDirection.In, own), TimeClockRules.Place(null, at, own, closed));
        Assert.Equal((PunchDirection.Out, own), TimeClockRules.Place(PunchDirection.Out, at, own, null));
        Assert.Equal((PunchDirection.Out, own), TimeClockRules.Place(PunchDirection.Out, at, own, closed));
    }

    [Fact]
    public void Three_nights_of_ten_pm_to_six_am_are_three_days_of_one_span_each_dated_when_the_shift_began()
    {
        var placed = new List<(DateTimeOffset At, PunchDirection Direction, DateOnly Date)>();
        foreach (var at in new[]
                 {
                     Local(11, 2, 22, 0), Local(11, 3, 6, 0), Local(11, 3, 22, 0), Local(11, 4, 6, 0), Local(11, 4, 22, 0), Local(11, 5, 6, 0)
                 })
        {
            var latest = placed.Count == 0 ? (EarlierPunch?)null : new EarlierPunch(placed[^1].At, placed[^1].Direction, placed[^1].Date);
            var (direction, date) = TimeClockRules.Place(null, at, TimeClockRules.WorkDateOf(at, Manila), latest);
            placed.Add((at, direction, date));
        }

        Assert.Equal([PunchDirection.In, PunchDirection.Out, PunchDirection.In, PunchDirection.Out, PunchDirection.In, PunchDirection.Out],
            placed.Select(item => item.Direction));
        Assert.Equal([2, 2, 3, 3, 4, 4], placed.Select(item => item.Date.Day));

        foreach (var night in placed.GroupBy(item => item.Date))
        {
            var day = Work(night.Select(item => new PunchMoment(item.At, item.Direction)).ToArray());
            // 8 hours between the scans, the 60-minute break once: 7 hours, all of them at night, and nothing missing.
            Assert.Equal((7m, 0m, 7m, TimeClockException.None), (day.RegularHours, day.ProposedOvertimeHours, day.NightHours, day.Exceptions));
        }
    }

    // ---- A day's hours -------------------------------------------------------------------------------------------------------------

    [Fact]
    public void A_long_single_span_has_the_break_taken_off_once_and_is_rounded_once_on_the_total()
    {
        // 07:02 to 17:03 is 10 h 01 min; less the 60-minute break 9 h 01 min; to the nearest 15 minutes 9 h. 8 regular, 1 overtime proposed.
        var day = Work(In(Local(11, 2, 7, 2)), Out(Local(11, 2, 17, 3)));

        Assert.Equal((60, 540, 8m, 1m, 0m), (day.BreakMinutes, day.WorkedMinutes, day.RegularHours, day.ProposedOvertimeHours, day.NightHours));
        Assert.Equal(TimeClockException.None, day.Exceptions);
        Assert.Equal(Local(11, 2, 7, 2), day.FirstIn);
        Assert.Equal(Local(11, 2, 17, 3), day.LastOut);
    }

    [Theory]
    [InlineData(12, 7, 29, 240, 4.00)] // 5 h 07 min 29 s less the break is 4 h 07 min 29 s: under half of 15 minutes past the hour, so 4 h
    [InlineData(12, 7, 30, 255, 4.25)] // exactly half past: rounds up (half away from zero) to 4 h 15
    [InlineData(11, 59, 59, 300, 5.00)] // 4 h 59 min 59 s: under 5 hours, so no break is taken off; rounds to 5 h
    public void Rounding_is_to_the_nearest_fifteen_minutes_with_halves_going_up(int hour, int minute, int second, int minutes, double hours)
    {
        var day = Work(In(Local(11, 2, 7, 0)), Out(Local(11, 2, hour, minute, second)));

        Assert.Equal(minutes, day.WorkedMinutes);
        Assert.Equal((decimal)hours, day.TotalHours);
    }

    [Fact]
    public void A_span_under_five_hours_has_no_break_taken_off()
    {
        var day = Work(In(Local(11, 2, 13, 0)), Out(Local(11, 2, 17, 59)));

        Assert.Equal((0, 300), (day.BreakMinutes, day.WorkedMinutes));
    }

    [Fact]
    public void When_the_worker_scanned_out_for_the_break_the_gap_is_the_break_and_nothing_more_is_taken_off()
    {
        // 07:00-12:00 and 12:45-17:30: 9 h 45 worked, the 45-minute gap was the break.
        var day = Work(In(Local(11, 2, 7, 0)), Out(Local(11, 2, 12, 0)), In(Local(11, 2, 12, 45)), Out(Local(11, 2, 17, 30)));

        Assert.Equal((45, 585, 8m, 1.75m), (day.BreakMinutes, day.WorkedMinutes, day.RegularHours, day.ProposedOvertimeHours));
    }

    [Fact]
    public void Rounding_off_keeps_the_nearest_minute_and_hours_have_two_decimals()
    {
        // 4 h 07 min 20 s is 247 minutes: 4.1166... hours, kept as 4.12.
        var day = Work(Manila with { RoundingMinutes = 0 }, In(Local(11, 2, 7, 0)), Out(Local(11, 2, 11, 7, 20)));

        Assert.Equal((247, 4.12m, 0m), (day.WorkedMinutes, day.RegularHours, day.ProposedOvertimeHours));
    }

    [Fact]
    public void A_break_of_zero_takes_nothing_off()
    {
        var day = Work(Manila with { UnpaidBreakMinutes = 0 }, In(Local(11, 2, 7, 0)), Out(Local(11, 2, 15, 0)));

        Assert.Equal((0, 8m, 0m), (day.BreakMinutes, day.RegularHours, day.ProposedOvertimeHours));
    }

    [Fact]
    public void Regular_hours_are_capped_at_the_companys_paid_hours_and_the_rest_is_proposed_overtime()
    {
        var day = Work(Manila with { HoursPerDay = 7.5m }, In(Local(11, 2, 6, 0)), Out(Local(11, 2, 18, 10)));

        // 12 h 10 less the break: 11 h 10, rounded 11 h 15. 7.5 regular, 3.75 overtime proposed.
        Assert.Equal((7.5m, 3.75m), (day.RegularHours, day.ProposedOvertimeHours));
    }

    [Fact]
    public void Overtime_is_never_recorded_until_it_is_approved()
    {
        var day = Work(In(Local(11, 2, 6, 0)), Out(Local(11, 2, 18, 0)));
        Assert.Equal(3m, day.ProposedOvertimeHours);

        Assert.Equal((8m, 0m, 0m), day.ToAttendance(approveOvertime: false));
        Assert.Equal((8m, 3m, 0m), day.ToAttendance(approveOvertime: true));
        Assert.False(day.IsCleanWithout(overtimeApproved: false));
        Assert.True(day.IsCleanWithout(overtimeApproved: true));
    }

    [Fact]
    public void Night_hours_are_the_worked_time_between_22_00_and_06_00()
    {
        // 21:00 to 06:30 the next morning, scanned on the evening's date: 9 h 30 less the break is 8 h 30; 8 of those fell in the night.
        var day = Work(In(Local(11, 2, 21, 0)), Out(Local(11, 3, 6, 30)));

        Assert.Equal((8m, 0.5m, 8m), (day.RegularHours, day.ProposedOvertimeHours, day.NightHours));
    }

    [Fact]
    public void Night_hours_are_rounded_the_same_way_and_never_more_than_the_hours_worked()
    {
        // 22:00 to 04:00 is 6 h, all of it night; less the break 5 h worked, so night is held to 5.
        var capped = Work(In(Local(11, 2, 22, 0)), Out(Local(11, 3, 4, 0)));
        Assert.Equal((5m, 5m), (capped.TotalHours, capped.NightHours));

        // 18:00 to 22:07: 7 minutes of night round to nothing on the quarter hour (8 would round up to 15); with rounding off they are 0.12 h.
        Assert.Equal(0m, Work(In(Local(11, 2, 18, 0)), Out(Local(11, 2, 22, 7))).NightHours);
        Assert.Equal(0.25m, Work(In(Local(11, 2, 18, 0)), Out(Local(11, 2, 22, 8))).NightHours);
        Assert.Equal(0.12m, Work(Manila with { RoundingMinutes = 0 }, In(Local(11, 2, 18, 0)), Out(Local(11, 2, 22, 7))).NightHours);

        // Unapproved overtime leaves night hours held to the regular hours that remain.
        var longNight = Work(In(Local(11, 2, 20, 0)), Out(Local(11, 3, 7, 0)));
        Assert.Equal((8m, 2m, 8m), (longNight.RegularHours, longNight.ProposedOvertimeHours, longNight.NightHours));
        Assert.Equal((8m, 0m, 8m), longNight.ToAttendance(approveOvertime: false));
    }

    [Fact]
    public void Two_ins_in_a_row_or_a_last_in_with_no_out_is_a_missing_time_out()
    {
        var twoIns = Work(In(Local(11, 2, 7, 0)), In(Local(11, 2, 8, 0)), Out(Local(11, 2, 12, 0)));
        Assert.Equal(TimeClockException.MissingTimeOut, twoIns.Exceptions);
        Assert.True(twoIns.HasMissingScan);
        // Only the paired span counts: 08:00 to 12:00.
        Assert.Equal(4m, twoIns.TotalHours);

        var lastIn = Work(In(Local(11, 2, 7, 0)));
        Assert.Equal((TimeClockException.MissingTimeOut, 0m), (lastIn.Exceptions, lastIn.TotalHours));
        Assert.Null(lastIn.LastOut);
    }

    [Fact]
    public void An_out_with_no_in_is_a_missing_time_in()
    {
        var day = Work(Out(Local(11, 2, 7, 0)), In(Local(11, 2, 8, 0)), Out(Local(11, 2, 12, 0)));

        Assert.Equal(TimeClockException.MissingTimeIn, day.Exceptions);
        Assert.False(day.IsCleanWithout(true));
    }

    [Fact]
    public void A_worker_not_posted_here_and_a_day_over_sixteen_hours_are_flagged()
    {
        var notPosted = TimeClockDay.Work([In(Local(11, 2, 7, 0)), Out(Local(11, 2, 11, 0))], Manila, notPostedHere: true);
        Assert.Equal(TimeClockException.NotPostedHere, notPosted.Exceptions);
        Assert.False(notPosted.HasMissingScan);

        var sixteen = Work(In(Local(11, 2, 6, 0)), Out(Local(11, 2, 22, 0)));
        Assert.Equal(TimeClockException.None, sixteen.Exceptions);
        var tooLong = Work(In(Local(11, 2, 6, 0)), Out(Local(11, 2, 22, 0, 1)));
        Assert.Equal(TimeClockException.TooLong, tooLong.Exceptions);
    }

    [Fact]
    public void Scans_are_taken_in_time_order_whatever_order_they_arrived_in()
    {
        var day = Work(Out(Local(11, 2, 16, 0)), In(Local(11, 2, 7, 0)));

        Assert.Equal((TimeClockException.None, 8m), (day.Exceptions, day.TotalHours));
    }

    // ---- A supervisor's scan and removing one -------------------------------------------------------------------------------------

    [Fact]
    public void A_manual_scan_needs_a_direction_a_reason_and_a_time_not_in_the_future()
    {
        var worker = TestData.NewWorker();
        var project = TestData.NewProject();

        var ex = Assert.Throws<ValidationFailedException>(() => TimePunch.AddManual(Guid.NewGuid(), TestData.Tenant, worker, project,
            TestData.Now.AddMinutes(6), null, " ", Manila, false, TestData.UserId, null, TestData.Now));
        Assert.Equal(["at", "direction", "reason"], ex.Errors.Keys.Order());

        var punch = TimePunch.AddManual(Guid.NewGuid(), TestData.Tenant, worker, project, Local(11, 1, 17, 0), PunchDirection.Out, "Badge left at home",
            Manila, false, TestData.UserId, null, TestData.Now);
        Assert.Equal((PunchSource.Manual, PunchDirection.Out, new DateOnly(2026, 11, 1), "Badge left at home"),
            (punch.Source, punch.Direction, punch.WorkDate, punch.Reason));
    }

    [Fact]
    public void A_manual_scan_is_refused_on_a_day_the_worker_was_not_active_or_an_archived_project()
    {
        var worker = TestData.NewWorker();
        var project = TestData.NewProject();

        var before = Assert.Throws<ValidationFailedException>(() => TimePunch.AddManual(Guid.NewGuid(), TestData.Tenant, worker, project,
            new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.FromHours(8)), PunchDirection.In, "Missing", Manila, false, TestData.UserId, null, TestData.Now));
        Assert.Equal(["at"], before.Errors.Keys);

        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Throws<DomainRuleException>(() => TimePunch.AddManual(Guid.NewGuid(), TestData.Tenant, worker, project, Local(11, 1, 8, 0), PunchDirection.In,
            "Missing", Manila, false, TestData.UserId, null, TestData.Now));
    }

    [Fact]
    public void A_scan_is_voided_with_a_reason_once_and_never_on_an_archived_project()
    {
        var project = TestData.NewProject();
        var punch = TimePunch.Receive(Guid.NewGuid(), TestData.Tenant, Guid.NewGuid(), project.Id, Guid.NewGuid(), Local(11, 1, 7, 0), new DateOnly(2026, 11, 1),
            PunchDirection.In, true, PunchSource.Kiosk, "scan-1", false, null, TestData.Now);

        Assert.Throws<ValidationFailedException>(() => punch.Void(project, "", TestData.UserId, TestData.Now));
        punch.Void(project, "Scanned the wrong badge", TestData.UserId, TestData.Now);
        Assert.True(punch.IsVoided);
        Assert.Equal("This scan was already removed.", Assert.Throws<DomainRuleException>(() => punch.Void(project, "Again", TestData.UserId, TestData.Now)).Message);

        var other = TimePunch.Receive(Guid.NewGuid(), TestData.Tenant, Guid.NewGuid(), project.Id, null, Local(11, 1, 7, 0), new DateOnly(2026, 11, 1),
            PunchDirection.In, true, PunchSource.Import, null, false, TestData.UserId, TestData.Now);
        project.Archive(ArchiveReason.Finished, null, TestData.Now);
        Assert.Throws<DomainRuleException>(() => other.Void(project, "Wrong", TestData.UserId, TestData.Now));
    }

    // ---- Company settings ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_company_starts_with_the_default_time_clock_settings_and_keeps_them_when_left_out()
    {
        var organization = Organization.Create(TestData.Tenant, "Acme", TestData.Now);
        Assert.Equal((240, 60, 15), (organization.TimeClockDayStartMinutes, organization.UnpaidBreakMinutes, organization.PunchRoundingMinutes));

        organization.UpdateSettings("Acme", "PHP", 1200, "Asia/Manila", 8m, 12500, VarianceBasis.IncludingVat, TestData.Now, null, 300, 0, 30);
        Assert.Equal((300, 0, 30), (organization.TimeClockDayStartMinutes, organization.UnpaidBreakMinutes, organization.PunchRoundingMinutes));
        organization.UpdateSettings("Acme", "PHP", 1200, "Asia/Manila", 8m, 12500, VarianceBasis.IncludingVat, TestData.Now);
        Assert.Equal(new TimeClockSettings("Asia/Manila", 300, 0, 30, 8m), organization.TimeClock);
    }

    [Fact]
    public void Time_clock_settings_out_of_range_are_refused_together()
    {
        var organization = Organization.Create(TestData.Tenant, "Acme", TestData.Now);

        var ex = Assert.Throws<ValidationFailedException>(() => organization.UpdateSettings("Acme", "PHP", 1200, "Asia/Manila", 8m, 12500,
            VarianceBasis.IncludingVat, TestData.Now, null, 721, 181, 61));

        Assert.Equal(["punchRoundingMinutes", "timeClockDayStartMinutes", "unpaidBreakMinutes"], ex.Errors.Keys.Order());
        Assert.Throws<ValidationFailedException>(() => organization.UpdateSettings("Acme", "PHP", 1200, "Asia/Manila", 8m, 12500,
            VarianceBasis.IncludingVat, TestData.Now, null, -1, -1, -1));
    }

    // ---- Import files ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void An_import_file_skips_its_header_and_blank_lines_and_keeps_each_rows_number()
    {
        var rows = PunchCsv.Parse("code,timestamp,direction\r\nABCD-EFGH-JKMN,2026-11-02 07:02,In\r\n\r\n\"23456789ABCD\", 2026-11-02T17:03:00+08:00 \r\n");

        Assert.Equal([(2, "ABCD-EFGH-JKMN", "2026-11-02 07:02", "In"), (4, "23456789ABCD", "2026-11-02T17:03:00+08:00", null)],
            rows.Select(row => (row.Row, row.Code, row.Time, row.Direction)));
    }

    [Fact]
    public void An_import_file_is_bounded_and_must_have_a_row()
    {
        Assert.Equal(["file"], Assert.Throws<ValidationFailedException>(() => PunchCsv.Parse("code,timestamp\n")).Errors.Keys);
        Assert.Throws<ValidationFailedException>(() => PunchCsv.Parse(" "));
        var tooMany = string.Join('\n', Enumerable.Repeat("ABCDEFGHJKMN,2026-11-02 07:02", 5_001));
        Assert.Equal("A file can have at most 5,000 rows. Split it and import each part.",
            Assert.Throws<ValidationFailedException>(() => PunchCsv.Parse(tooMany)).Errors["file"][0]);
        Assert.Equal(5_000, PunchCsv.Parse(string.Join('\n', Enumerable.Repeat("ABCDEFGHJKMN,2026-11-02 07:02", 5_000))).Count);
    }

    [Fact]
    public void An_import_time_without_an_offset_is_the_companys_local_time()
    {
        var zone = Manila.TimeZone;
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 23, 2, 0, TimeSpan.Zero), PunchCsv.ParseTime("2026-11-02 07:02", zone));
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 9, 3, 0, TimeSpan.Zero), PunchCsv.ParseTime("2026-11-02T17:03:00+08:00", zone));
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 9, 3, 0, TimeSpan.Zero), PunchCsv.ParseTime("2026-11-02T09:03:00Z", zone));
        Assert.Null(PunchCsv.ParseTime("2026-11-02", zone));
        Assert.Null(PunchCsv.ParseTime("yesterday at 7:00", zone));
    }
}
