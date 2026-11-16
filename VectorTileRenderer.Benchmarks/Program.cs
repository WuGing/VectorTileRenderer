using BenchmarkDotNet.Running;

namespace VectorTileRenderer.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--fresh-decode-timing")
        {
            FreshDecodeTiming.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--submission-timing")
        {
            SubmissionCensus.Time(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--submission-census")
        {
            SubmissionCensus.Run(args[1], args[2]);
            return;
        }
        if (args.Length > 0 && args[0] == "--paired-request-comparison")
        {
            PairedRequestComparison.Run(args[1], args[2], args[3], int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--preparation-timing")
        {
            AllocationInvestigation.TimePreparation(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--style-evaluation-comparison")
        {
            StyleEvaluationComparison.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--path-construction-comparison")
        {
            PathConstructionComparison.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--png-encoding-comparison")
        {
            PngEncodingComparison.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--request-cpu-workload")
        {
            RequestCpuWorkload.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--request-cpu-report")
        {
            ParserCpuReport.Run(args[1], args[2], "VectorTileRenderer-Request");
            return;
        }
        if (args.Length > 0 && args[0] == "--fused-comparison")
        {
            FusedConversionComparison.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
            return;
        }
        if (args.Length > 0 && args[0] == "--protobuf-comparison")
        {
            ParserComparison.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), protobufExperiment: true);
            return;
        }
        if (args.Length > 0 && args[0] == "--parser-comparison")
        {
            ParserComparison.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture), args.Length > 4 ? args[4] : null);
            return;
        }
        if (args.Length > 0 && args[0] == "--parser-cpu-workload")
        {
            AllocationInvestigation.Run(args[1..], parserCpu: true);
            return;
        }
        if (args.Length > 0 && args[0] == "--parser-cpu-report")
        {
            ParserCpuReport.Run(args[1], args[2]);
            return;
        }
        if (args.Length > 0 && args[0] == "--decode-snapshot")
        {
            DecodeSnapshots.Run(args[1], args[2]);
            return;
        }
        if (args.Length > 0 && args[0] == "--allocation-workload")
        {
            AllocationInvestigation.Run(args[1..]);
            return;
        }
        if (args.Length > 0 && args[0] == "--allocation-report")
        {
            AllocationInvestigation.Report(args[1], args[2]);
            return;
        }
        if (args.Length > 0 && args[0] == "--request-timing")
        {
            Environment.ExitCode = RequestTiming.Run(args[1..]);
            return;
        }
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
