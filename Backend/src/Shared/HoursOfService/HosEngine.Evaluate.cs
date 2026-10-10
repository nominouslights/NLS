namespace NorthernLink.Shared.HoursOfService;

/// <summary>
/// The pure, deterministic Hours-of-Service rule engine (Manitoba M.R. 72/2007, adopting federal
/// SOR/2005-313). System-only: no clock, no I/O, no randomness, no other NorthernLink namespace.
/// Every "now" is the <c>asOfUnix</c> argument; durations are integer seconds; odometers are
/// tenths of a kilometre. See README.md beside this file for the purity contract and the
/// regulation section behind each rule.
/// </summary>
public static partial class HosEngine
{
    public static HosEvaluation Evaluate(DutyLedger ledger, long asOfUnix, HosCalendar calendar, HosBands bands) =>
        Evaluate(ledger, asOfUnix, calendar, bands, long.MaxValue);

    /// <summary>
    /// <paramref name="adverseCutoffUnix"/>: adverse-conditions extensions apply only to figures
    /// evaluated at instants at or before it. <see cref="Project"/> passes its as-of so a declared
    /// extension never stretches projected time.
    /// </summary>
    internal static HosEvaluation Evaluate(DutyLedger ledger, long asOfUnix, HosCalendar calendar, HosBands bands, long adverseCutoffUnix)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(calendar);
        return new Evaluation(ledger, asOfUnix, calendar, bands ?? new HosBands(), adverseCutoffUnix).Run();
    }

    private static int Sec(long value) => value >= int.MaxValue ? int.MaxValue : value <= int.MinValue ? int.MinValue : (int)value;

    private static long Overlap(long aStart, long aEnd, long bStart, long bEnd) =>
        Math.Max(0, Math.Min(aEnd, bEnd) - Math.Max(aStart, bStart));

    private sealed class Segment
    {
        public long Start;
        public long End;
        public DutyStatus Status;
        public EventSource Source;
        public int? StartOdo;
        public int? EndOdo;
        public bool HasClosingEvent;
        public bool Synthetic;
    }

    private sealed class Piece
    {
        public required Segment Segment;
        public long Start;
        public long End;
        public DutyStatus Status;
        public DateOnly Date;
        public int Decikm;
        public long Length => End - Start;
        public bool IsOff => Status is DutyStatus.OffDuty or DutyStatus.PersonalConveyance;
        public bool IsOnDuty => Status is DutyStatus.OnDuty or DutyStatus.Driving;
        public bool IsDriving => Status == DutyStatus.Driving;
    }

    private readonly record struct Run(long Start, long End)
    {
        public long Length => End - Start;
    }

    private sealed class Pending
    {
        public HosRule Rule;
        public DateOnly Date;
        public long AtUnix;
        public int Figure;
        public int Limit;
    }

    private sealed class Evaluation
    {
        private readonly DutyLedger _ledger;
        private readonly long _asOf;
        private readonly HosCalendar _cal;
        private readonly HosBands _bands;
        private readonly long _cutoff;
        private readonly long _knownFrom;
        private readonly DateOnly _today;

        private readonly List<HosLedgerProblem> _problems = [];
        private readonly Dictionary<(HosRule Rule, string Key), Pending> _pending = [];
        private readonly List<DutyEvent> _events = [];
        private readonly HashSet<DateOnly> _ownEventDays = [];
        private readonly Dictionary<DateOnly, OtherCarrierDay> _otherCarrier = [];

        /// <summary>Other-carrier days rendered as synthetic pieces (declared hours at the day start), so their totals are not added twice.</summary>
        private readonly HashSet<DateOnly> _otherCarrierOnTimeline = [];
        private readonly HashSet<DateOnly> _certified = [];
        private readonly List<(long From, long Until)> _oos = [];
        private readonly List<Segment> _segments = [];
        private readonly List<Piece> _pieces = [];
        private readonly List<Run> _runs = [];
        private readonly Dictionary<long, int> _adverseByShift = [];
        private readonly Dictionary<DateOnly, int> _adverseByDay = [];
        private readonly Dictionary<DateOnly, OffDutyDeferral> _deferrals = [];
        private readonly List<(Emergency Exception, long From, long To)> _emergencies = [];
        private readonly Dictionary<DateOnly, int> _pcDeclared = [];
        private readonly HashSet<DateOnly> _deferralFailedDayOne = [];

        private bool _noLedger;
        private DutyStatus _currentStatus = DutyStatus.OffDuty;
        private long _currentSince;

        public Evaluation(DutyLedger ledger, long asOf, HosCalendar cal, HosBands bands, long cutoff)
        {
            _ledger = ledger;
            _asOf = asOf;
            _cal = cal;
            _bands = bands;
            _cutoff = cutoff;
            _today = cal.DayOf(asOf);
            _knownFrom = ledger.KnownFromUnix;
            if (_knownFrom > asOf)
            {
                Problem("KnownFromAfterAsOf", "The ledger's known-from instant is after the evaluation instant; nothing is known.", _knownFrom);
                _knownFrom = asOf;
            }

            _currentSince = _knownFrom;
        }

        public HosEvaluation Run()
        {
            NormaliseEvents();
            LoadReferenceData();
            BuildSegments();
            BuildPieces();
            ReclassifyPersonalConveyance();
            BuildRuns();
            LoadExceptions();
            var days = BuildDays();
            EvaluateDrivingPieces();
            EvaluateDailyOff(days);
            EvaluateRecords(days);
            return Assemble(days);
        }

        // ---- 1. Normalise ----------------------------------------------------------------

        private void NormaliseEvents()
        {
            var kept = new List<DutyEvent>();
            foreach (var e in _ledger.Events ?? [])
            {
                if (!Enum.IsDefined(e.Status) || !Enum.IsDefined(e.Source))
                {
                    Problem("InvalidEvent", "An event carries an unknown status or source and was ignored.", e.AtUnix);
                    continue;
                }

                if (e.AtUnix > _asOf + HosLimits.FutureSkewSeconds)
                {
                    Problem("FutureEvent", "An event is more than five minutes in the future and was ignored.", e.AtUnix);
                    continue;
                }

                kept.Add(e);
            }

            kept.Sort(static (a, b) => a.AtUnix != b.AtUnix ? a.AtUnix.CompareTo(b.AtUnix) : a.Sequence.CompareTo(b.Sequence));

            for (var i = 0; i < kept.Count;)
            {
                var j = i;
                while (j + 1 < kept.Count && kept[j + 1].AtUnix == kept[i].AtUnix)
                {
                    j++;
                }

                var winner = kept[j];
                for (var k = i; k < j; k++)
                {
                    if (kept[k].Source != winner.Source && kept[k].Status != winner.Status)
                    {
                        var date = _cal.DayOf(winner.AtUnix);
                        Record(HosRule.ConflictingSources, $"{winner.AtUnix}", date, winner.AtUnix, 0, 0);
                    }
                }

                _events.Add(winner);
                i = j + 1;
            }

            foreach (var e in _events)
            {
                if (e.AtUnix <= _asOf)
                {
                    _ownEventDays.Add(_cal.DayOf(e.AtUnix));
                }
            }
        }

        private void LoadReferenceData()
        {
            var firstOwn = _events.Count > 0 ? _events[0].AtUnix : long.MaxValue;
            foreach (var day in _ledger.OtherCarrierDays ?? [])
            {
                if (day is null || day.OnDutySeconds < 0 || day.DrivingSeconds < 0 || day.DrivingSeconds > day.OnDutySeconds)
                {
                    Problem("InvalidOtherCarrierDay", "An other-carrier day has impossible hours and was ignored.", null);
                    continue;
                }

                if (_otherCarrier.ContainsKey(day.Date))
                {
                    Problem("DuplicateOtherCarrierDay", $"Two other-carrier declarations exist for {day.Date:yyyy-MM-dd}; the first was kept.", _cal.DayStartUnix(day.Date));
                    continue;
                }

                if (_cal.DayEndUnix(day.Date) > firstOwn)
                {
                    Problem("UntimedOtherCarrierHoursAfterOwnRecords",
                        $"Other-carrier hours for {day.Date:yyyy-MM-dd} are declared after the driver's own records begin; they are counted conservatively.",
                        _cal.DayStartUnix(day.Date));
                }

                _otherCarrier[day.Date] = day;
            }

            _noLedger = _events.Count == 0 && _otherCarrier.Count == 0;

            foreach (var date in _ledger.CertifiedDays ?? [])
            {
                _certified.Add(date);
            }

            foreach (var span in _ledger.OutOfService ?? [])
            {
                if (span is null)
                {
                    continue;
                }

                var until = span.UntilUnix ?? long.MaxValue;
                if (until <= span.FromUnix)
                {
                    Problem("InvalidOutOfService", "An out-of-service span ends before it starts and was ignored.", span.FromUnix);
                    continue;
                }

                _oos.Add((span.FromUnix, until));
            }
        }

        // ---- 2. Segments and pieces -------------------------------------------------------

        private void BuildSegments()
        {
            Segment? current = null;
            int? pendingEndOdo = null;
            for (var i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                var end = i + 1 < _events.Count ? _events[i + 1].AtUnix : Math.Max(_asOf, e.AtUnix);
                if (current is not null && current.Status == e.Status)
                {
                    current.End = end;
                    if (e.OdometerDecikm is not null)
                    {
                        pendingEndOdo = e.OdometerDecikm;
                    }

                    continue;
                }

                if (current is not null)
                {
                    current.EndOdo = e.OdometerDecikm ?? pendingEndOdo;
                    current.HasClosingEvent = true;
                }

                current = new Segment
                {
                    Start = e.AtUnix,
                    End = end,
                    Status = e.Status,
                    Source = e.Source,
                    StartOdo = e.OdometerDecikm,
                };
                pendingEndOdo = null;
                _segments.Add(current);
            }

            if (current is not null)
            {
                current.EndOdo = pendingEndOdo;
            }

            foreach (var s in _segments)
            {
                if (s.Start <= _asOf)
                {
                    _currentStatus = s.Status;
                    _currentSince = s.Start;
                }
            }

            if (_segments.Count == 0 || _currentSince < _knownFrom)
            {
                _currentSince = Math.Max(_currentSince, _knownFrom);
            }
        }

        private void BuildPieces()
        {
            var clamped = new List<Segment>();
            foreach (var s in _segments)
            {
                var start = Math.Max(s.Start, _knownFrom);
                var end = Math.Min(s.End, _asOf);
                if (start < end)
                {
                    clamped.Add(new Segment
                    {
                        Start = start, End = end, Status = s.Status, Source = s.Source,
                        StartOdo = s.StartOdo, EndOdo = s.EndOdo, HasClosingEvent = s.HasClosingEvent,
                    });
                }
            }

            var firstStart = clamped.Count > 0 ? clamped[0].Start : _asOf;
            if (firstStart > _knownFrom)
            {
                clamped.InsertRange(0, SyntheticSegments(_knownFrom, firstStart));
            }

            foreach (var s in clamped)
            {
                var cuts = new List<long>();
                var date = _cal.DayOf(s.Start);
                var boundary = _cal.DayEndUnix(date);
                while (boundary < s.End)
                {
                    cuts.Add(boundary);
                    date = date.AddDays(1);
                    boundary = _cal.DayEndUnix(date);
                }

                if (_cutoff > s.Start && _cutoff < s.End)
                {
                    cuts.Add(_cutoff);
                }

                cuts.Sort();
                var totalKm = s.Status == DutyStatus.PersonalConveyance && s.StartOdo is int so && s.EndOdo is int eo && eo >= so ? eo - so : 0;
                var allocated = 0L;
                var pieceStart = s.Start;
                for (var c = 0; c <= cuts.Count; c++)
                {
                    var pieceEnd = c < cuts.Count ? cuts[c] : s.End;
                    if (pieceEnd <= pieceStart)
                    {
                        continue;
                    }

                    var piece = new Piece
                    {
                        Segment = s,
                        Start = pieceStart,
                        End = pieceEnd,
                        Status = s.Status,
                        Date = _cal.DayOf(pieceStart),
                    };
                    if (totalKm > 0)
                    {
                        var share = c == cuts.Count ? totalKm - allocated : totalKm * (pieceEnd - pieceStart) / (s.End - s.Start);
                        piece.Decikm = Sec(share);
                        allocated += share;
                    }

                    _pieces.Add(piece);
                    pieceStart = pieceEnd;
                }
            }
        }

        /// <summary>
        /// Time between known-from and the first own event: off duty, except that a fully covered
        /// day with a declared other-carrier row becomes that declaration laid out from the day
        /// start (driving, then other on-duty, then off) — the untimed hours have to sit somewhere,
        /// and the day start is the conservative place for the shift and daily rules.
        /// </summary>
        private List<Segment> SyntheticSegments(long from, long to)
        {
            var list = new List<Segment>();
            var cursor = from;
            while (cursor < to)
            {
                var date = _cal.DayOf(cursor);
                var dayStart = _cal.DayStartUnix(date);
                var dayEnd = Math.Min(_cal.DayEndUnix(date), to);
                if (_otherCarrier.TryGetValue(date, out var oc) && oc.OnDutySeconds > 0 && dayStart >= from && _cal.DayEndUnix(date) <= to)
                {
                    _otherCarrierOnTimeline.Add(date);
                    var drivingEnd = Math.Min(dayStart + oc.DrivingSeconds, dayEnd);
                    var onDutyEnd = Math.Min(dayStart + oc.OnDutySeconds, dayEnd);
                    Add(dayStart, drivingEnd, DutyStatus.Driving);
                    Add(drivingEnd, onDutyEnd, DutyStatus.OnDuty);
                    Add(onDutyEnd, dayEnd, DutyStatus.OffDuty);
                }
                else
                {
                    Add(cursor, dayEnd, DutyStatus.OffDuty);
                }

                cursor = dayEnd;
            }

            return list;

            void Add(long start, long end, DutyStatus status)
            {
                if (start < end)
                {
                    list.Add(new Segment { Start = start, End = end, Status = status, Source = EventSource.OtherCarrier, Synthetic = true });
                }
            }
        }

        // ---- 3. Personal conveyance ------------------------------------------------------

        private void ReclassifyPersonalConveyance()
        {
            var used = new Dictionary<DateOnly, int>();
            for (var i = 0; i < _pieces.Count; i++)
            {
                var p = _pieces[i];
                if (p.Status != DutyStatus.PersonalConveyance)
                {
                    continue;
                }

                var seg = p.Segment;
                var key = $"{seg.Start}";
                var segDate = _cal.DayOf(seg.Start);
                var odometerMissing = seg.StartOdo is null
                    || (seg.HasClosingEvent && seg.EndOdo is null)
                    || (seg.EndOdo is int eo && eo < seg.StartOdo);
                if (odometerMissing)
                {
                    p.Status = DutyStatus.Driving;
                    p.Decikm = 0;
                    Record(HosRule.PcOdometerMissing, key, segDate, seg.Start, 0, 0);
                    continue;
                }

                var oosOverlapFrom = OosOverlapStart(seg.Start, seg.End);
                if (oosOverlapFrom is long from)
                {
                    p.Status = DutyStatus.Driving;
                    p.Decikm = 0;
                    Record(HosRule.PcWhileOutOfService, key, segDate, Math.Max(seg.Start, from), 0, 0);
                    continue;
                }

                if (!seg.HasClosingEvent && seg.EndOdo is null)
                {
                    continue; // in progress: no distance yet, still personal conveyance
                }

                _pcDeclared[p.Date] = _pcDeclared.GetValueOrDefault(p.Date) + p.Decikm;
                var allowance = HosLimits.PcDailyLimitDecikm - used.GetValueOrDefault(p.Date);
                if (p.Decikm <= allowance)
                {
                    used[p.Date] = used.GetValueOrDefault(p.Date) + p.Decikm;
                    continue;
                }

                var dayKey = p.Date.ToString("yyyy-MM-dd");
                if (allowance <= 0)
                {
                    p.Status = DutyStatus.Driving;
                    Record(HosRule.PcExceeds75km, dayKey, p.Date, p.Start, _pcDeclared[p.Date], HosLimits.PcDailyLimitDecikm);
                    continue;
                }

                var crossing = p.Start + p.Length * allowance / p.Decikm;
                used[p.Date] = HosLimits.PcDailyLimitDecikm;
                var driving = new Piece
                {
                    Segment = seg, Start = crossing, End = p.End, Status = DutyStatus.Driving, Date = p.Date, Decikm = p.Decikm - allowance,
                };
                p.End = crossing;
                p.Decikm = allowance;
                if (p.Length <= 0)
                {
                    _pieces[i] = driving;
                }
                else
                {
                    _pieces.Insert(i + 1, driving);
                    i++;
                }

                Record(HosRule.PcExceeds75km, dayKey, p.Date, crossing, _pcDeclared[p.Date], HosLimits.PcDailyLimitDecikm);
            }
        }

        private long? OosOverlapStart(long start, long end)
        {
            long? best = null;
            foreach (var (from, until) in _oos)
            {
                if (Overlap(start, end, from, until) > 0 && (best is null || from < best))
                {
                    best = from;
                }
            }

            return best;
        }

        // ---- 4. Runs ----------------------------------------------------------------------

        private void BuildRuns()
        {
            long? runStart = null;
            long runEnd = 0;
            foreach (var p in _pieces)
            {
                if (p.IsOff)
                {
                    if (runStart is null || p.Start != runEnd)
                    {
                        if (runStart is not null)
                        {
                            _runs.Add(new Run(runStart.Value, runEnd));
                        }

                        runStart = p.Start;
                    }

                    runEnd = p.End;
                }
                else if (runStart is not null)
                {
                    _runs.Add(new Run(runStart.Value, runEnd));
                    runStart = null;
                }
            }

            if (runStart is not null)
            {
                _runs.Add(new Run(runStart.Value, runEnd));
            }
        }

        private bool InsideLongRun(long t, long minimum)
        {
            foreach (var r in _runs)
            {
                if (r.Length >= minimum && r.Start <= t && t < r.End)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>End of the most recent run of at least 8h ending at or before <paramref name="t"/>; null when none.</summary>
        private long? LastRunEndAtOrBefore(long t, long minimum)
        {
            long? best = null;
            foreach (var r in _runs)
            {
                if (r.Length >= minimum && r.End <= t && (best is null || r.End > best))
                {
                    best = r.End;
                }
            }

            return best;
        }

        private long ShiftStartAt(long t) => LastRunEndAtOrBefore(t, HosLimits.MandatoryConsecutiveOffSeconds) ?? _knownFrom;

        private bool BetweenShiftsAt(long t)
        {
            if (_currentStatus is not (DutyStatus.OffDuty or DutyStatus.PersonalConveyance))
            {
                return false;
            }

            foreach (var r in _runs)
            {
                if (r.Start <= t && t <= r.End && t - r.Start >= HosLimits.MandatoryConsecutiveOffSeconds)
                {
                    return true;
                }
            }

            return false;
        }

        // ---- 5. Exceptions ---------------------------------------------------------------

        private void LoadExceptions()
        {
            foreach (var ex in (_ledger.Exceptions ?? []).OrderBy(e => e?.DeclaredAtUnix ?? long.MaxValue))
            {
                switch (ex)
                {
                    case AdverseDrivingConditions adverse:
                        LoadAdverse(adverse);
                        break;
                    case Emergency emergency:
                        LoadEmergency(emergency);
                        break;
                    case OffDutyDeferral deferral:
                        LoadDeferral(deferral);
                        break;
                    default:
                        Problem("InvalidException", "An exception of unknown kind was ignored.", null);
                        break;
                }
            }
        }

        private void LoadAdverse(AdverseDrivingConditions adverse)
        {
            var t = adverse.DeclaredAtUnix;
            var insideShift = t >= _knownFrom && t <= _asOf && !InsideLongRun(t, HosLimits.MandatoryConsecutiveOffSeconds);
            var date = _cal.DayOf(t);
            if (!insideShift)
            {
                Record(HosRule.AdverseDeclaredOutsideShift, adverse.Id, date, t, 0, 0);
                return;
            }

            if (adverse.ExtensionSeconds <= 0)
            {
                Problem("InvalidException", $"Adverse-conditions exception {adverse.Id} declares no extension and was ignored.", t);
                return;
            }

            var ext = adverse.ExtensionSeconds;
            if (ext > HosLimits.AdverseMaxExtensionSeconds)
            {
                Problem("AdverseExtensionClamped", $"Adverse-conditions exception {adverse.Id} asked for more than 2h; it was clamped to 2h.", t);
                ext = HosLimits.AdverseMaxExtensionSeconds;
            }

            var shift = ShiftStartAt(t);
            if (_adverseByShift.ContainsKey(shift))
            {
                Problem("DuplicateException", $"Adverse-conditions exception {adverse.Id} is the second in its shift and was ignored.", t);
                return;
            }

            _adverseByShift[shift] = ext;
            _adverseByDay[date] = Math.Max(_adverseByDay.GetValueOrDefault(date), ext);
        }

        private void LoadEmergency(Emergency emergency)
        {
            var from = emergency.DeclaredAtUnix;
            long to;
            if (emergency.EndedAtUnix is long ended)
            {
                if (ended < from)
                {
                    Problem("InvalidException", $"Emergency {emergency.Id} ends before it starts and was ignored.", from);
                    return;
                }

                to = ended;
            }
            else
            {
                to = from;
                foreach (var s in _segments)
                {
                    if (s.Status == DutyStatus.Driving && s.Start <= from && from < s.End)
                    {
                        to = s.End;
                    }
                }
            }

            _emergencies.Add((emergency, from, to));
        }

        private void LoadDeferral(OffDutyDeferral deferral)
        {
            if (deferral.DeferredSeconds <= 0 || deferral.DeferredSeconds > HosLimits.DeferralMaxSeconds)
            {
                Problem("InvalidException", $"Deferral {deferral.Id} defers an amount outside 1s..2h and was ignored.", deferral.DeclaredAtUnix);
                return;
            }

            if (_deferrals.ContainsKey(deferral.DayOne))
            {
                Problem("DuplicateException", $"Deferral {deferral.Id} duplicates another for the same day one and was ignored.", deferral.DeclaredAtUnix);
                return;
            }

            _deferrals[deferral.DayOne] = deferral;
        }

        private int AdverseForDay(DateOnly date, long at) => at <= _cutoff ? _adverseByDay.GetValueOrDefault(date) : 0;

        private int AdverseForShift(long shiftStart, long at) => at <= _cutoff ? _adverseByShift.GetValueOrDefault(shiftStart) : 0;

        // ---- Figures ------------------------------------------------------------------------

        private long SumBetween(long from, long to, bool driving)
        {
            long total = 0;
            foreach (var p in _pieces)
            {
                if (driving ? p.IsDriving : p.IsOnDuty)
                {
                    total += Overlap(p.Start, p.End, from, to);
                }
            }

            return total;
        }

        /// <summary>Declared other-carrier hours not already on the timeline as synthetic pieces.</summary>
        private int OtherCarrier(DateOnly date, bool driving) =>
            !_otherCarrierOnTimeline.Contains(date) && _otherCarrier.TryGetValue(date, out var oc)
                ? (driving ? oc.DrivingSeconds : oc.OnDutySeconds)
                : 0;

        private int DeclaredOtherCarrierOnDuty(DateOnly date) =>
            _otherCarrier.TryGetValue(date, out var oc) ? oc.OnDutySeconds : 0;

        private int DailyDrivingAt(DateOnly date, long t) =>
            Sec(SumBetween(_cal.DayStartUnix(date), t, driving: true)) + OtherCarrier(date, driving: true);

        private int DailyOnDutyAt(DateOnly date, long t) =>
            Sec(SumBetween(_cal.DayStartUnix(date), t, driving: false)) + OtherCarrier(date, driving: false);

        private int QualifyingOff(DateOnly date, int minimumRun = HosLimits.MinOffBlockSeconds)
        {
            var dayStart = _cal.DayStartUnix(date);
            var dayEnd = _cal.DayEndUnix(date);
            long total = 0;
            foreach (var r in _runs)
            {
                if (r.Length >= minimumRun)
                {
                    total += Overlap(r.Start, r.End, dayStart, dayEnd);
                }
            }

            return Sec(total);
        }

        private bool RunTouches(DateOnly date, long minimumRun)
        {
            var dayStart = _cal.DayStartUnix(date);
            var dayEnd = _cal.DayEndUnix(date);
            foreach (var r in _runs)
            {
                if (r.Length >= minimumRun && Overlap(r.Start, r.End, dayStart, dayEnd) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private CycleState CycleAt(long t)
        {
            var date = _cal.DayOf(t);
            var windowStart = date.AddDays(-(HosLimits.CycleDays - 1));
            var resetEnd = LastRunEndAtOrBefore(t, HosLimits.CycleResetSeconds);
            var from = Math.Max(_cal.DayStartUnix(windowStart), resetEnd ?? long.MinValue);
            long total = SumBetween(from, t, driving: false);
            var resetDate = resetEnd is long re ? _cal.DayOf(re) : (DateOnly?)null;
            for (var d = windowStart; d <= date; d = d.AddDays(1))
            {
                if (resetDate is null || d >= resetDate)
                {
                    total += OtherCarrier(d, driving: false);
                }
            }

            var incomplete = resetEnd is null && _cal.DayStartUnix(windowStart) < _knownFrom;
            var onDuty = Sec(total);
            return new CycleState(onDuty, HosLimits.CycleOnDutySeconds, Math.Max(0, HosLimits.CycleOnDutySeconds - onDuty), windowStart, resetEnd, incomplete);
        }

        private Rest24State Rest24At(long t)
        {
            var windowStart = t - HosLimits.Rest24WindowSeconds;
            long best = 0;
            long? bestEnd = null;
            long? expires = null;
            foreach (var r in _runs)
            {
                var end = Math.Min(r.End, t);
                var overlap = Overlap(r.Start, end, windowStart, t);
                if (overlap > best)
                {
                    best = overlap;
                }

                if (overlap >= HosLimits.Rest24Seconds)
                {
                    var candidate = end - HosLimits.Rest24Seconds + HosLimits.Rest24WindowSeconds;
                    if (expires is null || candidate > expires)
                    {
                        expires = candidate;
                        bestEnd = end;
                    }
                }
            }

            var satisfied = expires is not null;
            var incomplete = !satisfied && windowStart < _knownFrom;
            return new Rest24State(satisfied, bestEnd, expires, Sec(best), incomplete);
        }

        // ---- 6. Days ------------------------------------------------------------------------

        private List<DayTotals> BuildDays()
        {
            var days = new List<DayTotals>();
            for (var offset = -13; offset <= 0; offset++)
            {
                var date = _today.AddDays(offset);
                days.Add(BuildDay(date));
            }

            return days;
        }

        private DayTotals BuildDay(DateOnly date)
        {
            var dayStart = _cal.DayStartUnix(date);
            var dayEnd = _cal.DayEndUnix(date);
            var known = dayEnd > _knownFrom;
            var complete = date < _today;
            var end = Math.Min(dayEnd, _asOf);
            var driving = known ? DailyDrivingAt(date, end) : 0;
            var onDuty = known ? DailyOnDutyAt(date, end) : 0;
            long rawOff = 0;
            foreach (var p in _pieces)
            {
                if (p.IsOff)
                {
                    rawOff += Overlap(p.Start, p.End, dayStart, dayEnd);
                }
            }

            var qualifying = known ? QualifyingOff(date) : 0;
            var required = RequiredOff(date);
            var shortfall = Math.Max(0, required - qualifying);
            return new DayTotals(
                date,
                known,
                complete,
                driving,
                onDuty,
                DeclaredOtherCarrierOnDuty(date),
                qualifying,
                Sec(rawOff),
                _pcDeclared.GetValueOrDefault(date),
                _ownEventDays.Contains(date),
                _certified.Contains(date),
                known && RunTouches(date, HosLimits.MandatoryConsecutiveOffSeconds),
                required,
                shortfall,
                known && shortfall == 0);
        }

        private int RequiredOff(DateOnly date)
        {
            var required = HosLimits.DailyOffSeconds;
            if (_adverseByDay.TryGetValue(date, out var ext))
            {
                required = Math.Max(HosLimits.MandatoryConsecutiveOffSeconds, HosLimits.DailyOffSeconds - ext);
            }

            if (_deferrals.TryGetValue(date, out var deferral) && !_deferralFailedDayOne.Contains(date))
            {
                required = Math.Min(required, HosLimits.DailyOffSeconds - deferral.DeferredSeconds);
            }

            return required;
        }

        // ---- 7. Driving pieces ----------------------------------------------------------

        private void EvaluateDrivingPieces()
        {
            foreach (var p in _pieces)
            {
                if (!p.IsDriving)
                {
                    continue;
                }

                var t = p.End;
                var date = p.Date;
                var dayKey = date.ToString("yyyy-MM-dd");
                var extDay = AdverseForDay(date, t);
                Check(HosRule.DailyDriving13h, dayKey, date, DailyDrivingAt(date, t), HosLimits.DailyDrivingSeconds + extDay, p);
                Check(HosRule.DailyOnDuty14h, dayKey, date, DailyOnDutyAt(date, t), HosLimits.DailyOnDutySeconds + extDay, p);

                var shift = ShiftStartAt(p.Start);
                var shiftKey = $"{shift}";
                var extShift = AdverseForShift(shift, t);
                Check(HosRule.ShiftDriving13h, shiftKey, null, Sec(SumBetween(shift, t, driving: true)), HosLimits.ShiftDrivingSeconds + extShift, p);
                Check(HosRule.ShiftOnDuty14h, shiftKey, null, Sec(SumBetween(shift, t, driving: false)), HosLimits.ShiftOnDutySeconds + extShift, p);
                Check(HosRule.ShiftElapsed16h, shiftKey, null, Sec(t - shift), HosLimits.ShiftElapsedSeconds + extShift, p);

                var cycle = CycleAt(t);
                if (!cycle.RecordsIncomplete)
                {
                    Check(HosRule.Cycle1_70hIn7d, dayKey, date, cycle.OnDutySeconds, HosLimits.CycleOnDutySeconds, p);
                }

                var rest = Rest24At(t);
                if (!rest.Satisfied && !rest.RecordsIncomplete)
                {
                    var at = Math.Max(Rest24ExpiryBefore(p.Start) ?? p.Start, p.Start);
                    Record(HosRule.Rest24hIn14d, dayKey, date, at, rest.BestOffSeconds, HosLimits.Rest24Seconds);
                }

                for (var i = 0; i < _oos.Count; i++)
                {
                    var (from, until) = _oos[i];
                    var overlap = Overlap(p.Start, p.End, from, until);
                    if (overlap > 0)
                    {
                        var key = $"{i}|{dayKey}";
                        if (_pending.TryGetValue((HosRule.DroveWhileOutOfService, key), out var existing))
                        {
                            existing.Figure += Sec(overlap);
                        }
                        else
                        {
                            Record(HosRule.DroveWhileOutOfService, key, date, Math.Max(from, p.Start), Sec(overlap), 0);
                        }
                    }
                }
            }
        }

        /// <summary>The instant the most recent 24h-in-14d entitlement lapsed, if one ever existed.</summary>
        private long? Rest24ExpiryBefore(long t)
        {
            long? latest = null;
            foreach (var r in _runs)
            {
                if (r.End <= t && r.Length >= HosLimits.Rest24Seconds)
                {
                    var expires = r.End - HosLimits.Rest24Seconds + HosLimits.Rest24WindowSeconds;
                    if (latest is null || expires > latest)
                    {
                        latest = expires;
                    }
                }
            }

            return latest;
        }

        private void Check(HosRule rule, string key, DateOnly? date, int figure, int ceiling, Piece p)
        {
            if (figure <= ceiling)
            {
                return;
            }

            if (_pending.TryGetValue((rule, key), out var existing))
            {
                existing.Figure = figure;
                existing.Limit = ceiling;
                return;
            }

            var at = Math.Max(p.End - (figure - ceiling), p.Start);
            Record(rule, key, date ?? _cal.DayOf(at), at, figure, ceiling);
        }

        private void Record(HosRule rule, string key, DateOnly date, long at, int figure, int limit)
        {
            if (_pending.ContainsKey((rule, key)))
            {
                return;
            }

            _pending[(rule, key)] = new Pending { Rule = rule, Date = date, AtUnix = at, Figure = figure, Limit = limit };
        }

        // ---- 8. Daily off, deferral, records ------------------------------------------------

        private void EvaluateDailyOff(List<DayTotals> days)
        {
            foreach (var deferral in _deferrals.Values)
            {
                EvaluateDeferral(deferral);
            }

            for (var i = 0; i < days.Count; i++)
            {
                var day = days[i];
                if (!day.Known || _cal.DayStartUnix(day.Date) < _knownFrom)
                {
                    continue;
                }

                if (_deferralFailedDayOne.Contains(day.Date))
                {
                    // Re-evaluated with the plain 10h by EvaluateDeferral; refresh the totals row.
                    days[i] = day = BuildDay(day.Date);
                }

                var dayKey = day.Date.ToString("yyyy-MM-dd");
                var dayEnd = _cal.DayEndUnix(day.Date);
                if (day.Complete)
                {
                    if (day.OffDutySeconds < day.DailyOffRequiredSeconds)
                    {
                        Record(HosRule.DailyOffDuty10h, dayKey, day.Date, dayEnd, day.OffDutySeconds, day.DailyOffRequiredSeconds);
                    }

                    if (day.HasOwnEvents && !day.Certified)
                    {
                        Record(HosRule.RodsNotCertified, dayKey, day.Date, dayEnd, 0, 0);
                    }

                    continue;
                }

                // Today: a shortfall is reported, and becomes a violation only once it can no longer be made up.
                var remaining = dayEnd - _asOf;
                long credit = 0;
                foreach (var r in _runs)
                {
                    if (r.End == _asOf && r.Length < HosLimits.MinOffBlockSeconds)
                    {
                        credit = Overlap(r.Start, r.End, _cal.DayStartUnix(day.Date), dayEnd);
                    }
                }

                if (day.OffDutySeconds + remaining + credit < day.DailyOffRequiredSeconds)
                {
                    Record(HosRule.DailyOffDuty10h, dayKey, day.Date, _asOf, day.OffDutySeconds, day.DailyOffRequiredSeconds);
                }
            }
        }

        private void EvaluateDeferral(OffDutyDeferral deferral)
        {
            var d1 = deferral.DayOne;
            var d2 = d1.AddDays(1);
            if (d1 >= _today || _cal.DayStartUnix(d1) < _knownFrom)
            {
                return;
            }

            var d = deferral.DeferredSeconds;
            var off1 = QualifyingOff(d1);
            var ok = off1 >= HosLimits.DailyOffSeconds - d && RunTouches(d1, HosLimits.MandatoryConsecutiveOffSeconds);
            var at = _cal.DayEndUnix(d1);
            int off2 = 0;
            if (ok && d2 < _today)
            {
                off2 = QualifyingOff(d2);
                var driving = DailyDrivingAt(d1, _cal.DayEndUnix(d1)) + DailyDrivingAt(d2, _cal.DayEndUnix(d2));
                ok = off2 >= HosLimits.DailyOffSeconds + d
                    && RunTouches(d2, HosLimits.MandatoryConsecutiveOffSeconds + d)
                    && off1 + off2 >= HosLimits.DeferralTwoDayOffMinSeconds
                    && driving <= HosLimits.DeferralTwoDayDrivingMaxSeconds;
                at = _cal.DayEndUnix(d2);
            }

            if (ok)
            {
                return;
            }

            _deferralFailedDayOne.Add(d1);
            Record(HosRule.DeferralConditions, d2.ToString("yyyy-MM-dd"), d2, at, off2, HosLimits.DailyOffSeconds + d);
            if (off1 < HosLimits.DailyOffSeconds)
            {
                Record(HosRule.DailyOffDuty10h, d1.ToString("yyyy-MM-dd"), d1, _cal.DayEndUnix(d1), off1, HosLimits.DailyOffSeconds);
            }
        }

        private bool _records14dIncomplete;

        private void EvaluateRecords(List<DayTotals> days)
        {
            if (_noLedger)
            {
                _records14dIncomplete = true;
                Record(HosRule.RecordsIncomplete14d, "ledger", _today, _asOf, 0, HosLimits.Rest24WindowSeconds);
                return;
            }

            if (_knownFrom > _asOf - HosLimits.Rest24WindowSeconds)
            {
                _records14dIncomplete = true;
                Record(HosRule.RecordsIncomplete14d, "knownFrom", _today, _asOf, Sec(_asOf - _knownFrom), HosLimits.Rest24WindowSeconds);
            }

            foreach (var day in days)
            {
                if (!day.Complete || _cal.DayStartUnix(day.Date) < _knownFrom)
                {
                    continue;
                }

                if (!day.HasOwnEvents && !_otherCarrier.ContainsKey(day.Date))
                {
                    _records14dIncomplete = true;
                    Record(HosRule.RecordsIncomplete14d, day.Date.ToString("yyyy-MM-dd"), day.Date, _cal.DayStartUnix(day.Date), 0, 0);
                }
            }
        }

        // ---- 9. Current state -----------------------------------------------------------

        private HosEvaluation Assemble(List<DayTotals> days)
        {
            var today = days[^1];
            var between = BetweenShiftsAt(_asOf);
            var qualifyingShiftRun = LastRunEndAtOrBefore(_asOf, HosLimits.MandatoryConsecutiveOffSeconds);
            var shiftStart = between ? _asOf : qualifyingShiftRun ?? _knownFrom;
            var extDay = AdverseForDay(_today, _asOf);
            var extShift = between ? 0 : AdverseForShift(shiftStart, _asOf);

            var drivingToday = DailyDrivingAt(_today, _asOf);
            var onDutyToday = DailyOnDutyAt(_today, _asOf);
            var dayDrivingCeil = HosLimits.DailyDrivingSeconds + extDay;
            var dayOnDutyCeil = HosLimits.DailyOnDutySeconds + extDay;

            var shiftDriving = between ? 0 : Sec(SumBetween(shiftStart, _asOf, driving: true));
            var shiftOnDuty = between ? 0 : Sec(SumBetween(shiftStart, _asOf, driving: false));
            var shiftElapsed = between ? 0 : Sec(_asOf - shiftStart);
            var shiftDrivingCeil = HosLimits.ShiftDrivingSeconds + extShift;
            var shiftOnDutyCeil = HosLimits.ShiftOnDutySeconds + extShift;
            var shiftElapsedCeil = HosLimits.ShiftElapsedSeconds + extShift;

            var remDayDriving = Math.Max(0, dayDrivingCeil - drivingToday);
            var remDayOnDuty = Math.Max(0, dayOnDutyCeil - onDutyToday);
            var remShiftDriving = Math.Max(0, shiftDrivingCeil - shiftDriving);
            var remShiftOnDuty = Math.Max(0, shiftOnDutyCeil - shiftOnDuty);
            var remWindow = Math.Max(0, shiftElapsedCeil - shiftElapsed);

            var cycle = CycleAt(_asOf);
            var rest = Rest24At(_asOf);
            var shiftIncomplete = !between && qualifyingShiftRun is null && _asOf - HosLimits.ShiftElapsedSeconds < _knownFrom;
            var recordsIncomplete = _records14dIncomplete || shiftIncomplete || rest.RecordsIncomplete || cycle.RecordsIncomplete;
            var oosActive = _oos.Any(span => span.From <= _asOf && _asOf < span.Until);

            var blocking = new List<BlockingReason>();
            if (_noLedger)
            {
                blocking.Add(BlockingReason.NoLedger);
            }

            if (recordsIncomplete)
            {
                blocking.Add(BlockingReason.RecordsIncomplete);
            }

            if (oosActive)
            {
                blocking.Add(BlockingReason.OutOfService);
            }

            if (!rest.Satisfied && !rest.RecordsIncomplete)
            {
                blocking.Add(BlockingReason.Rest24Expired);
            }

            if (cycle.RemainingSeconds == 0)
            {
                blocking.Add(BlockingReason.CycleExhausted);
            }

            if (remDayDriving == 0)
            {
                blocking.Add(BlockingReason.DailyDrivingExhausted);
            }

            if (remDayOnDuty == 0)
            {
                blocking.Add(BlockingReason.DailyOnDutyExhausted);
            }

            if (!between && remShiftDriving == 0)
            {
                blocking.Add(BlockingReason.ShiftDrivingExhausted);
            }

            if (!between && remShiftOnDuty == 0)
            {
                blocking.Add(BlockingReason.ShiftOnDutyExhausted);
            }

            if (!between && remWindow == 0)
            {
                blocking.Add(BlockingReason.ShiftWindowClosed);
            }

            var remainingDriving = Min(remDayDriving, remShiftDriving, remDayOnDuty, remShiftOnDuty, remWindow, cycle.RemainingSeconds);
            var remainingOnDuty = Min(remDayOnDuty, remShiftOnDuty, remWindow, cycle.RemainingSeconds);
            var restRemaining = rest.Satisfied ? Sec(Math.Max(0, rest.ExpiresAtUnix!.Value - _asOf)) : 0;
            var tightest = _noLedger ? 0 : Min(remainingDriving, remainingOnDuty, cycle.RemainingSeconds, restRemaining);

            var canDrive = blocking.Count == 0;
            var band = _noLedger ? HosBand.Off
                : !canDrive ? HosBand.Over
                : tightest <= _bands.SoonSeconds ? HosBand.Soon
                : HosBand.Ontime;

            long? mustStop = between ? null : Math.Min(Math.Min(_asOf + remShiftDriving, _asOf + remShiftOnDuty), shiftStart + shiftElapsedCeil);
            var shift = new ShiftWindow(
                between ? null : shiftStart,
                shiftDriving, shiftOnDuty, shiftElapsed,
                shiftDrivingCeil, shiftOnDutyCeil, shiftElapsedCeil,
                extShift,
                mustStop);

            NextRequiredOffDuty? next = null;
            if (!_noLedger)
            {
                var candidates = new List<(long At, HosRule Rule)>
                {
                    (_asOf + remDayDriving, HosRule.DailyDriving13h),
                    (_asOf + remDayOnDuty, HosRule.DailyOnDuty14h),
                    (_asOf + remShiftDriving, HosRule.ShiftDriving13h),
                    (_asOf + remShiftOnDuty, HosRule.ShiftOnDuty14h),
                    (_asOf + remWindow, HosRule.ShiftElapsed16h),
                    (_asOf + cycle.RemainingSeconds, HosRule.Cycle1_70hIn7d),
                };
                if (rest.ExpiresAtUnix is long expires)
                {
                    candidates.Add((Math.Max(expires, _asOf), HosRule.Rest24hIn14d));
                }
                else if (!rest.RecordsIncomplete)
                {
                    candidates.Add((_asOf, HosRule.Rest24hIn14d));
                }

                var (at, rule) = candidates.OrderBy(c => c.At).ThenBy(c => (int)c.Rule).First();
                next = new NextRequiredOffDuty(
                    at,
                    rule,
                    rule == HosRule.Rest24hIn14d ? HosLimits.Rest24Seconds : HosLimits.MandatoryConsecutiveOffSeconds,
                    today.OffDutyShortfallSeconds);
            }

            var pcToday = _pcDeclared.GetValueOrDefault(_today);
            return new HosEvaluation(
                _asOf,
                _currentStatus,
                _currentSince,
                days,
                today,
                remainingDriving,
                remainingOnDuty,
                shift,
                cycle,
                rest,
                pcToday,
                Math.Max(0, HosLimits.PcDailyLimitDecikm - pcToday),
                MaterialiseViolations(),
                canDrive,
                blocking,
                next,
                tightest,
                band,
                _problems);
        }

        private static int Min(params ReadOnlySpan<int> values)
        {
            var min = int.MaxValue;
            foreach (var v in values)
            {
                min = Math.Min(min, v);
            }

            return min;
        }

        private List<HosViolation> MaterialiseViolations()
        {
            var list = new List<HosViolation>();
            foreach (var v in _pending.Values)
            {
                string? suppressedBy = null;
                foreach (var (exception, from, to) in _emergencies)
                {
                    if (from <= v.AtUnix && v.AtUnix <= to)
                    {
                        suppressedBy = exception.Id;
                        break;
                    }
                }

                list.Add(new HosViolation(
                    v.Rule, v.Date, v.AtUnix, v.Figure, v.Limit,
                    suppressedBy is not null, suppressedBy,
                    HosMessages.For(v.Rule, v.Figure, v.Limit)));
            }

            list.Sort(static (a, b) =>
            {
                var c = a.AtUnix.CompareTo(b.AtUnix);
                if (c != 0) return c;
                c = ((int)a.Rule).CompareTo((int)b.Rule);
                return c != 0 ? c : a.Date.CompareTo(b.Date);
            });
            return list;
        }

        private void Problem(string code, string message, long? at) => _problems.Add(new HosLedgerProblem(code, message, at));
    }
}
